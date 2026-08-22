using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using ERP2028.Application.Abstractions.Identity;
using ERP2028.Modules.Accounting.Contracts.Permissions;
using ERP2028.Modules.Accounting.Web.Navigation;
using ERP2028.Modules.Cash.Contracts.Permissions;
using ERP2028.Modules.Cash.Web.Navigation;
using ERP2028.Modules.Inventory.Contracts.Permissions;
using ERP2028.Modules.Inventory.Web.Navigation;
using ERP2028.Modules.Organization.Contracts.Permissions;
using ERP2028.Modules.Organization.Web.Navigation;
using ERP2028.Modules.Partners.Contracts.Permissions;
using ERP2028.Modules.Partners.Web.Navigation;
using ERP2028.Modules.Purchasing.Contracts.Permissions;
using ERP2028.Modules.Purchasing.Web.Navigation;
using ERP2028.Modules.Sales.Contracts.Permissions;
using ERP2028.Modules.Sales.Web.Navigation;
using ERP2028.Web.Shared.Navigation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sms.Web.Navigation;
using Xunit;
// Both systems call it a NavItem, which is exactly the collision the sidebar exists to resolve;
// in this file the unqualified name is always this system's.
using NavItem = Sms.Web.Navigation.NavItem;
using SharedResource = ERP2028.Web.Shared.Resources.SharedResource;

namespace Sms.Web.Tests
{
    /// <summary>
    /// The seam that puts every embedded ERP screen under this system's accounting section
    /// (docs/Integration/01-Embedded-Accounting-Plan.md §7).
    /// <para>
    /// These tests exist because the sidebar is the only place the two systems' navigation models
    /// meet, and every way it can go wrong is silent: a permission-gated entry shown to someone who
    /// cannot open it, a link generated into the wrong area, a whole module missing because its
    /// provider was never registered. None of those throws — they just render a menu that lies.
    /// </para>
    /// </summary>
    public class ErpNavigationSourceTests
    {
        // The providers, the localizer and the principals live in TestErpNavigation, shared with the
        // workspace tests: both ask the same question of the same seam.
        private static ErpNavigationSource Source() => TestErpNavigation.Source();

        private static ClaimsPrincipal AdministratorHolding(params string[] permissions) =>
            TestErpNavigation.Holding(permissions);

        private static ClaimsPrincipal Administrator() => TestErpNavigation.Administrator();

        [Theory]
        [InlineData("erp-organization")]
        [InlineData("erp-accounting")]
        [InlineData("erp-inventory")]
        [InlineData("erp-purchasing")]
        [InlineData("erp-sales")]
        [InlineData("erp-pos")]
        [InlineData("erp-cash")]
        [InlineData("erp-partners")]
        public void Every_hosted_module_contributes_a_group(string key)
        {
            var groups = Source().BuildGroupsFor(Administrator());

            var group = Assert.Single(groups, g => g.Key == key);
            Assert.NotEmpty(group.Items);
        }

        /// <summary>
        /// The screens the owner asked for by name: stores, selling, buying, the till, and the money
        /// in and out. Named individually rather than counted, because a count passes while pointing
        /// at the wrong screen.
        /// </summary>
        [Theory]
        [InlineData("erp-inventory", "Inventory", "Warehouses", "Index")]
        [InlineData("erp-inventory", "Inventory", "Items", "Index")]
        [InlineData("erp-inventory", "Inventory", "StockDocuments", "Index")]
        [InlineData("erp-sales", "Sales", "Customers", "Index")]
        [InlineData("erp-sales", "Sales", "SalesInvoices", "Index")]
        [InlineData("erp-purchasing", "Purchasing", "Vendors", "Index")]
        [InlineData("erp-purchasing", "Purchasing", "VendorBills", "Index")]
        [InlineData("erp-cash", "Cash", "ReceiptVouchers", "Index")]
        [InlineData("erp-cash", "Cash", "PaymentVouchers", "Index")]
        [InlineData("erp-cash", "Cash", "BankAccounts", "Index")]
        [InlineData("erp-pos", "POS", "Till", "Index")]
        [InlineData("erp-pos", "POS", "PosOrders", "Index")]
        public void The_requested_screens_are_present(string groupKey, string area, string controller, string action)
        {
            var groups = Source().BuildGroupsFor(Administrator());

            var group = Assert.Single(groups, g => g.Key == groupKey);
            Assert.Contains(group.Items, i =>
                i.Area == area && i.Controller == controller && i.Action == action);
        }

