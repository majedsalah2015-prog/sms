using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Security;
using Sms.Application.Seeding;
using Sms.Domain.Security;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Seeding
{
    /// <summary>
    /// Bootstrap System Administrator account (doc 06 §4.3 SYSADMIN) so a
    /// freshly provisioned tenant can be signed into at all. The password is
    /// issued as a one-time credential through
    /// <see cref="IAuthenticationService.SetTemporaryPasswordAsync"/>, so
    /// BR-SEC-005 forces a change on the first login — the temporary value
    /// never survives past onboarding. Idempotent on the user name.
    /// </summary>
    public class SysAdminAccountSeedContributor : ISeedContributor
    {
        public const string UserName = "admin";

        /// <summary>One-time password (meets PasswordPolicy.ProductMinimum); rotated at first login by BR-SEC-005.</summary>
        public const string TemporaryPassword = "Admin@2026!";

        private readonly AppDbContext _db;
        private readonly IAuthenticationService _auth;

        public SysAdminAccountSeedContributor(AppDbContext db, IAuthenticationService auth)
        {
            _db = db;
            _auth = auth;
        }

        public string Name => "System administrator account (doc 06 §4.3)";

        // After role templates (20) so the SYSADMIN assignment resolves.
        public int Order => 25;

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            if (await _db.UserAccounts.IgnoreQueryFilters().AnyAsync(u => u.UserName == UserName, cancellationToken))
            {
                return;
            }

            var account = new UserAccount { UserName = UserName, AccountType = AccountType.Staff };
            _db.UserAccounts.Add(account);
            await _db.SaveChangesAsync(cancellationToken);

            var sysAdminRole = await _db.Roles.SingleOrDefaultAsync(r => r.Code == "SYSADMIN", cancellationToken);
            if (sysAdminRole != null)
            {
                _db.RoleAssignments.Add(new RoleAssignment { UserAccountId = account.Id, RoleId = sysAdminRole.Id });
                await _db.SaveChangesAsync(cancellationToken);
            }

            await _auth.SetTemporaryPasswordAsync(account.Id, TemporaryPassword, cancellationToken);
        }
    }
}
