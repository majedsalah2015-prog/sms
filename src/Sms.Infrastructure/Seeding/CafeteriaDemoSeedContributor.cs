using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Cafeteria;
using Sms.Application.Common.Interfaces;
using Sms.Application.Seeding;
using Sms.Domain.Cafeteria;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Seeding
{
    /// <summary>
    /// A counter with something behind it: a short catalogue and today's menu, so
    /// doc/Modules/27 §8.1 has stock to sell the moment it opens.
    /// <para>
    /// A fixture, not content. The items are the ones every school canteen has,
    /// spread across the three nutrition classes so the POS's colour coding means
    /// something, with one carrying a real allergen so the allergy path can be
    /// walked rather than only read about. VAT is left unset on all of them —
    /// whether school food is taxable is the owner's answer, and the default has
    /// to be the one that changes nobody's bill.
    /// </para>
    /// <para>
    /// Separate from <c>DemoSeedContributor</c>, which returns early once a school
    /// exists. This one is idempotent on its own terms, so it fills a database
    /// that was provisioned before the cafeteria screens existed.
    /// </para>
    /// </summary>
    public class CafeteriaDemoSeedContributor : ISeedContributor
    {
        private readonly AppDbContext _db;
        private readonly ICafeteriaAdmin _cafeteria;
        private readonly IClock _clock;

        public CafeteriaDemoSeedContributor(AppDbContext db, ICafeteriaAdmin cafeteria, IClock clock)
        {
            _db = db;
            _cafeteria = cafeteria;
            _clock = clock;
        }

        public string Name => "Cafeteria demo catalogue (doc/Modules/27 §8.1)";

        // After the demo tenant (40-ish) so a school exists to scope the items to.
        public int Order => 45;

        private static readonly (string Ar, string En, string Category, decimal Price, NutritionClass Class, string? Allergens, bool StaffOnly)[] Catalogue =
        {
            ("ماء معدني", "Bottled water", "drinks", 1.00m, NutritionClass.Green, null, false),
            ("عصير برتقال طبيعي", "Orange juice", "drinks", 3.50m, NutritionClass.Green, null, false),
            ("حليب", "Milk", "drinks", 2.50m, NutritionClass.Green, "milk", false),
            ("قهوة", "Coffee", "drinks", 5.00m, NutritionClass.Amber, null, true),

            ("ساندويتش جبنة", "Cheese sandwich", "food", 6.00m, NutritionClass.Green, "milk,gluten", false),
            ("ساندويتش زعتر", "Zaatar sandwich", "food", 4.50m, NutritionClass.Green, "gluten,sesame", false),
            ("سلطة فواكه", "Fruit salad", "food", 5.50m, NutritionClass.Green, null, false),
            ("معجنات بالجبنة", "Cheese pastry", "food", 4.00m, NutritionClass.Amber, "milk,gluten", false),

            ("بسكويت", "Biscuits", "snacks", 2.00m, NutritionClass.Amber, "gluten", false),
            ("مكسرات مشكلة", "Mixed nuts", "snacks", 6.50m, NutritionClass.Amber, "peanuts,nuts", false),
            ("شوكولاتة", "Chocolate bar", "snacks", 3.00m, NutritionClass.Red, "milk,nuts", false),
            ("رقائق بطاطس", "Crisps", "snacks", 2.50m, NutritionClass.Red, null, false),
        };

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            if (!await _db.Schools.AnyAsync(cancellationToken))
            {
                return;
            }

            var existing = await _db.CafeteriaItems.IgnoreQueryFilters()
                .Where(i => i.SchoolId == _db.CurrentSchoolId)
                .Select(i => i.NameEn)
                .ToListAsync(cancellationToken);
            var have = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var (ar, en, category, price, nutrition, allergens, staffOnly) in Catalogue)
            {
                if (have.Contains(en))
                {
                    continue;
                }

                await _cafeteria.DefineItemAsync(ar, en, category, price, nutrition, allergens, staffOnly, cancellationToken);
            }

            // Today's menu, so the counter's "today only" filter has something to filter to. Staff-only
            // items stay off it: a menu is what the students queue for.
            var today = _clock.UtcNow.Date;
            if (await _db.Menus.AnyAsync(m => m.Date == today, cancellationToken))
            {
                return;
            }

            var itemIds = await _db.CafeteriaItems
                .Where(i => !i.IsStaffOnly)
                .OrderBy(i => i.Id)
                .Select(i => i.Id)
                .ToListAsync(cancellationToken);
            if (itemIds.Count > 0)
            {
                await _cafeteria.DefineMenuAsync(today, itemIds, publish: true, cancellationToken);
            }
        }
    }
}