        /// <summary>
        /// Every entry states its area, empty string included. <c>Url.Action</c> inherits the current
        /// request's area when the caller says nothing, so a silent entry would generate a link into
        /// whichever ERP area happened to be open.
        /// </summary>
        [Fact]
        public void Every_entry_states_its_area_or_carries_a_url()
        {
            var groups = Source().BuildGroupsFor(Administrator());

            foreach (var item in groups.SelectMany(g => g.Items))
            {
                Assert.True(
                    item.Area != null || item.Url != null,
                    $"'{item.TitleEn}' states neither an area nor a URL.");
            }
        }

        /// <summary>
        /// The two manual voucher entries are one screen reached with different query strings, which
        /// controller/action cannot express. They must survive as URLs rather than being dropped or
        /// throwing — the failure this test was written after.
        /// </summary>
        [Fact]
        public void An_entry_the_ERP_addresses_by_url_keeps_its_url()
        {
            var groups = Source().BuildGroupsFor(Administrator());

            var accounting = Assert.Single(groups, g => g.Key == "erp-accounting");
            Assert.Contains(accounting.Items, i => i.Url == "/Accounting/ManualVouchers/Create?side=Debit");
            Assert.Contains(accounting.Items, i => i.Url == "/Accounting/ManualVouchers/Create?side=Credit");
        }

        [Fact]
        public void A_user_holding_no_ERP_permission_gets_no_groups()
        {
            var groups = Source().BuildGroupsFor(AdministratorHolding());

            Assert.Empty(groups);
        }

        /// <summary>
        /// One granted permission brings its own screens and nothing else — the property that makes
        /// this menu honest about what the signed-in user can actually open.
        /// </summary>
        [Fact]
        public void A_user_holding_one_permission_gets_only_what_it_opens()
        {
            var groups = Source().BuildGroupsFor(AdministratorHolding(InventoryPermissions.WarehousesView));

            var group = Assert.Single(groups);
            Assert.Equal("erp-inventory", group.Key);
            var item = Assert.Single(group.Items);
            Assert.Equal("Warehouses", item.Controller);
        }

        [Fact]
        public void Titles_are_carried_in_both_languages()
        {
            var groups = Source().BuildGroupsFor(Administrator());

            var inventory = Assert.Single(groups, g => g.Key == "erp-inventory");
            Assert.Equal("Inventory", inventory.TitleEn);
            Assert.Equal("المخزون", inventory.TitleAr);

            var till = Assert.Single(groups, g => g.Key == "erp-pos").Items.First(i => i.Controller == "Till");
            Assert.Equal("Cashier", till.TitleEn);
            Assert.Equal("الكاشير", till.TitleAr);
        }

        /// <summary>
        /// The whole point of the change: the school's sidebar gains one section, not eight, and
        /// every ERP screen is inside it.
        /// </summary>
        [Fact]
        public void The_groups_are_nested_inside_one_accounting_section()
        {
            var erpGroups = Source().BuildGroupsFor(Administrator());

            var sidebar = ModuleCatalog.BuildSidebar(_ => false, _ => false, canExportToLedger: true, erpGroups);

            var accounting = Assert.Single(sidebar, n => n.Key == "accounting");
            Assert.Equal("المحاسبة", accounting.TitleAr);
            // This system's own GL export, then one sub-group per ERP module.
            Assert.Equal("acc-glexport", accounting.Items[0].Key);
            Assert.Equal(erpGroups.Count, accounting.Items.Count - 1);
            Assert.All(accounting.Items.Skip(1), g => Assert.True(g.HasChildren));
            Assert.DoesNotContain(sidebar, n => erpGroups.Any(g => g.Key == n.Key));
        }

        /// <summary>
        /// No accounting section at all for a user with no accounting rights — rather than an empty
        /// one, or one holding only the GL export they also cannot open.
        /// </summary>
        [Fact]
        public void The_accounting_section_disappears_when_there_is_nothing_in_it()
        {
            var sidebar = ModuleCatalog.BuildSidebar(
                _ => false, _ => false, canExportToLedger: false, new List<NavItem>());

            Assert.DoesNotContain(sidebar, n => n.Key == "accounting");
        }

        /// <summary>
        /// The ERP publishes several controllers whose names this system also uses, or which repeat
        /// across its own areas. Without the area in the comparison, opening one would highlight all
        /// of them.
        /// </summary>
        [Fact]
        public void The_highlighted_entry_is_told_apart_by_area()
        {
            var posReports = new NavItem("pos-reports", "Reports", "التقارير", "bi-graph-up", "Reports", "Index", area: "POS");
            var schoolReports = new NavItem("RPT", "Reports", "التقارير", "bi-file", "Reports", "Index");

            var onPosReports = new RouteValueDictionary
            {
                ["area"] = "POS", ["controller"] = "Reports", ["action"] = "Index",
            };

            Assert.True(NavLinks.IsActive(posReports, onPosReports));
            Assert.False(NavLinks.IsActive(schoolReports, onPosReports));
        }
    }
}
