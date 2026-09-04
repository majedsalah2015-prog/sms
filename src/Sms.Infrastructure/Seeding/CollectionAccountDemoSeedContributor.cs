using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Payments;
using Sms.Application.Seeding;
using Sms.Domain.Payments;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Seeding
{
    /// <summary>
    /// One account of each kind behind the cashier's destination picker, so
    /// doc/Modules/21 §3 BR-PAY-002's "which pot did this money join" has an
    /// answer on a demo tenant.
    /// <para>
    /// A fixture, not content. A real school defines its own at
    /// <c>/payments/accounts</c>, and these are not a starting position anyone
    /// should collect into — which is why the bank row carries the published
    /// IBAN test value rather than anything a transfer could reach.
    /// </para>
    /// <para>
    /// Separate from <c>DemoSeedContributor</c>, which returns early once a
    /// school exists. These two accounts lived at the tail of that method and
    /// were therefore unreachable on every database provisioned before the
    /// catalogue existed: the cashier screen offered "— none —" and nothing
    /// else, and the "الحساب المحوَّل إليه" picker on <c>/payments</c> stayed
    /// empty for good. Idempotent on <see cref="CollectionAccount.Code"/> here,
    /// so it fills those databases on the next seeder run and adds nothing to
    /// one that already has them.
    /// </para>
    /// </summary>
    public class CollectionAccountDemoSeedContributor : ISeedContributor
    {
        private readonly AppDbContext _db;
        private readonly ICollectionAccountAdmin _accounts;

        public CollectionAccountDemoSeedContributor(AppDbContext db, ICollectionAccountAdmin accounts)
        {
            _db = db;
            _accounts = accounts;
        }

        public string Name => "Collection accounts (doc/Modules/21 §3 BR-PAY-002)";

        // After DemoSeedContributor (50), which creates the school these are scoped to. Ordered
        // before it, this would guard on "no school yet" and write nothing on a fresh database —
        // the silent no-op SeedOrderTests exists to catch.
        public int Order => 58;

        /// <summary>
        /// One of each kind, because <c>CollectionAccountSelector.KindFor</c>
        /// sends cash to a cash box and every other method to a bank account:
        /// with only one of them defined, half the payment methods still have
        /// nothing to point at.
        /// </summary>
        private static readonly (string Code, string NameAr, string NameEn, CollectionAccountKind Kind, string? BankName, string? AccountNo, string? Iban)[] Catalogue =
        {
            ("SAFE-MAIN", "الصندوق الرئيسي", "Main cash box", CollectionAccountKind.CashBox, null, null, null),
            ("BANK-MAIN", "الحساب الجاري الرئيسي", "Main current account", CollectionAccountKind.Bank,
                "مصرف الراجحي", "608010167519", "SA0380000000608010167519"),
        };

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            if (!await _db.Schools.AnyAsync(cancellationToken))
            {
                return;
            }

            // IgnoreQueryFilters: a code stays taken by a retired account (CollectionAccountAdmin
            // guards on the unfiltered set), so seeding past one would only earn a duplicate-code
            // refusal. A school that retired the demo account meant to.
            var existing = await _db.CollectionAccounts.IgnoreQueryFilters().AsNoTracking()
                .Where(a => a.SchoolId == _db.CurrentSchoolId)
                .Select(a => new { a.Code, a.Kind, a.IsDefault })
                .ToListAsync(cancellationToken);
            var have = existing.Select(a => a.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var (code, nameAr, nameEn, kind, bankName, accountNo, iban) in Catalogue)
            {
                if (have.Contains(code))
                {
                    continue;
                }

                // Default only into an empty seat. ICollectionAccountAdmin.DefineAsync clears the
                // other default of the same kind, so passing true unconditionally would have a
                // seeder re-point the cashier's pre-selection at demo data every run — quietly,
                // and over a choice the school made on purpose.
                var kindHasDefault = existing.Any(a => a.Kind == kind && a.IsDefault);

                await _accounts.DefineAsync(
                    code, nameAr, nameEn, kind,
                    bankLookupId: null, bankName: bankName, accountNo: accountNo, iban: iban,
                    glExportCode: null, displayOrder: 1, isDefault: !kindHasDefault, cancellationToken: cancellationToken);
            }
        }
    }
}
