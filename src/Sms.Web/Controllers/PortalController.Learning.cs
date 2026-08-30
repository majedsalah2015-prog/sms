using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sms.Application.Common.Exceptions;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.Web.Models;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// Module 37 §8.10 portal slice — "my work": what has been set to each of
    /// the family's students and when it is due.
    ///
    /// <para>
    /// <b>Read-only, and that is a stated gap rather than an oversight.</b> §8.10
    /// also asks for the student's upload-and-submit path, and §8.11 for the
    /// timed sitting. Both need the portal to accept a write, which it has never
    /// done in this product; that is its own slice, with its own review. What is
    /// here is the half that can be built without it — and it is the half a
    /// parent asks for first.
    /// </para>
    ///
    /// <para>
    /// One page for the whole family, like the statement, rather than a page per
    /// child: a parent of three wants Sunday's work in one list, and a student
    /// account's family is itself, so the same screen serves both audiences
    /// (§5). Every read goes through the same BR-SEC-011 gate as the rest of the
    /// portal, and a denial is a 404 (BR-SEC-010 posture), never a 403.
    /// </para>
    ///
    /// Kept in a partial so the E-304 controller body is untouched.
    /// </summary>
    public partial class PortalController
    {
        [HttpGet("work")]
        [RequirePermission(ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Work, ActionVerb.View)]
        public async Task<IActionResult> Work()
        {
            var m = new PortalWorkViewModel();

            foreach (var (student, isSelf) in await FamilyAsync())
            {
                var (_, section) = await PlacementAsync(student.Id);
                try
                {
                    var work = await _portal.GetSetWorkAsync(_user.UserId, student.Id);
                    m.Students.Add(new PortalWorkViewModel.StudentWork(student, isSelf, section, work));
                }
                catch (PortalAccessDeniedException)
                {
                    // Same posture as the family page: a child the gate refuses
                    // simply is not listed. There is nothing to tell a parent
                    // here that would not be telling them about someone else's
                    // child.
                }
            }

            return View(m);
        }

        /// <summary>
        /// Module 37 §5's other half — "Student (portal: <b>read content</b>,
        /// submit homework, sit an exam)". The published lesson plans for the
        /// subjects this family's students study, and the material filed against
        /// them (§8.2).
        ///
        /// <para>
        /// This is the gap the owner reported as "the portal shows nothing but
        /// the homework". It was accurate: §8's numbered screen list enumerates
        /// only "my work" (§8.10) and "my sitting" (§8.11) for the portal, so the
        /// content half was built for the teacher and for nobody else — even
        /// though §1 puts content "surfaced through the portal", §2 puts the
        /// lesson plans and the resource library in scope, BR-LRN-003 makes
        /// publication "the event families see", BR-LRN-006 speaks of serving a
        /// resource "to the portal", and <c>Lesson.PublishedAtUtc</c> has always
        /// documented itself as "the moment the lesson becomes visible in the
        /// portal". A teacher could publish a lesson that no family could open.
        /// </para>
        ///
        /// <para>
        /// One page for the whole family, like "my work" and the statement: a
        /// parent of three wants this week's lessons in one list, and a student
        /// account's family is itself. Grouped by subject rather than by child,
        /// because content follows the offering — two children in the same grade
        /// read one plan, and printing it twice under two names would say
        /// something untrue about it.
        /// </para>
        /// </summary>
        [HttpGet("lessons")]
        [RequirePermission(ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Lessons, ActionVerb.View)]
        public async Task<IActionResult> Lessons()
        {
            var m = new PortalLessonsViewModel();

            foreach (var (student, isSelf) in await FamilyAsync())
            {
                var (grade, _) = await PlacementAsync(student.Id);
                try
                {
                    var lessons = await _portal.GetPublishedLessonsAsync(_user.UserId, student.Id);
                    m.Students.Add(new PortalLessonsViewModel.StudentLessons(student, isSelf, grade, lessons));
                }
                catch (PortalAccessDeniedException)
                {
                    // Same posture as the family page: a child the gate refuses
                    // is simply not listed.
                }
            }

            return View(m);
        }

        /// <summary>
        /// §8.2's material, served to the family. Two gates, both of which have
        /// to be here rather than on the teacher's action: BR-SEC-011 asked of
        /// the resource (a parent may not read another family's lesson material,
        /// and a denial is a 404 like every other portal refusal, never a 403 —
        /// BR-SEC-010), and BR-LRN-006's scan gate, which
        /// <c>AttachmentIntake.ReadAsync</c> applies to the bytes by returning
        /// nothing for a quarantined or still-pending file.
        /// <para>
        /// The portal never lists an unscanned resource in the first place, so
        /// reaching this and being refused means the scan verdict changed
        /// between the page and the click — which is exactly when refusing is
        /// the point.
        /// </para>
        /// </summary>
        [HttpGet("resources/{resourceId:int}/file")]
        [RequirePermission(ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Lessons, ActionVerb.View)]
        public async Task<IActionResult> LessonFile(int resourceId)
        {
            if (!await _portal.CanReadLessonResourceAsync(_user.UserId, resourceId, HttpContext.RequestAborted))
            {
                return NotFound();
            }

            var attachmentId = await _db.LessonResources.AsNoTracking()
                .Where(r => r.Id == resourceId)
                .Select(r => (int?)r.AttachmentId)
                .SingleOrDefaultAsync(HttpContext.RequestAborted);
            if (attachmentId == null)
            {
                return NotFound();
            }

            var intake = HttpContext.RequestServices.GetRequiredService<Sms.Web.Services.AttachmentIntake>();
            Sms.Web.Services.AttachmentIntake.StoredFile? stored;
            try
            {
                stored = await intake.ReadAsync(attachmentId.Value, HttpContext.RequestAborted);
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidOperationException)
            {
                // The row says there is a file and the store cannot produce it. Rare, and a raw 500
                // is the wrong answer to it here: this is the portal, the reader is a parent, and
                // an unhandled exception page is neither translated nor anything they can act on.
                // Same sentence as a file still being checked — from the family's side both mean
                // "not yet", and neither is theirs to fix.
                stored = null;
            }

            if (stored == null)
            {
                TempData["Error"] = T(
                    "That material is not available yet — the school's file check has not cleared it.",
                    "هذه المادة غير متاحة بعد — لم يكتمل فحص الملف لدى المدرسة.");
                return RedirectToAction(nameof(Lessons));
            }

            return File(stored.Content, stored.ContentType, stored.FileName);
        }
    }
}
