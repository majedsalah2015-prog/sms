using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Application.Sections;
using Sms.Domain.Common;
using Sms.Domain.Grades;
using Sms.Domain.Schools;
using Sms.Domain.Sections;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/06 §8.1–§8.4: section list per grade/year with capacity
    /// meters, section detail (roster, homeroom history, assign student,
    /// transfer dialog, close), and §8.3's drag-drop assignment board with
    /// rule-based auto-distribute and its proposal diff.
    /// <para>
    /// The board's proposals come from <see cref="SectionBalanceProposer"/> and
    /// cover BR-SCN-008's size-balance and gender-ratio inputs only. Language and
    /// curriculum grouping, sibling together/apart preferences and Discipline
    /// keep-apart pairs are the rule's other inputs and are <b>not</b> applied —
    /// the flags they read are not modelled — and the screen says so rather than
    /// letting a registrar assume a behavioural pairing was honoured.
    /// </para>
    /// <para>
    /// §4 also asks for VP approval (P2) on a bulk transfer above a configurable
    /// count. Not wired: there is no workflow definition for a section transfer in
    /// the catalogue, and the threshold is doc/Modules/06 §14's open question 2,
    /// unconfirmed. The board applies directly, reason-coded and effective-dated;
    /// the approval route is outstanding work, not a substituted design.
    /// </para>
    /// <para>
    /// §8.5's merge/close wizard is still outstanding.
    /// </para>
    /// Homeroom teachers are UserAccount ids on HomeroomAssignment (the
    /// documented identity-bridge inconsistency): the picker lists
    /// TeacherProfiles and can only assign those whose Employee has a linked
    /// user account.
    /// </summary>
    [Route("sections")]
    public class SectionsController : Controller
    {
        private readonly ISectionAdmin _sections;
        private readonly AppDbContext _db;
        private readonly IWorkingYearContext _workingYear;
        private readonly IClock _clock;

        public SectionsController(ISectionAdmin sections, AppDbContext db, IWorkingYearContext workingYear, IClock clock)
        {
            _sections = sections;
            _db = db;
            _workingYear = workingYear;
            _clock = clock;
        }

        /// <summary>
        /// A grade with more than a dozen sections is a data-entry mistake, not a
        /// school — and the letter sequence itself runs out at ten. The cap keeps a
        /// mistyped count from opening two hundred rows nobody asked for.
        /// </summary>
        private const int MaxSectionsPerBatch = 12;

        private CancellationToken Ct => HttpContext.RequestAborted;

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        [HttpGet("")]
        [RequirePermission(ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Sections_, ActionVerb.View)]
        public async Task<IActionResult> Index(int? year = null)
        {
            var years = await _db.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartDate).ToListAsync();
            var selected = years.FirstOrDefault(y => y.Id == (year ?? _workingYear.AcademicYearId)) ?? years.FirstOrDefault(y => y.Status == AcademicYearStatus.Active) ?? years.FirstOrDefault();
            var model = new SectionListViewModel { Years = years, Year = selected, Capacity = 25 };
            if (selected != null)
            {
                var profiles = await _db.GradeYearProfiles.AsNoTracking().Where(p => p.AcademicYearId == selected.Id).ToListAsync();
                var grades = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking().Where(g => g.SchoolId == _db.CurrentSchoolId).ToListAsync();
                var sections = await _db.Sections.AsNoTracking().Where(s => s.AcademicYearId == selected.Id).OrderBy(s => s.NameEn).ToListAsync();
                var members = await _db.SectionMemberships.AsNoTracking().Where(m => m.AcademicYearId == selected.Id && m.EffectiveToUtc == null).GroupBy(m => m.SectionId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
                var homerooms = await _db.HomeroomAssignments.AsNoTracking().Where(h => h.AcademicYearId == selected.Id && h.EffectiveToUtc == null).ToListAsync();
                var teacherNames = await TeacherNamesByUserAsync();
                var rooms = await _db.Rooms.AsNoTracking().ToDictionaryAsync(r => r.Id, r => IsArabic ? r.Name.NameAr : r.Name.NameEn);

                model.Profiles = profiles.Where(p => p.IsActive).Select(p => { var g = grades.First(x => x.Id == p.GradeLevelId); return (p.Id, g.Name.NameAr, g.Name.NameEn, p.TargetSections, p.TargetSectionSize); }).OrderBy(x => x.NameEn).ToList();
                model.Rows = sections.Select(s =>
                {
                    var p = profiles.First(x => x.Id == s.GradeYearProfileId);
                    var g = grades.First(x => x.Id == p.GradeLevelId);
                    var hr = homerooms.FirstOrDefault(h => h.SectionId == s.Id);
                    return new SectionListViewModel.Row(s, g, p, members.TryGetValue(s.Id, out var n) ? n : 0,
                        hr == null ? null : (teacherNames.TryGetValue(hr.TeacherUserId, out var tn) ? tn : $"#{hr.TeacherUserId}"),
                        s.DefaultClassroomId != null && rooms.TryGetValue(s.DefaultClassroomId.Value, out var rn) ? rn : null);
                }).OrderBy(r => r.Grade.SequenceOrder).ThenBy(r => r.Section.NameEn).ToList();
            }

            return View(model);
        }

        [HttpPost("")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Sections_, ActionVerb.Create)]
        public async Task<IActionResult> Define(SectionListViewModel form, int? year)
        {
            try
            {
                if (form.GradeYearProfileId == null) throw new InvalidOperationException(T("Choose a grade.", "اختر صفاً."));
                Require(form.NameAr, T("Name (Arabic)", "الاسم (عربي)"));
                Require(form.NameEn, T("Name (English)", "الاسم (إنجليزي)"));
                var s = await _sections.DefineSectionAsync(form.GradeYearProfileId.Value, form.NameAr!, form.NameEn!, form.Capacity ?? 25, form.GenderPolicy);
                TempData["Flash"] = T("Section created.", "تم إنشاء الشعبة.");
                return RedirectToAction(nameof(Details), new { id = s.Id });
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Index), new { year });
        }

        /// <summary>
        /// Opens a grade's sections in one go, named from its own convention
        /// (BR-SCN-001). A grade is planned as a number of sections — four sections of
        /// twenty-five — and typing four names by hand is the step that produces
        /// "1-A", "1-b" and "1 - C" in the same grade by the third year.
        /// </summary>
        [HttpPost("bulk")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Sections_, ActionVerb.Create)]
        public async Task<IActionResult> DefineMany(int? gradeYearProfileId, int count, int? capacity, GenderPolicy genderPolicy, int? year)
        {
            try
            {
                if (gradeYearProfileId == null) throw new InvalidOperationException(T("Choose a grade.", "اختر صفاً."));
                if (count is < 1 or > MaxSectionsPerBatch)
                {
                    throw new InvalidOperationException(string.Format(
                        T("Choose between 1 and {0} sections.", "اختر عدداً بين 1 و{0} شعبة."), MaxSectionsPerBatch));
                }

                var created = await _sections.DefineSectionsAsync(gradeYearProfileId.Value, count, capacity ?? 25, genderPolicy, Ct);
                TempData["Flash"] = string.Format(
                    T("{0} section(s) opened: {1}.", "فُتحت {0} شعبة: {1}."),
                    created.Count,
                    string.Join("، ", created.Select(s => IsArabic ? s.NameAr : s.NameEn)));
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Index), new { year });
        }

        /// <summary>
        /// The proposed names, for the screen to show before anything is written. A
        /// batch that silently picks names is one an operator has to undo section by
        /// section when it picks the wrong ones.
        /// </summary>
        [HttpGet("bulk/preview")]
        [RequirePermission(ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Sections_, ActionVerb.View)]
        public async Task<IActionResult> PreviewNames(int gradeYearProfileId, int count)
        {
            if (count is < 1 or > MaxSectionsPerBatch)
            {
                return Json(Array.Empty<object>());
            }

            var profile = await _db.GradeYearProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.Id == gradeYearProfileId, Ct);
            if (profile == null)
            {
                return NotFound();
            }

            var grade = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking()
                .SingleAsync(g => g.Id == profile.GradeLevelId, Ct);
            var existing = (await _db.Sections.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.SchoolId == _db.CurrentSchoolId && s.GradeYearProfileId == gradeYearProfileId)
                .OrderBy(s => s.Id)
                .Select(s => new { s.NameAr, s.NameEn })
                .ToListAsync(Ct))
                .Select(s => new SectionNameSequence.ExistingName(s.NameAr, s.NameEn))
                .ToList();

            var names = SectionNameSequence.Next(grade.Name.NameAr, grade.Name.NameEn, existing, count);
            return Json(names.Select(n => new { ar = n.NameAr, en = n.NameEn }));
        }

        [HttpGet("{id:int}")]
        [RequirePermission(ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Sections_, ActionVerb.View)]
        public async Task<IActionResult> Details(int id)
        {
            var model = await BuildDetailAsync(id);
            return model == null ? NotFound() : View(model);
        }

        [HttpPost("{id:int}/homeroom")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Roster, ActionVerb.Edit)]
        public async Task<IActionResult> Homeroom(int id, int? teacherUserId, DateTime? effectiveFrom)
        {
            try
            {
                if (teacherUserId == null) throw new InvalidOperationException(T("Choose a teacher with a linked user account.", "اختر معلماً له حساب مستخدم مرتبط."));
                await _sections.AssignHomeroomTeacherAsync(id, teacherUserId.Value, DateTime.SpecifyKind(effectiveFrom ?? _clock.UtcNow.Date, DateTimeKind.Utc));
                TempData["Flash"] = T("Homeroom teacher assigned; the previous assignment was closed (BR-SCN-004).", "تم تعيين رائد الفصل وإغلاق التعيين السابق (BR-SCN-004).");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost("{id:int}/assign")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Roster, ActionVerb.Edit)]
        public async Task<IActionResult> Assign(int id, int? enrollmentId, DateTime? effectiveFrom)
        {
            try
            {
                if (enrollmentId == null) throw new InvalidOperationException(T("Choose a student.", "اختر طالباً."));
                await _sections.AssignMembershipAsync(id, enrollmentId.Value, DateTime.SpecifyKind(effectiveFrom ?? _clock.UtcNow.Date, DateTimeKind.Utc));
                TempData["Flash"] = T("Student assigned.", "تم إسناد الطالب.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost("{id:int}/transfer")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Roster, ActionVerb.Edit)]
        public async Task<IActionResult> Transfer(int id, int enrollmentId, int? targetSectionId, string? reasonCode, DateTime? effectiveDate)
        {
            try
            {
                if (targetSectionId == null) throw new InvalidOperationException(T("Choose a target section.", "اختر الشعبة المستهدفة."));
                Require(reasonCode, T("Reason code", "رمز السبب"));
                await _sections.TransferMembershipAsync(enrollmentId, targetSectionId.Value, reasonCode!, effectiveDate ?? _clock.UtcNow.Date);
                TempData["Flash"] = T("Student transferred; history kept (BR-SCN-005/006).", "تم نقل الطالب مع حفظ السجل (BR-SCN-005/006).");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Details), new { id });
        }

        // --- Edit / delete ---------------------------------------------------------

        [HttpGet("{id:int}/edit")]
        [RequirePermission(ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Sections_, ActionVerb.Edit)]
        public async Task<IActionResult> Edit(int id)
        {
            var model = await BuildEditAsync(id);
            if (model == null) return NotFound();
            model.NameAr = model.Section.NameAr;
            model.NameEn = model.Section.NameEn;
            model.Capacity = model.Section.Capacity;
            model.GenderPolicy = model.Section.GenderPolicy;
            model.DefaultClassroomId = model.Section.DefaultClassroomId;
            return View(model);
        }

        [HttpPost("{id:int}/edit")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Sections_, ActionVerb.Edit)]
        public async Task<IActionResult> Edit(int id, SectionEditViewModel form)
        {
            var section = await _db.Sections.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id);
            if (section == null) return NotFound();
            try
            {
                Require(form.NameAr, T("Name (Arabic)", "الاسم (عربي)"));
                Require(form.NameEn, T("Name (English)", "الاسم (إنجليزي)"));
                await _sections.UpdateSectionAsync(id, form.NameAr!.Trim(), form.NameEn!.Trim(), form.Capacity ?? section.Capacity, form.GenderPolicy, form.DefaultClassroomId);
                TempData["Flash"] = T("Section updated.", "تم تحديث الشعبة.");
                return RedirectToAction(nameof(Index), new { year = section.AcademicYearId });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, UserMessage.For(ex, IsArabic));
                var model = (await BuildEditAsync(id))!;
                model.NameAr = form.NameAr; model.NameEn = form.NameEn; model.Capacity = form.Capacity; model.GenderPolicy = form.GenderPolicy; model.DefaultClassroomId = form.DefaultClassroomId;
                return View(model);
            }
        }

        [HttpPost("{id:int}/delete")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Sections_, ActionVerb.Deactivate)]
        public async Task<IActionResult> Delete(int id)
        {
            var section = await _db.Sections.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id);
            try
            {
                await _sections.DeleteSectionAsync(id);
                TempData["Flash"] = T("Section deleted.", "تم حذف الشعبة.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Index), new { year = section?.AcademicYearId });
        }

        private async Task<SectionEditViewModel?> BuildEditAsync(int id)
        {
            var section = await _db.Sections.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id);
            if (section == null) return null;
            var profile = await _db.GradeYearProfiles.AsNoTracking().SingleAsync(p => p.Id == section.GradeYearProfileId);
            var grade = await _db.GradeLevels.AsNoTracking().SingleAsync(g => g.Id == profile.GradeLevelId);
            var rooms = await _db.Rooms.AsNoTracking().ToListAsync();
            return new SectionEditViewModel
            {
                Id = id, Section = section,
                GradeLabelAr = $"{grade.Code} {grade.Name.NameAr}", GradeLabelEn = $"{grade.Code} {grade.Name.NameEn}",
                PlanSectionSize = profile.TargetSectionSize, GradeGender = profile.GenderPolicy,
                CurrentMembers = await _db.SectionMemberships.CountAsync(m => m.SectionId == id && m.EffectiveToUtc == null),
                Rooms = rooms.Select(r => (r.Id, r.Name.NameAr, r.Name.NameEn)).OrderBy(r => r.NameEn).ToList(),
            };
        }

        // The bare "close this section" POST that used to live here is gone: it could
        // only ever succeed on an empty section, so on a real one its whole answer was
        // "not allowed", which leaves the reader no further forward. CloseWizard below
        // handles both cases — an empty section closes in one press, a populated one
        // shows what closing costs and where everybody goes.

        // --- §8.3 assignment board -------------------------------------------------

        /// <summary>
        /// doc/Modules/06 §8.3. A whole grade's sections side by side with every
        /// student in them, and the column of students who are in none. Until now the
        /// only way to see a grade's distribution was to open each section in turn and
        /// hold the numbers in your head, which is not a way to decide anything.
        /// </summary>
        [HttpGet("board")]
        [RequirePermission(ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Board, ActionVerb.View)]
        public async Task<IActionResult> Board(int? year = null, int? grade = null)
        {
            var model = await BuildBoardAsync(year, grade);
            return View(model);
        }

        /// <summary>
        /// BR-SCN-008: rules propose, humans confirm. Nothing is written here — the
        /// diff comes back on the same screen with the moves, the headcount each
        /// section would end at, and anybody no compatible section had room for.
        /// </summary>
        [HttpPost("board/propose")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Board, ActionVerb.Edit)]
        public async Task<IActionResult> Propose(int? year, int? grade, bool rebalance = false)
        {
            var model = await BuildBoardAsync(year, grade);
            if (model.Grade == null || model.Columns.Count == 0)
            {
                TempData["Error"] = T("Open a grade with at least one section first.", "افتح صفاً له شعبة واحدة على الأقل أولاً.");
                return View(nameof(Board), model);
            }

            var students = model.Columns
                .SelectMany(c => c.Students.Select(s => new BalanceStudent(s.EnrollmentId, s.Gender, c.Section.Id)))
                .Concat(model.Unassigned.Select(s => new BalanceStudent(s.EnrollmentId, s.Gender, null)))
                .ToList();
            var seats = model.Columns.Select(c => new BalanceSeat(c.Section.Id, c.Capacity, c.Section.GenderPolicy)).ToList();

            var proposal = SectionBalanceProposer.Propose(students, seats, rebalance);
            var names = model.Columns.ToDictionary(c => c.Section.Id, c => IsArabic ? c.Section.NameAr : c.Section.NameEn);
            var cards = model.Columns.SelectMany(c => c.Students).Concat(model.Unassigned).ToDictionary(s => s.EnrollmentId);
            var before = model.Columns.ToDictionary(c => c.Section.Id, c => c.Students.Count);

            BoardProposalViewModel.Row Row(int enrollmentId, int? from, int to)
            {
                cards.TryGetValue(enrollmentId, out var card);
                return new BoardProposalViewModel.Row(
                    enrollmentId,
                    card?.StudentNo ?? "?",
                    card == null ? "?" : (IsArabic ? card.NameAr : card.NameEn),
                    from == null ? null : names.GetValueOrDefault(from.Value),
                    names.GetValueOrDefault(to, "?"),
                    to);
            }

            model.Proposal = new BoardProposalViewModel
            {
                Rebalanced = rebalance,
                Moves = proposal.Moves.Select(m => Row(m.EnrollmentId, m.FromSectionId, m.ToSectionId)).ToList(),
                Unplaced = proposal.UnplacedEnrollmentIds.Select(id => Row(id, null, 0)).ToList(),
                Tallies = model.Columns.Select(c => new BoardProposalViewModel.Tally(
                    IsArabic ? c.Section.NameAr : c.Section.NameEn,
                    before[c.Section.Id],
                    proposal.Fill.GetValueOrDefault(c.Section.Id),
                    c.Capacity)).ToList(),
                Payload = string.Join(",", proposal.Moves.Select(m => $"{m.EnrollmentId}:{m.ToSectionId}")),
            };

            return View(nameof(Board), model);
        }

        /// <summary>
        /// Writes a layout — the one a proposal offered, or the one a registrar dragged
        /// into place. Both arrive as the same payload and go through the same
        /// whole-batch validation in <see cref="ISectionAdmin.ApplyDistributionAsync"/>:
        /// the browser's live capacity and gender checks are a courtesy to the person
        /// dragging, not the place either rule is enforced.
        /// </summary>
        [HttpPost("board/apply")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Board, ActionVerb.Edit)]
        public async Task<IActionResult> ApplyBoard(int? year, int? grade, string? placements, string? reasonCode, DateTime? effectiveDate)
        {
            try
            {
                Require(reasonCode, T("Reason code", "رمز السبب"));
                var parsed = ParsePlacements(placements);
                if (parsed.Count == 0)
                {
                    TempData["Flash"] = T("Nothing to apply — no student changed section.", "لا شيء لتطبيقه — لم تتغير شعبة أي طالب.");
                    return RedirectToAction(nameof(Board), new { year, grade });
                }

                var moved = await _sections.ApplyDistributionAsync(
                    parsed, reasonCode!, effectiveDate ?? _clock.UtcNow.Date, Ct);

                TempData["Flash"] = moved == 0
                    ? T("Nothing to apply — no student changed section.", "لا شيء لتطبيقه — لم تتغير شعبة أي طالب.")
                    : T($"{moved} student(s) placed; every move is effective-dated and reason-coded (BR-SCN-005).",
                        $"تم إسناد {moved} طالباً؛ كل نقل مؤرَّخ ومُعلَّل (BR-SCN-005).");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }

            return RedirectToAction(nameof(Board), new { year, grade });
        }

        /// <summary>
        /// "12:3,15:4" → {12→3, 15→4}. Anything unparseable is dropped rather than
        /// guessed at: a malformed pair is a bug in the page, and acting on half of it
        /// would move a child nobody named.
        /// </summary>
        private static IReadOnlyDictionary<int, int> ParsePlacements(string? payload)
        {
            var result = new Dictionary<int, int>();
            if (string.IsNullOrWhiteSpace(payload))
            {
                return result;
            }

            foreach (var pair in payload.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split(':');
                if (parts.Length == 2
                    && int.TryParse(parts[0], out var enrollmentId)
                    && int.TryParse(parts[1], out var sectionId)
                    && enrollmentId > 0 && sectionId > 0)
                {
                    result[enrollmentId] = sectionId;
                }
            }

            return result;
        }

        private async Task<SectionBoardViewModel> BuildBoardAsync(int? year, int? grade)
        {
            var years = await _db.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartDate).ToListAsync(Ct);
            var selectedYear = years.FirstOrDefault(y => y.Id == (year ?? _workingYear.AcademicYearId))
                ?? years.FirstOrDefault(y => y.Status == AcademicYearStatus.Active)
                ?? years.FirstOrDefault();

            var model = new SectionBoardViewModel
            {
                Years = years, Year = selectedYear,
                EffectiveDate = _clock.UtcNow.Date,
                ReasonCode = SectionBoardViewModel.ReasonCodes[0],
            };
            if (selectedYear == null)
            {
                return model;
            }

            var profiles = await _db.GradeYearProfiles.AsNoTracking()
                .Where(p => p.AcademicYearId == selectedYear.Id && p.IsActive).ToListAsync(Ct);

            // IgnoreQueryFilters on the grade catalogue: a grade the school retired
            // mid-year still has children sitting in its sections, and the board is
            // where they get moved out of it.
            var grades = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking()
                .Where(g => g.SchoolId == _db.CurrentSchoolId).ToListAsync(Ct);

            var profileIds = profiles.Select(p => p.Id).ToList();
            var sectionCounts = await _db.Sections.AsNoTracking()
                .Where(s => profileIds.Contains(s.GradeYearProfileId) && s.Status == SectionStatus.Active)
                .GroupBy(s => s.GradeYearProfileId).Select(g => new { g.Key, N = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.N, Ct);
            var studentCounts = await _db.Enrollments.AsNoTracking()
                .Where(e => profileIds.Contains(e.GradeYearProfileId) && e.ExitDate == null)
                .GroupBy(e => e.GradeYearProfileId).Select(g => new { g.Key, N = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.N, Ct);

            model.Grades = profiles
                .Select(p =>
                {
                    var g = grades.First(x => x.Id == p.GradeLevelId);
                    return new SectionBoardViewModel.GradeOption(
                        p.Id, g.Name.NameAr, g.Name.NameEn,
                        sectionCounts.GetValueOrDefault(p.Id), studentCounts.GetValueOrDefault(p.Id));
                })
                .OrderBy(g => grades.First(x => x.Name.NameEn == g.NameEn).SequenceOrder)
                .ToList();

            var profile = profiles.FirstOrDefault(p => p.Id == grade) ?? profiles.FirstOrDefault();
            if (profile == null)
            {
                return model;
            }

            model.Grade = model.Grades.FirstOrDefault(g => g.ProfileId == profile.Id);
            model.GradeGenderPolicy = profile.GenderPolicy;

            var sections = await _db.Sections.AsNoTracking()
                .Where(s => s.GradeYearProfileId == profile.Id && s.Status == SectionStatus.Active)
                .OrderBy(s => s.NameEn).ToListAsync(Ct);

            var enrollments = await _db.Enrollments.AsNoTracking()
                .Where(e => e.GradeYearProfileId == profile.Id && e.ExitDate == null).ToListAsync(Ct);
            var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();
            var students = await _db.Students.IgnoreQueryFilters().AsNoTracking()
                .Where(s => studentIds.Contains(s.Id) && s.SchoolId == _db.CurrentSchoolId)
                .ToDictionaryAsync(s => s.Id, Ct);

            var enrollmentIds = enrollments.Select(e => e.Id).ToList();
            var memberships = await _db.SectionMemberships.AsNoTracking()
                .Where(m => enrollmentIds.Contains(m.EnrollmentId) && m.EffectiveToUtc == null)
                .ToDictionaryAsync(m => m.EnrollmentId, m => m.SectionId, Ct);

            SectionBoardViewModel.Card Card(Sms.Domain.Students.Enrollment e)
            {
                students.TryGetValue(e.StudentId, out var st);
                return new SectionBoardViewModel.Card(
                    e.Id,
                    st?.StudentNo ?? "?",
                    st == null ? "?" : $"{st.FirstNameAr} {st.FamilyNameAr}",
                    st == null ? "?" : $"{st.FirstNameEn} {st.FamilyNameEn}",
                    st?.Gender ?? Gender.Male);
            }

            var homerooms = await _db.HomeroomAssignments.AsNoTracking()
                .Where(h => h.AcademicYearId == selectedYear.Id && h.EffectiveToUtc == null).ToListAsync(Ct);
            var teacherNames = await TeacherNamesByUserAsync();
            var rooms = await _db.Rooms.IgnoreQueryFilters().AsNoTracking()
                .Where(r => r.SchoolId == _db.CurrentSchoolId)
                .ToDictionaryAsync(r => r.Id, r => IsArabic ? r.Name.NameAr : r.Name.NameEn, Ct);

            model.Columns = sections.Select(s =>
            {
                var hr = homerooms.FirstOrDefault(h => h.SectionId == s.Id);
                return new SectionBoardViewModel.Column(
                    s, s.Capacity,
                    enrollments.Where(e => memberships.TryGetValue(e.Id, out var sid) && sid == s.Id)
                        .Select(Card).OrderBy(c => IsArabic ? c.NameAr : c.NameEn).ToList(),
                    hr == null ? null : teacherNames.GetValueOrDefault(hr.TeacherUserId),
                    s.DefaultClassroomId == null ? null : rooms.GetValueOrDefault(s.DefaultClassroomId.Value));
            }).ToList();

            model.Unassigned = enrollments.Where(e => !memberships.ContainsKey(e.Id))
                .Select(Card).OrderBy(c => IsArabic ? c.NameAr : c.NameEn).ToList();

            return model;
        }

        // --- §8.5 merge / close wizard ---------------------------------------------

        /// <summary>
        /// doc/Modules/06 §8.5. Closing a section is a decision about where its
        /// students go, so the target mapping and the impact list share one page:
        /// reading the mapping without the impact is how a section gets closed on top
        /// of a published timetable.
        /// </summary>
        [HttpGet("{id:int}/close-wizard")]
        [RequirePermission(ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Sections_, ActionVerb.Approve)]
        public async Task<IActionResult> CloseWizard(int id)
        {
            var model = await BuildCloseAsync(id);
            return model == null ? NotFound() : View(model);
        }

        /// <summary>
        /// BR-SCN-007: the students move out and the section closes in one transaction.
        /// Doing it as two operations leaves a real failure mode where thirty children
        /// have been moved and the section they came from is still open — neither the
        /// old state nor the new one.
        /// </summary>
        [HttpPost("{id:int}/close-wizard")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Sections_, ActionVerb.Approve)]
        public async Task<IActionResult> CloseWizard(int id, string? placements, string? reasonCode, DateTime? effectiveDate)
        {
            var section = await _db.Sections.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, Ct);
            try
            {
                Require(reasonCode, T("Reason code", "رمز السبب"));
                var moved = await _sections.MergeAndCloseSectionAsync(
                    id, ParsePlacements(placements), reasonCode!, effectiveDate ?? _clock.UtcNow.Date, Ct);

                TempData["Flash"] = moved == 0
                    ? T("Section closed. Its history stays readable (BR-SCN-007).", "أُغلقت الشعبة، ويبقى سجلها قابلاً للقراءة (BR-SCN-007).")
                    : T($"{moved} student(s) moved out and the section closed. Its history stays readable (BR-SCN-007).",
                        $"نُقل {moved} طالباً وأُغلقت الشعبة، ويبقى سجلها قابلاً للقراءة (BR-SCN-007).");
                return RedirectToAction(nameof(Index), new { year = section?.AcademicYearId });
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }

            return RedirectToAction(nameof(CloseWizard), new { id });
        }

        private async Task<SectionCloseViewModel?> BuildCloseAsync(int id)
        {
            var section = await _db.Sections.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, Ct);
            if (section == null)
            {
                return null;
            }

            var profile = await _db.GradeYearProfiles.AsNoTracking().SingleAsync(p => p.Id == section.GradeYearProfileId, Ct);
            var grade = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking()
                .SingleAsync(g => g.Id == profile.GradeLevelId && g.SchoolId == _db.CurrentSchoolId, Ct);
            var year = await _db.AcademicYears.AsNoTracking().SingleAsync(y => y.Id == section.AcademicYearId, Ct);

            var memberEnrollmentIds = await _db.SectionMemberships.AsNoTracking()
                .Where(m => m.SectionId == id && m.EffectiveToUtc == null)
                .Select(m => m.EnrollmentId).ToListAsync(Ct);
            var enrollments = await _db.Enrollments.AsNoTracking()
                .Where(e => memberEnrollmentIds.Contains(e.Id)).ToListAsync(Ct);
            var students = await _db.Students.IgnoreQueryFilters().AsNoTracking()
                .Where(s => enrollments.Select(e => e.StudentId).Contains(s.Id) && s.SchoolId == _db.CurrentSchoolId)
                .ToDictionaryAsync(s => s.Id, Ct);

            var siblings = await _db.Sections.AsNoTracking()
                .Where(s => s.GradeYearProfileId == profile.Id && s.Id != id && s.Status == SectionStatus.Active)
                .OrderBy(s => s.NameEn).ToListAsync(Ct);
            var siblingIds = siblings.Select(s => s.Id).ToList();
            var siblingCounts = await _db.SectionMemberships.AsNoTracking()
                .Where(m => siblingIds.Contains(m.SectionId) && m.EffectiveToUtc == null)
                .GroupBy(m => m.SectionId).Select(g => new { g.Key, N = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.N, Ct);

            // The default mapping is the same engine the board uses, run over the
            // sibling sections with this one's students treated as unassigned. The
            // registrar can override any row; what they cannot do is close the section
            // leaving somebody without a seat.
            var proposal = SectionBalanceProposer.Propose(
                enrollments.Select(e => new BalanceStudent(
                    e.Id,
                    students.TryGetValue(e.StudentId, out var st) ? st.Gender : Gender.Male,
                    null)).ToList(),
                siblings.Select(s => new BalanceSeat(s.Id, s.Capacity, s.GenderPolicy)).ToList());
            var suggested = proposal.Moves.ToDictionary(m => m.EnrollmentId, m => m.ToSectionId);

            SectionCloseViewModel.MemberRow Row(Sms.Domain.Students.Enrollment e)
            {
                students.TryGetValue(e.StudentId, out var st);
                return new SectionCloseViewModel.MemberRow(
                    e.Id,
                    st?.StudentNo ?? "?",
                    st == null ? "?" : (IsArabic ? $"{st.FirstNameAr} {st.FamilyNameAr}" : $"{st.FirstNameEn} {st.FamilyNameEn}"),
                    st?.Gender ?? Gender.Male,
                    suggested.TryGetValue(e.Id, out var target) ? target : null);
            }

            var members = enrollments.Select(Row).OrderBy(m => m.Name).ToList();

            var homeroom = await _db.HomeroomAssignments.AsNoTracking()
                .SingleOrDefaultAsync(h => h.SectionId == id && h.EffectiveToUtc == null, Ct);
            var teacherNames = homeroom == null ? null : await TeacherNamesByUserAsync();

            var placementCount = await _db.Placements.AsNoTracking().CountAsync(p => p.SectionId == id, Ct);
            var sessionCount = await (
                from s in _db.Sessions.AsNoTracking()
                join p in _db.Placements.AsNoTracking() on s.PlacementId equals p.Id
                where p.SectionId == id && s.Date >= _clock.UtcNow.Date
                select s.Id).CountAsync(Ct);

            return new SectionCloseViewModel
            {
                Section = section, Grade = grade, Year = year,
                Members = members,
                Unplaceable = members.Where(m => m.SuggestedSectionId == null).ToList(),
                Targets = siblings.Select(s => new SectionCloseViewModel.TargetOption(
                    s.Id, IsArabic ? s.NameAr : s.NameEn,
                    siblingCounts.GetValueOrDefault(s.Id), s.Capacity, s.GenderPolicy)).ToList(),
                HomeroomTeacher = homeroom == null ? null : teacherNames!.GetValueOrDefault(homeroom.TeacherUserId),
                Impacts = new[]
                {
                    new SectionCloseViewModel.Impact("Students to move", "طلاب يجب نقلهم", members.Count, members.Count > 0),
                    new SectionCloseViewModel.Impact("Homeroom assignment to end", "تعيين رائد فصل يُنهى", homeroom == null ? 0 : 1, false),
                    new SectionCloseViewModel.Impact("Timetable placements naming this section", "حصص في الجدول تسمّي هذه الشعبة", placementCount, false),
                    new SectionCloseViewModel.Impact("Sessions from today onward", "حصص من اليوم فصاعداً", sessionCount, false),
                },
                EffectiveDate = _clock.UtcNow.Date,
            };
        }

        private async Task<SectionDetailViewModel?> BuildDetailAsync(int id)
        {
            var section = await _db.Sections.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id);
            if (section == null) return null;
            var profile = await _db.GradeYearProfiles.AsNoTracking().SingleAsync(p => p.Id == section.GradeYearProfileId);
            var grade = await _db.GradeLevels.AsNoTracking().SingleAsync(g => g.Id == profile.GradeLevelId);
            var year = await _db.AcademicYears.AsNoTracking().SingleAsync(y => y.Id == section.AcademicYearId);
            var memberships = await _db.SectionMemberships.AsNoTracking().Where(m => m.SectionId == id).OrderByDescending(m => m.EffectiveFromUtc).ToListAsync();
            var enrollmentIds = memberships.Select(m => m.EnrollmentId).Distinct().ToList();
            var enrollments = await _db.Enrollments.AsNoTracking().Where(e => enrollmentIds.Contains(e.Id)).ToListAsync();
            var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();
            var students = await _db.Students.IgnoreQueryFilters().AsNoTracking().Where(s => studentIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id);
            SectionDetailViewModel.MemberRow Row(SectionMembership m)
            {
                var e = enrollments.First(x => x.Id == m.EnrollmentId);
                students.TryGetValue(e.StudentId, out var st);
                return new SectionDetailViewModel.MemberRow(m, st?.StudentNo ?? "?", st == null ? "?" : $"{st.FirstNameAr} {st.FatherNameAr} {st.FamilyNameAr}", st == null ? "?" : $"{st.FirstNameEn} {st.FatherNameEn} {st.FamilyNameEn}", e.Id);
            }

            var homerooms = await _db.HomeroomAssignments.AsNoTracking().Where(h => h.SectionId == id).OrderByDescending(h => h.EffectiveFromUtc).ToListAsync();
            var teacherNames = await TeacherNamesByUserAsync();
            var teacherOptions = await TeacherOptionsAsync();

            // Unassigned = enrollments of this grade-year profile with no current membership anywhere.
            var assignedNow = await _db.SectionMemberships.AsNoTracking().Where(m => m.AcademicYearId == section.AcademicYearId && m.EffectiveToUtc == null).Select(m => m.EnrollmentId).ToListAsync();
            var candidates = await _db.Enrollments.AsNoTracking().Where(e => e.GradeYearProfileId == profile.Id && e.ExitDate == null && !assignedNow.Contains(e.Id)).ToListAsync();
            var candStudents = await _db.Students.AsNoTracking().Where(s => candidates.Select(c => c.StudentId).Contains(s.Id)).ToDictionaryAsync(s => s.Id);
            var siblings = await _db.Sections.AsNoTracking().Where(s => s.GradeYearProfileId == profile.Id && s.Id != id && s.Status == SectionStatus.Active).ToListAsync();
            var room = section.DefaultClassroomId == null ? null : await _db.Rooms.AsNoTracking().SingleOrDefaultAsync(r => r.Id == section.DefaultClassroomId);

            return new SectionDetailViewModel
            {
                Section = section, Grade = grade, Year = year,
                Members = memberships.Where(m => m.EffectiveToUtc == null).Select(Row).OrderBy(r => r.NameEn).ToList(),
                PastMembers = memberships.Where(m => m.EffectiveToUtc != null).Select(Row).ToList(),
                Homerooms = homerooms.Select(h => new SectionDetailViewModel.HomeroomRow(h, teacherNames.TryGetValue(h.TeacherUserId, out var n) ? n : $"#{h.TeacherUserId}")).ToList(),
                Teachers = teacherOptions,
                Unassigned = candidates.Select(c => { candStudents.TryGetValue(c.StudentId, out var st); return new SectionDetailViewModel.EnrollmentOption(c.Id, st?.StudentNo ?? "?", st == null ? "?" : $"{st.FirstNameAr} {st.FamilyNameAr}", st == null ? "?" : $"{st.FirstNameEn} {st.FamilyNameEn}"); }).ToList(),
                SiblingSections = siblings,
                RoomName = room == null ? null : (IsArabic ? room.Name.NameAr : room.Name.NameEn),
            };
        }

        private async Task<Dictionary<int, string>> TeacherNamesByUserAsync()
        {
            var employees = await _db.Employees.AsNoTracking().Where(e => e.UserAccountId != null).ToListAsync();
            return employees.GroupBy(e => e.UserAccountId!.Value).ToDictionary(g => g.Key, g => { var e = g.First(); return IsArabic ? $"{e.FirstNameAr} {e.FamilyNameAr}" : $"{e.FirstNameEn} {e.FamilyNameEn}"; });
        }

        private async Task<IReadOnlyList<SectionDetailViewModel.TeacherOption>> TeacherOptionsAsync()
        {
            var profiles = await _db.TeacherProfiles.AsNoTracking().ToListAsync();
            var ids = profiles.Select(p => p.EmployeeId).ToList();
            var employees = await _db.Employees.AsNoTracking().Where(e => ids.Contains(e.Id)).ToListAsync();
            return employees.Select(e => new SectionDetailViewModel.TeacherOption(e.UserAccountId, $"{e.FirstNameAr} {e.FamilyNameAr}", $"{e.FirstNameEn} {e.FamilyNameEn}")).OrderBy(t => t.NameEn).ToList();
        }

        private static void Require(string? v, string f)
        {
            if (string.IsNullOrWhiteSpace(v)) throw new InvalidOperationException(T($"{f} is required.", $"الحقل {f} مطلوب."));
        }
    }
}
