using System.Collections.Generic;
using System.Linq;
using ERP2028.Modules.Accounting.Contracts.Permissions;
using ERP2028.Modules.Cash.Contracts.Permissions;
using ERP2028.Modules.Inventory.Contracts.Permissions;
using ERP2028.Modules.Organization.Contracts.Permissions;
using ERP2028.Modules.Partners.Contracts.Permissions;
using ERP2028.Modules.Purchasing.Contracts.Permissions;
using ERP2028.Modules.Sales.Contracts.Permissions;
using Sms.Application.Security;

namespace Sms.Erp.Bridge.Identity
{
    /// <summary>
    /// Declares the embedded ERP modules' permissions to this system's own
    /// catalogue, so an administrator grants access to the accounting screens
    /// through the ordinary role screen rather than a second, parallel one.
    /// <para>
    /// The names come straight from the modules' <c>.Contracts</c> assemblies —
    /// the same constants the modules' <c>[HasPermission]</c> attributes name — so
    /// a permission added upstream appears here on the next submodule bump with
    /// nothing to maintain. That is the whole reason the names are carried
    /// verbatim instead of mapped.
    /// </para>
    /// <para>
    /// Only <c>SYSADMIN</c> is granted by default. Who in a school may post to the
    /// ledger is a decision for that school, not for this file, and a broader
    /// default would hand out on first start what nobody chose.
    /// </para>
    /// </summary>
    public sealed class ErpPermissionCatalog : IExternalPermissionCatalog
    {
        /// <summary>
        /// Reserved: no module in doc 06 uses this code, so these rows can never be
        /// confused with a school module's own permissions — in a query, in an audit
        /// trail, or on the role screen.
        /// </summary>
        public const string ErpModuleCode = "ERP";

        public string ModuleCode => ErpModuleCode;

        /// <summary>
        /// Every embedded module's permissions, in the order the modules are registered in
        /// <c>Startup.AddEmbeddedAccounting</c>. The list must name each module hosted there and
        /// nothing else: a module registered without its permissions here has screens that only
        /// SYSADMIN can ever reach, and a module named here without being registered offers an
        /// administrator a grant that leads to a 404.
        /// </summary>
        public IReadOnlyList<string> PermissionNames { get; } =
            OrganizationPermissions.All
                .Concat(AccountingPermissions.All)
                .Concat(InventoryPermissions.All)
                .Concat(PurchasingPermissions.All)
                .Concat(SalesPermissions.All)
                .Concat(CashPermissions.All)
                .Concat(PartnersPermissions.All)
                .Select(p => p.Name)
                .ToList();

        public IReadOnlyList<string> DefaultGrantRoleCodes { get; } = new[] { "SYSADMIN" };
    }
}
