using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Seeding;
using Sms.Domain.Common;
using Sms.Domain.Geography;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Seeding
{
    /// <summary>
    /// The residence hierarchy a student address is recorded in: governorate →
    /// area → neighbourhood.
    /// <para>
    /// Seeded rather than typed, because the top level is not the school's data
    /// to invent — the codes below are the official ones, and a school typing
    /// them by hand would produce five spellings of "Deir Al-Balah" across five
    /// deployments and no way to report across them.
    /// </para>
    /// <para>
    /// Idempotent on <c>Code</c>, and it only ever adds: a name an administrator
    /// corrected, or a level they added beneath, survives every later run. The
    /// seeder establishes a starting point, it does not own the table.
    /// </para>
    /// </summary>
    public class GeographySeedContributor : ISeedContributor
    {
        private readonly AppDbContext _db;

        public GeographySeedContributor(AppDbContext db)
        {
            _db = db;
        }

        public string Name => "Residence hierarchy (governorates and localities)";

        // With the other reference data, before anything that could point at a neighbourhood.
        public int Order => 15;

        /// <summary>
        /// The five governorates of the Gaza Strip, with their official numeric
        /// codes. Ordered north to south, which is how they are always listed.
        /// </summary>
        private static readonly (string Code, string Ar, string En)[] Governorates =
        {
            ("55", "شمال غزة", "North Gaza"),
            ("60", "غزة", "Gaza"),
            ("65", "دير البلح", "Deir Al-Balah"),
            ("70", "خان يونس", "Khan Yunis"),
            ("75", "رفح", "Rafah"),
        };

        /// <summary>
        /// The PCBS localities of each governorate, as the middle level.
        /// <para>
        /// A camp is listed as its own locality beside the town it adjoins —
        /// Jabalia and Jabalia Camp, Deir Al-Balah and Deir Al-Balah Camp — because
        /// that is how PCBS records them and, more to the point, how the school
        /// needs them: the two have different services, different agencies and
        /// different fee-exemption arguments, so folding a camp into its town
        /// would erase a distinction the record exists to carry.
        /// </para>
        /// <para>
        /// Codes are the governorate's, then a two-digit sequence. Locally
        /// assigned, and stated as such: PCBS locality codes were not to hand, and
        /// inventing a number that looks official is worse than an obviously local
        /// one. They are unique per governorate, which is all the schema asks.
        /// </para>
        /// </summary>
        private static readonly (string GovernorateCode, string Ar, string En)[] Areas =
        {
            ("55", "جباليا", "Jabalia"),
            ("55", "جباليا البلد", "Jabalia Al-Balad"),
            ("55", "مخيم جباليا", "Jabalia Camp"),
            ("55", "بيت لاهيا", "Beit Lahia"),
            ("55", "بيت حانون", "Beit Hanoun"),
            ("55", "أم النصر", "Umm Al-Nasr"),

            ("60", "مدينة غزة", "Gaza City"),
            ("60", "مخيم الشاطئ", "Al-Shati Camp"),
            ("60", "مدينة الزهراء", "Al-Zahra City"),
            ("60", "المغراقة", "Al-Mughraqa"),
            ("60", "جحر الديك", "Juhr Al-Dik"),

            ("65", "دير البلح", "Deir Al-Balah"),
            ("65", "مخيم دير البلح", "Deir Al-Balah Camp"),
            ("65", "النصيرات", "Al-Nuseirat"),
            ("65", "مخيم النصيرات", "Al-Nuseirat Camp"),
            ("65", "البريج", "Al-Bureij"),
            ("65", "مخيم البريج", "Al-Bureij Camp"),
            ("65", "المغازي", "Al-Maghazi"),
            ("65", "مخيم المغازي", "Al-Maghazi Camp"),
            ("65", "الزوايدة", "Al-Zawaida"),
            ("65", "وادي السلقا", "Wadi Al-Salqa"),
            ("65", "المصدر", "Al-Musaddar"),

            ("70", "خان يونس", "Khan Yunis"),
            ("70", "مخيم خان يونس", "Khan Yunis Camp"),
            ("70", "القرارة", "Al-Qarara"),
            ("70", "بني سهيلا", "Bani Suheila"),
            ("70", "عبسان الجديدة", "Abasan Al-Jadida"),
            ("70", "عبسان الكبيرة", "Abasan Al-Kabira"),
            ("70", "خزاعة", "Khuzaa"),
            ("70", "الفخاري", "Al-Fukhari"),

            ("75", "رفح", "Rafah"),
            ("75", "مخيم رفح", "Rafah Camp"),
            ("75", "النصر", "Al-Nasr"),
            ("75", "الشوكة", "Al-Shouka"),
        };

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            var existing = await _db.Governorates.IgnoreQueryFilters()
                .Where(g => g.SchoolId == _db.CurrentSchoolId)
                .Select(g => g.Code)
                .ToListAsync(cancellationToken);
            var known = new HashSet<string>(existing);

            var order = 0;
            var added = new List<Governorate>();
            foreach (var (code, ar, en) in Governorates)
            {
                order += 10;
                if (known.Contains(code))
                {
                    continue;
                }

                added.Add(new Governorate
                {
                    Code = code,
                    Name = new LocalizedName(ar, en),
                    SortOrder = order,
                });
            }

            if (added.Count > 0)
            {
                _db.Governorates.AddRange(added);
                await _db.SaveChangesAsync(cancellationToken);
            }

            await SeedAreasAsync(cancellationToken);
        }

        private async Task SeedAreasAsync(CancellationToken cancellationToken)
        {
            // Re-read rather than reuse what was just added: on a re-run the governorates already
            // existed and none were added, so the list above would be empty while the areas below still
            // need their parents.
            var governorates = await _db.Governorates.IgnoreQueryFilters()
                .Where(g => g.SchoolId == _db.CurrentSchoolId)
                .Select(g => new { g.Id, g.Code })
                .ToDictionaryAsync(g => g.Code, g => g.Id, cancellationToken);

            var governorateIds = governorates.Values.ToList();
            var existing = await _db.ResidenceAreas.IgnoreQueryFilters()
                .Where(a => governorateIds.Contains(a.GovernorateId))
                .Select(a => a.Code)
                .ToListAsync(cancellationToken);
            var known = new HashSet<string>(existing);

            var added = new List<ResidenceArea>();
            var sequenceByGovernorate = new Dictionary<string, int>();
            foreach (var (governorateCode, ar, en) in Areas)
            {
                if (!governorates.TryGetValue(governorateCode, out var governorateId))
                {
                    // A governorate an administrator removed. Its localities go with it rather than
                    // being re-created orphaned under a parent that no longer exists.
                    continue;
                }

                var sequence = sequenceByGovernorate.TryGetValue(governorateCode, out var n) ? n + 1 : 1;
                sequenceByGovernorate[governorateCode] = sequence;

                var code = $"{governorateCode}-{sequence:D2}";
                if (known.Contains(code))
                {
                    continue;
                }

                added.Add(new ResidenceArea
                {
                    GovernorateId = governorateId,
                    Code = code,
                    Name = new LocalizedName(ar, en),
                    SortOrder = sequence * 10,
                });
            }

            if (added.Count == 0)
            {
                return;
            }

            _db.ResidenceAreas.AddRange(added);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
