using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Common.Interfaces;
using Sms.Application.Rollover;
using Sms.Application.Schools;
using Sms.Domain.Schools;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/03 §8.1–8.3: year list & status board (lifecycle actions,
    /// checklists inline when a rollover batch exists), year definition with
    /// the semester/term builder, checklist consoles. §8.4–8.7 (rollover
    /// cockpit, promotion grid, re-registration, section board) are the
    /// E-801 rollover screens — the batch engine exists (IRolloverAdmin);
    /// this board links to it once those screens land.
    /// </summary>
    [Route("years")]
    public class AcademicYearsController : Controller
    {
        private readonly IAcademicYearAdmin _years;
        private readonly IRolloverAdmin _rollover;
        private readonly AppDbContext _db;
        private readonly ITenantContext _tenant;
        private readonly IWorkingYearContext _workingYear;
        private readonly IAuditContext _audit;

        public AcademicYearsController(
            IAcademicYearAdmin years, IRolloverAdmin rollover, AppDbContext db, ITenantContext tenant, IWorkingYearContext workingYear, IAuditContext audit)
        {
            _years = years;
            _rollover = rollover;
            _db = db;
            _tenant = tenant;
            _workingYear = workingYear;
            _audit = audit;
        }

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var years = await _db.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartDate).ToListAsync();
            var semesters = await _db.Semesters.AsNoTracking().ToListAsync();
            var terms = await _db.Terms.AsNoTracking().ToListAsync();
            var batches = await _db.RolloverBatches.AsNoTracking().ToListAsync();
            var school = await _db.Schools.AsNoTracking().SingleOrDefaultAsync(s => s.Id == _tenant.SchoolId);

            var rows = new List<YearBoardViewModel.Row>();
            foreach (var y in years)
            {
                var row = new YearBoardViewModel.Row
                {
                    Year = y,
                    Semesters = semesters.Count(s => s.AcademicYearId == y.Id),
                    Terms = terms.Count(t => t.AcademicYearId == y.Id),
                    IncomingBatch = batches.FirstOrDefault(b => b.TargetAcademicYearId == y.Id),
                    OutgoingBatch = batches.FirstOrDefault(b => b.SourceAcademicYearId == y.Id),
                };
                if (row.IncomingBatch != null && y.Status == AcademicYearStatus.Preparation)
                {
                    row.OpeningChecklist = await _rollover.GetOpeningChecklistAsync(row.IncomingBatch.Id);
                }

                if (row.OutgoingBatch != null && y.Status == AcademicYearStatus.Closing)
                {
                    row.ClosingChecklist = await _rollover.GetClosingChecklistAsync(row.OutgoingBatch.Id);
                }

                rows.Add(row);
            }

            return View(new YearBoardViewModel
            {
                Rows = rows,
                WorkingYearId = _workingYear.AcademicYearId,
                WorkingYear = years.FirstOrDefault(y => y.Id == _workingYear.AcademicYearId),
                ActiveYear = years.FirstOrDefault(y => y.Status == AcademicYearStatus.Active),
                SetupComplete = school?.SetupCompletedAtUtc != null,
            });
        }

        [HttpPost("{id:int}/activate")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Activate(int id, string? reason) => Lifecycle(id, reason, () => _years.ActivateAsync(id), T("Year activated (the previous Active year moved to Closing).", "تم تفعيل العام (انتقل العام السابق إلى مرحلة الإغلاق)."));

        [HttpPost("{id:int}/close")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Close(int id, string? reason) => Lifecycle(id, reason, () => _years.CloseAsync(id), T("Year closed (read-only; postings need WF-13).", "تم إغلاق العام (للقراءة فقط؛ الترحيل يتطلب WF-13)."));

        [HttpPost("{id:int}/archive")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Archive(int id, string? reason) => Lifecycle(id, reason, () => _years.ArchiveAsync(id), T("Year archived (dropped from default pickers).", "تمت أرشفة العام (أُزيل من القوائم الافتراضية)."));

        private async Task<IActionResult> Lifecycle(int id, string? reason, Func<Task> action, string success)
        {
            try
            {
                _audit.Reason = string.IsNullOrWhiteSpace(reason) ? null : reason;
                await action();
                TempData["Flash"] = success;
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // ------------------------------------------------------------------

        [HttpGet("new")]
        public IActionResult Define()
        {
            return View(new YearDefinitionViewModel());
        }

        [HttpPost("new")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Define(YearDefinitionViewModel form)
        {
            try
            {
                if (form.StartDate == null || form.EndDate == null)
                {
                    throw new InvalidOperationException(T("Start and end dates are required.", "تاريخا البداية والنهاية مطلوبان."));
                }

                // BR-AYR-001: label generated from dates when not supplied.
                var labelEn = string.IsNullOrWhiteSpace(form.LabelEn) ? $"{form.StartDate.Value.Year}-{form.EndDate.Value.Year}" : form.LabelEn.Trim();
                var labelAr = string.IsNullOrWhiteSpace(form.LabelAr) ? ToArabicDigits(labelEn) : form.LabelAr.Trim();
                var hijri = string.IsNullOrWhiteSpace(form.HijriLabel) ? HijriLabelFor(form.StartDate.Value) : form.HijriLabel.Trim();
                var year = await _years.DefineYearAsync(labelAr, labelEn, hijri, form.StartDate.Value, form.EndDate.Value);
                TempData["Flash"] = T("Academic year created in Preparation.", "تم إنشاء العام الدراسي في مرحلة الإعداد.");
                return RedirectToAction(nameof(Details), new { id = year.Id });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(form);
            }
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Details(int id)
        {
            var model = await BuildDetailsAsync(id);
            return model == null ? NotFound() : View(model);
        }

        [HttpPost("{id:int}/semester")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DefineSemester(int id, YearDefinitionViewModel form)
        {
            try
            {
                if (form.SemesterStart == null || form.SemesterEnd == null || form.SemesterSequence == null)
                {
                    throw new InvalidOperationException(T("Sequence, start and end are required.", "التسلسل والبداية والنهاية مطلوبة."));
                }

                await _years.DefineSemesterAsync(id, form.SemesterSequence.Value, form.SemesterNameAr ?? $"الفصل {form.SemesterSequence}", form.SemesterNameEn ?? $"Semester {form.SemesterSequence}", form.SemesterStart.Value, form.SemesterEnd.Value);
                TempData["Flash"] = T("Semester saved.", "تم حفظ الفصل الدراسي.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost("{id:int}/term")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DefineTerm(int id, YearDefinitionViewModel form)
        {
            try
            {
                if (form.TermSemesterId == null || form.TermStart == null || form.TermEnd == null || form.TermSequence == null)
                {
                    throw new InvalidOperationException(T("Semester, sequence, start and end are required.", "الفصل والتسلسل والبداية والنهاية مطلوبة."));
                }

                await _years.DefineTermAsync(form.TermSemesterId.Value, form.TermSequence.Value, form.TermNameAr ?? $"الفترة {form.TermSequence}", form.TermNameEn ?? $"Term {form.TermSequence}", form.TermStart.Value, form.TermEnd.Value);
                TempData["Flash"] = T("Term saved.", "تم حفظ الفترة.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task<YearDefinitionViewModel?> BuildDetailsAsync(int id)
        {
            var year = await _db.AcademicYears.AsNoTracking().SingleOrDefaultAsync(y => y.Id == id);
            if (year == null)
            {
                return null;
            }

            var semesters = await _db.Semesters.AsNoTracking().Where(s => s.AcademicYearId == id).OrderBy(s => s.SequenceNumber).ToListAsync();
            var terms = await _db.Terms.AsNoTracking().Where(t => t.AcademicYearId == id).OrderBy(t => t.SemesterId).ThenBy(t => t.SequenceNumber).ToListAsync();
            return new YearDefinitionViewModel
            {
                YearId = id,
                Year = year,
                LabelAr = year.LabelAr, LabelEn = year.LabelEn, HijriLabel = year.HijriLabel, StartDate = year.StartDate, EndDate = year.EndDate,
                Semesters = semesters,
                Terms = terms,
                SemesterSequence = semesters.Count + 1,
                SemesterStart = semesters.Count == 0 ? year.StartDate : semesters.Max(s => s.EndDate).AddDays(1),
                SemesterEnd = year.EndDate,
                TermSequence = 1,
            };
        }

        private static string ToArabicDigits(string s) => new(s.Select(c => c >= '0' && c <= '9' ? (char)('٠' + (c - '0')) : c).ToArray());

        /// <summary>Tabular Hijri year of the start date, e.g. "١٤٤٩هـ" (±1 day accuracy is documented; the label is editable).</summary>
        private static string HijriLabelFor(DateTime start)
        {
            var hijri = new HijriCalendar();
            return ToArabicDigits(hijri.GetYear(start).ToString(CultureInfo.InvariantCulture)) + "هـ";
        }
    }
}
