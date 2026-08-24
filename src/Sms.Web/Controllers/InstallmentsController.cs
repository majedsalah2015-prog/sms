using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Guards;
using Sms.Application.Common.Interfaces;
using Sms.Application.Installments;
using Sms.Application.Setup;
using Sms.Domain.Installments;
using Sms.Domain.Payments;
using Sms.Domain.Schools;
using Sms.Domain.Students;
using Sms.Infrastructure.Persistence;
using Sms.Web.Finance;
using Sms.Web.Models;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/20 §8 — screens over <see cref="IInstallmentAdmin"/>, whose
    /// engine has been complete since E-501: 8.1 Template designer, 8.2
    /// Assignment console, 8.3 Family schedule (the collection officer's day),
    /// 8.4 Reschedule wizard, 8.5 Dunning console.
    /// <para>
    /// Nothing here decides anything. Statuses are derived by the engine as of
    /// now (BR-INS-007) and never stored, so these screens read
    /// <c>GetScheduleAsync</c> rather than an <c>Installment.Status</c> column —
    /// there is none, deliberately. Every rule that could refuse an action
    /// (a proposal that does not cover the remainder, a promise outside the
    /// horizon, a PDC belonging to another payer) lives behind the interface and
    /// surfaces here as a message, which is why the actions are thin.
    /// </para>
    /// <para>
    /// Weekend days come from the school's configured week, because BR-INS-002
    /// shifts a due date off a non-working day and getting that wrong silently
    /// moves money in the schedule.
    /// </para>
    /// </summary>
    [Route("installments")]
    public class InstallmentsController : Controller
    {
        private readonly IInstallmentAdmin _installments;
        private readonly AppDbContext _db;
        private readonly IWorkingYearContext _workingYear;
        private readonly ICurrentUser _user;
        private readonly ISystemSetupAdmin _setup;
        private readonly IUsageInspector<PlanTemplate> _templateUsage;

        public InstallmentsController(IInstallmentAdmin installments, AppDbContext db, IWorkingYearContext workingYear, ICurrentUser user, ISystemSetupAdmin setup, IUsageInspector<PlanTemplate> templateUsage)
        {
            _installments = installments;
            _db = db;
            _workingYear = workingYear;
            _user = user;
            _setup = setup;
            _templateUsage = templateUsage;
        }

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        // ================================================================== 8.1 Template designer

        [HttpGet("")]
        [RequirePermission(ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Templates, ActionVerb.View)]
        public async Task<IActionResult> Index(int? year = null)
        {
            var m = new TemplatesViewModel();
            await FillPageAsync(m, year);
            if (m.Year == null)
            {
                return View(m);
            }

            var yid = m.Year.Id;
            var templates = await _db.PlanTemplates.IgnoreQueryFilters().AsNoTracking()
                .Include(t => t.Installments)
                .Where(t => t.SchoolId == _db.CurrentSchoolId && t.AcademicYearId == yid)
                .OrderBy(t => t.NameEn).ToListAsync();

            var counts = await _db.PlanAssignments.AsNoTracking()
                .Where(a => a.AcademicYearId == yid)
                .GroupBy(a => a.PlanTemplateId)
                .Select(g => new { TemplateId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.TemplateId, x => x.Count);

            // The guard is asked per template, not inferred from the assignment count above: the count is
            // scoped to this year, while what blocks a delete is any assignment at all.
            var rows = new List<TemplatesViewModel.Row>();
            foreach (var t in templates)
            {
                rows.Add(new TemplatesViewModel.Row(
                    t,
                    t.FeeCategoryId == null ? null : m.Categories.FirstOrDefault(c => c.Id == t.FeeCategoryId),
                    counts.TryGetValue(t.Id, out var c) ? c : 0,
                    await _templateUsage.InspectAsync(t.Id, HttpContext.RequestAborted)));
            }

            m.Rows = rows;

            return View(m);
        }

        [HttpPost("templates/new")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Templates, ActionVerb.Create)]
        public async Task<IActionResult> CreateTemplate(
            int academicYearId, string nameAr, string nameEn, int? feeCategoryId,
            decimal downPaymentPercent, int graceDays,
            string[] percents, string[] dueDates, string[] offsets)
        {
            var parsed = ParseSplits(percents, dueDates, offsets, academicYearId);
            if (parsed.Failure != null)
            {
                return parsed.Failure;
            }


            try
            {
                await _installments.DefineTemplateAsync(
                    academicYearId, nameAr?.Trim() ?? string.Empty, nameEn?.Trim() ?? string.Empty, parsed.Splits,
                    feeCategoryId, downPaymentPercent, graceDays, HttpContext.RequestAborted);
                TempData["Flash"] = T("Plan template saved as a draft.", "حُفظ قالب الخطة كمسودة.");
            }
            catch (InvalidTemplateSplitException)
            {
                // The engine's own message is English and written for a developer reading a stack trace.
                // The checks above catch both of its cases first, so reaching here means a rule it holds
                // and this screen does not — say so in the operator's language rather than leaking it.
                TempData["Error"] = T(
                    "The splits were refused: they must total 100% and each needs a due-date rule (BR-INS-001).",
                    "رُفضت الشرائح: يجب أن يبلغ مجموعها 100% وأن تحمل كل شريحة قاعدة استحقاق (BR-INS-001).");
            }

            return RedirectToAction(nameof(Index), new { year = academicYearId });
        }

        /// <summary>The i-th posted value, or null when the array is shorter — a row whose field the browser omitted entirely.</summary>
        private static string? At(string[]? values, int index)
            => values != null && index < values.Length ? values[index] : null;

        /// <summary>
        /// Turns the designer's three posted columns into splits, or a redirect
        /// carrying the reason. Shared by create and edit so the two cannot drift
        /// into disagreeing about what a valid template is.
        /// <para>
        /// The columns are bound as <c>string[]</c>, not as
        /// <c>decimal[]</c>/<c>DateTime?[]</c>/<c>int?[]</c>, and that is not a style
        /// choice. Model binding drops an entry it cannot convert, so a row filling
        /// only its date and a row filling only its offset collapse into shorter
        /// arrays — and the second row's offset silently lands on the first. The
        /// three stay index-aligned only if every posted field survives binding,
        /// empty ones included.
        /// </para>
        /// </summary>
        private (List<TemplateSplit> Splits, IActionResult? Failure) ParseSplits(
            string[] percents, string[] dueDates, string[] offsets, int academicYearId)
        {
            var splits = new List<TemplateSplit>();
            var rowCount = percents?.Length ?? 0;
            for (var i = 0; i < rowCount; i++)
            {
                if (!decimal.TryParse(At(percents, i), NumberStyles.Number, CultureInfo.InvariantCulture, out var percent) || percent <= 0m)
                {
                    // A blank percentage means a spare row the operator never filled, not an error.
                    continue;
                }

                var due = DateTime.TryParse(At(dueDates, i), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : (DateTime?)null;
                var offset = int.TryParse(At(offsets, i), NumberStyles.Integer, CultureInfo.InvariantCulture, out var o) ? o : (int?)null;

                if (due == null && offset == null)
                {
                    return (splits, Refuse(academicYearId, T(
                        $"Split #{splits.Count + 1} has no due-date rule: give it either a date or an offset in days from the start of the year (BR-INS-001).",
                        $"الشريحة رقم {splits.Count + 1} بلا قاعدة استحقاق: امنحها تاريخاً أو عدد أيام من بداية العام (BR-INS-001).")));
                }

                splits.Add(new TemplateSplit(percent, due, offset));
            }

            if (splits.Count == 0)
            {
                return (splits, Refuse(academicYearId, T("Add at least one split before saving.", "أضف شريحة واحدة على الأقل قبل الحفظ.")));
            }

            var total = splits.Sum(s => s.Percent);
            if (Math.Abs(total - 100m) > 0.005m)
            {
                return (splits, Refuse(academicYearId, T(
                    $"The splits total {total:0.##}%, not 100% (BR-INS-001).",
                    $"مجموع الشرائح {total:0.##}% لا 100% (BR-INS-001).")));
            }

            return (splits, null);
        }

        private IActionResult Refuse(int academicYearId, string message)
        {
            TempData["Error"] = message;
            return RedirectToAction(nameof(Index), new { year = academicYearId });
        }

        [HttpGet("templates/{id:int}")]
        [RequirePermission(ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Templates, ActionVerb.View)]
        public async Task<IActionResult> TemplateDetails(int id)
        {
            var template = await _db.PlanTemplates.IgnoreQueryFilters().AsNoTracking()
                .Include(t => t.Installments)
                .SingleOrDefaultAsync(t => t.Id == id && t.SchoolId == _db.CurrentSchoolId);
            if (template == null)
            {
                return NotFound();
            }

            var year = await _db.AcademicYears.AsNoTracking().SingleOrDefaultAsync(y => y.Id == template.AcademicYearId);

            var m = new TemplateDetailViewModel
            {
                Template = template,
                Year = year,
                Category = template.FeeCategoryId == null
                    ? null
                    : await _db.FeeCategories.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(c => c.Id == template.FeeCategoryId),
            };

            // An offset counts from the year's start, so a template with no year to count from can only
            // show the rule itself. That is a real state — a template outlives the picker that made it.
            m.Splits = template.Installments.OrderBy(s => s.SequenceNumber)
                .Select(s => new TemplateDetailViewModel.SplitRow(
                    s,
                    s.DueDate ?? (s.OffsetDaysFromYearStart is int off && year != null ? year.StartDate.AddDays(off) : null)))
                .ToList();

            var assignments = await _db.PlanAssignments.AsNoTracking().Where(a => a.PlanTemplateId == id).ToListAsync();
            if (assignments.Count > 0)
            {
                var studentIds = assignments.Select(a => a.StudentId).Distinct().ToList();
                var students = await _db.Students.IgnoreQueryFilters().AsNoTracking().Where(s => studentIds.Contains(s.Id)).ToListAsync();
                var payerCards = await PayerCardsAsync(assignments.Select(a => a.PayerId));
                var assignmentIds = assignments.Select(a => a.Id).ToList();
                var counts = await _db.Installments.AsNoTracking()
                    .Where(i => assignmentIds.Contains(i.PlanAssignmentId) && !i.IsSuperseded)
                    .GroupBy(i => i.PlanAssignmentId)
                    .Select(g => new { Id = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Id, x => x.Count);

                m.Usage = assignments.Select(a => new TemplateDetailViewModel.UsageRow(
                    a,
                    students.First(s => s.Id == a.StudentId),
                    payerCards.FirstOrDefault(p => p.Payer.Id == a.PayerId),
                    counts.TryGetValue(a.Id, out var c) ? c : 0)).ToList();
            }

            return View(m);
        }

        [HttpPost("templates/{id:int}/approve")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Templates, ActionVerb.Approve)]
        public async Task<IActionResult> ApproveTemplate(int id, int? year)
        {
            await _installments.ApproveTemplateAsync(id, HttpContext.RequestAborted);
            TempData["Flash"] = T("Template approved; it can now be assigned.", "اعتُمد القالب؛ صار قابلاً للإسناد.");
            return RedirectToAction(nameof(Index), new { year });
        }

        [HttpPost("templates/{id:int}/edit")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Templates, ActionVerb.Edit)]
        public async Task<IActionResult> EditTemplate(
            int id, int academicYearId, string nameAr, string nameEn, int? feeCategoryId,
            decimal downPaymentPercent, int graceDays,
            string[] percents, string[] dueDates, string[] offsets)
        {
            var parsed = ParseSplits(percents, dueDates, offsets, academicYearId);
            if (parsed.Failure != null)
            {
                return parsed.Failure;
            }

            try
            {
                await _installments.UpdateTemplateAsync(
                    id, nameAr?.Trim() ?? string.Empty, nameEn?.Trim() ?? string.Empty, parsed.Splits,
                    feeCategoryId, downPaymentPercent, graceDays, HttpContext.RequestAborted);
                TempData["Flash"] = T("Template updated.", "تم تحديث القالب.");
            }
            catch (PlanTemplateNotDraftException)
            {
                TempData["Error"] = T(
                    "An approved template cannot be edited — schedules were already generated from it. Create a new template instead.",
                    "لا يُعدَّل قالب معتمد — فقد وُلِّدت منه جداول بالفعل. أنشئ قالباً جديداً بدلاً من ذلك.");
            }
            catch (InvalidTemplateSplitException)
            {
                TempData["Error"] = T(
                    "The splits were refused: they must total 100% and each needs a due-date rule (BR-INS-001).",
                    "رُفضت الشرائح: يجب أن يبلغ مجموعها 100% وأن تحمل كل شريحة قاعدة استحقاق (BR-INS-001).");
            }

            return RedirectToAction(nameof(TemplateDetails), new { id });
        }

        [HttpPost("templates/{id:int}/delete")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Templates, ActionVerb.Deactivate)]
        public async Task<IActionResult> DeleteTemplate(int id, int? year)
        {
            try
            {
                await _installments.DeleteTemplateAsync(id, HttpContext.RequestAborted);
                TempData["Flash"] = T("Template deleted.", "حُذف القالب.");
            }
            catch (RecordInUseException ex)
            {
                // The screen hides the button when the guard says no, so arriving here means either a
                // stale page or a hand-made request. Either way the answer names what is in the way.
                TempData["Error"] = T(
                    $"This template cannot be deleted: {ex.Usage.Describe(arabic: false)}.",
                    $"لا يمكن حذف هذا القالب: {ex.Usage.Describe(arabic: true)}.");
            }

            return RedirectToAction(nameof(Index), new { year });
        }

        // ================================================================== 8.2 Assignment console

        [HttpGet("assign")]
        [RequirePermission(ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Assignment, ActionVerb.View)]
        public async Task<IActionResult> Assign(int? year = null, int? studentId = null)
        {
            var m = new AssignViewModel();
            await FillPageAsync(m, year);
            if (m.Year == null)
            {
                return View(m);
            }

            var yid = m.Year.Id;
            m.ApprovedTemplates = await _db.PlanTemplates.AsNoTracking()
                .Where(t => t.AcademicYearId == yid && t.Status == PlanTemplateStatus.Approved)
                .OrderBy(t => t.NameEn).ToListAsync();

            var enrolledIds = await _db.Enrollments.AsNoTracking()
                .Where(e => e.AcademicYearId == yid && e.Status == EnrollmentStatus.Active)
                .Select(e => e.StudentId).Distinct().ToListAsync();
            var students = await _db.Students.AsNoTracking()
                .Where(s => enrolledIds.Contains(s.Id)).OrderBy(s => s.StudentNo).Take(500).ToListAsync();
            var grades = await _db.GradeLevels.AsNoTracking().ToListAsync();
            var enrollments = await _db.Enrollments.AsNoTracking()
                .Where(e => e.AcademicYearId == yid && e.Status == EnrollmentStatus.Active).ToListAsync();

            // An enrolment names a GradeYearProfile, not a grade — the profile is the grade *as run in
            // this year*, with its own curriculum and age rules. The picker wants the grade's name, so
            // it hops through the profile rather than pretending the enrolment carries one.
            var profiles = await _db.GradeYearProfiles.AsNoTracking()
                .Where(p => p.AcademicYearId == yid).ToListAsync();

            m.Students = students.Select(s =>
            {
                var profileId = enrollments.FirstOrDefault(e => e.StudentId == s.Id)?.GradeYearProfileId;
                var gradeId = profiles.FirstOrDefault(p => p.Id == profileId)?.GradeLevelId;
                return new AssignViewModel.StudentOption(s, grades.FirstOrDefault(g => g.Id == gradeId));
            }).ToList();

            if (studentId is int sid)
            {
                m.Selected = students.FirstOrDefault(s => s.Id == sid);
                if (m.Selected != null)
                {
                    // Only guardians the family marked financially responsible are offered first — assigning a
                    // plan to the wrong parent is not a cosmetic mistake, it addresses every dunning message
                    // and every statement to someone who did not agree to pay.
                    var links = await _db.StudentGuardianLinks.AsNoTracking().Where(l => l.StudentId == sid).ToListAsync();
                    var parentIds = links.Select(l => l.ParentId).ToList();
                    var parents = await _db.Parents.AsNoTracking().Where(p => parentIds.Contains(p.Id)).ToListAsync();
                    var payers = await _db.Payers.AsNoTracking().Where(p => p.ParentId != null && parentIds.Contains(p.ParentId.Value)).ToListAsync();

                    m.Payers = parents.Select(p => new AssignViewModel.PayerOption(
                        p,
                        links.Any(l => l.ParentId == p.Id && l.IsFinanciallyResponsible),
                        payers.FirstOrDefault(x => x.ParentId == p.Id)))
                        .OrderByDescending(o => o.IsFinanciallyResponsible)
                        .ToList();

                    var existing = await _db.PlanAssignments.AsNoTracking()
                        .Where(a => a.StudentId == sid && a.AcademicYearId == yid).ToListAsync();
                    var templateIds = existing.Select(a => a.PlanTemplateId).ToList();
                    var templates = await _db.PlanTemplates.IgnoreQueryFilters().AsNoTracking()
                        .Where(t => templateIds.Contains(t.Id)).ToListAsync();
                    m.Existing = existing
                        .Select(a => new AssignViewModel.ExistingRow(a, templates.First(t => t.Id == a.PlanTemplateId)))
                        .ToList();
                }
            }

            return View(m);
        }

        [HttpPost("assign")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Assignment, ActionVerb.Create)]
        public async Task<IActionResult> AssignPlan(int studentId, int payerId, int planTemplateId, bool isException, string? exceptionReason, int? year)
        {
            try
            {
                var assignment = await _installments.AssignPlanAsync(
                    studentId, payerId, planTemplateId, await WeekendDaysAsync(),
                    isException, Blank(exceptionReason), HttpContext.RequestAborted);
                TempData["Flash"] = T("Schedule generated.", "تم توليد الجدول.");
                return RedirectToAction(nameof(Schedule), new { id = assignment.Id });
            }
            catch (Exception ex) when (ex is PlanTemplateNotApprovedException or NoChargesToScheduleException or PlanAssignmentExistsException or ArgumentException)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
                return RedirectToAction(nameof(Assign), new { year, studentId });
            }
        }

        // ================================================================== 8.3 Family schedule

        [HttpGet("family")]
        [RequirePermission(ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Schedule, ActionVerb.View)]
        public async Task<IActionResult> Family(int? payerId = null, string? q = null)
        {
            var m = new FamilyScheduleViewModel { Q = q };
            m.Matches = await FinanceQueries.SearchPayersAsync(_db, q);
            m.Selected = payerId != null ? await FinanceQueries.CardAsync(_db, payerId.Value) : null;
            if (m.Selected == null)
            {
                return View(m);
            }

            var pid = m.Selected.Payer.Id;
            var assignments = await _db.PlanAssignments.AsNoTracking().Where(a => a.PayerId == pid).ToListAsync();
            if (assignments.Count == 0)
            {
                return View(m);
            }

            var templateIds = assignments.Select(a => a.PlanTemplateId).Distinct().ToList();
            var templates = await _db.PlanTemplates.IgnoreQueryFilters().AsNoTracking().Where(t => templateIds.Contains(t.Id)).ToListAsync();
            var studentIds = assignments.Select(a => a.StudentId).Distinct().ToList();
            var students = await _db.Students.IgnoreQueryFilters().AsNoTracking().Where(s => studentIds.Contains(s.Id)).ToListAsync();

            var openPromises = await _db.PromisesToPay.AsNoTracking()
                .Where(p => p.Status == PromiseStatus.Open).Select(p => p.InstallmentId).ToListAsync();
            var pdcs = await _db.Pdcs.AsNoTracking().Where(p => p.PayerId == pid).ToListAsync();
            var covered = await _db.Installments.AsNoTracking()
                .Where(i => i.CoveringPdcId != null).Select(i => new { i.Id, i.CoveringPdcId }).ToListAsync();

            var rows = new List<FamilyScheduleViewModel.Row>();
            foreach (var a in assignments)
            {
                foreach (var v in await _installments.GetScheduleAsync(a.Id, HttpContext.RequestAborted))
                {
                    var pdcId = covered.FirstOrDefault(c => c.Id == v.InstallmentId)?.CoveringPdcId;
                    rows.Add(new FamilyScheduleViewModel.Row(
                        v, a, templates.First(t => t.Id == a.PlanTemplateId),
                        students.First(s => s.Id == a.StudentId),
                        openPromises.Contains(v.InstallmentId),
                        pdcId == null ? null : pdcs.FirstOrDefault(p => p.Id == pdcId)));
                }
            }

            m.Rows = rows.OrderBy(r => r.View.DueDate).ThenBy(r => r.Student.StudentNo).ToList();
            m.LivePdcs = pdcs.Where(p => p.Status is PdcStatus.Lodged or PdcStatus.Due or PdcStatus.Deposited).ToList();
            return View(m);
        }

        [HttpGet("schedule/{id:int}")]
        [RequirePermission(ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Schedule, ActionVerb.View)]
        public async Task<IActionResult> Schedule(int id)
        {
            var assignment = await _db.PlanAssignments.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id);
            if (assignment == null)
            {
                return NotFound();
            }

            var views = await _installments.GetScheduleAsync(id, HttpContext.RequestAborted);
            var installmentIds = views.Select(v => v.InstallmentId).ToList();
            var promises = await _db.PromisesToPay.AsNoTracking()
                .Where(p => installmentIds.Contains(p.InstallmentId) && p.Status == PromiseStatus.Open).ToListAsync();
            var pdcs = await _db.Pdcs.AsNoTracking().Where(p => p.PayerId == assignment.PayerId).ToListAsync();
            var covered = await _db.Installments.AsNoTracking()
                .Where(i => installmentIds.Contains(i.Id) && i.CoveringPdcId != null)
                .Select(i => new { i.Id, i.CoveringPdcId }).ToListAsync();

            var m = new ScheduleDetailViewModel
            {
                Assignment = assignment,
                Template = await _db.PlanTemplates.IgnoreQueryFilters().AsNoTracking().SingleAsync(t => t.Id == assignment.PlanTemplateId),
                Student = await _db.Students.IgnoreQueryFilters().AsNoTracking().SingleAsync(s => s.Id == assignment.StudentId),
                Payer = await FinanceQueries.CardAsync(_db, assignment.PayerId),
                LivePdcs = pdcs.Where(p => p.Status is PdcStatus.Lodged or PdcStatus.Due or PdcStatus.Deposited).ToList(),
                RescheduleCases = await _db.RescheduleCases.AsNoTracking()
                    .Where(c => c.PlanAssignmentId == id).OrderByDescending(c => c.Id).ToListAsync(),
            };

            m.Rows = views.Select(v =>
            {
                var pdcId = covered.FirstOrDefault(c => c.Id == v.InstallmentId)?.CoveringPdcId;
                return new ScheduleDetailViewModel.Row(
                    v,
                    promises.FirstOrDefault(p => p.InstallmentId == v.InstallmentId),
                    pdcId == null ? null : pdcs.FirstOrDefault(p => p.Id == pdcId));
            }).ToList();

            return View(m);
        }

        // ---- per-installment actions, shared by both schedule screens ----

        [HttpPost("installments/{id:int}/promise")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Schedule, ActionVerb.Edit)]
        public async Task<IActionResult> RecordPromise(int id, DateTime promisedDate, decimal amount, string? returnUrl)
        {
            try
            {
                await _installments.RecordPromiseAsync(id, _user.UserId, promisedDate, amount, cancellationToken: HttpContext.RequestAborted);
                TempData["Flash"] = T("Promise to pay recorded.", "سُجِّل وعد بالسداد.");
            }
            catch (Exception ex) when (ex is PromiseDateOutOfRangeException or InvalidOperationException)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return Back(returnUrl);
        }

        [HttpPost("installments/{id:int}/cover-pdc")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Schedule, ActionVerb.Edit)]
        public async Task<IActionResult> CoverWithPdc(int id, int pdcId, string? returnUrl)
        {
            try
            {
                await _installments.MarkPdcCoveredAsync(id, pdcId, HttpContext.RequestAborted);
                TempData["Flash"] = T("Installment marked as covered by the cheque.", "وُسم القسط كمغطّى بالشيك.");
            }
            catch (PdcNotCoverableException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return Back(returnUrl);
        }

        [HttpPost("installments/{id:int}/write-off")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Schedule, ActionVerb.Approve)]
        public async Task<IActionResult> WriteOff(int id, string reason, string? returnUrl)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = T("A write-off reason is required.", "سبب الشطب مطلوب.");
                return Back(returnUrl);
            }

            await _installments.WriteOffAsync(id, reason.Trim(), HttpContext.RequestAborted);
            TempData["Flash"] = T("Installment written off.", "شُطب القسط.");
            return Back(returnUrl);
        }

        [HttpPost("schedule/{id:int}/reduce")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Schedule, ActionVerb.Edit)]
        public async Task<IActionResult> Reduce(int id, decimal reduction, string reason)
        {
            try
            {
                await _installments.ReduceScheduleAsync(id, reduction, reason?.Trim() ?? string.Empty, HttpContext.RequestAborted);
                TempData["Flash"] = T("Schedule reduced.", "خُفِّض الجدول.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Schedule), new { id });
        }

        // ================================================================== 8.4 Reschedule wizard

        [HttpGet("schedule/{id:int}/reschedule")]
        [RequirePermission(ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Schedule, ActionVerb.Submit)]
        public async Task<IActionResult> Reschedule(int id)
        {
            var assignment = await _db.PlanAssignments.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id);
            if (assignment == null)
            {
                return NotFound();
            }

            var views = await _installments.GetScheduleAsync(id, HttpContext.RequestAborted);
            var m = new RescheduleWizardViewModel
            {
                Assignment = assignment,
                Template = await _db.PlanTemplates.IgnoreQueryFilters().AsNoTracking().SingleAsync(t => t.Id == assignment.PlanTemplateId),
                Student = await _db.Students.IgnoreQueryFilters().AsNoTracking().SingleAsync(s => s.Id == assignment.StudentId),
                Payer = await FinanceQueries.CardAsync(_db, assignment.PayerId),
                Unpaid = views.Where(v => v.Status is not (InstallmentStatus.Paid or InstallmentStatus.Rescheduled or InstallmentStatus.WrittenOff)).ToList(),
            };

            return View(m);
        }

        [HttpPost("schedule/{id:int}/reschedule")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Schedule, ActionVerb.Submit)]
        public async Task<IActionResult> ProposeReschedule(int id, string reason, DateTime[] dueDates, decimal[] amounts)
        {
            var proposal = new List<ProposedInstallment>();
            for (var i = 0; i < (dueDates?.Length ?? 0); i++)
            {
                if (amounts == null || i >= amounts.Length || amounts[i] <= 0m)
                {
                    continue;
                }

                proposal.Add(new ProposedInstallment(dueDates![i], amounts[i]));
            }

            try
            {
                await _installments.ProposeRescheduleAsync(
                    id, _user.UserId, reason?.Trim() ?? string.Empty, proposal,
                    await WeekendDaysAsync(), cancellationToken: HttpContext.RequestAborted);
                TempData["Flash"] = T("Reschedule proposed; it awaits a decision.", "قُدِّم اقتراح إعادة الجدولة؛ بانتظار القرار.");
                return RedirectToAction(nameof(Cases));
            }
            catch (Exception ex) when (ex is RescheduleRemainderMismatchException or ArgumentException or InvalidOperationException)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
                return RedirectToAction(nameof(Reschedule), new { id });
            }
        }

        [HttpGet("cases")]
        [RequirePermission(ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Cases, ActionVerb.View)]
        public async Task<IActionResult> Cases(RescheduleCaseStatus? status = null)
        {
            var query = _db.RescheduleCases.AsNoTracking().AsQueryable();
            if (status != null)
            {
                query = query.Where(c => c.Status == status);
            }

            var cases = await query.OrderByDescending(c => c.Id).Take(300).ToListAsync();
            var assignmentIds = cases.Select(c => c.PlanAssignmentId).Distinct().ToList();
            var assignments = await _db.PlanAssignments.AsNoTracking().Where(a => assignmentIds.Contains(a.Id)).ToListAsync();
            var studentIds = assignments.Select(a => a.StudentId).Distinct().ToList();
            var students = await _db.Students.IgnoreQueryFilters().AsNoTracking().Where(s => studentIds.Contains(s.Id)).ToListAsync();
            var payerCards = await PayerCardsAsync(assignments.Select(a => a.PayerId));

            var m = new RescheduleCasesViewModel
            {
                Filter = status,
                Rows = cases.Select(c =>
                {
                    var a = assignments.First(x => x.Id == c.PlanAssignmentId);
                    return new RescheduleCasesViewModel.Row(
                        c, a, students.First(s => s.Id == a.StudentId),
                        payerCards.FirstOrDefault(p => p.Payer.Id == a.PayerId));
                }).ToList(),
            };

            return View(m);
        }

        [HttpPost("cases/{id:int}/decide")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Cases, ActionVerb.Approve)]
        public async Task<IActionResult> DecideCase(int id, bool approve, string? decisionReason)
        {
            try
            {
                await _installments.DecideRescheduleAsync(id, approve, Blank(decisionReason), HttpContext.RequestAborted);
                TempData["Flash"] = approve
                    ? T("Reschedule approved; the new schedule is live.", "اعتُمدت إعادة الجدولة؛ الجدول الجديد سارٍ.")
                    : T("Reschedule rejected.", "رُفضت إعادة الجدولة.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Cases));
        }

        // ================================================================== 8.5 Dunning console

        [HttpGet("dunning")]
        [RequirePermission(ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Dunning, ActionVerb.View)]
        public async Task<IActionResult> Dunning()
        {
            var m = new DunningConsoleViewModel { LastRunFiredCount = TempData["DunningFired"] as int? };

            var events = await _db.DunningEvents.AsNoTracking().OrderByDescending(e => e.Id).Take(200).ToListAsync();
            var brokenPromises = await _db.PromisesToPay.AsNoTracking()
                .Where(p => p.Status == PromiseStatus.Broken).OrderByDescending(p => p.Id).Take(100).ToListAsync();

            // One installment can appear in all three panels; loading the supporting rows once keeps this
            // to a fixed number of queries regardless of how many events the ladder produced.
            var installmentIds = events.Select(e => e.InstallmentId)
                .Concat(brokenPromises.Select(p => p.InstallmentId)).Distinct().ToList();
            var installments = await _db.Installments.AsNoTracking().Where(i => installmentIds.Contains(i.Id)).ToListAsync();
            var assignmentIds = installments.Select(i => i.PlanAssignmentId).Distinct().ToList();
            var assignments = await _db.PlanAssignments.AsNoTracking().Where(a => assignmentIds.Contains(a.Id)).ToListAsync();
            var studentIds = assignments.Select(a => a.StudentId).Distinct().ToList();
            var students = await _db.Students.IgnoreQueryFilters().AsNoTracking().Where(s => studentIds.Contains(s.Id)).ToListAsync();
            var payerCards = await PayerCardsAsync(assignments.Select(a => a.PayerId));

            Student? StudentFor(int installmentId)
            {
                var inst = installments.FirstOrDefault(i => i.Id == installmentId);
                var a = inst == null ? null : assignments.FirstOrDefault(x => x.Id == inst.PlanAssignmentId);
                return a == null ? null : students.FirstOrDefault(s => s.Id == a.StudentId);
            }

            PayerCard? PayerFor(int installmentId)
            {
                var inst = installments.FirstOrDefault(i => i.Id == installmentId);
                var a = inst == null ? null : assignments.FirstOrDefault(x => x.Id == inst.PlanAssignmentId);
                return a == null ? null : payerCards.FirstOrDefault(p => p.Payer.Id == a.PayerId);
            }

            m.Recent = events
                .Where(e => installments.Any(i => i.Id == e.InstallmentId))
                .Select(e =>
                {
                    var inst = installments.First(i => i.Id == e.InstallmentId);
                    var a = assignments.First(x => x.Id == inst.PlanAssignmentId);
                    return new DunningConsoleViewModel.EventRow(e, inst, a, students.First(s => s.Id == a.StudentId), PayerFor(e.InstallmentId));
                }).ToList();

            m.BrokenPromises = brokenPromises
                .Where(p => StudentFor(p.InstallmentId) != null)
                .Select(p => new DunningConsoleViewModel.BrokenPromiseRow(
                    p, installments.First(i => i.Id == p.InstallmentId), StudentFor(p.InstallmentId)!, PayerFor(p.InstallmentId)))
                .ToList();

            // The escalation panel is the point of the screen: the ladder's last rung means a human has to
            // act, so those are listed with their live derived status rather than the status at firing time.
            var escalated = events.Where(e => e.Step == DunningStep.Escalation).Select(e => e.InstallmentId).Distinct().ToList();
            var escalationRows = new List<DunningConsoleViewModel.EscalationRow>();
            foreach (var assignmentId in installments.Where(i => escalated.Contains(i.Id)).Select(i => i.PlanAssignmentId).Distinct())
            {
                var a = assignments.First(x => x.Id == assignmentId);
                foreach (var v in await _installments.GetScheduleAsync(assignmentId, HttpContext.RequestAborted))
                {
                    if (!escalated.Contains(v.InstallmentId))
                    {
                        continue;
                    }

                    escalationRows.Add(new DunningConsoleViewModel.EscalationRow(
                        installments.First(i => i.Id == v.InstallmentId), v,
                        students.First(s => s.Id == a.StudentId),
                        payerCards.FirstOrDefault(p => p.Payer.Id == a.PayerId)));
                }
            }

            m.Escalations = escalationRows.OrderBy(r => r.View.DueDate).ToList();
            return View(m);
        }

        [HttpPost("dunning/run")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Dunning, ActionVerb.Post)]
        public async Task<IActionResult> RunDunning()
        {
            var fired = await _installments.RunDunningAsync(HttpContext.RequestAborted);
            TempData["DunningFired"] = fired.Count;
            TempData["Flash"] = T($"Dunning pass complete: {fired.Count} step(s) fired.", $"اكتملت جولة المطالبات: أُطلقت {fired.Count} خطوة.");
            return RedirectToAction(nameof(Dunning));
        }

        [HttpPost("dunning/evaluate-promises")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Dunning, ActionVerb.Post)]
        public async Task<IActionResult> EvaluatePromises()
        {
            var broken = await _installments.EvaluatePromisesAsync(HttpContext.RequestAborted);
            TempData["Flash"] = T($"Promises evaluated: {broken} broken.", $"قُيّمت الوعود: {broken} مُخلَف.");
            return RedirectToAction(nameof(Dunning));
        }

        // ================================================================== helpers

        private async Task FillPageAsync(InstallmentsPageViewModel m, int? yearId)
        {
            var years = await _db.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartDate).ToListAsync();
            m.Years = years;
            m.Year = years.FirstOrDefault(y => y.Id == (yearId ?? _workingYear.AcademicYearId))
                ?? years.FirstOrDefault(y => y.Status == AcademicYearStatus.Active)
                ?? years.FirstOrDefault();

            if (m is TemplatesViewModel t)
            {
                t.Categories = await _db.FeeCategories.AsNoTracking().OrderBy(c => c.NameEn).ToListAsync();
            }
        }

        /// <summary>
        /// The school's non-working days, so BR-INS-002 shifts a due date off one.
        /// Read from the same <c>Regional.WorkingDays</c> setting the calendar
        /// board uses, through the same helper — a second source for the working
        /// week would let a due date land on a day the calendar calls a weekend.
        /// </summary>
        private async Task<ISet<DayOfWeek>> WeekendDaysAsync()
        {
            var working = await _setup.GetSettingAsync(SettingKeys.WorkingDays, _workingYear.AcademicYearId)
                ?? "Sunday,Monday,Tuesday,Wednesday,Thursday";
            return new HashSet<DayOfWeek>(WorkingWeek.WeekendDays(working));
        }

        private async Task<IReadOnlyList<PayerCard>> PayerCardsAsync(IEnumerable<int> payerIds)
        {
            var ids = payerIds.Distinct().ToList();
            if (ids.Count == 0)
            {
                return Array.Empty<PayerCard>();
            }

            var payers = await _db.Payers.AsNoTracking().Where(p => ids.Contains(p.Id)).ToListAsync();
            return await FinanceQueries.CardsAsync(_db, payers, includeChildren: false);
        }

        private IActionResult Back(string? returnUrl)
            => !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Family));

        private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}
