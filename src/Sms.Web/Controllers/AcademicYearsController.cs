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
            var enrollmentsByYear = await _db.Enrollments.AsNoTracking()
                .GroupBy(e => e.AcademicYearId)
                .Select(g => new { YearId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.YearId, x => x.Count);

            var rows = new List<YearBoardViewModel.Row>();
            foreach (var y in years)
            {
                var row = new YearBoardViewModel.Row
                {
                    Year = y,
                    Semesters = semesters.Count(s => s.AcademicYearId == y.Id),
                    Terms = terms.Count(t => t.AcademicYearId == y.Id),
                    Enrollments = enrollmentsByYear.TryGetValue(y.Id, out var n) ? n : 0,
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
                var labelAr = string.IsNullOrWhiteSpace(form.LabelAr) ? DefaultArabicLabel(labelEn) : form.LabelAr.Trim();
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

        // --- Edit / Delete: only while no student is enrolled in the year ----------

        [HttpGet("{id:int}/edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var year = await _db.AcademicYears.AsNoTracking().SingleOrDefaultAsync(y => y.Id == id);
            if (year == null)
            {
                return NotFound();
            }

            // Enrollments lock the year's span, not its name. Turning the whole screen away made a typo
            // in a year's label permanent from the first enrolment onward — the labels are shown on every
            // screen in the product, and the only way out was deleting the year, which enrollments also
            // forbid. The dates are what the rest of the schema nests inside, so those are what lock.
            var enrolled = await _db.Enrollments.CountAsync(e => e.AcademicYearId == id);

            return View(new YearDefinitionViewModel
            {
                YearId = id, Year = year, EnrolledCount = enrolled,
                LabelAr = year.LabelAr, LabelEn = year.LabelEn, HijriLabel = year.HijriLabel, StartDate = year.StartDate, EndDate = year.EndDate,
            });
        }

        [HttpPost("{id:int}/edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, YearDefinitionViewModel form)
        {
            var year = await _db.AcademicYears.AsNoTracking().SingleOrDefaultAsync(y => y.Id == id);
            if (year == null)
            {
                return NotFound();
            }

            var enrolled = await _db.Enrollments.CountAsync(e => e.AcademicYearId == id);
            form.YearId = id;
            form.Year = year;
            form.EnrolledCount = enrolled;
            try
            {
                // The dates the form posts are ignored when they are locked — the inputs are disabled, so
                // a browser sends nothing for them anyway, and a hand-crafted request must not be able to
                // move a span the screen refused to offer.
                var start = enrolled > 0 ? year.StartDate : form.StartDate;
                var end = enrolled > 0 ? year.EndDate : form.EndDate;
                if (start == null || end == null)
                {
                    throw new InvalidOperationException(T("Start and end dates are required.", "تاريخا البداية والنهاية مطلوبان."));
                }

                var labelEn = string.IsNullOrWhiteSpace(form.LabelEn) ? $"{start.Value.Year}-{end.Value.Year}" : form.LabelEn.Trim();
                var labelAr = string.IsNullOrWhiteSpace(form.LabelAr) ? DefaultArabicLabel(labelEn) : form.LabelAr.Trim();
                var hijri = string.IsNullOrWhiteSpace(form.HijriLabel) ? HijriLabelFor(start.Value) : form.HijriLabel.Trim();
                _audit.Reason = string.IsNullOrWhiteSpace(form.Reason) ? null : form.Reason;

                if (enrolled > 0)
                {
                    await _years.RelabelYearAsync(id, labelAr, labelEn, hijri);
                    TempData["Flash"] = T("Academic year labels updated.", "تم تحديث تسميات العام الدراسي.");
                }
                else
                {
                    await _years.UpdateYearAsync(id, labelAr, labelEn, hijri, start.Value, end.Value);
                    TempData["Flash"] = T("Academic year updated.", "تم تحديث العام الدراسي.");
                }

                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(form);
            }
        }

        [HttpPost("{id:int}/delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, string? reason)
        {
            try
            {
                _audit.Reason = string.IsNullOrWhiteSpace(reason) ? null : reason;
                await _years.DeleteYearAsync(id);
                TempData["Flash"] = T("Academic year deleted.", "تم حذف العام الدراسي.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
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

        /// <summary>
        /// The Arabic label defaults to the same Western digits as the English one.
        /// <para>
        /// This used to substitute Arabic-Indic digits (٢٠٢٦-٢٠٢٧). Owner decision,
        /// 2026-08-21: numerals stay Western in both languages. doc/02 §6 already
        /// called Arabic-Indic display "a UI preference, storage is invariant" —
        /// this settles the preference, and settling it in one direction is what
        /// keeps a year's name comparable, searchable and copyable between the two
        /// interfaces instead of being two different strings.
        /// </para>
        /// </summary>
        private static string DefaultArabicLabel(string labelEn) => labelEn;

        /// <summary>Tabular Hijri year of the start date, e.g. "1449هـ" (±1 day accuracy is documented; the label is editable).</summary>
        private static string HijriLabelFor(DateTime start)
        {
            var hijri = new HijriCalendar();
            return hijri.GetYear(start).ToString(CultureInfo.InvariantCulture) + "هـ";
        }
    }
}
