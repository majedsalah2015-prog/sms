using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ERP2028.Modules.Accounting.Contracts.ChartOfAccounts;
using Sms.Application.GlExport;

namespace Sms.Erp.Bridge.GlPosting
{
    /// <summary>
    /// Places each school accounting role in ERP 2028's chart of accounts.
    /// <para>
    /// Two kinds of answer. Most roles are already in the ERP's seeded chart and
    /// are simply named here — cash, bank, card clearing, cheques, customer
    /// advances, discount allowed. The handful the chart has no equivalent for
    /// are created, as children of the appropriate group through
    /// <see cref="IChartOfAccountsProvisioning"/>, which is Accounting's own
    /// sanctioned write path and enforces every chart invariant on the way in.
    /// This system never writes <c>acc.Accounts</c>.
    /// </para>
    /// <para>
    /// Reusing a standard account is not laziness: <c>110606</c> exists precisely
    /// for card takings awaiting settlement, and inventing a school-specific twin
    /// of it would split one real balance across two accounts for no gain.
    /// </para>
    /// </summary>
    public sealed class ErpGlAccountProvisioner : IGlAccountProvisioner
    {
        // Groups (non-postable parents) the created accounts hang under.
        private const string CurrentAssetsGroup = "11";
        private const string CurrentLiabilitiesGroup = "21";
        private const string OperatingRevenueGroup = "41";
        private const string OperatingExpenseGroup = "52";

        /// <summary>
        /// Roles the ERP's default chart already answers. Codes from
        /// <c>DefaultChartOfAccounts</c>; each is an active postable leaf.
        /// </summary>
        private static readonly IReadOnlyDictionary<GlAccountRole, string> Standard = new Dictionary<GlAccountRole, string>
        {
            [GlAccountRole.CashOnHand] = "1101",           // النقدية بالصندوق
            [GlAccountRole.BankAccount] = "110201",        // الحساب البنكي الرئيسي
            [GlAccountRole.CardClearing] = "110606",       // مقبوضات إلكترونية تحت التسوية
            [GlAccountRole.ChequesReceivable] = "1105",    // أوراق القبض
            [GlAccountRole.AdvancesFromPayers] = "210601", // دفعات مقدمة من العملاء
            [GlAccountRole.WalletLiability] = "210602",    // أمانات وتأمينات الغير
            [GlAccountRole.DiscountsAllowed] = "4104",     // الخصم المسموح به — contra-revenue, debit in use
            [GlAccountRole.BadDebtExpense] = "5404",       // ديون معدومة ومشكوك في تحصيلها
        };

        private readonly IChartOfAccountsProvisioning _provisioning;
        private readonly IChartOfAccountsDirectory _directory;

        public ErpGlAccountProvisioner(IChartOfAccountsProvisioning provisioning, IChartOfAccountsDirectory directory)
        {
            _provisioning = provisioning;
            _directory = directory;
        }

        public async Task<GlAccountRef?> ResolveAsync(GlAccountRole role, string? name = null, CancellationToken cancellationToken = default)
        {
            if (Standard.TryGetValue(role, out var code))
            {
                // Confirmed rather than assumed: a chart is an administrator's to edit, and a code that
                // has been deactivated or turned into a group must not be handed out as a mapping that
                // will only fail at the first posting.
                var account = await _directory.FindPostableByCodeAsync(code, cancellationToken);
                return account == null ? null : new GlAccountRef(account.Code, account.Name);
            }

            var (parent, defaultName) = role switch
            {
                GlAccountRole.StudentReceivables => (CurrentAssetsGroup, "ذمم الطلبة"),
                GlAccountRole.OutputVat => (CurrentLiabilitiesGroup, "ضريبة القيمة المضافة - المخرجات"),
                GlAccountRole.CafeteriaRevenue => (OperatingRevenueGroup, "إيرادات المقصف"),
                GlAccountRole.StoreRevenue => (OperatingRevenueGroup, "إيرادات المتجر المدرسي"),
                GlAccountRole.FeeRevenue => (OperatingRevenueGroup, "إيرادات الرسوم الدراسية"),
                GlAccountRole.CashOverShort => (OperatingExpenseGroup, "فروق الصندوق"),
                GlAccountRole.WalletAdjustments => (OperatingExpenseGroup, "تسويات محافظ المقصف"),
                _ => (null, null),
            };

            if (parent == null)
            {
                return null;
            }

            // Idempotent by (parent, name) on the ERP's side, so re-seeding returns the same account
            // rather than allocating another code beneath the group.
            var created = await _provisioning.CreateChildAsync(parent, name ?? defaultName!, cancellationToken);
            return created.IsSuccess ? new GlAccountRef(created.Value.Code, created.Value.Name) : null;
        }
    }
}
