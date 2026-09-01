using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Payroll;
using Sms.Application.Security;
using Sms.Domain.Payroll;
using Sms.Domain.Security;
using Sms.Web.Models;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// سلف الموظفين وكشوفاتها — requesting an advance, deciding it, handing the money over, and the
    /// two advances statements (owner request, 2026-08-28).
    /// <para>
    /// The same controller as the payroll runs because the two are one subject: an advance exists
    /// to be recovered from a salary, and the screens cross-link constantly. Its own file because
    /// <c>PayrollController.cs</c> already carries the run lifecycle and the four print views —
    /// the same split as <c>FeesController.StudentFinance.cs</c>.
    /// </para>
    /// <para>
    /// Repayment is by automatic instalment deduction only (the owner's choice on 2026-08-28).
    /// There is no cash-repayment path: an employee who wants to clear an advance early does it
    /// through the schedule, and a school that forgives one waives the remaining instalments.
    /// </para>
    /// </summary>
    public partial class PayrollController
    {
        [HttpGet("advances")]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Advances, ActionVerb.View)]
        public async Task<IActionResult> Advances(SalaryAdvanceStatus? status = null)
        {
            return View(await BuildAdvancesAsync(status));
        }

        [HttpGet("advances/{id:int}")]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Advances, ActionVerb.View)]
        public async Task<IActionResult> Advance(int id)
        {
            var model = await BuildAdvanceAsync(id);
            return model == null ? NotFound() : View(model);
        }

        [HttpPost("advances/request")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Advances, ActionVerb.Create)]
        public async Task<IActionResult> RequestAdvance(SalaryAdvanceForm form)
        {
            if (!ModelState.IsValid)
            {
                return View(nameof(Advances), await BuildAdvancesAsync(null));
            }

            try
            {
                var advance = await _advances.RequestAsync(
                    form.EmployeeId, form.RequestDate, form.Amount, form.InstallmentCount,
                    form.FirstDeductionYear, form.FirstDeductionMonth, Blank(form.Reason));

                TempData["Flash"] = T($"Advance {advance.AdvanceNo} recorded.", $"تم تسجيل السلفة {advance.AdvanceNo}.");
                return RedirectToAction(nameof(Advance), new { id = advance.Id });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, UserMessage.For(ex, IsArabic));
                return View(nameof(Advances), await BuildAdvancesAsync(null));
            }
        }

        [HttpPost("advances/{id:int}/update")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Advances, ActionVerb.Edit)]
        public Task<IActionResult> UpdateAdvance(int id, SalaryAdvanceForm form) => AdvanceOperation(id, async () =>
        {
            // The amount and the instalment count are T1 with a required reason: what an employee
            // owes is not a field that changes without somebody saying why.
            _audit.Reason = Blank(form.AuditReason);

            await _advances.UpdateRequestAsync(
                id, form.RequestDate, form.Amount, form.InstallmentCount,
                form.FirstDeductionYear, form.FirstDeductionMonth, Blank(form.Reason));

            return T("Request updated.", "تم تحديث الطلب.");
        });

        [HttpPost("advances/{id:int}/approve")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Advances, ActionVerb.Approve)]
        public Task<IActionResult> ApproveAdvance(int id, string? note) => AdvanceOperation(id, async () =>
        {
            await _advances.ApproveAsync(id, Blank(note));
            return T("Advance approved — not yet disbursed.", "تمت الموافقة على السلفة، ولم تُصرف بعد.");
        });

        [HttpPost("advances/{id:int}/reject")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Advances, ActionVerb.Approve)]
        public Task<IActionResult> RejectAdvance(int id, string? note) => AdvanceOperation(id, async () =>
        {
            await _advances.RejectAsync(id, Blank(note));
            return T("Advance rejected.", "تم رفض السلفة.");
        });

        [HttpPost("advances/{id:int}/cancel")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Advances, ActionVerb.Deactivate)]
        public Task<IActionResult> CancelAdvance(int id, string? note) => AdvanceOperation(id, async () =>
        {
            await _advances.CancelAsync(id, Blank(note));
            return T("Advance cancelled.", "تم إلغاء السلفة.");
        });

        [HttpPost("advances/{id:int}/disburse")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Advances, ActionVerb.Post)]
        public Task<IActionResult> DisburseAdvance(int id, DisburseAdvanceForm form) => AdvanceOperation(id, async () =>
        {
            var advance = await _advances.DisburseAsync(id, form.DisbursedOn, form.Method, Blank(form.ReferenceNo));
            return T(
                $"Disbursed — {advance.InstallmentCount} instalments scheduled.",
                $"تم الصرف، وجُدولت {advance.InstallmentCount} أقساط.");
        });

        [HttpPost("advances/{id:int}/reschedule")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Advances, ActionVerb.Edit)]
        public Task<IActionResult> RescheduleAdvance(int id, RescheduleAdvanceForm form) => AdvanceOperation(id, async () =>
        {
            if (string.IsNullOrWhiteSpace(form.AuditReason))
            {
                throw new InvalidOperationException(
                    T("State why the schedule is changing.", "اكتب سبب تعديل الجدول."));
            }

            _audit.Reason = form.AuditReason.Trim();
            await _advances.RescheduleAsync(id, form.InstallmentCount, form.FirstDeductionYear, form.FirstDeductionMonth);
            return T("Repayment schedule rebuilt.", "أُعيد بناء جدول السداد.");
        });

        [HttpPost("advances/{id:int}/installment/{installmentId:int}/waive")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Advances, ActionVerb.Approve)]
        public Task<IActionResult> WaiveInstallment(int id, int installmentId, string? note) =>
            AdvanceOperation(id, async () =>
            {
                await _advances.WaiveInstallmentAsync(installmentId, Blank(note));
                return T("Instalment waived.", "تم إعفاء القسط.");
            });

        [HttpPost("advances/{id:int}/waive-remaining")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Advances, ActionVerb.Approve)]
        public Task<IActionResult> WaiveRemaining(int id, string? note) => AdvanceOperation(id, async () =>
        {
            await _advances.WaiveRemainingAsync(id, Blank(note));
            return T("Remaining balance waived — the advance is settled.", "تم إعفاء المتبقي، والسلفة مسدَّدة.");
        });

        // ---------------------------------------------------------- statements

        /// <summary>كشف السلف — one employee's advances, every instalment, and what is still owed.</summary>
        [HttpGet("advances/statement")]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Advances, ActionVerb.Print)]
        public async Task<IActionResult> AdvanceStatement(int? employeeId = null)
        {
            var school = await _db.Schools.AsNoTracking()
                .Where(s => s.Id == _db.CurrentSchoolId)
                .Select(s => new { s.NameAr, s.NameEn })
                .SingleOrDefaultAsync();

            // Only staff who actually have an advance: a picker listing everyone would make the
            // reader hunt for the handful of names the statement can say anything about.
            var withAdvances = await _db.SalaryAdvances.AsNoTracking()
                .Select(a => a.EmployeeId)
                .Distinct()
                .ToListAsync();

            var employees = (await CandidatesAsync())
                .Where(c => withAdvances.Contains(c.EmployeeId))
                .ToList();

            var selected = employeeId ?? employees.FirstOrDefault()?.EmployeeId;

            return View(new AdvanceStatementViewModel
            {
                Statement = selected == null ? null : await _statements.BuildAdvanceStatementAsync(selected.Value),
                Employees = employees,
                SelectedEmployeeId = selected,
                SchoolNameAr = school?.NameAr ?? string.Empty,
                SchoolNameEn = school?.NameEn ?? string.Empty,
            });
        }

        /// <summary>كشف السلف القائمة — what the school is owed by its staff, across everybody.</summary>
        [HttpGet("advances/outstanding")]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Advances, ActionVerb.Print)]
        public async Task<IActionResult> OutstandingAdvances()
        {
            return View(await PrintAsync(await _statements.BuildOutstandingAdvancesAsync()));
        }

        // ------------------------------------------------------------ internals

        private async Task<IActionResult> AdvanceOperation(int advanceId, Func<Task<string>> operation)
        {
            try
            {
                TempData["Flash"] = await operation();
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Advance), new { id = advanceId });
        }

        private async Task<AdvancesIndexViewModel> BuildAdvancesAsync(SalaryAdvanceStatus? status)
        {
            var rows = await (
                from advance in _db.SalaryAdvances.AsNoTracking()
                join employee in _db.Employees.AsNoTracking() on advance.EmployeeId equals employee.Id
                select new { Advance = advance, Employee = employee })
                .ToListAsync();

            var advanceIds = rows.Select(r => r.Advance.Id).ToList();

            // Materialised then summed in memory — SumAsync() over a decimal column throws at
            // runtime on Sqlite, which is where every test of this screen runs.
            var recovered = (await _db.SalaryAdvanceInstallments.AsNoTracking()
                    .Where(i => advanceIds.Contains(i.SalaryAdvanceId)
                                && i.Status != SalaryAdvanceInstallmentStatus.Scheduled)
                    .Select(i => new { i.SalaryAdvanceId, i.Amount })
                    .ToListAsync())
                .GroupBy(i => i.SalaryAdvanceId)
                .ToDictionary(g => g.Key, g => g.Sum(i => i.Amount));

            decimal Outstanding(Domain.Payroll.SalaryAdvance a) =>
                a.Status == SalaryAdvanceStatus.Disbursed
                    ? a.Amount - (recovered.TryGetValue(a.Id, out var taken) ? taken : 0m)
                    : 0m;

            var all = rows
                .Select(r => new SalaryAdvanceRow(
                    r.Advance.Id, r.Advance.AdvanceNo, r.Employee.Id, r.Employee.EmployeeNo, Name(r.Employee),
                    r.Advance.RequestDate, r.Advance.Amount, r.Advance.InstallmentCount, r.Advance.Status,
                    r.Advance.DisbursedOn, Outstanding(r.Advance)))
                .OrderByDescending(a => a.RequestDate).ThenByDescending(a => a.Id)
                .ToList();

            // Anyone whose advance is still open cannot be given another (the engine refuses it),
            // so they are kept out of the picker rather than offered and then rejected.
            var blocked = rows
                .Where(r => SalaryAdvanceStatusTransitions.IsOutstanding(r.Advance.Status))
                .Select(r => r.Employee.Id)
                .ToHashSet();

            return new AdvancesIndexViewModel
            {
                Advances = status == null ? all : all.Where(a => a.Status == status).ToList(),
                StatusFilter = status,
                Candidates = await CandidatesAsync(exclude: blocked),
                TotalOutstanding = all.Sum(a => a.Outstanding),
                OutstandingCount = all.Count(a => a.Status == SalaryAdvanceStatus.Disbursed),
            };
        }

        private async Task<SalaryAdvanceViewModel?> BuildAdvanceAsync(int id)
        {
            var advance = await _db.SalaryAdvances.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id);
            if (advance == null)
            {
                return null;
            }

            // The statement already assembles the schedule with the run that consumed each
            // instalment named; rebuilding that here would be a second answer to one question.
            var statement = await _statements.BuildAdvanceStatementAsync(advance.EmployeeId);
            var view = statement.Advances.SingleOrDefault(a => a.AdvanceId == id);

            return new SalaryAdvanceViewModel
            {
                Id = advance.Id,
                AdvanceNo = advance.AdvanceNo,
                Employee = statement.Employee,
                RequestDate = advance.RequestDate,
                Amount = advance.Amount,
                InstallmentCount = advance.InstallmentCount,
                FirstDeductionYear = advance.FirstDeductionYear,
                FirstDeductionMonth = advance.FirstDeductionMonth,
                Status = advance.Status,
                Reason = advance.Reason,
                DecisionNote = advance.DecisionNote,
                DecisionAtUtc = advance.DecisionAtUtc,
                DisbursedOn = advance.DisbursedOn,
                DisbursementMethod = advance.DisbursementMethod,
                DisbursementRefNo = advance.DisbursementRefNo,
                Installments = view?.Installments ?? (IReadOnlyList<AdvanceInstallmentView>)Array.Empty<AdvanceInstallmentView>(),
                DeductedTotal = view?.DeductedTotal ?? 0m,
                WaivedTotal = view?.WaivedTotal ?? 0m,
                OutstandingBalance = view?.OutstandingBalance ?? 0m,
            };
        }
    }
}
