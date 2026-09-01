using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ERP2028.Modules.Accounting.Contracts.ChartOfAccounts;
using Sms.Application.GlExport;
using Sms.Erp.Bridge.GlPosting;
using Sms.Web.Models;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// docs/Integration/00-ERP-SMS-Integration-Analysis.md names the free-text GL account code as
    /// the interface's one remaining gap: nothing checked <c>FeeCategory.GlExportCode</c> against
    /// any chart, so a transposed digit survived until a posting failed — or until it posted to a
    /// real but wrong account, which fails silently and is worse.
    /// <para>
    /// The fix has two halves and both are tested here: the bridge must publish the ledger's chart
    /// faithfully, and the screen must be able to say which account a stored code names — including
    /// when it names none.
    /// </para>
    /// </summary>
    public class GlAccountPickerTests
    {
        private sealed class FakeChart : IChartOfAccountsDirectory
        {
            private readonly IReadOnlyList<AccountSummary> _accounts;

            public FakeChart(params AccountSummary[] accounts) => _accounts = accounts;

            public Task<IReadOnlyList<AccountSummary>> GetPostableAccountsAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(_accounts);

            public Task<AccountSummary?> FindPostableByCodeAsync(string code, CancellationToken cancellationToken = default) =>
                Task.FromResult(_accounts.FirstOrDefault(a => a.Code == code));
        }

        private static FeeCategoryCatalogViewModel Catalog(params GlAccountOption[] accounts) =>
            new() { GlAccounts = accounts };

        // ---------------------------------------------------------------- the bridge

        [Fact]
        public async Task Directory_translates_every_nature_value_for_value()
        {
            var chart = new FakeChart(
                new AccountSummary("1101", "النقدية بالصندوق", AccountNature.Asset),
                new AccountSummary("2106", "دفعات مقدمة", AccountNature.Liability),
                new AccountSummary("3101", "رأس المال", AccountNature.Equity),
                new AccountSummary("4101", "إيرادات الرسوم", AccountNature.Revenue),
                new AccountSummary("5404", "ديون معدومة", AccountNature.Expense));

            var accounts = await new ErpGlAccountDirectory(chart).GetPostableAccountsAsync();

            Assert.Equal(
                new[] { GlAccountNature.Asset, GlAccountNature.Liability, GlAccountNature.Equity, GlAccountNature.Revenue, GlAccountNature.Expense },
                accounts.Select(a => a.Nature));
        }

        [Fact]
        public async Task Directory_keeps_an_unclassified_account_in_the_list()
        {
            // The safe failure: a classification this system does not know must not remove a real
            // postable account from the picker — it is still an account the ledger will accept.
            var chart = new FakeChart(new AccountSummary("4109", "إيرادات أخرى", AccountNature.Unspecified));

            var accounts = await new ErpGlAccountDirectory(chart).GetPostableAccountsAsync();

            var only = Assert.Single(accounts);
            Assert.Equal("4109", only.Code);
            Assert.Equal(GlAccountNature.Unspecified, only.Nature);
        }

        [Fact]
        public async Task Directory_orders_by_code_because_the_contract_promises_no_order()
        {
            var chart = new FakeChart(
                new AccountSummary("410103", "ج", AccountNature.Revenue),
                new AccountSummary("1101", "أ", AccountNature.Asset),
                new AccountSummary("210601", "ب", AccountNature.Liability));

            var accounts = await new ErpGlAccountDirectory(chart).GetPostableAccountsAsync();

            Assert.Equal(new[] { "1101", "210601", "410103" }, accounts.Select(a => a.Code));
        }

        [Fact]
        public async Task Directory_carries_the_name_so_a_screen_can_show_more_than_a_number()
        {
            var chart = new FakeChart(new AccountSummary("410103", "إيرادات الرسوم الدراسية", AccountNature.Revenue));

            var accounts = await new ErpGlAccountDirectory(chart).GetPostableAccountsAsync();

            Assert.Equal("إيرادات الرسوم الدراسية", Assert.Single(accounts).Name);
        }

        // ---------------------------------------------------------------- the screen

        [Fact]
        public void No_ledger_attached_leaves_the_field_free_text()
        {
            // A standalone school system — no ERP bridge registered — must keep working. Empty is
            // "no ledger", never "no accounts", so nothing may be flagged as missing from a chart
            // this deployment cannot see.
            var m = Catalog();

            Assert.False(m.HasLedger);
            Assert.Null(m.FindAccount("410103"));
        }

        [Fact]
        public void A_code_in_the_chart_resolves_to_its_account()
        {
            var m = Catalog(new GlAccountOption("410103", "إيرادات الرسوم الدراسية", GlAccountNature.Revenue));

            Assert.True(m.HasLedger);
            Assert.Equal("إيرادات الرسوم الدراسية", m.FindAccount("410103")!.Name);
        }

        [Fact]
        public void A_code_the_chart_does_not_have_resolves_to_nothing()
        {
            // The transposed digit this whole change exists to surface: 410130 is not 410103.
            var m = Catalog(new GlAccountOption("410103", "إيرادات الرسوم الدراسية", GlAccountNature.Revenue));

            Assert.Null(m.FindAccount("410130"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void An_unset_code_is_not_a_wrong_one(string? code)
        {
            // The GL code is optional. Blank must resolve to null so the screen shows nothing at
            // all, rather than warning a school that has simply not mapped this category yet.
            var m = Catalog(new GlAccountOption("410103", "إيرادات الرسوم الدراسية", GlAccountNature.Revenue));

            Assert.Null(m.FindAccount(code));
        }

        [Fact]
        public void A_stored_code_resolves_despite_stray_whitespace()
        {
            var m = Catalog(new GlAccountOption("410103", "إيرادات الرسوم الدراسية", GlAccountNature.Revenue));

            Assert.NotNull(m.FindAccount("  410103 "));
        }

        [Fact]
        public void Account_nature_is_labelled_in_both_languages()
        {
            Assert.Equal("Revenue", FinanceLabels.AccountNature(GlAccountNature.Revenue, ar: false));
            Assert.Equal("إيرادات", FinanceLabels.AccountNature(GlAccountNature.Revenue, ar: true));
            Assert.Equal("Liability", FinanceLabels.AccountNature(GlAccountNature.Liability, ar: false));
            Assert.Equal("التزامات", FinanceLabels.AccountNature(GlAccountNature.Liability, ar: true));
        }

        [Fact]
        public void An_unclassified_account_is_labelled_with_a_dash_not_an_enum_name()
        {
            // Never enum.ToString() on screen, in either language (CLAUDE.md).
            Assert.Equal("—", FinanceLabels.AccountNature(GlAccountNature.Unspecified, ar: false));
            Assert.Equal("—", FinanceLabels.AccountNature(GlAccountNature.Unspecified, ar: true));
        }
    }
}
