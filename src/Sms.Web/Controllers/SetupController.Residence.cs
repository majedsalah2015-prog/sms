using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Geography;
using Sms.Application.Security;
using Sms.Domain.Geography;
using Sms.Domain.Security;
using Sms.Web.Models;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// ثوابت السكن — where the residence hierarchy a student's and a parent's address are chosen
    /// from is authored: محافظة → منطقة → حي (owner request, 2026-08-31).
    /// <para>
    /// The hierarchy arrived seeded from PCBS and read-only in practice: the seeder only ever adds,
    /// and there was no screen behind it, so a misspelt quarter or a locality the pack never listed
    /// could be fixed by nothing short of a hand-written INSERT. The student file offered three
    /// drop-downs and no way to put anything into them.
    /// </para>
    /// <para>
    /// Not a lookup category, and so not the generic lookup editor: <c>LookupValue</c> is a flat
    /// list per category, and this is a tree — a quarter only means something inside its locality.
    /// Flattening it would let a student be recorded in a quarter that does not exist in the
    /// governorate beside it. Hence its own screen, laid out as the tree it is: three columns, each
    /// filled from the selection in the one before it.
    /// </para>
    /// <para>
    /// It sits in System Setup rather than on the student module because both the student file and
    /// the guardian file pick from these lists, and a constant owned by one of the two modules is a
    /// constant the other cannot maintain. The student file links straight here (BR-SEC-010 hides
    /// the link from anyone this screen would refuse), which is what keeps a registrar from having
    /// to abandon a half-typed address to add the quarter it needs.
    /// </para>
    /// <para>
    /// <b>Nothing here deletes.</b> BR-SET-002 / BR-GLB-005: a row an address already points at is
    /// deactivated — it leaves the pickers and stays legible on every record that names it — and
    /// can be brought back. The usage count beside each row is doc/Modules/01 §9's "usage counter
    /// before deactivate", and is the only thing on the page that says whether retiring a row is
    /// about to empty an address somebody reads.
    /// </para>
    /// </summary>
    public partial class SetupController
    {
        /// <summary>
        /// The three columns, with the selection that fills each from the one before it. Everything
        /// is read past the soft-active filter (and so with the school re-applied by hand): the
        /// deactivated rows are what an operator comes here to reactivate, and a list that hid them
        /// would answer a duplicate-code refusal with a row nobody can see.
        /// </summary>
        [HttpGet("residence")]
        [RequirePermission(ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Residence, ActionVerb.View)]
        public async Task<IActionResult> Residence(int? governorateId, int? localityId)
            => View(await BuildResidenceAsync(governorateId, localityId));

        /// <summary>
        /// Adds one row at whichever level the form names. One action for the three levels rather
        /// than three near-identical ones: the difference between them is the parent it hangs from,
        /// which is a value, not a different act.
        /// <para>
        /// The code may be left blank — <see cref="ResidenceCodeGenerator"/> derives one from the
        /// English name. The person adding "حي النصر" has no view about what it should be called
        /// internally, and made to type one they type "1".
        /// </para>
        /// </summary>
        [HttpPost("residence/add")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Residence, ActionVerb.Create)]
        public async Task<IActionResult> AddResidenceRow(
            [FromServices] IResidenceAdmin residence,
            ResidenceLevel level, int? parentId, string? code, string? nameAr, string? nameEn, int? sortOrder,
            int? governorateId, int? localityId)
        {
            try
            {
                Require(nameAr, "Name (Arabic)", "الاسم بالعربية");
                Require(nameEn, "Name (English)", "الاسم بالإنجليزية");
                var parent = RequireParent(level, parentId);

                switch (level)
                {
                    case ResidenceLevel.Governorate:
                        await residence.SaveGovernorateAsync(null, code, nameAr!, nameEn!, sortOrder ?? 0, Ct);
                        break;
                    case ResidenceLevel.Locality:
                        var added = await residence.SaveLocalityAsync(null, parent, code, nameAr!, nameEn!, sortOrder ?? 0, Ct);
                        localityId ??= added.Id;
                        break;
                    default:
                        await residence.SaveQuarterAsync(null, parent, code, nameAr!, nameEn!, sortOrder ?? 0, Ct);
                        break;
                }

                TempData["Flash"] = AddedMessage(level);
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }

            return RedirectToAction(nameof(Residence), new { governorateId, localityId });
        }

        /// <summary>
        /// Corrects the two names and the order of one row. The code is not among them: it is the
        /// stable key the seeder is idempotent on, so a rename that changed it would have the next
        /// seed run insert the original again beside it.
        /// </summary>
        [HttpPost("residence/update")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Residence, ActionVerb.Edit)]
        public async Task<IActionResult> UpdateResidenceRow(
            [FromServices] IResidenceAdmin residence,
            ResidenceLevel level, int id, int? parentId, string? nameAr, string? nameEn, int? sortOrder,
            int? governorateId, int? localityId)
        {
            try
            {
                Require(nameAr, "Name (Arabic)", "الاسم بالعربية");
                Require(nameEn, "Name (English)", "الاسم بالإنجليزية");
                var parent = RequireParent(level, parentId);

                switch (level)
                {
                    case ResidenceLevel.Governorate:
                        await residence.SaveGovernorateAsync(id, null, nameAr!, nameEn!, sortOrder ?? 0, Ct);
                        break;
                    case ResidenceLevel.Locality:
                        await residence.SaveLocalityAsync(id, parent, null, nameAr!, nameEn!, sortOrder ?? 0, Ct);
                        break;
                    default:
                        await residence.SaveQuarterAsync(id, parent, null, nameAr!, nameEn!, sortOrder ?? 0, Ct);
                        break;
                }

                TempData["Flash"] = T("Saved.", "تم الحفظ.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }

            return RedirectToAction(nameof(Residence), new { governorateId, localityId });
        }

        /// <summary>
        /// Takes the row out of the pickers. BR-SET-002: it is not deleted, every address already
        /// recorded against it still reads, and the button beside it puts it back.
        /// </summary>
        [HttpPost("residence/deactivate")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Residence, ActionVerb.Deactivate)]
        public Task<IActionResult> DeactivateResidenceRow(
            [FromServices] IResidenceAdmin residence, ResidenceLevel level, int id, int? governorateId, int? localityId)
            => SetResidenceActiveAsync(residence, level, id, false, governorateId, localityId);

        /// <summary>Puts a retired row back into the pickers.</summary>
        [HttpPost("residence/activate")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Residence, ActionVerb.Edit)]
        public Task<IActionResult> ActivateResidenceRow(
            [FromServices] IResidenceAdmin residence, ResidenceLevel level, int id, int? governorateId, int? localityId)
            => SetResidenceActiveAsync(residence, level, id, true, governorateId, localityId);

        // ------------------------------------------------------------------ helpers

        private async Task<IActionResult> SetResidenceActiveAsync(
            IResidenceAdmin residence, ResidenceLevel level, int id, bool isActive, int? governorateId, int? localityId)
        {
            try
            {
                switch (level)
                {
                    case ResidenceLevel.Governorate:
                        await residence.SetGovernorateActiveAsync(id, isActive, Ct);
                        break;
                    case ResidenceLevel.Locality:
                        await residence.SetLocalityActiveAsync(id, isActive, Ct);
                        break;
                    default:
                        await residence.SetQuarterActiveAsync(id, isActive, Ct);
                        break;
                }

                TempData["Flash"] = isActive
                    ? T("Put back into the address lists.", "أُعيد إلى قوائم العناوين.")
                    : T("Taken out of the address lists — the records that name it still read.", "أُخرِج من قوائم العناوين — وتبقى السجلات التي تذكره مقروءة.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }

            return RedirectToAction(nameof(Residence), new { governorateId, localityId });
        }

        /// <summary>
        /// The parent a locality or a quarter is being hung from. A governorate has none, and the
        /// zero the form posts for it must not travel into a lookup — hence the level decides
        /// whether the value is required at all rather than the value deciding for itself.
        /// </summary>
        private static int RequireParent(ResidenceLevel level, int? parentId)
        {
            if (level == ResidenceLevel.Governorate) return 0;

            if (parentId is not int id || id <= 0)
            {
                throw new InvalidOperationException(level == ResidenceLevel.Locality
                    ? T("Choose the governorate this locality belongs to first.", "اختر المحافظة التي تتبعها هذه المنطقة أولاً.")
                    : T("Choose the locality this quarter belongs to first.", "اختر المنطقة التي يتبعها هذا الحي أولاً."));
            }

            return id;
        }

        private static string AddedMessage(ResidenceLevel level) => level switch
        {
            ResidenceLevel.Governorate => T("Governorate added.", "تمت إضافة المحافظة."),
            ResidenceLevel.Locality => T("Locality added.", "تمت إضافة المنطقة."),
            _ => T("Quarter added.", "تمت إضافة الحي."),
        };

        private async Task<ResidenceCatalogViewModel> BuildResidenceAsync(int? governorateId, int? localityId)
        {
            var governorates = await _db.Governorates.IgnoreQueryFilters().AsNoTracking()
                .Where(g => g.SchoolId == _tenant.SchoolId)
                .OrderBy(g => g.SortOrder).ThenBy(g => g.Code)
                .ToListAsync(Ct);

            // Opening on the first governorate rather than on nothing: a page of three empty columns
            // does not show what it is for, and the operator's first click would be this anyway.
            var selectedGovernorate = governorates.FirstOrDefault(g => g.Id == governorateId)?.Id ?? governorates.FirstOrDefault()?.Id;

            var localities = selectedGovernorate is int gid
                ? await _db.ResidenceAreas.IgnoreQueryFilters().AsNoTracking()
                    .Where(a => a.SchoolId == _tenant.SchoolId && a.GovernorateId == gid)
                    .OrderBy(a => a.SortOrder).ThenBy(a => a.Code)
                    .ToListAsync(Ct)
                : new List<ResidenceArea>();

            // The locality is not auto-selected the way the governorate is: most localities have no
            // quarters at all, so landing on one would suggest the third column is where the work is.
            var selectedLocality = localities.FirstOrDefault(a => a.Id == localityId)?.Id;

            var quarters = selectedLocality is int lid
                ? await _db.Neighbourhoods.IgnoreQueryFilters().AsNoTracking()
                    .Where(n => n.SchoolId == _tenant.SchoolId && n.ResidenceAreaId == lid)
                    .OrderBy(n => n.SortOrder).ThenBy(n => n.Code)
                    .ToListAsync(Ct)
                : new List<Neighbourhood>();

            return new ResidenceCatalogViewModel
            {
                Governorates = governorates,
                SelectedGovernorateId = selectedGovernorate,
                Localities = localities,
                SelectedLocalityId = selectedLocality,
                Quarters = quarters,
                LocalityUsage = await LocalityUsageAsync(localities.Select(a => a.Id).ToList()),
                QuarterUsage = await QuarterUsageAsync(quarters.Select(n => n.Id).ToList()),
                NextGovernorateSort = Next(governorates.Select(g => g.SortOrder)),
                NextLocalitySort = Next(localities.Select(a => a.SortOrder)),
                NextQuarterSort = Next(quarters.Select(n => n.SortOrder)),
            };
        }

        private static int Next(IEnumerable<int> sortOrders)
        {
            var taken = sortOrders.ToList();
            return taken.Count == 0 ? 1 : taken.Max() + 1;
        }

        /// <summary>
        /// How many people record each of these localities as their address — students and
        /// guardians together, because the operator about to retire one wants the number of records
        /// affected, not a split by which module holds them.
        /// <para>
        /// Two grouped counts over the ids actually on the page, not a count per row: the same list
        /// rendered one query at a time is thirty-four queries on the Gaza pack alone.
        /// </para>
        /// </summary>
        private async Task<IReadOnlyDictionary<int, int>> LocalityUsageAsync(IReadOnlyList<int> ids)
        {
            if (ids.Count == 0) return new Dictionary<int, int>();

            var students = await _db.Students.AsNoTracking()
                .Where(s => s.ResidenceAreaId != null && ids.Contains(s.ResidenceAreaId.Value))
                .GroupBy(s => s.ResidenceAreaId!.Value)
                .Select(g => new { Id = g.Key, Count = g.Count() })
                .ToListAsync(Ct);

            var parents = await _db.Parents.AsNoTracking()
                .Where(p => p.ResidenceAreaId != null && ids.Contains(p.ResidenceAreaId.Value))
                .GroupBy(p => p.ResidenceAreaId!.Value)
                .Select(g => new { Id = g.Key, Count = g.Count() })
                .ToListAsync(Ct);

            return Merge(ids, students.Select(x => (x.Id, x.Count)), parents.Select(x => (x.Id, x.Count)));
        }

        private async Task<IReadOnlyDictionary<int, int>> QuarterUsageAsync(IReadOnlyList<int> ids)
        {
            if (ids.Count == 0) return new Dictionary<int, int>();

            var students = await _db.Students.AsNoTracking()
                .Where(s => s.NeighbourhoodId != null && ids.Contains(s.NeighbourhoodId.Value))
                .GroupBy(s => s.NeighbourhoodId!.Value)
                .Select(g => new { Id = g.Key, Count = g.Count() })
                .ToListAsync(Ct);

            var parents = await _db.Parents.AsNoTracking()
                .Where(p => p.NeighbourhoodId != null && ids.Contains(p.NeighbourhoodId.Value))
                .GroupBy(p => p.NeighbourhoodId!.Value)
                .Select(g => new { Id = g.Key, Count = g.Count() })
                .ToListAsync(Ct);

            return Merge(ids, students.Select(x => (x.Id, x.Count)), parents.Select(x => (x.Id, x.Count)));
        }

        private static IReadOnlyDictionary<int, int> Merge(
            IReadOnlyList<int> ids, IEnumerable<(int Id, int Count)> first, IEnumerable<(int Id, int Count)> second)
        {
            var totals = ids.ToDictionary(id => id, _ => 0);
            foreach (var (id, count) in first.Concat(second))
            {
                if (totals.ContainsKey(id)) totals[id] += count;
            }

            return totals;
        }
    }
}
