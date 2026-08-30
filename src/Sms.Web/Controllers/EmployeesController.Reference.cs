using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Lookups;
using Sms.Application.Security;
using Sms.Domain.Lookups;
using Sms.Domain.Security;
using Sms.Web.Models;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// الثوابت — where the staff file's four catalogues are authored (owner request, 2026-08-27).
    /// <para>
    /// The qualifications tab of the employee file picks a qualification, a university, a
    /// specialization and a bank rather than typing them, and until this screen existed there was
    /// nowhere to put the values it picks from. The generic lookup editor could not do it: three of
    /// the four categories are <see cref="LookupCategoryTier.ProductSeeded"/>, which
    /// <c>SetupController.Lookups</c> then rendered read-only on tier alone, and two of them ship
    /// with no values at all — so a school opening that screen found a university list it could
    /// neither use nor fill. This screen was the same exception <c>SetupController.Nationalities</c>
    /// already made for the nationality list: a dedicated screen that may edit a product-tier
    /// catalogue, because the tier means "the product ships the values", not "the school may not
    /// have any".
    /// </para>
    /// <para>
    /// That exception is now named once, in <see cref="SchoolAuthoredLookups"/>, and the generic
    /// lookup screen consults it too — so these four lists are editable there as well, and the two
    /// screens can no longer disagree about whether a school owns its own specializations. This
    /// screen stays because it is the one the registrar can reach without leaving the staff module,
    /// and because it says what each list is for. When this epic lands, the lookup screen should
    /// gain a cross-link to it, and <c>SetupController.GenerateCode</c> should collapse into
    /// <see cref="LookupCodeGenerator"/> — both were left out of the lookup change so that its
    /// commit did not depend on files this epic has not committed yet.
    /// </para>
    /// <para>
    /// It lives on the employee controller, and in the staff sub-navigation, because that is where
    /// the person who needs it is. A registrar halfway through entering a teacher's degree wants the
    /// missing university added and to carry on; sending them to System Setup for it is how a
    /// catalogue ends up with "أخرى" in it forever.
    /// </para>
    /// <para>
    /// The Banks list was authored here and read nowhere for three days: <c>Employee.BankName</c>
    /// was free text, so nothing counted as a usage of a bank value and the employee file did not
    /// offer the list. <c>Employee.BankLookupId</c> closed that on 2026-08-30 — the file's personal
    /// tab now picks from this catalogue, the payroll transfer list carries what it picks, and the
    /// deactivate prompt below can finally say how many employees are paid into a bank before it is
    /// retired. The free-text column stays as the fallback for registers entered before the picker
    /// and for what the Excel import writes, which still matches no catalogue.
    /// </para>
    /// </summary>
    public partial class EmployeesController
    {
        /// <summary>
        /// The three services this screen needs arrive per action rather than through the
        /// constructor: they are the reference lists' own, nothing else on this controller reads a
        /// lookup catalogue, and the directory's constructor should stay about the directory.
        /// </summary>
        [HttpGet("reference")]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Reference, ActionVerb.View)]
        public async Task<IActionResult> Reference([FromServices] ILookupUsageQuery usage, string? list = null)
            => View(await BuildStaffReferenceAsync(list, usage));

        /// <summary>
        /// Adds a value, or overwrites the name and order of the one that already holds the code.
        /// <para>
        /// The code may be left blank and is then derived from the English name
        /// (<see cref="LookupCodeGenerator"/>). Requiring one would be asking the person cataloguing
        /// eighty universities to invent eighty stable keys, which is how a catalogue ends up keyed
        /// 1, 2, 3.
        /// </para>
        /// </summary>
        [HttpPost("reference/save")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Reference, ActionVerb.Create)]
        public async Task<IActionResult> SaveReferenceValue(
            [FromServices] ILookupAdmin lookups,
            string? list,
            string? code,
            string? nameAr,
            string? nameEn,
            int? sortOrder)
        {
            var selected = StaffReferenceCatalogue.Find(list);
            if (selected == null)
            {
                return NotFound();
            }

            try
            {
                RequireReferenceText(nameAr, "Arabic name", "الاسم بالعربية");
                RequireReferenceText(nameEn, "English name", "الاسم بالإنجليزية");

                var category = await FindStaffReferenceCategoryAsync(selected.CategoryCode);
                if (category == null)
                {
                    // Only when the deployment's seeder predates the list. Never on an existing
                    // category: DefineCategoryAsync overwrites the tier and both names, so calling
                    // it unconditionally would let this screen rename a catalogue it shares with
                    // the parent record.
                    category = await lookups.DefineCategoryAsync(
                        selected.CategoryCode, LookupCategoryTier.ProductSeeded, selected.CategoryNameAr, selected.CategoryNameEn);
                }

                var existing = await _db.LookupValues.IgnoreQueryFilters().AsNoTracking()
                    .Where(v => v.LookupCategoryId == category.Id && v.SchoolId == _db.CurrentSchoolId)
                    .ToListAsync();

                var typed = code?.Trim().ToUpperInvariant();
                var finalCode = string.IsNullOrWhiteSpace(typed)
                    ? LookupCodeGenerator.FromName(nameEn, existing.Select(v => v.Code), selected.CategoryCode)
                    : typed!;

                var isNew = !existing.Any(v => string.Equals(v.Code, finalCode, StringComparison.OrdinalIgnoreCase));
                var order = sortOrder ?? (existing.Count == 0 ? 1 : existing.Max(v => v.SortOrder) + 1);
                await lookups.DefineValueAsync(selected.CategoryCode, finalCode, nameAr!.Trim(), nameEn!.Trim(), order);

                TempData["Flash"] = isNew
                    ? T($"Added to {selected.TitleEn}.", $"أُضيفت إلى «{selected.TitleAr}».")
                    : T("Saved.", "تم الحفظ.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Reference), new { list = selected.CategoryCode });
        }

        /// <summary>
        /// BR-SET-002 / BR-GLB-005: retired, never deleted. Rows already pointing at the value keep
        /// it; it just stops being offered. The screen has already shown the operator how many those
        /// are.
        /// </summary>
        [HttpPost("reference/{id:int}/deactivate")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Reference, ActionVerb.Deactivate)]
        public async Task<IActionResult> DeactivateReferenceValue([FromServices] ILookupAdmin lookups, int id, string? list)
        {
            var selected = StaffReferenceCatalogue.Find(list);
            if (selected == null)
            {
                return NotFound();
            }

            try
            {
                await lookups.DeactivateValueAsync(id);
                TempData["Flash"] = T("Deactivated — it stays on the records that already use it.", "أُلغي التفعيل — وتبقى في السجلات التي تستخدمها بالفعل.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Reference), new { list = selected.CategoryCode });
        }

        /// <summary>
        /// Puts a retired value back into the pickers. An upsert of its own name and order, because
        /// <c>DefineValueAsync</c> is what sets <c>IsActive</c> back to true — the same route
        /// <c>SetupController.ActivateNationality</c> takes.
        /// </summary>
        [HttpPost("reference/{id:int}/activate")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Reference, ActionVerb.Edit)]
        public async Task<IActionResult> ActivateReferenceValue([FromServices] ILookupAdmin lookups, int id, string? list)
        {
            var selected = StaffReferenceCatalogue.Find(list);
            if (selected == null)
            {
                return NotFound();
            }

            try
            {
                // Past the filter: a retired value is exactly what this reads, and reading through
                // the filter would fail with "sequence contains no elements" on every click.
                var value = await _db.LookupValues.IgnoreQueryFilters().AsNoTracking()
                    .SingleOrDefaultAsync(v => v.Id == id && v.SchoolId == _db.CurrentSchoolId);
                if (value == null)
                {
                    return NotFound();
                }

                await lookups.DefineValueAsync(selected.CategoryCode, value.Code, value.Name.NameAr, value.Name.NameEn, value.SortOrder);
                TempData["Flash"] = T("Reactivated.", "أُعيد التفعيل.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Reference), new { list = selected.CategoryCode });
        }

        private async Task<StaffReferenceViewModel> BuildStaffReferenceAsync(string? list, ILookupUsageQuery usage)
        {
            var selected = StaffReferenceCatalogue.Find(list) ?? StaffReferenceCatalogue.Default;
            var codes = StaffReferenceCatalogue.All.Select(l => l.CategoryCode).ToList();

            // IgnoreQueryFilters for the *lookup*, filtered for the *picker*: a category or value
            // someone retired must still be findable by the screen that retired it.
            var categories = await _db.LookupCategories.IgnoreQueryFilters().AsNoTracking()
                .Where(c => c.SchoolId == _db.CurrentSchoolId && codes.Contains(c.Code))
                .ToListAsync();

            var categoryIds = categories.Select(c => c.Id).ToList();
            var activeCounts = await _db.LookupValues.AsNoTracking()
                .Where(v => categoryIds.Contains(v.LookupCategoryId))
                .GroupBy(v => v.LookupCategoryId)
                .Select(g => new { CategoryId = g.Key, Count = g.Count() })
                .ToListAsync();
            var countByCategoryId = activeCounts.ToDictionary(x => x.CategoryId, x => x.Count);

            var selectedCategory = categories.FirstOrDefault(c => c.Code == selected.CategoryCode);
            var values = selectedCategory == null
                ? new List<LookupValue>()
                : await _db.LookupValues.IgnoreQueryFilters().AsNoTracking()
                    .Where(v => v.LookupCategoryId == selectedCategory.Id && v.SchoolId == _db.CurrentSchoolId)
                    .OrderBy(v => v.SortOrder).ThenBy(v => v.Id)
                    .ToListAsync();

            return new StaffReferenceViewModel
            {
                Selected = selected,
                Values = values,
                Usage = values.Count == 0
                    ? new Dictionary<int, IReadOnlyList<LookupUsage>>()
                    : await usage.CountUsagesAsync(values.Select(v => v.Id).ToList()),
                Counts = StaffReferenceCatalogue.All.ToDictionary(
                    l => l.CategoryCode,
                    l => StaffReferenceCount(l.CategoryCode, categories, countByCategoryId)),
                NextSortOrder = values.Count == 0 ? 1 : values.Max(v => v.SortOrder) + 1,
            };
        }

        /// <summary>Active values in a category — zero for one the seeder has not created here yet.</summary>
        private static int StaffReferenceCount(string categoryCode, IReadOnlyList<LookupCategory> categories, IReadOnlyDictionary<int, int> countByCategoryId)
        {
            var category = categories.FirstOrDefault(c => c.Code == categoryCode);
            return category != null && countByCategoryId.TryGetValue(category.Id, out var count) ? count : 0;
        }

        private async Task<LookupCategory?> FindStaffReferenceCategoryAsync(string categoryCode)
            => await _db.LookupCategories.IgnoreQueryFilters().AsNoTracking()
                .SingleOrDefaultAsync(c => c.SchoolId == _db.CurrentSchoolId && c.Code == categoryCode);

        private static void RequireReferenceText(string? value, string fieldEn, string fieldAr)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(T($"{fieldEn} is required.", $"الحقل «{fieldAr}» مطلوب."));
            }
        }
    }
}
