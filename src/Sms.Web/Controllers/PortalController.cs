using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Portal;
using Sms.Domain.Messaging;
using Sms.Domain.Students;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;
using Sms.Web.Security;
using Sms.Application.Security;
using Sms.Domain.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// E-304 Portal essentials — the parent/student front door over the
    /// read-only IParentPortalQuery (E-304 engine): My family (doc/Modules/11
    /// §8.5), the portal student profile (doc/Modules/10 §8: attendance /
    /// published results / posted fees — BR-SEC-012 by construction), My
    /// statement (all children, pay-ready), announcements read-only (E-703's
    /// Sent announcements). BR-SEC-010 is enforced by the global
    /// PortalAreaFilter (portal accounts → staff URLs 404); BR-SEC-011 by the
    /// query itself (denied → 404 here, never 403); BR-SEC-013 by
    /// [PortalReauth] on the fee pages. Deferred: My contacts self-service,
    /// channel preferences (BR-NOT-007), report-card download (O6), appeal
    /// request (M17 full), online payment (BR-PAY-007 dormant).
    /// </summary>
    [Route("portal")]
    public partial class PortalController : Controller
    {
        private readonly IParentPortalQuery _portal;
        private readonly AppDbContext _db;
        private readonly ICurrentUser _user;
        private readonly IWorkingYearContext _workingYear;
        private readonly Sms.Web.Services.PersonPhotoService _photos;

        public PortalController(IParentPortalQuery portal, AppDbContext db, ICurrentUser user, IWorkingYearContext workingYear, Sms.Web.Services.PersonPhotoService photos)
        {
            _portal = portal;
            _db = db;
            _user = user;
            _workingYear = workingYear;
            _photos = photos;
        }

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        /// <summary>
        /// BR-SEC-013 companion (WCAG 2.2.1): the layout's idle-warning dialog
        /// extends the session with one authenticated round-trip — session
        /// activity is refreshed by <c>SessionCookieEvents</c>/<c>ValidateSessionAsync</c>
        /// on any authenticated request, so an empty 204 suffices.
        /// </summary>
        [HttpGet("ping")]
        [NoPermissionRequired("Liveness probe for the portal shell.")]
        public IActionResult Ping() => NoContent();

        // ================================================================== My family

        [HttpGet("")]
        [RequirePermission(ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Home, ActionVerb.View)]
        public async Task<IActionResult> Index()
        {
            var m = new PortalHomeViewModel
            {
                IsPortalAccount = PortalAreaFilter.IsPortalAccount(User.FindFirst(SmsClaimTypes.AccountType)?.Value),
                Year = await _db.AcademicYears.AsNoTracking().SingleOrDefaultAsync(y => y.Id == _workingYear.AcademicYearId),
            };
            var parent = await _db.Parents.AsNoTracking().SingleOrDefaultAsync(p => p.UserAccountId == _user.UserId);
            if (parent != null) { m.ParentNameAr = parent.NameAr; m.ParentNameEn = parent.NameEn; }

            var family = await FamilyAsync();
            var cards = new List<PortalChildCard>();
            foreach (var (student, isSelf) in family)
            {
                var (grade, section) = await PlacementAsync(student.Id);
                PortalAttendanceSummary? att = null; decimal? fee = null; var results = 0;
                try
                {
                    att = await _portal.GetAttendanceSummaryAsync(_user.UserId, student.Id);
                    fee = (await _portal.GetFeePositionAsync(_user.UserId, student.Id)).Position;
                    results = (await _portal.GetPublishedResultsAsync(_user.UserId, student.Id)).Count;
                }
                catch (PortalAccessDeniedException) { /* card stays summary-less; the detail page 404s the same way */ }
                cards.Add(new PortalChildCard(student, isSelf, grade, section, att, fee, results));
            }
            m.Children = cards;
            m.Announcements = await AnnouncementsAsync(5);
            return View(m);
        }

        // ================================================================== Portal student profile

        [HttpGet("students/{id:int}")]
        [RequirePermission(ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Child, ActionVerb.View)]
        public async Task<IActionResult> Student(int id, string? tab = null)
        {
            var student = await _db.Students.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id);
            if (student == null) return NotFound();
            PortalAttendanceSummary attendance; IReadOnlyList<PortalResultSummary> results; PortalFeePosition fees;
            try
            {
                // Every read goes through the BR-SEC-011 gate; denial is a 404 (BR-SEC-010 posture), never a 403.
                attendance = await _portal.GetAttendanceSummaryAsync(_user.UserId, id);
                results = await _portal.GetPublishedResultsAsync(_user.UserId, id);
                fees = await _portal.GetFeePositionAsync(_user.UserId, id);
            }
            catch (PortalAccessDeniedException) { return NotFound(); }

            var (grade, section) = await PlacementAsync(id);
            var enrollment = await _db.Enrollments.AsNoTracking().Where(e => e.StudentId == id && e.AcademicYearId == _workingYear.AcademicYearId).FirstOrDefaultAsync();
            var recent = enrollment == null ? new List<Sms.Domain.Attendance.AttendanceDay>() : await _db.AttendanceDays.AsNoTracking().Where(a => a.EnrollmentId == enrollment.Id).OrderByDescending(a => a.Date).Take(30).ToListAsync();
            var offIds = results.Select(r => r.CurriculumOfferingId).Distinct().ToList();
            var offerings = await _db.CurriculumOfferings.AsNoTracking().Where(o => offIds.Contains(o.Id)).ToListAsync();
            var subjects = await _db.Subjects.IgnoreQueryFilters().AsNoTracking().Where(s => offerings.Select(o => o.SubjectId).Contains(s.Id)).ToListAsync();
            var terms = await _db.Terms.AsNoTracking().Where(t => t.AcademicYearId == _workingYear.AcademicYearId).OrderBy(t => t.SequenceNumber).ToListAsync();

            var m = new PortalStudentViewModel
            {
                Student = student, IsSelf = student.UserAccountId == _user.UserId, Grade = grade, Section = section,
                Year = await _db.AcademicYears.AsNoTracking().SingleOrDefaultAsync(y => y.Id == _workingYear.AcademicYearId),
                ActiveTab = tab ?? "attendance", Attendance = attendance, RecentDays = recent, Terms = terms, Fees = fees,
                ResultsHidden = student.Status == StudentStatus.Suspended,
                Results = results.Select(r =>
                {
                    var o = offerings.FirstOrDefault(x => x.Id == r.CurriculumOfferingId);
                    var subj = o == null ? null : subjects.FirstOrDefault(s => s.Id == o.SubjectId);
                    var term = terms.FirstOrDefault(t => t.Id == r.TermId);
                    return subj == null || term == null ? null : new PortalStudentViewModel.ResultRow(term, subj, r);
                }).Where(x => x != null).Select(x => x!).OrderBy(x => x.Term.SequenceNumber).ThenBy(x => x.Subject.Code).ToList(),
            };
            return View(m);
        }

        /// <summary>
        /// The child's photograph, behind the same BR-SEC-011 gate as everything else on the portal:
        /// a parent who may not read this student's record does not get their face either, and the
        /// refusal is a 404 like every other denial here (BR-SEC-010).
        /// </summary>
        [HttpGet("students/{id:int}/photo")]
        [RequirePermission(ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Child, ActionVerb.View)]
        public async Task<IActionResult> StudentPhoto(int id)
        {
            try
            {
                await _portal.GetAttendanceSummaryAsync(_user.UserId, id);
            }
            catch (PortalAccessDeniedException) { return NotFound(); }

            var photoId = await _db.Students.AsNoTracking()
                .Where(s => s.Id == id).Select(s => s.PhotoAttachmentId).SingleOrDefaultAsync();
            var photo = await _photos.ReadAsync(photoId, HttpContext.RequestAborted);
            return photo == null ? NotFound() : File(photo.Value.Content, photo.Value.ContentType);
        }

        // ================================================================== My statement (BR-SEC-013: re-auth after 15 idle minutes)

        [HttpGet("statement")]
        [PortalReauth]
        [RequirePermission(ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Statement, ActionVerb.View)]
        public async Task<IActionResult> Statement()
        {
            var lines = new List<PortalStatementViewModel.Line>();
            foreach (var (student, _) in await FamilyAsync())
            {
                try { lines.Add(new PortalStatementViewModel.Line(student, await _portal.GetFeePositionAsync(_user.UserId, student.Id))); }
                catch (PortalAccessDeniedException) { }
            }
            return View(new PortalStatementViewModel { Lines = lines, Total = lines.Sum(l => l.Position.Position) });
        }

        // ================================================================== Announcements (read-only)

        [HttpGet("announcements")]
        [RequirePermission(ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Announcements, ActionVerb.View)]
        public async Task<IActionResult> Announcements()
            => View(new PortalAnnouncementsViewModel { Announcements = await AnnouncementsAsync(50) });

        // ================================================================== helpers

        /// <summary>Guardian-visible children (BR-PAR-004 / BR-SEC-011) plus the caller's own student record, if any.</summary>
        private async Task<IReadOnlyList<(Student Student, bool IsSelf)>> FamilyAsync()
        {
            var list = new List<(Student, bool)>();
            var self = await _db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.UserAccountId == _user.UserId);
            if (self != null) list.Add((self, true));
            var children = await _portal.GetVisibleChildrenAsync(_user.UserId);
            var ids = children.Select(c => c.StudentId).Where(id => self == null || id != self.Id).ToList();
            var students = await _db.Students.AsNoTracking().Where(s => ids.Contains(s.Id)).OrderBy(s => s.StudentNo).ToListAsync();
            list.AddRange(students.Select(s => (s, false)));
            return list;
        }

        private async Task<(Sms.Domain.Grades.GradeLevel? Grade, Sms.Domain.Sections.Section? Section)> PlacementAsync(int studentId)
        {
            var enrollment = await _db.Enrollments.AsNoTracking().Where(e => e.StudentId == studentId && e.AcademicYearId == _workingYear.AcademicYearId).FirstOrDefaultAsync();
            if (enrollment == null) return (null, null);
            var profile = await _db.GradeYearProfiles.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(p => p.Id == enrollment.GradeYearProfileId);
            var grade = profile == null ? null : await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(g => g.Id == profile.GradeLevelId);
            var membership = await _db.SectionMemberships.AsNoTracking().Where(m => m.EnrollmentId == enrollment.Id && m.EffectiveToUtc == null).FirstOrDefaultAsync();
            var section = membership == null ? null : await _db.Sections.AsNoTracking().SingleOrDefaultAsync(s => s.Id == membership.SectionId);
            return (grade, section);
        }

        /// <summary>BR-SEC-012: only Sent (published) announcements ever reach the portal.</summary>
        private Task<List<Announcement>> AnnouncementsAsync(int take)
            => _db.Announcements.AsNoTracking().Where(a => a.Status == AnnouncementStatus.Sent).OrderByDescending(a => a.SentAtUtc).Take(take).ToListAsync();
    }
}
