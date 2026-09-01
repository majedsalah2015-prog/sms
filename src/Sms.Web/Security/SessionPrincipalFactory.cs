using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.Infrastructure.Persistence;
// AppClaimTypes (the ERP's own claim names) from the abstractions; the reserved "ERP"
// module code from this system's bridge, which is what sec.Permission actually stores.
using ERP2028.Application.Abstractions.Identity;
using Sms.Erp.Bridge.Identity;

namespace Sms.Web.Security
{
    /// <summary>
    /// Builds the claims identity that stands for a <see cref="UserSession"/>
    /// (doc 06 §3). One place, because two transports now mint it: the browser's
    /// cookie sign-in (<c>AccountController</c>) and the mobile API's bearer
    /// handler (<c>Api.Auth.SessionTokenAuthenticationHandler</c>).
    /// <para>
    /// Two identities built by two copies of this code is how a permission ends
    /// up on one transport and not the other — and the ERP claims below are
    /// exactly the kind that would go missing silently, because an accounting
    /// screen with no claim does not error, it denies. Session lifetime itself
    /// is not decided here: <see cref="IAuthenticationService.ValidateSessionAsync"/>
    /// owns BR-SEC-004 for both transports alike.
    /// </para>
    /// </summary>
    public sealed class SessionPrincipalFactory
    {
        private readonly AppDbContext _db;
        private readonly IPermissionService _permissions;

        public SessionPrincipalFactory(AppDbContext db, IPermissionService permissions)
        {
            _db = db;
            _permissions = permissions;
        }

        /// <summary>
        /// The identity for <paramref name="session"/>'s account, tagged with
        /// <paramref name="authenticationScheme"/> so the cookie and the bearer
        /// token stay distinguishable on the principal they produce.
        /// </summary>
        public async Task<ClaimsIdentity> BuildAsync(
            UserSession session,
            bool mustChangePassword,
            string authenticationScheme,
            CancellationToken cancellationToken = default)
        {
            var account = await _db.UserAccounts.AsNoTracking()
                .SingleAsync(u => u.Id == session.UserAccountId, cancellationToken);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, account.Id.ToString(CultureInfo.InvariantCulture)),
                new(ClaimTypes.Name, account.UserName),
                new(SmsClaimTypes.SessionToken, session.SessionToken),
                new(SmsClaimTypes.SchoolId, account.SchoolId.ToString(CultureInfo.InvariantCulture)),
                new(SmsClaimTypes.AccountType, account.AccountType.ToString()),
            };

            if (mustChangePassword)
            {
                claims.Add(new Claim(SmsClaimTypes.MustChangePassword, "1"));
            }

            // The embedded ERP modules authorize by claim, not by a service call, so every accounting
            // permission this account holds has to be on the principal before it is signed in. They are
            // ordinary sec.RolePermission grants under the reserved "ERP" module code
            // (IExternalPermissionCatalog); an account with none simply carries none, and every
            // accounting screen denies it — the correct deny-by-default answer, not a gap to patch.
            var erpPermissions = await _permissions.GetGrantedScreenCodesAsync(
                account.Id, ErpPermissionCatalog.ErpModuleCode, ActionVerb.View, cancellationToken);
            claims.AddRange(erpPermissions.Select(p => new Claim(AppClaimTypes.Permission, p)));

            return new ClaimsIdentity(claims, authenticationScheme);
        }

        /// <summary>BR-SEC-005, read once so both transports ask the same question of the same column.</summary>
        public Task<bool> MustChangePasswordAsync(int userAccountId, CancellationToken cancellationToken = default)
            => _db.UserAccounts.AsNoTracking()
                .Where(u => u.Id == userAccountId)
                .Select(u => u.MustChangePassword)
                .SingleAsync(cancellationToken);
    }
}
