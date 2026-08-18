using System;
using System.Threading;
using System.Threading.Tasks;
using Sms.Application.Fees;
using Sms.Application.Lookups;
using Sms.Application.Seeding;
using Sms.Application.Setup;
using Sms.Domain.Lookups;

namespace Sms.Infrastructure.Seeding
{
    /// <summary>
    /// KSA-01 content pack v1 (BR-SET-004). Since E-101 the pack is a real
    /// <see cref="Sms.Domain.Setup.CountryPack"/> row (product tier) that
    /// binds VAT default, ID-type requirements (BR-GLB-003 — the E-010
    /// "IdType" codes), Hijri display default, audit-retention floor
    /// (BR-AUD-006) and the statutory report set; schools bind to it in the
    /// wizard's COUNTRY_PACK step. The "HolidayType" lookup below is the
    /// pack's reference-content half (S3/E-305).
    ///
    /// Only the two Gregorian-fixed KSA national holidays are named as
    /// content (National Day = Sep 23, Founding Day = Feb 22) — the
    /// religious holidays are Hijri-moving and this codebase's documented
    /// Hijri-accuracy gap (UmmAlQuraCalendar unavailable in net5.0) means
    /// seeding their dates would be guessing; the types exist, the dated
    /// instances are a per-year school concern (DemoSeedContributor).
    /// </summary>
    public class Ksa01ContentPackSeedContributor : ISeedContributor
    {
        public const string PackCode = "KSA-01";

        private readonly ILookupAdmin _lookups;
        private readonly ISystemSetupAdmin _setup;

        public Ksa01ContentPackSeedContributor(ILookupAdmin lookups, ISystemSetupAdmin setup)
        {
            _lookups = lookups;
            _setup = setup;
        }

        public string Name => "KSA-01 content pack v1 (BR-SET-004)";

        public int Order => 15;

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            await _setup.DefineCountryPackAsync(new CountryPackDefinition(
                Code: PackCode,
                NameAr: "المملكة العربية السعودية — الحزمة 01",
                NameEn: "Saudi Arabia — pack 01",
                CountryIsoCode: "SA",
                DefaultCurrencyCode: "SAR",
                DefaultTimeZoneId: "Arab Standard Time",
                DefaultVatRate: KsaVatRates.Standard,
                HijriDisplayDefault: true,
                RequiredIdTypeCodes: new[] { "NationalId", "Iqama", "Passport" },
                AuditRetentionYearsMinimum: 10,
                // doc/Modules/30 statutory set for KSA — codes as the report registry names them.
                StatutoryReportCodes: new[] { "RPT-STU-001", "RPT-ATT-001", "RPT-FEE-004", "RPT-VAT-001" },
                DefaultWorkingDays: new[] { DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday }),
                cancellationToken);

            await _lookups.DefineCategoryAsync("HolidayType", LookupCategoryTier.ProductSeeded, "نوع العطلة", "Holiday Type", cancellationToken);
            await SeedValues(cancellationToken,
                ("NationalDay", "اليوم الوطني", "National Day"),
                ("FoundingDay", "يوم التأسيس", "Founding Day"),
                ("EidAlFitr", "عيد الفطر", "Eid al-Fitr"),
                ("EidAlAdha", "عيد الأضحى", "Eid al-Adha"),
                ("MidYearBreak", "إجازة منتصف العام", "Mid-Year Break"));
        }

        private async Task SeedValues(CancellationToken cancellationToken, params (string Code, string Ar, string En)[] values)
        {
            for (var i = 0; i < values.Length; i++)
            {
                var (code, ar, en) = values[i];
                await _lookups.DefineValueAsync("HolidayType", code, ar, en, sortOrder: i + 1, cancellationToken);
            }
        }
    }
}
