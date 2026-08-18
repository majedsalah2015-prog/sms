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

            // E-101 / BR-SET-001 "ISO currencies" + BR-GLB-112: the wizard's currency
            // step and School.CurrencyCode validate against this product list.
            await _lookups.DefineCategoryAsync("Currency", LookupCategoryTier.ProductSeeded, "العملة", "Currency", cancellationToken);
            await SeedValues("Currency", cancellationToken,
                ("SAR", "ريال سعودي", "Saudi Riyal"), ("AED", "درهم إماراتي", "UAE Dirham"), ("KWD", "دينار كويتي", "Kuwaiti Dinar"),
                ("BHD", "دينار بحريني", "Bahraini Dinar"), ("QAR", "ريال قطري", "Qatari Riyal"), ("OMR", "ريال عماني", "Omani Rial"),
                ("EGP", "جنيه مصري", "Egyptian Pound"), ("JOD", "دينار أردني", "Jordanian Dinar"), ("USD", "دولار أمريكي", "US Dollar"),
                ("EUR", "يورو", "Euro"), ("GBP", "جنيه إسترليني", "Pound Sterling"));

            await _lookups.DefineCategoryAsync("JobTitle", LookupCategoryTier.ProductSeeded, "المسمى الوظيفي", "Job Title", cancellationToken);
            await SeedValues("JobTitle", cancellationToken,
                ("Teacher", "معلم", "Teacher"), ("Administrator", "إداري", "Administrator"), ("Accountant", "محاسب", "Accountant"),
                ("HrOfficer", "موظف موارد بشرية", "HR Officer"), ("ItSupport", "دعم تقني", "IT Support"),
                ("Librarian", "أمين مكتبة", "Librarian"), ("Maintenance", "صيانة", "Maintenance"), ("Driver", "سائق", "Driver"));
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
