using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Setup;
using Sms.Web.Models;
using Sms.Web.Timetable;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// E-401 portal slice (doc/Modules/15 §8.7 "section timetable (portal,
    /// printable bilingual)" + §11 "child's today/week schedule with live
    /// room changes"): the child's section week off the operational
    /// version, gated by the same BR-SEC-011 check as the rest of the
    /// portal (denied → 404). Kept in a partial so the E-304 controller
    /// body is untouched.
    /// </summary>
    public partial class PortalController
    {
        [HttpGet("students/{id:int}/timetable")]
        [RequirePermission(ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Child, ActionVerb.View)]
        public async Task<IActionResult> Timetable(int id)
        {
            var student = await _db.Students.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id);
            if (student == null) return NotFound();
            try { await _portal.GetAttendanceSummaryAsync(_user.UserId, id); }
            catch (PortalAccessDeniedException) { return NotFound(); }

            var (grade, section) = await PlacementAsync(id);
            var setup = HttpContext.RequestServices.GetRequiredService<ISystemSetupAdmin>();
            var clock = HttpContext.RequestServices.GetRequiredService<IClock>();
            var m = section == null
                ? new PersonalTimetableViewModel { Kind = "section" }
                : await TimetableQueries.PersonalAsync(_db, setup, "section", section.Id, _workingYear.AcademicYearId, clock.UtcNow.Date, null, IsArabic);
            m.Title = IsArabic ? $"{student.FirstNameAr} {student.FamilyNameAr}" : $"{student.FirstNameEn} {student.FamilyNameEn}";
            m.Subtitle = section == null
                ? T("No current section", "لا شعبة حالية")
                : $"{(grade == null ? "" : grade.Code + " · ")}{TimetableLabels.SectionName(section, IsArabic)}";
            m.BackId = id;
            return View(m);
        }
    }
}
