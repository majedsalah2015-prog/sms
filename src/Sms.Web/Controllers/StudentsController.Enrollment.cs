using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Guards;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.Domain.Students;
using Sms.Web.Models;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// Correcting the academic record — the two things a registrar could not do to an enrollment
    /// once it was written (owner request, 2026-09-02; doc/Modules/10 §8.10).
    /// <para>
    /// The academic-history tab listed a child's enrollments and offered one action: add another.
    /// So a grade keyed wrongly stayed wrong. Enrolling the child again is refused by BR-GLB-024
    /// (one active enrollment per year), the rollover only moves whole grades between years, and
    /// nothing else in the product writes <c>GradeYearProfileId</c> — which left every register,
    /// mark sheet and fee schedule reading a grade the school knew to be false, with no screen able
    /// to say so.
    /// </para>
    /// <para>
    /// Two operations answer it, and they are deliberately different in kind:
    /// </para>
    /// <list type="bullet">
    /// <item><b>Correct</b> re-points the enrollment at another grade <i>of the same year</i>. The
    /// record stays, its id stays, and the T2 audit keeps what the grade was before.</item>
    /// <item><b>Remove</b> deletes a row that should never have existed, and only ever that: the
    /// usage guard refuses the moment anything — one attendance day, one mark, one charge — was
    /// recorded against it, at which point the enrollment is history and BR-GLB-005 applies. The
    /// reason is mandatory (BR-GLB-032) and is written as an explicit audit entry, because the
    /// declarative captor never sees a delete.</item>
    /// </list>
    /// <para>
    /// A third control, <b>leave the section</b>, is on the same rows: until now the only way out of
    /// a section was into another one, which is both a real gap of its own and the precondition for
    /// correcting the grade under a seated child.
    /// </para>
    /// <para>
    /// The verbs are split so a school can grant them separately —
    /// <c>STU/Enrollment/Create</c> to enroll, <c>STU/Enrollment/Edit</c> to re-grade,
    /// <c>STU/Enrollment/Deactivate</c> to remove, <c>SEC/Roster/Edit</c> to seat and unseat. New
    /// permissions: <c>tools/Sms.Seeder</c> must be re-run or they answer not-found for everyone,
    /// sysadmin included.
    /// </para>
    /// <para>
    /// <b>What this does not do.</b> It does not close an enrollment with an exit date — that is
    /// BR-STU-006's withdrawal (WF-03, clearance board with a finance veto), still deferred, and the
    /// file's status tab says so. It does not introduce a "cancelled" enrollment state either:
    /// BR-GLB-032 describes one, but <c>EnrollmentStatus</c> has no such member in
    /// docs/Database/03 §A3 and adding one is an owner decision, not a reading of the spec. So a
    /// wrong enrollment that has already been used is neither removable nor cancellable here — it
    /// waits for the withdrawal wizard, and the screen says which of the two it is.
    /// </para>
    /// </summary>
    public partial class StudentsController
    {
        /// <summary>
        /// Re-points one enrollment at another grade of its own year, and fixes the date and source
        /// that were usually mistyped along with it.
        /// <para>
        /// The reason is passed through <see cref="Sms.Application.Audit.IAuditContext"/> rather
        /// than stored on the enrollment: the change itself is what is being explained, and the T2
        /// captor writes the old and new grade beside it in the same transaction.
        /// </para>
        /// </summary>
        [HttpPost("{id:int}/enrollments/{enrollmentId:int}/correct")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.Enrollment, ActionVerb.Edit)]
        public async Task<IActionResult> CorrectEnrollment(
            int id, int enrollmentId, int? gradeYearProfileId, DateTime? enrollmentDate, EnrollmentSourceType sourceType,
            string? reason, string? returnTo = null)
        {
            try
            {
                var enrollment = await OwnedEnrollmentAsync(id, enrollmentId);
                if (enrollment == null) return NotFound();

                if (gradeYearProfileId == null) throw new InvalidOperationException(T("Choose the grade-year it should have been.", "اختر الصف السنوي الصحيح."));

                _audit.Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
                await _students.CorrectEnrollmentAsync(
                    enrollmentId, gradeYearProfileId.Value, enrollmentDate ?? enrollment.EnrollmentDate, sourceType,
                    HttpContext.RequestAborted);

                TempData["Flash"] = T("The enrollment was corrected.", "تم تصحيح القيد.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }

            return Back(id, returnTo);
        }

        /// <summary>
        /// Removes an enrollment that should never have been written. The screen only draws the
        /// button when the usage guard is clear, so reaching the refusal below means a stale page or
        /// a hand-made request — it still answers with what is in the way rather than a bare no.
        /// </summary>
        [HttpPost("{id:int}/enrollments/{enrollmentId:int}/remove")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.Enrollment, ActionVerb.Deactivate)]
        public async Task<IActionResult> RemoveEnrollment(int id, int enrollmentId, string? reason, string? returnTo = null)
        {
            try
            {
                var enrollment = await OwnedEnrollmentAsync(id, enrollmentId);
                if (enrollment == null) return NotFound();

                await _students.RemoveEnrollmentAsync(enrollmentId, reason ?? string.Empty, HttpContext.RequestAborted);
                TempData["Flash"] = T("The enrollment was removed and the removal recorded in the audit trail.", "حُذف القيد وسُجِّل الحذف في سجل التدقيق.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }

            return Back(id, returnTo);
        }

        /// <summary>
        /// Ends the child's section membership without opening another (BR-SCN-006). Carries
        /// <c>SEC/Roster/Edit</c> and not this screen's own right, exactly as seating them does —
        /// the roster is the Sections module's to change, wherever the button happens to be drawn.
        /// </summary>
        [HttpPost("{id:int}/enrollments/{enrollmentId:int}/leave-section")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Roster, ActionVerb.Edit)]
        public async Task<IActionResult> LeaveSection(
            int id, int enrollmentId, string? reasonCode, DateTime? effectiveDate, string? returnTo = null)
        {
            try
            {
                var enrollment = await OwnedEnrollmentAsync(id, enrollmentId);
                if (enrollment == null) return NotFound();

                if (string.IsNullOrWhiteSpace(reasonCode)) throw new InvalidOperationException(T("Leaving a section needs a reason (BR-SCN-005).", "الخروج من الشعبة يحتاج سبباً (BR-SCN-005)."));

                var effective = DateTime.SpecifyKind((effectiveDate ?? _clock.UtcNow).Date, DateTimeKind.Utc);
                var closed = await _sections.EndMembershipAsync(enrollmentId, reasonCode.Trim(), effective, HttpContext.RequestAborted);

                TempData["Flash"] = closed == null
                    ? T("The student was not in a section.", "الطالب ليس في شعبة.")
                    : T("The student left the section; the membership was closed and kept (BR-SCN-006).", "خرج الطالب من الشعبة؛ وأُغلقت العضوية مع حفظها (BR-SCN-006).");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }

            return Back(id, returnTo);
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// The enrollment, but only if it is this student's and this school's. The route carries
        /// both ids and a request can pair them freely; without this check a correction aimed at
        /// another child's enrollment would be performed under this student's permission check.
        /// </summary>
        private async Task<Enrollment?> OwnedEnrollmentAsync(int studentId, int enrollmentId)
        {
            var student = await _db.Students.IgnoreQueryFilters().AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == studentId && s.SchoolId == _db.CurrentSchoolId, HttpContext.RequestAborted);
            if (student == null) return null;

            return await _db.Enrollments.AsNoTracking()
                .SingleOrDefaultAsync(e => e.Id == enrollmentId && e.StudentId == studentId, HttpContext.RequestAborted);
        }

        /// <summary>Back where the operator pressed the button — the file's academic tab, or the placement screen.</summary>
        private IActionResult Back(int id, string? returnTo)
            => returnTo == "placement"
                ? RedirectToAction(nameof(Placement), new { id })
                : RedirectToAction(nameof(File), new { id, tab = "academic" });

        /// <summary>
        /// The grades one enrollment may be corrected into: its own year's live profiles, plus the
        /// one the record currently names whether or not that is still live.
        /// <para>
        /// The two halves answer different questions, and this is the trap the codebase has paid for
        /// before. The <i>picker</i> offers what a child may be put into today, so a retired
        /// grade-year must not appear in it. But the current profile is not an offer, it is what the
        /// row <b>says</b> — and if a grade was retired after this enrollment was written, dropping
        /// it would leave the drop-down with nothing selected, so the browser would preselect the
        /// first option and a clerk who opened the form to fix a date would save a different grade
        /// without ever choosing one.
        /// </para>
        /// <para>
        /// Grade <i>names</i> are read past the soft-active filter for the same reason: a retired
        /// grade still has to be nameable as the thing being corrected away from.
        /// </para>
        /// </summary>
        private async Task<IReadOnlyList<GradeYearOption>> CorrectionOptionsAsync(int academicYearId, int currentProfileId)
        {
            var ct = HttpContext.RequestAborted;
            var profiles = await _db.GradeYearProfiles.AsNoTracking()
                .Where(p => p.AcademicYearId == academicYearId).ToListAsync(ct);
            var gradeIds = profiles.Select(p => p.GradeLevelId).ToList();
            var grades = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking()
                .Where(g => g.SchoolId == _db.CurrentSchoolId && gradeIds.Contains(g.Id)).ToListAsync(ct);

            return profiles
                .Where(p => p.IsActive || p.Id == currentProfileId)
                .Select(p => grades.FirstOrDefault(g => g.Id == p.GradeLevelId) is { } g
                    ? new GradeYearOption(p.Id, g.Code, g.Name.NameAr, g.Name.NameEn)
                    : null)
                .Where(o => o != null)
                .Select(o => o!)
                .OrderBy(o => o.Code)
                .ToList();
        }

        /// <summary>
        /// What may be done to each of the student's enrollment rows — which is what lets the tab
        /// grey out a remove button that could only ever refuse, and say why (BR-SEC-010's reasoning
        /// applied to a guard rather than to a permission).
        /// <para>
        /// Asked for every row in one pass. Row by row it was fourteen counts per year of the
        /// child's school career, on a page a registrar opens all day, to decide the state of a
        /// handful of buttons.
        /// </para>
        /// </summary>
        private async Task<IReadOnlyDictionary<int, StudentFileViewModel.EnrollmentActions>> EnrollmentActionsAsync(
            IReadOnlyList<Enrollment> enrollments)
        {
            var ct = HttpContext.RequestAborted;
            var ids = enrollments.Select(e => e.Id).ToList();
            var seated = await _db.SectionMemberships.AsNoTracking()
                .Where(m => ids.Contains(m.EnrollmentId) && m.EffectiveToUtc == null)
                .Select(m => m.EnrollmentId).ToListAsync(ct);
            var usage = await _enrollmentUsage.InspectManyAsync(ids, ct);

            return enrollments.ToDictionary(
                e => e.Id,
                e => new StudentFileViewModel.EnrollmentActions(
                    e.Id,
                    usage.TryGetValue(e.Id, out var report) ? report : UsageReport.Free,
                    seated.Contains(e.Id)));
        }
    }
}
