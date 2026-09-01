using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Security;
using Sms.Domain.Common;
using Sms.Domain.Grades;
using Sms.Domain.Sections;
using Sms.Domain.Security;
using Sms.Domain.Students;
using Sms.Web.Models;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// Placing a whole intake at once (owner request, 2026-08-26; doc/Modules/10 §8, BR-STU-010,
    /// BR-GLB-024).
    /// <para>
    /// The Access importer brings a school's register across as students and guardians and stops
    /// there — it maps no grade column, because one school's register keeps the grade as a code,
    /// the next as free text, and the next in a separate table. So an import lands every child
    /// registered and none of them enrolled, and enrolling was one child at a time from the
    /// placement screen. A 481-student register is 481 forms, which nobody does; what actually
    /// happened was that the year's fee screens, the section board, attendance and the charge
    /// pickers all read through <c>Enrollment</c> and stayed empty, and the system read as broken
    /// rather than as unfinished.
    /// </para>
    /// <para>
    /// This is the missing step, and it is deliberately not a new rule: every row goes through
    /// <c>IStudentAdmin.EnrollAsync</c> and <c>ISectionAdmin.AssignMembershipAsync</c> — the same
    /// two ports the single placement screen calls — so the duplicate-enrollment guard
    /// (BR-GLB-024), section capacity (BR-SCN-002) and the section's gender policy (BR-SCN-003)
    /// are enforced where they always were. What is new is only the selection, the preview and the
    /// per-row report.
    /// </para>
    /// <para>
    /// BR-STU-010 requires a dry-run preview for a bulk student change and a per-record audit
    /// trail. The preview is the middle request: it names every selected child and what will happen
    /// to them — enrol, seat, skip, or refuse and why — before anything is written. The audit trail
    /// is not written here: <c>AuditCaptor</c> already records each enrollment inside the same
    /// transaction that creates it, one entry per record, which is what the rule asks for.
    /// </para>
    /// <para>
    /// <b>Deliberately not built here:</b> rule-based auto-distribution across a grade's sections
    /// (doc/Modules/06 §8.3, BR-SCN-008 — size balance, gender ratio, keep-apart pairs) is the
    /// assignment board's job and stays there. This screen seats everyone in one chosen section or
    /// in none; a registrar who wants them spread evenly enrols here and distributes there.
    /// </para>
    /// </summary>
    public partial class StudentsController
    {
        /// <summary>
        /// Rows offered per page. The unplaced roll after an import is the whole school, and a
        /// checkbox grid of two thousand children is not a selection instrument — the grade and
        /// name filters are. The count above the grid says what the filter actually matched, so a
        /// truncated page never reads as the whole answer.
        /// </summary>
        private const int BulkPlacementPageSize = 300;

        /// <summary>
        /// The largest number of children one submission may place. Not a technical limit — the
        /// loop below clears the change tracker and would run far past it — but the point at which
        /// a mis-click stops being recoverable by hand. A bigger intake is placed a page at a time.
        /// </summary>
        private const int BulkPlacementMaxPerCommit = 500;

        // ================================================================== the roll

        [HttpGet("placement")]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.Enrollment, ActionVerb.View)]
        public async Task<IActionResult> BulkPlacement(
            int? profileId = null, int? sectionId = null, string? q = null, int? grade = null, bool placedToo = false)
            => View(await BuildBulkPlacementAsync(profileId, sectionId, q, grade, placedToo, null, null));

        // ================================================================== the dry run (BR-STU-010)

        /// <summary>
        /// Shows what the commit would do and writes nothing. Every selected child gets a verdict
        /// of its own rather than one summary line: a run that would place 300 children and refuse
        /// 12 is a good run, and the reader has to be able to see which 12 before they agree to it.
        /// </summary>
        [HttpPost("placement/preview")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.Enrollment, ActionVerb.View)]
        public async Task<IActionResult> BulkPlacementPreview(
            int? profileId, int? sectionId, string? q, int? grade, bool placedToo, DateTime? enrollmentDate, int[]? studentIds)
        {
            var model = await BuildBulkPlacementAsync(profileId, sectionId, q, grade, placedToo, enrollmentDate, studentIds);
            if (model.Profile == null)
            {
                model.Error = T("Choose the grade-year to place these students into.", "اختر الصف السنوي الذي تُقيَّد فيه هذه الأسماء.");
                return View(nameof(BulkPlacement), model);
            }
            if (model.Preview.Count == 0)
            {
                model.Error = T("No students were selected.", "لم يُحدَّد أي طالب.");
                return View(nameof(BulkPlacement), model);
            }
            return View(nameof(BulkPlacement), model);
        }

        // ================================================================== the commit

        [HttpPost("placement/commit")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.Enrollment, ActionVerb.Create)]
        public async Task<IActionResult> BulkPlacementCommit(
            int? profileId, int? sectionId, string? q, int? grade, bool placedToo, DateTime? enrollmentDate, int[]? studentIds)
        {
            var model = await BuildBulkPlacementAsync(profileId, sectionId, q, grade, placedToo, enrollmentDate, studentIds);
            if (model.Profile == null || model.Preview.Count == 0)
            {
                model.Error = model.Profile == null
                    ? T("Choose the grade-year to place these students into.", "اختر الصف السنوي الذي تُقيَّد فيه هذه الأسماء.")
                    : T("No students were selected.", "لم يُحدَّد أي طالب.");
                return View(nameof(BulkPlacement), model);
            }
            if (model.Preview.Count > BulkPlacementMaxPerCommit)
            {
                model.Error = T(
                    $"One run places at most {BulkPlacementMaxPerCommit} students — narrow the filter and run it again.",
                    $"الدفعة الواحدة تقيّد {BulkPlacementMaxPerCommit} طالباً على الأكثر — ضيّق التصفية وأعد التنفيذ.");
                return View(nameof(BulkPlacement), model);
            }

            // Seating needs the roster right as well as the enrollment right, and the two are
            // separately grantable (BR-SEC-010). A user holding only the enrollment right places
            // the children in the grade and leaves the section to somebody who may seat them,
            // rather than having the whole run refused at the last row.
            var canSeat = await _permissions.HasPermissionAsync(
                ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Roster, ActionVerb.Edit, HttpContext.RequestAborted);
            var seatInto = canSeat ? model.Section : null;

            var date = DateTime.SpecifyKind((enrollmentDate ?? _clock.UtcNow.Date).Date, DateTimeKind.Utc);
            var enrolled = 0;
            var seated = 0;
            var refused = 0;
            var failed = new List<string>();

            foreach (var row in model.Preview)
            {
                // The verdict decides, not the loop. A child the preview showed as "enrolled but not
                // seated — the section is full" must be enrolled and left unseated: calling the
                // roster port anyway would report a failure against a run that did exactly what the
                // reader agreed to.
                var willEnroll = row.Verdict is BulkPlacementViewModel.Verdict.WillEnroll or BulkPlacementViewModel.Verdict.WillEnrollAndSeat
                    or BulkPlacementViewModel.Verdict.SectionFull or BulkPlacementViewModel.Verdict.GenderMismatch;
                var willSeat = row.Verdict is BulkPlacementViewModel.Verdict.WillSeat or BulkPlacementViewModel.Verdict.WillEnrollAndSeat;

                if (row.Verdict is BulkPlacementViewModel.Verdict.AlreadyThere or BulkPlacementViewModel.Verdict.EnrolledElsewhere)
                {
                    continue;
                }
                if (willEnroll && row.Enrollment != null) willEnroll = false;

                try
                {
                    var enrollmentId = row.Enrollment?.Id;
                    if (willEnroll)
                    {
                        var created = await _students.EnrollAsync(
                            row.Student.Id, model.Profile.Id, date, EnrollmentSourceType.Admission, HttpContext.RequestAborted);
                        enrollmentId = created.Id;
                        enrolled++;
                    }

                    if (willSeat && seatInto != null && enrollmentId != null)
                    {
                        await _sections.AssignMembershipAsync(seatInto.Id, enrollmentId.Value, date, HttpContext.RequestAborted);
                        seated++;
                    }

                    if (row.Verdict is BulkPlacementViewModel.Verdict.SectionFull or BulkPlacementViewModel.Verdict.GenderMismatch)
                    {
                        refused++;
                    }
                }
                catch (InvalidOperationException ex)
                {
                    // One child's refusal is not the run's. A section that fills on the twenty-sixth
                    // name has still placed twenty-five correctly, and rolling those back would
                    // punish the reader for a limit the school set on purpose. Named rather than
                    // counted, up to five, exactly as the register import reports its bad rows.
                    if (failed.Count < 5)
                    {
                        failed.Add($"{row.Student.StudentNo} — {UserMessage.For(ex, IsArabic)}");
                    }
                    else
                    {
                        refused++;
                    }
                }

                // A loop that commits per row on one context is quadratic: the tracker keeps every
                // entity it has seen and DetectChanges re-walks the lot on each save. Clearing after
                // each committed child is what keeps a 500-name intake a matter of seconds.
                _db.ChangeTracker.Clear();
            }

            TempData["Flash"] = seated > 0
                ? T($"{enrolled} student(s) enrolled, {seated} seated in the section.", $"تم قيد {enrolled} طالباً، وإسناد {seated} منهم إلى الشعبة.")
                : T($"{enrolled} student(s) enrolled.", $"تم قيد {enrolled} طالباً.");
            if (failed.Count > 0 || refused > 0)
            {
                // "of them", not "more": a child refused a seat was still enrolled and is already
                // counted above. Reporting them as an extra number would make the run look bigger
                // than it was and the refusals look like losses.
                var tail = refused > 0
                    ? T($"{refused} of them were enrolled without a seat (section full or gender policy).", $"منهم {refused} قُيّدوا بلا مقعد (الشعبة مكتملة أو سياسة الجنس).")
                    : null;
                TempData["Error"] = string.Join(" · ", failed.Concat(tail == null ? Array.Empty<string>() : new[] { tail }));
            }

            return RedirectToAction(nameof(BulkPlacement), new { profileId, sectionId, q, grade, placedToo });
        }

        // ================================================================== helpers

        /// <summary>
        /// Builds the screen and, when students were selected, the dry run beside it. One method for
        /// both requests so the preview a reader agrees to is computed by the same code that the
        /// commit then walks — a preview built by a second reading of the rules is a preview that
        /// can disagree with the write it authorises.
        /// </summary>
        private async Task<BulkPlacementViewModel> BuildBulkPlacementAsync(
            int? profileId, int? sectionId, string? q, int? gradeFilter, bool placedToo, DateTime? enrollmentDate, int[]? studentIds)
        {
            var model = new BulkPlacementViewModel
            {
                ProfileId = profileId,
                SectionId = sectionId,
                Q = q,
                GradeFilter = gradeFilter,
                PlacedToo = placedToo,
                EnrollmentDate = enrollmentDate ?? _clock.UtcNow.Date,
                CanEnroll = await _permissions.HasPermissionAsync(
                    ScreenCatalog.Modules.Students, ScreenCatalog.Students.Enrollment, ActionVerb.Create, HttpContext.RequestAborted),
                CanSeat = await _permissions.HasPermissionAsync(
                    ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Roster, ActionVerb.Edit, HttpContext.RequestAborted),
            };

            // Grade names are read past the soft-active filter because a retired grade still names
            // the year a child is already sitting in; the destination picker is built from the
            // filtered profiles, so a retired one can be displayed and not chosen.
            var grades = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking()
                .Where(g => g.SchoolId == _db.CurrentSchoolId).ToListAsync(HttpContext.RequestAborted);
            var years = await _db.AcademicYears.AsNoTracking().ToListAsync(HttpContext.RequestAborted);
            var allProfiles = await _db.GradeYearProfiles.IgnoreQueryFilters().AsNoTracking().ToListAsync(HttpContext.RequestAborted);

            string GradeName(int gradeLevelId)
            {
                var g = grades.FirstOrDefault(x => x.Id == gradeLevelId);
                return g == null ? "—" : (IsArabic ? g.Name.NameAr : g.Name.NameEn);
            }

            model.Grades = grades.Where(g => g.IsActive).OrderBy(g => g.SequenceOrder).ToList();
            model.Profiles = allProfiles
                .Where(p => p.IsActive)
                .Select(p => new BulkPlacementViewModel.ProfileOption(
                    p.Id, GradeName(p.GradeLevelId),
                    years.FirstOrDefault(y => y.Id == p.AcademicYearId) is { } y ? (IsArabic ? y.LabelAr : y.LabelEn) : "—",
                    p.AcademicYearId,
                    grades.FirstOrDefault(x => x.Id == p.GradeLevelId)?.SequenceOrder ?? int.MaxValue))
                .OrderByDescending(o => o.AcademicYearId).ThenBy(o => o.Order)
                .ToList();

            model.Profile = profileId == null ? null : allProfiles.FirstOrDefault(p => p.Id == profileId && p.IsActive);

            // The sections offered are the destination grade-year's own. A section of another grade
            // is not a placement, it is a mistake, and the picker must not be able to express it.
            if (model.Profile != null)
            {
                var sections = await _db.Sections.AsNoTracking()
                    .Where(s => s.GradeYearProfileId == model.Profile.Id && s.Status == SectionStatus.Active)
                    .OrderBy(s => s.NameEn).ToListAsync(HttpContext.RequestAborted);
                var sectionIds = sections.Select(s => s.Id).ToList();
                var counts = sectionIds.Count == 0
                    ? new Dictionary<int, int>()
                    : await _db.SectionMemberships.AsNoTracking()
                        .Where(m => sectionIds.Contains(m.SectionId) && m.EffectiveToUtc == null)
                        .GroupBy(m => m.SectionId).Select(g => new { g.Key, N = g.Count() })
                        .ToDictionaryAsync(x => x.Key, x => x.N, HttpContext.RequestAborted);

                model.Sections = sections
                    .Select(s => new BulkPlacementViewModel.SectionOption(s, counts.TryGetValue(s.Id, out var n) ? n : 0))
                    .ToList();
                model.Section = sections.FirstOrDefault(s => s.Id == sectionId);
                model.SeatsLeft = model.Section == null
                    ? null
                    : Math.Max(0, model.Section.Capacity - (counts.TryGetValue(model.Section.Id, out var taken) ? taken : 0));
                model.GradeYearName = GradeName(model.Profile.GradeLevelId);
                var year = years.FirstOrDefault(y => y.Id == model.Profile.AcademicYearId);
                model.YearName = year == null ? "—" : (IsArabic ? year.LabelAr : year.LabelEn);
            }

            // ---- the roll to choose from
            //
            // Only once a destination exists. "Unplaced" is a statement about a year, so before one
            // is chosen the roll would be the whole directory under a counter that reads as a
            // finding — 481 students, none of them "not placed" in anything. The screen asks for the
            // destination first and says so.
            if (model.Profile == null) return model;

            var query = _db.Students.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.SchoolId == _db.CurrentSchoolId && s.Status == StudentStatus.Enrolled);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var t = q.Trim();
                query = query.Where(s =>
                    s.StudentNo.Contains(t) || s.FirstNameAr.Contains(t) || s.FatherNameAr.Contains(t) || s.FamilyNameAr.Contains(t)
                    || s.FirstNameEn.Contains(t) || s.FatherNameEn.Contains(t) || s.FamilyNameEn.Contains(t)
                    || (s.PrimaryIdNo != null && s.PrimaryIdNo.Contains(t)));
            }

            var candidates = await query.OrderBy(s => s.StudentNo).ToListAsync(HttpContext.RequestAborted);
            var candidateIds = candidates.Select(s => s.Id).ToList();

            // Every open enrollment of every candidate, in one query. "Unplaced" is a statement about
            // the destination *year*, not about the student: a child enrolled in 2025-2026 and not in
            // 2026-2027 is exactly who this screen is for.
            var openEnrollments = await _db.Enrollments.AsNoTracking()
                .Where(e => candidateIds.Contains(e.StudentId) && e.ExitDate == null)
                .ToListAsync(HttpContext.RequestAborted);
            var enrollmentIds = openEnrollments.Select(e => e.Id).ToList();
            var memberships = enrollmentIds.Count == 0
                ? new List<SectionMembership>()
                : await _db.SectionMemberships.AsNoTracking()
                    .Where(m => enrollmentIds.Contains(m.EnrollmentId) && m.EffectiveToUtc == null)
                    .ToListAsync(HttpContext.RequestAborted);
            var membershipSectionIds = memberships.Select(m => m.SectionId).Distinct().ToList();
            var membershipSections = membershipSectionIds.Count == 0
                ? new List<Section>()
                : await _db.Sections.AsNoTracking().Where(s => membershipSectionIds.Contains(s.Id)).ToListAsync(HttpContext.RequestAborted);

            var targetYearId = model.Profile.AcademicYearId;

            Enrollment? InTargetYear(int studentId)
                => openEnrollments.FirstOrDefault(e => e.StudentId == studentId && e.AcademicYearId == targetYearId);

            Enrollment? Latest(int studentId) =>
                openEnrollments.Where(e => e.StudentId == studentId).OrderByDescending(e => e.EnrollmentDate).FirstOrDefault();

            var rows = new List<BulkPlacementViewModel.Row>(candidates.Count);
            foreach (var student in candidates)
            {
                var here = InTargetYear(student.Id);
                var latest = here ?? Latest(student.Id);
                var profile = latest == null ? null : allProfiles.FirstOrDefault(p => p.Id == latest.GradeYearProfileId);
                var membership = latest == null ? null : memberships.FirstOrDefault(m => m.EnrollmentId == latest.Id);
                var section = membership == null ? null : membershipSections.FirstOrDefault(s => s.Id == membership.SectionId);

                if (!placedToo && here != null) continue;
                if (gradeFilter != null && (profile == null || profile.GradeLevelId != gradeFilter)) continue;

                rows.Add(new BulkPlacementViewModel.Row(
                    student,
                    profile == null ? null : GradeName(profile.GradeLevelId),
                    section == null ? null : (IsArabic ? section.NameAr : section.NameEn),
                    latest == null ? null : years.FirstOrDefault(y => y.Id == latest.AcademicYearId) is { } ly ? (IsArabic ? ly.LabelAr : ly.LabelEn) : null,
                    here != null));
            }

            model.MatchCount = rows.Count;
            model.Rows = rows.Take(BulkPlacementPageSize).ToList();
            model.IsTruncated = rows.Count > model.Rows.Count;
            // School-wide and deliberately not filtered by the search box: it is the number the
            // screen exists to bring down, and one that moved when you typed a name would measure
            // the query rather than the year.
            var onRoll = await _db.Students.IgnoreQueryFilters().AsNoTracking()
                .CountAsync(s => s.SchoolId == _db.CurrentSchoolId && s.Status == StudentStatus.Enrolled, HttpContext.RequestAborted);
            var placedThisYear = await _db.Enrollments.AsNoTracking()
                .Where(e => e.AcademicYearId == targetYearId && e.ExitDate == null)
                .Select(e => e.StudentId).Distinct().CountAsync(HttpContext.RequestAborted);
            model.UnplacedTotal = Math.Max(0, onRoll - placedThisYear);

            // ---- the dry run
            if (studentIds == null || studentIds.Length == 0) return model;

            var selected = candidates.Where(s => studentIds.Contains(s.Id)).ToList();
            var seatsLeft = model.SeatsLeft;
            var preview = new List<BulkPlacementViewModel.PreviewRow>(selected.Count);

            foreach (var student in selected)
            {
                var here = InTargetYear(student.Id);
                var membership = here == null ? null : memberships.FirstOrDefault(m => m.EnrollmentId == here.Id);
                var currentSection = membership == null ? null : membershipSections.FirstOrDefault(s => s.Id == membership.SectionId);

                // BR-GLB-024 is enforced by the port; naming it here is what makes the preview
                // honest rather than optimistic, and the two must not be able to disagree.
                if (here != null && currentSection != null && (model.Section == null || currentSection.Id == model.Section.Id))
                {
                    preview.Add(new BulkPlacementViewModel.PreviewRow(
                        student, BulkPlacementViewModel.Verdict.AlreadyThere, here, currentSection,
                        T("Already enrolled and seated here.", "مقيَّد ومُسنَد هنا بالفعل.")));
                    continue;
                }

                if (here != null && model.Section != null && currentSection != null && currentSection.Id != model.Section.Id)
                {
                    // A move between sections is a transfer: BR-SCN-005 wants a reason code and
                    // BR-SCN-006 a marks-continuity check, neither of which a 300-row form can ask
                    // for honestly. The placement screen does one child properly instead.
                    preview.Add(new BulkPlacementViewModel.PreviewRow(
                        student, BulkPlacementViewModel.Verdict.EnrolledElsewhere, here, currentSection,
                        T("Already seated in another section — moving them is a transfer and needs a reason (BR-SCN-005).",
                          "مُسنَد إلى شعبة أخرى — ونقله تحويل يحتاج سبباً (BR-SCN-005).")));
                    continue;
                }

                var elsewhere = openEnrollments.FirstOrDefault(
                    e => e.StudentId == student.Id && e.AcademicYearId == model.Profile.AcademicYearId && e.GradeYearProfileId != model.Profile.Id);
                if (elsewhere != null)
                {
                    var otherProfile = allProfiles.FirstOrDefault(p => p.Id == elsewhere.GradeYearProfileId);
                    preview.Add(new BulkPlacementViewModel.PreviewRow(
                        student, BulkPlacementViewModel.Verdict.EnrolledElsewhere, elsewhere, null,
                        T($"Already enrolled this year in {(otherProfile == null ? "another grade" : GradeName(otherProfile.GradeLevelId))} — one open enrollment per year (BR-GLB-024).",
                          $"مقيَّد هذا العام في {(otherProfile == null ? "صف آخر" : GradeName(otherProfile.GradeLevelId))} — قيد مفتوح واحد لكل عام (BR-GLB-024).")));
                    continue;
                }

                if (model.Section == null)
                {
                    preview.Add(new BulkPlacementViewModel.PreviewRow(
                        student, here == null ? BulkPlacementViewModel.Verdict.WillEnroll : BulkPlacementViewModel.Verdict.AlreadyThere, here, null,
                        here == null
                            ? T("Will be enrolled in the grade-year; no section chosen.", "سيُقيَّد في الصف السنوي؛ ولم تُختَر شعبة.")
                            : T("Already enrolled here; no section chosen.", "مقيَّد هنا بالفعل؛ ولم تُختَر شعبة.")));
                    continue;
                }

                // BR-SCN-003. The section's policy narrows the grade's, so a Boys section refuses a
                // girl even where the grade is mixed. Checked here as well as in the port because a
                // preview that promises a seat the port will refuse is worse than no preview.
                if ((model.Section.GenderPolicy == GenderPolicy.Boys && student.Gender != Gender.Male)
                    || (model.Section.GenderPolicy == GenderPolicy.Girls && student.Gender != Gender.Female))
                {
                    preview.Add(new BulkPlacementViewModel.PreviewRow(
                        student, BulkPlacementViewModel.Verdict.GenderMismatch, here, null,
                        T("The section's gender policy does not admit this student (BR-SCN-003).", "سياسة جنس الشعبة لا تقبل هذا الطالب (BR-SCN-003).")));
                    continue;
                }

                // BR-SCN-002. Counted down across the preview so the reader sees which names fall
                // past the last seat rather than a single "the section is full" at the end.
                if (seatsLeft != null && seatsLeft <= 0)
                {
                    preview.Add(new BulkPlacementViewModel.PreviewRow(
                        student, BulkPlacementViewModel.Verdict.SectionFull, here, null,
                        T("The section is full (BR-SCN-002) — they will be enrolled in the grade but not seated.",
                          "الشعبة مكتملة (BR-SCN-002) — سيُقيَّد في الصف دون إسناد إلى الشعبة.")));
                    continue;
                }

                if (seatsLeft != null) seatsLeft--;
                preview.Add(new BulkPlacementViewModel.PreviewRow(
                    student, here == null ? BulkPlacementViewModel.Verdict.WillEnrollAndSeat : BulkPlacementViewModel.Verdict.WillSeat, here, null,
                    here == null
                        ? T("Will be enrolled and seated.", "سيُقيَّد ويُسنَد إلى الشعبة.")
                        : T("Already enrolled; will be seated.", "مقيَّد بالفعل؛ وسيُسنَد إلى الشعبة.")));
            }

            model.Preview = preview;
            return model;
        }
    }
}
