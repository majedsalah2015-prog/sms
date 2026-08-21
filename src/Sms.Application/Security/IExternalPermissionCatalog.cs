using System.Collections.Generic;

namespace Sms.Application.Security
{
    /// <summary>
    /// Declares the permissions of a subsystem hosted inside this application but
    /// owning its own authorization vocabulary — today the embedded ERP accounting
    /// modules (docs/Integration/01-Embedded-Accounting-Plan.md §5.1).
    /// <para>
    /// The problem this solves: a hosted subsystem checks permissions by a flat
    /// name of its own (<c>Accounting.Accounts.View</c>), while this system models
    /// a grant as (module, screen, verb). Translating between the two would mean
    /// maintaining a mapping that rots the moment either side adds a screen. So
    /// the foreign names are carried <b>verbatim</b> as the screen code under a
    /// reserved module code, and the subsystem's own guard keeps working
    /// unmodified.
    /// </para>
    /// <para>
    /// The catalog is declaration only. It says what may be granted, never what is
    /// granted: the grants are ordinary <c>sec.RolePermission</c> rows an
    /// administrator can see and revoke like any other, which is the point of not
    /// giving a hosted subsystem a private permission store.
    /// </para>
    /// <para>
    /// Implementations are resolved as a fan-out collection, so a deployment that
    /// does not host the subsystem contributes none and nothing is seeded — the
    /// same shape as <see cref="Seeding.ISeedContributor"/>.
    /// </para>
    /// </summary>
    public interface IExternalPermissionCatalog
    {
        /// <summary>The reserved module code these permissions are catalogued under, e.g. <c>ERP</c>. Must not collide with a real module code from doc 06.</summary>
        string ModuleCode { get; }

        /// <summary>Every permission name the subsystem may check, verbatim.</summary>
        IReadOnlyList<string> PermissionNames { get; }

        /// <summary>
        /// Role codes that receive every one of these permissions when the catalog
        /// is first seeded. Keep this to the administrator role: a hosted
        /// subsystem should not decide who in a school may post to the ledger, and
        /// anything broader would grant by default what an administrator never
        /// chose. Later grants are the role screen's business, and re-seeding
        /// never re-grants what was revoked.
        /// </summary>
        IReadOnlyList<string> DefaultGrantRoleCodes { get; }
    }
}
