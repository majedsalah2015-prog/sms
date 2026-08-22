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
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sms.Web.Navigation;
using SharedResource = ERP2028.Web.Shared.Resources.SharedResource;

namespace Sms.Web.Tests
{
    /// <summary>
    /// Builds a real <see cref="ErpNavigationSource"/> for tests — the actual providers and the
    /// actual resource file, not a fake. A fake here would test the test: the whole value of reading
    /// the ERP's own navigation is that it is the ERP's, so a stub of it proves nothing about what a
    /// user will see.
    /// </summary>
    internal static class TestErpNavigation
    {
        /// <summary>
        /// The same providers <c>Startup.AddEmbeddedAccounting</c> registers, in the same set. A
        /// module hosted there but absent here would make these tests pass while its screens were
        /// unreachable from the menu, so the two lists are meant to be read together.
        /// </summary>
        public static readonly INavigationProvider[] Providers =
        {
            new OrganizationNavigationProvider(),
            new AccountingNavigationProvider(),
            new InventoryNavigationProvider(),
            new PurchasingNavigationProvider(),
            new SalesNavigationProvider(),
            new CashNavigationProvider(),
            new PartnersNavigationProvider(),
        };

        public static ErpNavigationSource Source() => new(new NavigationMenu(Providers), Localizer());

        /// <summary>An <see cref="ErpNavigationSource"/> with no modules behind it, for the tests that are not about the ERP.</summary>
        public static ErpNavigationSource EmptySource() =>
            new(new NavigationMenu(new INavigationProvider[0]), Localizer());

        public static IStringLocalizer<SharedResource> Localizer()
        {
            var factory = new ResourceManagerStringLocalizerFactory(
                Options.Create(new LocalizationOptions()), NullLoggerFactory.Instance);
            return new StringLocalizer<SharedResource>(factory);
        }

        /// <summary>A principal carrying exactly the ERP permissions named.</summary>
        public static ClaimsPrincipal Holding(params string[] permissions) =>
            new(new ClaimsIdentity(
                permissions.Select(p => new Claim(AppClaimTypes.Permission, p)), "Test"));

        /// <summary>Everything an administrator holds once the ERP catalogue is seeded and granted.</summary>
        public static ClaimsPrincipal Administrator() =>
            Holding(OrganizationPermissions.All
                .Concat(AccountingPermissions.All)
                .Concat(InventoryPermissions.All)
                .Concat(PurchasingPermissions.All)
                .Concat(SalesPermissions.All)
                .Concat(CashPermissions.All)
                .Concat(PartnersPermissions.All)
                .Select(p => p.Name)
                .ToArray());
    }
}
