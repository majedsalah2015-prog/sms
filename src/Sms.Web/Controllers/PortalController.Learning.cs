using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
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
    }
}
