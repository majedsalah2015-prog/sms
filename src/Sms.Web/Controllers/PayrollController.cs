using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Common.Interfaces;
using Sms.Application.Payroll;
using Sms.Application.Security;
using Sms.Domain.Employees;
using Sms.Domain.Payroll;
using Sms.Domain.Security;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// مسير الرواتب والكشوفات — the monthly payroll run, its register, the payslips and the bank
    /// transfer list (owner request, 2026-08-28). Staff advances are the other half of this
    /// controller, in <c>PayrollController.Advances.cs</c>.
    /// <para>
    /// <b>Deviation, stated:</b> doc/Modules/12 §2 puts payroll calculation out of scope (scope
    /// decision Q7) and BR-EMP-007 says "the SMS never computes net salary" — the module was
    /// specified to produce a payroll-<i>preparation</i> export and hand the arithmetic to whatever
    /// the school runs payroll on. These screens compute it. That is a change request from the
    /// owner, not an implementation of the doc, and it is recorded here, on
    /// <c>Sms.Domain.Payroll.PayrollRun</c>, and in the commit that introduced it. The
    /// payroll-prep export of §8.7 is <b>not</b> what this is and is still unbuilt.
    /// </para>
    /// <para>
    /// <b>Not built, deliberately:</b> no GL journal is posted for a payroll run — the owner's
    /// choice on 2026-08-28 was statements first, accounting integration after, so the ledger
    /// knows nothing about salaries yet. Unpaid-leave and attendance-driven deductions are absent
    /// because staff attendance and leave (doc/Modules/12 §8.4/§8.5) do not exist; every deduction
    /// here is entered by hand or comes from an advance. Nothing prorates a mid-month joiner: no
    /// rule in the docs describes how, and inventing one would be a substitution.
    /// </para>
    /// <para>
    /// Salary data is the 🔒 restricted category (BR-EMP-003, BR-EMP-010). Both screens sit behind
    /// their own <c>ScreenCatalog</c> entries so they can be withheld from roles that legitimately
    /// hold the rest of the staff file.
    /// </para>
    /// </summary>
    [Route("payroll")]
    public partial class PayrollController : Controller
    {
        private readonly IPayrollAdmin _payroll;
        private readonly ISalaryAdvanceAdmin _advances;
        private readonly IPayrollStatements _statements;
        private readonly AppDbContext _db;
        private readonly IAuditContext _audit;
        private readonly IClock _clock;

        public PayrollController(
            IPayrollAdmin payroll, ISalaryAdvanceAdmin advances, IPayrollStatements statements,
            AppDbContext db, IAuditContext audit, IClock clock)
        {
            _payroll = payroll;
            _advances = advances;
            _statements = statements;
            _db = db;
            _audit = audit;
            _clock = clock;
        }

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        // ---------------------------------------------------------------- runs

        [HttpGet("")]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Payroll, ActionVerb.View)]
        public async Task<IActionResult> Index()
        {
            return View(await BuildIndexAsync());
        }

        [HttpPost("open")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Payroll, ActionVerb.Create)]
        public async Task<IActionResult> Open(OpenPayrollRunForm form)
        {
            if (!ModelState.IsValid)
            {
                return View(nameof(Index), await BuildIndexAsync());
            }

            try
            {
                var run = await _payroll.OpenRunAsync(form.PeriodYear, form.PeriodMonth, form.PaymentDate, Blank(form.Notes));
                TempData["Flash"] = T($"Payroll {run.PayrollRunNo} opened.", $"تم فتح المسير {run.PayrollRunNo}.");
                return RedirectToAction(nameof(Run), new { id = run.Id });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, UserMessage.For(ex, IsArabic));
                return View(nameof(Index), await BuildIndexAsync());
            }
        }

        [HttpGet("run/{id:int}")]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Payroll, ActionVerb.View)]
        public async Task<IActionResult> Run(int id)
        {
            var model = await BuildRunAsync(id);
            return model == null ? NotFound() : View(model);
        }

        [HttpPost("run/{id:int}/generate")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Payroll, ActionVerb.Edit)]
        public Task<IActionResult> Generate(int id) => RunOperation(id, async () =>
        {
            var run = await _payroll.GenerateLinesAsync(id);
            return T($"{run.LineCount} employees on the payroll.", $"تم إدراج {run.LineCount} موظفاً في المسير.");
        });

        [HttpPost("run/{id:int}/update")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Payroll, ActionVerb.Edit)]
        public Task<IActionResult> UpdateRun(int id, DateTime paymentDate, string? notes) => RunOperation(id, async () =>
        {
            await _payroll.UpdateRunAsync(id, paymentDate, Blank(notes));
            return T("Payroll updated.", "تم تحديث بيانات المسير.");
        });

        [HttpPost("run/{id:int}/line/add")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Payroll, ActionVerb.Edit)]
        public Task<IActionResult> AddLine(int id, int employeeId, decimal? basicSalary, decimal? allowances) =>
            RunOperation(id, async () =>
            {
                await _payroll.AddLineAsync(id, employeeId, basicSalary, allowances);
                return T("Employee added to the payroll.", "تمت إضافة الموظف إلى المسير.");
            });

        [HttpPost("run/{id:int}/line/{lineId:int}/remove")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Payroll, ActionVerb.Deactivate)]
        public Task<IActionResult> RemoveLine(int id, int lineId) => RunOperation(id, async () =>
        {
            await _payroll.RemoveLineAsync(lineId);
            return T("Employee removed from the payroll.", "تمت إزالة الموظف من المسير.");
        });

        [HttpPost("run/{id:int}/line/{lineId:int}/notes")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Payroll, ActionVerb.Edit)]
        public Task<IActionResult> SetLineNotes(int id, int lineId, string? notes) => RunOperation(id, async () =>
        {
            await _payroll.SetLineNotesAsync(lineId, Blank(notes));
            return T("Payslip note saved.", "تم حفظ ملاحظة القسيمة.");
        });

        [HttpPost("run/{id:int}/adjustment/add")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Payroll, ActionVerb.Edit)]
        public Task<IActionResult> AddAdjustment(int id, PayrollAdjustmentForm form) => RunOperation(id, async () =>
        {
            if (string.IsNullOrWhiteSpace(form.Description))
            {
                throw new InvalidOperationException(T("Describe the adjustment.", "اكتب بيان البند."));
            }

            await _payroll.AddAdjustmentAsync(form.LineId, form.Kind, form.Description.Trim(), form.Amount);
            return T("Adjustment added.", "تمت إضافة البند.");
        });

        [HttpPost("run/{id:int}/adjustment/{adjustmentId:int}/remove")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Payroll, ActionVerb.Deactivate)]
        public Task<IActionResult> RemoveAdjustment(int id, int adjustmentId) => RunOperation(id, async () =>
        {
            await _payroll.RemoveAdjustmentAsync(adjustmentId);
            return T("Adjustment removed.", "تم حذف البند.");
        });

        [HttpPost("run/{id:int}/approve")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Payroll, ActionVerb.Approve)]
        public Task<IActionResult> Approve(int id) => RunOperation(id, async () =>
        {
            var run = await _payroll.ApproveRunAsync(id);
            return T($"Payroll {run.PayrollRunNo} approved.", $"تم اعتماد المسير {run.PayrollRunNo}.");
        });

        [HttpPost("run/{id:int}/reopen")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Payroll, ActionVerb.Approve)]
        public Task<IActionResult> Reopen(int id) => RunOperation(id, async () =>
        {
            await _payroll.ReopenRunAsync(id);
            return T("Payroll reopened as a draft.", "أُعيد المسير إلى المسودة.");
        });

        [HttpPost("run/{id:int}/pay")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Payroll, ActionVerb.Post)]
        public Task<IActionResult> MarkPaid(int id, DateTime paidOn) => RunOperation(id, async () =>
        {
            var run = await _payroll.MarkRunPaidAsync(id, paidOn);
            return T(
                $"Payroll {run.PayrollRunNo} marked paid — advance instalments recovered.",
                $"تم تسجيل صرف المسير {run.PayrollRunNo} واستقطاع أقساط السلف.");
        });

        [HttpPost("run/{id:int}/cancel")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Payroll, ActionVerb.Deactivate)]
        public Task<IActionResult> Cancel(int id, string? reason) => RunOperation(id, async () =>
        {
            await _payroll.CancelRunAsync(id, Blank(reason));
            return T("Payroll cancelled.", "تم إلغاء المسير.");
        });

        // ---------------------------------------------------------- statements

        /// <summary>مسير الرواتب الشهري — the printable register.</summary>
        [HttpGet("run/{id:int}/register")]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Payroll, ActionVerb.Print)]
        public async Task<IActionResult> Register(int id)
        {
            if (!await _db.PayrollRuns.AnyAsync(r => r.Id == id))
            {
                return NotFound();
            }

            return View(await PrintAsync(await _statements.BuildRegisterAsync(id)));
        }

        /// <summary>قسيمة راتب الموظف.</summary>
        [HttpGet("payslip/{lineId:int}")]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Payroll, ActionVerb.Print)]
        public async Task<IActionResult> Payslip(int lineId)
        {
            if (!await _db.PayrollRunLines.AnyAsync(l => l.Id == lineId))
            {
                return NotFound();
            }

            return View(await PrintAsync(await _statements.BuildPayslipAsync(lineId)));
        }

        /// <summary>كشف التحويل البنكي — the list handed to the bank.</summary>
        [HttpGet("run/{id:int}/bank")]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Payroll, ActionVerb.Print)]
        public async Task<IActionResult> BankTransfer(int id)
        {
            if (!await _db.PayrollRuns.AnyAsync(r => r.Id == id))
            {
                return NotFound();
            }

            return View(await PrintAsync(await _statements.BuildBankTransferListAsync(id)));
        }

        /// <summary>
        /// The register as a spreadsheet-openable file.
        /// <para>
        /// CSV with a UTF-8 BOM, because without one Excel reads Arabic names as mojibake and the
        /// first thing anybody does with this file is open it in Excel. Separator is the comma and
        /// values are quoted — an employee's name can contain one.
        /// </para>
        /// </summary>
        [HttpGet("run/{id:int}/register.csv")]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Payroll, ActionVerb.Export)]
        public async Task<IActionResult> RegisterCsv(int id)
        {
            if (!await _db.PayrollRuns.AnyAsync(r => r.Id == id))
            {
                return NotFound();
            }

            var register = await _statements.BuildRegisterAsync(id);
            var arabic = IsArabic;
            var csv = new StringBuilder();

            csv.AppendLine(string.Join(",", new[]
            {
                T("Employee no", "الرقم الوظيفي"), T("Name", "الاسم"), T("Basic", "الأساسي"),
                T("Allowances", "البدلات"), T("Additions", "الإضافات"), T("Deductions", "الاستقطاعات"),
                T("Advance", "قسط السلفة"), T("Gross", "الإجمالي"), T("Net", "الصافي"),
            }.Select(Quote)));

            foreach (var line in register.Lines)
            {
                csv.AppendLine(string.Join(",", new[]
                {
                    Quote(line.Employee.EmployeeNo),
                    Quote(arabic ? line.Employee.NameAr : line.Employee.NameEn),
                    CsvAmount(line.BasicSalary),
                    CsvAmount(line.Allowances),
                    CsvAmount(line.AdditionsTotal),
                    CsvAmount(line.DeductionsTotal),
                    CsvAmount(line.AdvanceDeduction),
                    CsvAmount(line.GrossPay),
                    CsvAmount(line.NetPay),
                }));
            }

            csv.AppendLine(string.Join(",", new[]
            {
                Quote(string.Empty), Quote(T("Total", "الإجمالي")),
                CsvAmount(register.TotalBasic), CsvAmount(register.TotalAllowances),
                CsvAmount(register.TotalAdditions), CsvAmount(register.TotalDeductions),
                CsvAmount(register.TotalAdvanceDeduction), CsvAmount(register.TotalGross),
                CsvAmount(register.TotalNet),
            }));

            var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
            return File(bytes, "text/csv", $"payroll-{register.RunNo}.csv");
        }

        // ------------------------------------------------------------ internals

        /// <summary>
        /// Runs one write against a payroll run and comes back to its screen either way. Every
        /// refusal is translated at this boundary — the engine speaks English with rule ids in it,
        /// and a payroll officer must never meet that.
        /// </summary>
        private async Task<IActionResult> RunOperation(int runId, Func<Task<string>> operation)
        {
            try
            {
                TempData["Flash"] = await operation();
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Run), new { id = runId });
        }

        private async Task<PayrollIndexViewModel> BuildIndexAsync()
        {
            var runs = await _db.PayrollRuns.AsNoTracking()
                .OrderByDescending(r => r.PeriodYear).ThenByDescending(r => r.PeriodMonth).ThenByDescending(r => r.Id)
                .Select(r => new PayrollRunRow(
                    r.Id, r.PayrollRunNo, r.PeriodYear, r.PeriodMonth, r.PaymentDate, r.Status,
                    r.LineCount, r.TotalGross, r.TotalNet))
                .ToListAsync();

            // The month to suggest: this one, unless it already has a live run, in which case the
            // next. A payroll officer opening the screen on the 28th wants September, not a blank.
            var today = _clock.UtcNow;
            var (year, month) = (today.Year, today.Month);
            while (runs.Any(r => r.PeriodYear == year && r.PeriodMonth == month && r.Status != PayrollRunStatus.Cancelled))
            {
                (year, month) = PayrollPeriodMath.AddMonths(year, month, 1);
            }

            return new PayrollIndexViewModel
            {
                Runs = runs,
                DefaultYear = year,
                DefaultMonth = month,
                PayableEmployeeCount = await _db.Contracts.AsNoTracking()
                    .Where(c => c.Status == ContractStatus.Active)
                    .Select(c => c.EmployeeId)
                    .Distinct()
                    .CountAsync(),
            };
        }

        private async Task<PayrollRunViewModel?> BuildRunAsync(int id)
        {
            var run = await _db.PayrollRuns.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id);
            if (run == null)
            {
                return null;
            }

            var lineRows = await (
                from line in _db.PayrollRunLines.AsNoTracking()
                join employee in _db.Employees.AsNoTracking() on line.EmployeeId equals employee.Id
                where line.PayrollRunId == id
                select new { Line = line, Employee = employee })
                .ToListAsync();

            var adjustments = await _db.PayrollLineAdjustments.AsNoTracking()
                .Where(a => lineRows.Select(l => l.Line.Id).Contains(a.PayrollRunLineId))
                .OrderBy(a => a.Kind).ThenBy(a => a.Id)
                .ToListAsync();

            var byLine = adjustments
                .GroupBy(a => a.PayrollRunLineId)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<PayrollAdjustmentRow>)g
                        .Select(a => new PayrollAdjustmentRow(a.Id, a.Kind, a.Description, a.Amount)).ToList());

            var lines = lineRows
                .Select(r => new PayrollLineRow(
                    r.Line.Id, r.Employee.Id, r.Employee.EmployeeNo, Name(r.Employee),
                    r.Line.BasicSalary, r.Line.Allowances, r.Line.AdditionsTotal, r.Line.DeductionsTotal,
                    r.Line.AdvanceDeduction, r.Line.GrossPay, r.Line.NetPay, r.Line.Notes,
                    byLine.TryGetValue(r.Line.Id, out var own) ? own : Array.Empty<PayrollAdjustmentRow>()))
                .OrderBy(l => l.EmployeeNo, StringComparer.Ordinal)
                .ToList();

            var onTheRun = lines.Select(l => l.EmployeeId).ToHashSet();

            return new PayrollRunViewModel
            {
                Id = run.Id,
                RunNo = run.PayrollRunNo,
                PeriodYear = run.PeriodYear,
                PeriodMonth = run.PeriodMonth,
                PaymentDate = run.PaymentDate,
                Status = run.Status,
                Notes = run.Notes,
                ApprovedAtUtc = run.ApprovedAtUtc,
                PaidAtUtc = run.PaidAtUtc,
                Lines = lines,
                Candidates = run.Status == PayrollRunStatus.Draft
                    ? await CandidatesAsync(exclude: onTheRun)
                    : Array.Empty<PayrollCandidate>(),
                TotalGross = run.TotalGross,
                TotalDeductions = run.TotalDeductions,
                TotalAdvanceDeduction = lines.Sum(l => l.AdvanceDeduction),
                TotalNet = run.TotalNet,
                HasUnpayableLines = lines.Any(l => l.IsUnpayable),
            };
        }

        /// <summary>Active staff who are not already on the thing being built, with whether a contract backs them.</summary>
        private async Task<IReadOnlyList<PayrollCandidate>> CandidatesAsync(ISet<int>? exclude = null)
        {
            var employees = await _db.Employees.AsNoTracking()
                .Where(e => e.Status == EmployeeStatus.Active)
                .ToListAsync();

            var contracted = await _db.Contracts.AsNoTracking()
                .Where(c => c.Status == ContractStatus.Active)
                .Select(c => c.EmployeeId)
                .Distinct()
                .ToListAsync();

            var contractedSet = contracted.ToHashSet();

            return employees
                .Where(e => exclude == null || !exclude.Contains(e.Id))
                .Select(e => new PayrollCandidate(e.Id, e.EmployeeNo, Name(e), contractedSet.Contains(e.Id)))
                .OrderBy(c => c.EmployeeNo, StringComparer.Ordinal)
                .ToList();
        }

        private async Task<PayrollPrintViewModel<T>> PrintAsync<T>(T content)
        {
            var school = await _db.Schools.AsNoTracking()
                .Where(s => s.Id == _db.CurrentSchoolId)
                .Select(s => new { s.NameAr, s.NameEn })
                .SingleOrDefaultAsync();

            return new PayrollPrintViewModel<T>
            {
                Content = content,
                SchoolNameAr = school?.NameAr ?? string.Empty,
                SchoolNameEn = school?.NameEn ?? string.Empty,
                PrintedAtUtc = _clock.UtcNow,
            };
        }

        private static string Name(Employee e) => IsArabic
            ? Join(e.FirstNameAr, e.FatherNameAr, e.GrandfatherNameAr, e.FamilyNameAr)
            : Join(e.FirstNameEn, e.FatherNameEn, e.GrandfatherNameEn, e.FamilyNameEn);

        private static string Join(params string[] parts) =>
            string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));

        private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string Quote(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";

        /// <summary>
        /// An amount in a CSV cell: two decimals, invariant, and <b>no thousands separator</b>.
        /// <para>
        /// The on-screen format groups thousands, which is right for a person and wrong here — an
        /// unquoted "8,000.00" splits into two columns and shifts every cell after it, and quoting
        /// it instead would make Excel read the salary as text. The whole point of the export is
        /// that somebody sums the column.
        /// </para>
        /// </summary>
        private static string CsvAmount(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
