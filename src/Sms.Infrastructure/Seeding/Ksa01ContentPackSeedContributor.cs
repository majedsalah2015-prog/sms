using System.Threading;
using System.Threading.Tasks;
using Sms.Application.Lookups;
using Sms.Application.Seeding;
using Sms.Domain.Lookups;

namespace Sms.Infrastructure.Seeding
{
    /// <summary>
    /// S3/E-305 KSA-01 content pack v1 (BR-SET-004). A real `CountryPack`
    /// entity that binds VAT defaults/ID-type requirements/Hijri
    /// default/statutory report set doesn't exist yet — E-101 (System
    /// Setup + wizard) was never started — so this seeds the concrete
    /// reference data a KSA pilot school needs today: a "HolidayType"
    /// lookup (product-tier content, same shape as E-010's
    /// RelationshipType/JobTitle) and the standard VAT rate (as
    /// <see cref="Sms.Application.Fees.KsaVatRates"/>, a plain constant
    /// rather than a database row — there's no live-configurable VAT
    /// setting to seed into yet). BR-GLB-003's KSA ID-type requirement
    /// (National ID / Iqama / Passport) is already satisfied by E-010's
    /// "IdType" lookup — not re-seeded here.
    ///
    /// Only the two Gregorian-fixed KSA national holidays are named as
    /// content (National Day = Sep 23, Founding Day = Feb 22) — the
    /// religious holidays (Eid al-Fitr/Eid al-Adha) are Hijri-moving and
    /// this codebase's own documented Hijri-accuracy gap
    /// (UmmAlQuraCalendar unavailable in net5.0, ±1 day risk with the
    /// tabular HijriCalendar fallback) means seeding their dates here
    /// would be guessing, not content — deferred, needs a per-year
    /// ministry calendar confirmation instead. Actual CalendarEvent rows
    /// for a specific academic year are a school/year-instance concern
    /// (see DemoSeedContributor), not product-tier content — this
    /// contributor only seeds the holiday *type* catalog.
    /// </summary>
    public class Ksa01ContentPackSeedContributor : ISeedContributor
    {
        private readonly ILookupAdmin _lookups;

        public Ksa01ContentPackSeedContributor(ILookupAdmin lookups)
        {
            _lookups = lookups;
        }

        public string Name => "KSA-01 content pack v1 (BR-SET-004)";

        public int Order => 15;

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
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
