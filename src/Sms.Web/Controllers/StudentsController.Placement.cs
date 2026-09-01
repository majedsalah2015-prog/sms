using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.Domain.Sections;
using Sms.Web.Models;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// Placement — one child's grade-year and section, asked from the child's end
    /// (owner request, 2026-08-26; doc/Modules/10 §8, doc/Modules/06 §8.2).
    /// <para>
    /// Neither operation is new. Enrolling a student in a grade-year was a form at the foot of the
    /// file's academic tab; seating them in a section was on the section's own page, whose picker
    /// lists the grade's unseated children, and whose transfer row lists the section's members. Both
    /// screens answer "who is in this container", which is the right question for a registrar
    /// filling a grade and the wrong one for a clerk holding a single name: moving one child meant
    /// first knowing which section they were already in.
    /// </para>
    /// <para>
    /// So this screen is a different route to the same two services — <c>IStudentAdmin.EnrollAsync</c>
    /// and <c>ISectionAdmin.Assign/TransferMembershipAsync</c> — and no new rules. Capacity
    /// (BR-SCN-002), gender policy (BR-SCN-003), the duplicate-enrollment guard (BR-GLB-024) and the
    /// transfer's reason code and effective date (BR-SCN-005/006) are all enforced where they always
    /// were, and their refusals arrive here already translated.
    /// </para>
    /// <para>
    /// The two forms carry their own permissions and not this screen's: putting a child in a grade is
    /// <c>STU/Enrollment/Create</c>, and seating or moving them is <c>SEC/Roster/Edit</c> — the same
    /// right the section page demands. A user holding one and not the other sees one form.
    /// </para>
    /// </summary>
    public partial class StudentsController
    {
        [HttpGet("{id:int}/placement")]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.File, ActionVerb.View)]
        public async Task<IActionResult> Placement(int id)
        {
            var model = await BuildPlacementAsync(id);
            return model == null ? NotFound() : View(model);
        }

        /// <summary>
        /// Seats the student, or moves them. Which of the two it is follows from the record rather
        /// than from a choice on the form: a first seat carries no reason code (BR-SCN-005's reason
        /// answers "why was this child moved", and there is no answer when they were not), and a
        /// move always does.
        /// </summary>
        [HttpPost("{id:int}/placement/section")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Roster, ActionVerb.Edit)]
        public async Task<IActionResult> PlaceInSection(int id, int? sectionId, string? reasonCode, DateTime? effectiveDate)
        {
            try
            {
                var enrollment = await CurrentEnrollmentAsync(id);
                if (enrollment == null) throw new InvalidOperationException(T("Enroll the student in a grade first — a section belongs to a grade-year.", "قيِّد الطالب في صف أولاً — فالشعبة تتبع صفاً سنوياً."));
                if (sectionId == null) throw new InvalidOperationException(T("Choose a section.", "اختر شعبة."));

                // The picker only offers this enrollment's own grade-year, but the id arrives on a
                // request and a request can say anything. Seating a child in another grade's section
                // would leave them on a register they do not belong to, which nothing downstream
                // re-checks.
                var section = await _db.Sections.AsNoTracking().SingleOrDefaultAsync(s => s.Id == sectionId.Value, HttpContext.RequestAborted);
                if (section == null || section.GradeYearProfileId != enrollment.GradeYearProfileId) return NotFound();

                var current = await _db.SectionMemberships.AsNoTracking()
                    .SingleOrDefaultAsync(m => m.EnrollmentId == enrollment.Id && m.EffectiveToUtc == null, HttpContext.RequestAborted);

                var effective = DateTime.SpecifyKind((effectiveDate ?? _clock.UtcNow).Date, DateTimeKind.Utc);

                if (current == null)
                {
                    await _sections.AssignMembershipAsync(section.Id, enrollment.Id, effective, HttpContext.RequestAborted);
                    TempData["Flash"] = T("Student seated in the section.", "تم إسناد الطالب إلى الشعبة.");
                }
                else if (current.SectionId == section.Id)
                {
                    // Not an error and not a write. Transferring a child into the section they are
                    // already in would close a membership and open an identical one, which reads on
                    // the history as a move that never happened.
                    TempData["Flash"] = T("The student is already in that section.", "الطالب في هذه الشعبة بالفعل.");
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(reasonCode)) throw new InvalidOperationException(T("A transfer needs a reason (BR-SCN-005).", "النقل يحتاج سبباً (BR-SCN-005)."));
                    await _sections.TransferMembershipAsync(enrollment.Id, section.Id, reasonCode.Trim(), effective, HttpContext.RequestAborted);
                    TempData["Flash"] = T("Student transferred; the previous membership was closed and kept (BR-SCN-006).", "تم نقل الطالب؛ وأُغلقت العضوية السابقة مع حفظها (BR-SCN-006).");
                }
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }

            return RedirectToAction(nameof(Placement), new { id });
        }

        /// <summary>The student's open enrollment — the one with no exit date, latest first if a school left two.</summary>
        private async Task<Sms.Domain.Students.Enrollment?> CurrentEnrollmentAsync(int studentId)
            => await _db.Enrollments.AsNoTracking()
                .Where(e => e.StudentId == studentId && e.ExitDate == null)
                .OrderByDescending(e => e.EnrollmentDate)
                .FirstOrDefaultAsync(HttpContext.RequestAborted);

        private async Task<StudentPlacementViewModel?> BuildPlacementAsync(int id)
        {
            var student = await _db.Students.IgnoreQueryFilters().AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == id && s.SchoolId == _db.CurrentSchoolId, HttpContext.RequestAborted);
            if (student == null) return null;

            var enrollment = await CurrentEnrollmentAsync(id);

            var model = new StudentPlacementViewModel
            {
                Student = student,
                Enrollment = enrollment,
                CanEnroll = await _permissions.HasPermissionAsync(
                    ScreenCatalog.Modules.Students, ScreenCatalog.Students.Enrollment, ActionVerb.Create, HttpContext.RequestAborted),
                CanSeat = await _permissions.HasPermissionAsync(
                    ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Roster, ActionVerb.Edit, HttpContext.RequestAborted),
            };

            // The grade-year picker reads past the soft-active filter for the grade names it has to
            // *display*, and through it for the profiles it *offers* — a retired grade must still
            // name the year a student is already sitting in, without being offered as a destination.
            var grades = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking()
                .Where(g => g.SchoolId == _db.CurrentSchoolId).ToListAsync(HttpContext.RequestAborted);
            var years = await _db.AcademicYears.AsNoTracking().ToListAsync(HttpContext.RequestAborted);
            var profiles = await _db.GradeYearProfiles.AsNoTracking().ToListAsync(HttpContext.RequestAborted);

            model.Profiles = profiles
                .Where(p => p.IsActive)
                .Select(p => (p.Id,
                    grades.First(g => g.Id == p.GradeLevelId).Name.NameAr, grades.First(g => g.Id == p.GradeLevelId).Name.NameEn,
                    years.First(y => y.Id == p.AcademicYearId).LabelAr, years.First(y => y.Id == p.AcademicYearId).LabelEn))
                .OrderByDescending(x => x.LabelEn).ThenBy(x => x.NameEn)
                .ToList();

            if (enrollment == null) return model;

            var profile = profiles.First(p => p.Id == enrollment.GradeYearProfileId);
            model.Grade = grades.First(g => g.Id == profile.GradeLevelId);
            model.Year = years.First(y => y.Id == profile.AcademicYearId);

            model.Membership = await _db.SectionMemberships.AsNoTracking()
                .SingleOrDefaultAsync(m => m.EnrollmentId == enrollment.Id && m.EffectiveToUtc == null, HttpContext.RequestAborted);
            if (model.Membership != null)
            {
                model.Section = await _db.Sections.AsNoTracking()
                    .SingleOrDefaultAsync(s => s.Id == model.Membership.SectionId, HttpContext.RequestAborted);
            }

            // Occupancy is counted, not stored: the seat meter beside each option is the reason a
            // clerk picks one section over another, and a stale one sends the whole grade into the
            // section that looked emptiest.
            var sections = await _db.Sections.AsNoTracking()
                .Where(s => s.GradeYearProfileId == profile.Id && s.Status == SectionStatus.Active)
                .OrderBy(s => s.NameEn).ToListAsync(HttpContext.RequestAborted);
            var sectionIds = sections.Select(s => s.Id).ToList();
            var counts = await _db.SectionMemberships.AsNoTracking()
                .Where(m => sectionIds.Contains(m.SectionId) && m.EffectiveToUtc == null)
                .GroupBy(m => m.SectionId).Select(g => new { g.Key, N = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.N, HttpContext.RequestAborted);

            model.Sections = sections
                .Select(s => new StudentPlacementViewModel.SectionOption(
                    s, counts.TryGetValue(s.Id, out var n) ? n : 0, model.Membership?.SectionId == s.Id))
                .ToList();

            return model;
        }
    }
}
