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
    /// Demo portal account for the E-305 demo parent, closing E-304's "no
    /// admin service provisions portal accounts" gap far enough that the
    /// parent portal (BR-SEC-010..013) can actually be signed into and
    /// demoed. Full self-service provisioning stays Module 36 work — this
    /// only bridges the already-seeded demo Parent to a portal
    /// <see cref="UserAccount"/> (<see cref="AccountType.Parent"/>, so
    /// <c>PortalAreaFilter</c> routes it away from staff screens).
    /// Same one-time-password pattern as
    /// <see cref="SysAdminAccountSeedContributor"/> — BR-SEC-005 forces a
    /// change on first login. Idempotent on the user name; no-op when the
    /// demo tenant (DemoSeedContributor, Order 50) hasn't been seeded.
    /// </summary>
    public class PortalDemoAccountSeedContributor : ISeedContributor
    {
        public const string UserName = "parent";

        /// <summary>One-time password (meets PasswordPolicy.ProductMinimum); rotated at first login by BR-SEC-005.</summary>
        public const string TemporaryPassword = "Parent@2026!";

        /// <summary>Self-access demo account (PortalAccessEvaluator's Student.UserAccountId path) — bridged to the demo parent's portal-visible child.</summary>
        public const string StudentUserName = "student";

        public const string StudentTemporaryPassword = "Student@2026!";

        /// <summary>The demo parent's mobile as registered by DemoSeedContributor — the stable lookup key.</summary>
        public const string DemoParentMobile = "0500000001";

        private readonly AppDbContext _db;
        private readonly IAuthenticationService _auth;

        public PortalDemoAccountSeedContributor(AppDbContext db, IAuthenticationService auth)
        {
            _db = db;
            _auth = auth;
        }

        public string Name => "Demo parent portal account (E-304 portal essentials)";

        // After DemoSeedContributor (50) so the demo parent exists to bridge to.
        public int Order => 55;

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            var parent = await _db.Parents
                .Where(p => p.PrimaryMobile == DemoParentMobile)
                .OrderBy(p => p.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (parent == null)
            {
                return; // demo tenant not seeded — nothing to bridge
            }

            if (parent.UserAccountId == null
                && !await _db.UserAccounts.IgnoreQueryFilters().AnyAsync(u => u.UserName == UserName, cancellationToken))
            {
                var account = new UserAccount { UserName = UserName, AccountType = AccountType.Parent };
                _db.UserAccounts.Add(account);
                await _db.SaveChangesAsync(cancellationToken);

                parent.UserAccountId = account.Id;
                await _db.SaveChangesAsync(cancellationToken);

                await _auth.SetTemporaryPasswordAsync(account.Id, TemporaryPassword, cancellationToken);
            }

            // Student self-access half: bridge the demo parent's portal-visible child the same way.
            if (!await _db.UserAccounts.IgnoreQueryFilters().AnyAsync(u => u.UserName == StudentUserName, cancellationToken))
            {
                var childId = await _db.StudentGuardianLinks
                    .Where(l => l.ParentId == parent.Id && l.EffectiveToUtc == null && l.IsPortalVisible)
                    .OrderBy(l => l.StudentId)
                    .Select(l => (int?)l.StudentId)
                    .FirstOrDefaultAsync(cancellationToken);
                var child = childId == null
                    ? null
                    : await _db.Students.Where(s => s.Id == childId.Value && s.UserAccountId == null).FirstOrDefaultAsync(cancellationToken);
                if (child != null)
                {
                    var account = new UserAccount { UserName = StudentUserName, AccountType = AccountType.Student };
                    _db.UserAccounts.Add(account);
                    await _db.SaveChangesAsync(cancellationToken);

                    child.UserAccountId = account.Id;
                    await _db.SaveChangesAsync(cancellationToken);

                    await _auth.SetTemporaryPasswordAsync(account.Id, StudentTemporaryPassword, cancellationToken);
                }
            }
        }
    }
}
