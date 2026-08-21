using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using ERP2028.Application.Abstractions.Identity;
using Microsoft.AspNetCore.Http;

namespace Sms.Erp.Bridge.Identity
{
    /// <summary>
    /// Presents this system's signed-in user to the ERP modules as their own
    /// <see cref="ICurrentUser"/>. One sign-in, one session, one set of claims —
    /// the accounting screens run inside the school's authentication rather than
    /// beside a second user store
    /// (docs/Integration/01-Embedded-Accounting-Plan.md §5.1).
    /// <para>
    /// <b>Permissions are opaque strings here.</b> This system models a grant as
    /// (module, screen, verb) and the ERP as a flat name like
    /// <c>Accounting.Accounts.View</c>. Rather than translate between the two —
    /// which would mean maintaining a mapping that silently rots as either side
    /// adds a screen — the ERP's names are carried verbatim as
    /// <see cref="AppClaimTypes.Permission"/> claims minted at sign-in. The ERP's
    /// <c>[HasPermission]</c> then keeps working unmodified, because its handler
    /// reads exactly that claim.
    /// </para>
    /// <para>
    /// Until the role screen can grant them, no principal carries those claims and
    /// every accounting screen is denied. That is the correct default for a
    /// deny-by-default system (BR-GLB-070) and the reason this class needs no
    /// "allow everything" fallback: an empty permission set is not a bug to work
    /// around.
    /// </para>
    /// </summary>
    public sealed class ErpCurrentUserAdapter : ICurrentUser
    {
        private readonly IHttpContextAccessor _accessor;

        public ErpCurrentUserAdapter(IHttpContextAccessor accessor) => _accessor = accessor;

        private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

        public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

        public int? UserId
        {
            get
            {
                var value = Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                return int.TryParse(value, out var id) ? id : (int?)null;
            }
        }

        /// <summary>
        /// The login name. Falls back to the id because the ERP stamps this onto
        /// <c>JournalEntry.PostedBy</c>: a posted entry with a blank author is a
        /// hole in the audit trail, and a numeric id at least resolves.
        /// </summary>
        public string? UserName =>
            Principal?.FindFirstValue(ClaimTypes.Name)
            ?? UserId?.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public IReadOnlyCollection<string> Permissions =>
            Principal?.FindAll(AppClaimTypes.Permission).Select(c => c.Value).ToArray()
            ?? Array.Empty<string>();

        public bool HasPermission(string permission) =>
            Principal?.HasClaim(AppClaimTypes.Permission, permission) ?? false;
    }
}
