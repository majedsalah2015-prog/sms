using System.Threading;
using System.Threading.Tasks;
using Sms.Application.Lookups;
using Sms.Application.Seeding;
using Sms.Domain.Lookups;

namespace Sms.Infrastructure.Seeding
{
    /// <summary>
    /// BR-SET-001 product-tier starter set. A representative sample, not the
    /// exhaustive product catalog (real nationality/currency lists are a
    /// content-authoring task, not an engineering one) — proves the
    /// mechanism; module docs/content curation extend it.
    /// </summary>
    public class LookupProductSeedContributor : ISeedContributor
    {
        private readonly ILookupAdmin _lookups;

        public LookupProductSeedContributor(ILookupAdmin lookups)
        {
            _lookups = lookups;
        }

        public string Name => "Product-tier lookups (BR-SET-001)";

        public int Order => 10;

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            await _lookups.DefineCategoryAsync("Nationality", LookupCategoryTier.ProductSeeded, "الجنسية", "Nationality", cancellationToken);
            await SeedValues("Nationality", cancellationToken,
                ("SA", "سعودي", "Saudi"), ("EG", "مصري", "Egyptian"), ("JO", "أردني", "Jordanian"),
                ("IN", "هندي", "Indian"), ("PH", "فلبيني", "Filipino"), ("US", "أمريكي", "American"), ("GB", "بريطاني", "British"));

            await _lookups.DefineCategoryAsync("BloodType", LookupCategoryTier.ProductSeeded, "فصيلة الدم", "Blood Type", cancellationToken);
            await SeedValues("BloodType", cancellationToken,
                ("A+", "A+", "A+"), ("A-", "A-", "A-"), ("B+", "B+", "B+"), ("B-", "B-", "B-"),
                ("AB+", "AB+", "AB+"), ("AB-", "AB-", "AB-"), ("O+", "O+", "O+"), ("O-", "O-", "O-"));

            await _lookups.DefineCategoryAsync("IdType", LookupCategoryTier.ProductSeeded, "نوع الهوية", "ID Type", cancellationToken);
            await SeedValues("IdType", cancellationToken,
                ("NationalId", "هوية وطنية", "National ID"), ("Iqama", "إقامة", "Iqama"), ("Passport", "جواز سفر", "Passport"));

            await _lookups.DefineCategoryAsync("RelationshipType", LookupCategoryTier.ProductSeeded, "صلة القرابة", "Relationship Type", cancellationToken);
            await SeedValues("RelationshipType", cancellationToken,
                ("Father", "الأب", "Father"), ("Mother", "الأم", "Mother"), ("Guardian", "ولي أمر", "Guardian"),
                ("Grandfather", "الجد", "Grandfather"), ("Grandmother", "الجدة", "Grandmother"), ("Other", "أخرى", "Other"));
        }

        private async Task SeedValues(string categoryCode, CancellationToken cancellationToken, params (string Code, string Ar, string En)[] values)
        {
            for (var i = 0; i < values.Length; i++)
            {
                var (code, ar, en) = values[i];
                await _lookups.DefineValueAsync(categoryCode, code, ar, en, sortOrder: i + 1, cancellationToken);
            }
        }
    }
}
