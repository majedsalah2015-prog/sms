using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Security;
using Sms.Domain.Common;
using Sms.Domain.Security;
using Sms.Domain.Students;
using Sms.Web.Models;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// Where the student lives — محافظة ← منطقة ← حي (owner request, 2026-08-31).
    /// <para>
    /// On the student's own record rather than read through the guardian: the registrar looks for a
    /// pupil's address on the pupil's file, and on the day it was asked for not one of 987 parent
    /// files carried a residence, because reaching it meant opening a second person's file. The
    /// owner was told that this reopens the case a sibling's address can disagree with, and asked
    /// for it anyway; see <see cref="Sms.Domain.Students.Student.ResidenceAreaId"/>.
    /// </para>
    /// <para>
    /// Kept in its own file of the partial controller so that the residence is one thing to read,
    /// and so its arrival does not collide with the register, the import and the placement work all
    /// living in the same class.
    /// </para>
    /// </summary>
    public partial class StudentsController
    {
        /// <summary>
        /// The localities of one governorate, fetched as the level above it changes rather than
        /// written into the page: 34 localities across five governorates is already too many to
        /// scan at once, and it is the same list the parent file fetches from its own endpoint.
        /// </summary>
        [HttpGet("residence/areas")]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.File, ActionVerb.View)]
        public async Task<IActionResult> ResidenceAreas(int governorateId)
        {
            var areas = await _db.ResidenceAreas.AsNoTracking()
                .Where(a => a.GovernorateId == governorateId)
                .OrderBy(a => a.SortOrder)
                .Select(a => new { id = a.Id, ar = a.Name.NameAr, en = a.Name.NameEn })
                .ToListAsync();
            return Json(areas);
        }

        /// <summary>The quarters of one locality — empty for most of them, which is not an error.</summary>
        [HttpGet("residence/neighbourhoods")]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.File, ActionVerb.View)]
        public async Task<IActionResult> ResidenceNeighbourhoods(int areaId)
        {
            var hoods = await _db.Neighbourhoods.AsNoTracking()
                .Where(n => n.ResidenceAreaId == areaId)
                .OrderBy(n => n.SortOrder)
                .Select(n => new { id = n.Id, ar = n.Name.NameAr, en = n.Name.NameEn })
                .ToListAsync();
            return Json(hoods);
        }

        /// <summary>
        /// Records the residence. <see cref="ScreenCatalog.Students.File"/> + Edit, the same
        /// permission the identity form beside it carries — not the social profile's, which is
        /// gated away from most of the people who know where a child lives.
        /// </summary>
        [HttpPost("{id:int}/residence")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.File, ActionVerb.Edit)]
        public async Task<IActionResult> SaveResidence(int id, int? residenceAreaId, int? neighbourhoodId)
        {
            try
            {
                // No audit reason set: an address is a fact being recorded, not a decision being
                // defended, and a family that moves has not justified anything. The change is still
                // captured field-level, because Student is T1.
                await _students.SetResidenceAsync(id, residenceAreaId, neighbourhoodId, HttpContext.RequestAborted);
                TempData["Flash"] = T("Residence updated.", "تم تحديث السكن.");
            }
            catch (System.InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }

            return RedirectToAction(nameof(File), new { id, tab = "personal" });
        }

        /// <summary>
        /// Opens the picker on what is stored, and builds the one-line address the file shows beside
        /// it.
        /// <para>
        /// The governorate is resolved by walking up from the locality rather than being stored:
        /// two columns that must agree are two columns that eventually will not. A locality whose
        /// row has since been deactivated is still read here — <c>IgnoreQueryFilters</c> — because
        /// the question this asks is "what does this record point at", and a retired locality that
        /// silently rendered as "not recorded" would look like data loss.
        /// </para>
        /// </summary>
        private async Task FillResidenceAsync(StudentFileViewModel model, Student s)
        {
            model.Governorates = await _db.Governorates.AsNoTracking().OrderBy(g => g.SortOrder).ToListAsync();

            // The three lists are constants until somebody may maintain them, and the person who
            // first notices a missing quarter is the registrar typing the address — so the file
            // links to where they are authored. Asked of the permission service rather than
            // assumed: the link goes to System Setup, and most people who may edit a student file
            // may not open that (BR-SEC-010 — it disappears rather than refusing on click).
            model.CanManageResidenceLists = await _permissions.HasPermissionAsync(
                ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Residence, ActionVerb.View, HttpContext.RequestAborted);

            if (s.ResidenceAreaId is not int areaId) return;

            var area = await _db.ResidenceAreas.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(a => a.Id == areaId);
            var hood = s.NeighbourhoodId is int hoodId
                ? await _db.Neighbourhoods.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(n => n.Id == hoodId)
                : null;
            var governorate = area == null
                ? null
                : await _db.Governorates.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(g => g.Id == area.GovernorateId);

            static string? Nm(LocalizedName? n) => n == null ? null : (IsArabic ? n.NameAr : n.NameEn);
            var path = string.Join(" · ", new[] { Nm(governorate?.Name), Nm(area?.Name), Nm(hood?.Name) }
                .Where(x => !string.IsNullOrWhiteSpace(x)));

            model.CurrentGovernorateId = governorate?.Id;
            model.CurrentResidenceAreaId = area?.Id;
            model.CurrentResidencePath = string.IsNullOrWhiteSpace(path) ? null : path;
        }
    }
}
