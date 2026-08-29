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

            var parentAccount = await _db.UserAccounts.IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.UserName == UserName, cancellationToken);

            if (parentAccount == null && parent.UserAccountId == null)
            {
                parentAccount = new UserAccount
                {
                    UserName = UserName,
                    AccountType = AccountType.Parent,
                    PersonId = parent.Id,
                };
                _db.UserAccounts.Add(parentAccount);
                await _db.SaveChangesAsync(cancellationToken);

                parent.UserAccountId = parentAccount.Id;
                await _db.SaveChangesAsync(cancellationToken);

                await _auth.SetTemporaryPasswordAsync(parentAccount.Id, TemporaryPassword, cancellationToken);
            }

            Link(parentAccount, parent.UserAccountId, parent.Id);

            // Student self-access half: bridge the demo parent's portal-visible child the same way.
            var studentAccount = await _db.UserAccounts.IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.UserName == StudentUserName, cancellationToken);

            var childId = await _db.StudentGuardianLinks
                .Where(l => l.ParentId == parent.Id && l.EffectiveToUtc == null && l.IsPortalVisible)
                .OrderBy(l => l.StudentId)
                .Select(l => (int?)l.StudentId)
                .FirstOrDefaultAsync(cancellationToken);

            if (studentAccount == null)
            {
                var child = childId == null
                    ? null
                    : await _db.Students.Where(s => s.Id == childId.Value && s.UserAccountId == null).FirstOrDefaultAsync(cancellationToken);
                if (child != null)
                {
                    studentAccount = new UserAccount
                    {
                        UserName = StudentUserName,
                        AccountType = AccountType.Student,
                        PersonId = child.Id,
                    };
                    _db.UserAccounts.Add(studentAccount);
                    await _db.SaveChangesAsync(cancellationToken);

                    child.UserAccountId = studentAccount.Id;
                    await _db.SaveChangesAsync(cancellationToken);

                    await _auth.SetTemporaryPasswordAsync(studentAccount.Id, StudentTemporaryPassword, cancellationToken);
                }
            }
            else if (childId != null)
            {
                var child = await _db.Students
                    .Where(s => s.Id == childId.Value)
                    .Select(s => new { s.Id, s.UserAccountId })
                    .FirstOrDefaultAsync(cancellationToken);
                Link(studentAccount, child?.UserAccountId, child?.Id ?? 0);
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Writes the half of the link this seeder used to leave out. The person's own
        /// <c>UserAccountId</c> alone is not the relationship: <c>sec.UserAccount.PersonId</c> is what
        /// everything starting from an account reads to name who it belongs to, and with it null the
        /// account directory shows a login and no human being (BR-GLB-002, doc/Modules/06 §2).
        /// <para>
        /// Restated on every run rather than only at creation, so a database seeded before this was
        /// fixed heals itself the next time the seeder is run. Only ever set when the person already
        /// points back at this account — a disagreement between the two directions is repaired, never
        /// invented.
        /// </para>
        /// </summary>
        private static void Link(UserAccount? account, int? personsAccountId, int personId)
        {
            if (account != null && account.PersonId == null && personsAccountId == account.Id)
            {
                account.PersonId = personId;
            }
        }
    }
}
