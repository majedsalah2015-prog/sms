using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.GlExport;
using Sms.Application.Seeding;
using Sms.Domain.Payments;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Seeding
{
    /// <summary>
    /// Fills the E-503 mapping table by asking the attached ledger which account
    /// plays each accounting role, so a fresh tenant can generate its first
    /// journal batch without an administrator typing twenty account codes
    /// correctly first.
    /// <para>
    /// Does nothing when no <see cref="IGlAccountProvisioner"/> is registered —
    /// a deployment with no ledger fills the table by hand, which is the O3
    /// arrangement and still fully supported.
    /// </para>
    /// <para>
    /// <b>What it maps is what the journal builder can emit</b>: the fixed keys,
    /// one cash account per payment method, and one revenue account per fee
    /// category. Missing any one of them makes the whole period unexportable —
    /// <c>GenerateAsync</c> refuses with every unmapped key listed — so the
    /// mapping has to be complete, not merely started.
    /// </para>
    /// <para>
    /// Only ever adds. A mapping an accountant has repointed is left exactly as
    /// they set it: this seeds a starting position, it does not enforce one.
    /// </para>
    /// </summary>
    public class GlAccountMappingSeedContributor : ISeedContributor
    {
        private readonly AppDbContext _db;
        private readonly IGlExportService _glExport;
        private readonly IGlAccountProvisioner? _provisioner;

        public GlAccountMappingSeedContributor(AppDbContext db, IGlExportService glExport, IGlAccountProvisioner? provisioner = null)
        {
            _db = db;
            _glExport = glExport;
            _provisioner = provisioner;
        }

        public string Name => "GL account mapping (E-503 / Integration/01 §7.3)";

        // After the demo tenant (60) — the fee categories it creates are what the
        // per-category revenue mappings are built from.
        public int Order => 70;

        /// <summary>
        /// Which ledger role each payment method settles into. Cheques and
        /// post-dated cheques share one: both are a cheque the school holds and
        /// has not yet banked, and the difference between them is a date, not an
        /// accounting one.
        /// </summary>
        private static readonly IReadOnlyDictionary<PaymentMethod, GlAccountRole> MethodRoles = new Dictionary<PaymentMethod, GlAccountRole>
        {
            [PaymentMethod.Cash] = GlAccountRole.CashOnHand,
            [PaymentMethod.Card] = GlAccountRole.CardClearing,
            [PaymentMethod.BankTransfer] = GlAccountRole.BankAccount,
            [PaymentMethod.Cheque] = GlAccountRole.ChequesReceivable,
            [PaymentMethod.Pdc] = GlAccountRole.ChequesReceivable,
        };

        private static readonly IReadOnlyDictionary<string, GlAccountRole> FixedKeyRoles = new Dictionary<string, GlAccountRole>
        {
            [GlAccountKeys.Receivables] = GlAccountRole.StudentReceivables,
            [GlAccountKeys.VatOutput] = GlAccountRole.OutputVat,
            [GlAccountKeys.Discounts] = GlAccountRole.DiscountsAllowed,
            [GlAccountKeys.AdvancesReceived] = GlAccountRole.AdvancesFromPayers,
            [GlAccountKeys.WalletLiability] = GlAccountRole.WalletLiability,
            [GlAccountKeys.CafeteriaRevenue] = GlAccountRole.CafeteriaRevenue,
        };

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            if (_provisioner == null)
            {
                return;
            }

            var mapped = await _db.GlAccountMappings.Select(m => m.Key).ToListAsync(cancellationToken);
            var existing = new HashSet<string>(mapped);

            foreach (var (key, role) in FixedKeyRoles)
            {
                await DefineAsync(existing, key, role, name: null, cancellationToken);
            }

            foreach (var (method, role) in MethodRoles)
            {
                await DefineAsync(existing, GlAccountKeys.Cash(method.ToString()), role, name: null, cancellationToken);
            }

            // IgnoreQueryFilters: the soft-active filter hides a deactivated category, but its posted
            // charges keep the same revenue key forever and an unmapped key makes the period refuse to
            // export at all. A category is deactivated to stop new charges, not to strand old ones.
            var categories = await _db.FeeCategories.IgnoreQueryFilters()
                .Where(c => c.SchoolId == _db.CurrentSchoolId)
                .Select(c => new { c.Id, c.NameAr, c.NameEn, c.GlExportCode })
                .ToListAsync(cancellationToken);

            foreach (var category in categories)
            {
                var key = GlAccountKeys.Revenue(category.Id, category.GlExportCode);
                var accountName = string.IsNullOrWhiteSpace(category.NameAr) ? category.NameEn : category.NameAr;
                await DefineAsync(existing, key, GlAccountRole.FeeRevenue, accountName, cancellationToken);
            }
        }

        private async Task DefineAsync(HashSet<string> existing, string key, GlAccountRole role, string? name, CancellationToken cancellationToken)
        {
            if (existing.Contains(key))
            {
                return;
            }

            var code = await _provisioner!.ResolveAsync(role, name, cancellationToken);
            if (code == null)
            {
                // The ledger has no account for this role. Left unmapped on purpose: GenerateAsync then
                // names the missing key, which is a far better diagnosis than a mapping quietly pointing
                // at the wrong account.
                return;
            }

            var label = name ?? role.ToString();
            await _glExport.DefineMappingAsync(key, code, label, role.ToString(), cancellationToken);
            existing.Add(key);
        }
    }
}
