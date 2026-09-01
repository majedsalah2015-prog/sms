using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Notifications;
using Sms.Application.Setup;
using Sms.Domain.Notifications;
using Sms.Domain.Security;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Notifications
{
    /// <summary>
    /// Resolves a user account to the address a channel can reach it on, by walking the
    /// account back to the person it belongs to (BR-GLB-002: one person, one account).
    /// <para>
    /// Three tables, because there are three kinds of person and their contact columns
    /// were never unified: <c>Parent.PrimaryMobile</c>/<c>Parent.Email</c>,
    /// <c>Employee.Mobile</c>, <c>Student.Mobile</c>. Accounts are read past the
    /// soft-active filter on purpose — a delivery already queued to somebody whose
    /// account was deactivated this morning still has an address, and dropping it here
    /// would turn a deactivation into a silent hole in the log rather than a delivery
    /// that plainly failed.
    /// </para>
    /// <para>
    /// <b>Nobody gets a half-usable number.</b> Every phone value goes through
    /// <see cref="PhoneNumberRules"/> with the school's dialling code; anything that
    /// cannot be normalised is left out of the result entirely, because a gateway
    /// rejection per parent per message is worse than an honest "no address on file".
    /// </para>
    /// </summary>
    public class RecipientAddressBook : IRecipientAddressBook
    {
        private readonly AppDbContext _db;

        public RecipientAddressBook(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyDictionary<int, string>> ResolveAsync(
            IReadOnlyCollection<int> userIds,
            NotificationChannel channel,
            CancellationToken cancellationToken = default)
        {
            var resolved = new Dictionary<int, string>();
            if (userIds.Count == 0 || channel == NotificationChannel.InApp)
            {
                // In-app has no address: the delivery row is the destination.
                return resolved;
            }

            var accounts = await _db.UserAccounts
                .IgnoreQueryFilters()
                .Where(a => a.SchoolId == _db.CurrentSchoolId && userIds.Contains(a.Id) && a.PersonId != null)
                .Select(a => new { a.Id, a.AccountType, PersonId = a.PersonId!.Value })
                .ToListAsync(cancellationToken);

            if (accounts.Count == 0)
            {
                return resolved;
            }

            var wantsPhone = channel != NotificationChannel.Email;
            var diallingCode = wantsPhone ? await DiallingCodeAsync(cancellationToken) : null;

            foreach (var group in accounts.GroupBy(a => a.AccountType))
            {
                var personIds = group.Select(a => a.PersonId).Distinct().ToList();
                var contacts = await ContactsAsync(group.Key, personIds, wantsPhone, cancellationToken);

                foreach (var account in group)
                {
                    if (!contacts.TryGetValue(account.PersonId, out var raw) || string.IsNullOrWhiteSpace(raw))
                    {
                        continue;
                    }

                    if (!wantsPhone)
                    {
                        // A mailbox is taken as typed: there is no normalisation that would make a
                        // wrong one right, and the bounce is the honest signal.
                        resolved[account.Id] = raw.Trim();
                        continue;
                    }

                    var normalized = PhoneNumberRules.Normalize(raw, diallingCode);
                    if (normalized.IsValid)
                    {
                        resolved[account.Id] = normalized.E164!;
                    }
                }
            }

            return resolved;
        }

        /// <summary>
        /// The school's dialling code, read straight off <c>SchoolSetting</c> rather than
        /// through <c>ISystemSetupAdmin</c>.
        /// <para>
        /// Not a stylistic choice: <c>SystemSetupAdmin</c> injects
        /// <see cref="Application.Notifications.INotificationPublisher"/> so a settings change
        /// can notify, and the publisher injects this class — so asking the setup admin for a
        /// setting closes the loop and the container refuses to build. It fails at the first
        /// resolution of either service, which in the web application means the setup screens
        /// rather than anything to do with notifications.
        /// </para>
        /// <para>
        /// The school-wide row is the one wanted: the dialling code is not year-versionable
        /// (see <c>SettingKeys</c>), so there is no pinned row to prefer and no need for
        /// <c>SettingResolver</c>'s year logic here.
        /// </para>
        /// </summary>
        private async Task<string?> DiallingCodeAsync(CancellationToken cancellationToken)
            => await _db.SchoolSettings
                .Where(s => s.Key == SettingKeys.DefaultDiallingCode && s.AcademicYearId == null)
                .Select(s => s.Value)
                .SingleOrDefaultAsync(cancellationToken);

        private async Task<Dictionary<int, string?>> ContactsAsync(
            AccountType accountType, IReadOnlyCollection<int> personIds, bool wantsPhone, CancellationToken cancellationToken)
        {
            switch (accountType)
            {
                case AccountType.Parent:
                    return (await _db.Parents.IgnoreQueryFilters()
                            .Where(p => p.SchoolId == _db.CurrentSchoolId && personIds.Contains(p.Id))
                            .Select(p => new { p.Id, Phone = p.PrimaryMobile, p.Email })
                            .ToListAsync(cancellationToken))
                        .ToDictionary(p => p.Id, p => wantsPhone ? p.Phone : p.Email);

                case AccountType.Staff:
                    // Employees have no email column in this product — a staff member reached by
                    // email would need one added to Employee first, so the lookup answers nothing
                    // rather than inventing an address.
                    return wantsPhone
                        ? (await _db.Employees.IgnoreQueryFilters()
                                .Where(e => e.SchoolId == _db.CurrentSchoolId && personIds.Contains(e.Id))
                                .Select(e => new { e.Id, e.Mobile })
                                .ToListAsync(cancellationToken))
                            .ToDictionary(e => e.Id, e => e.Mobile)
                        : new Dictionary<int, string?>();

                case AccountType.Student:
                    return wantsPhone
                        ? (await _db.Students.IgnoreQueryFilters()
                                .Where(s => s.SchoolId == _db.CurrentSchoolId && personIds.Contains(s.Id))
                                .Select(s => new { s.Id, s.Mobile })
                                .ToListAsync(cancellationToken))
                            .ToDictionary(s => s.Id, s => s.Mobile)
                        : new Dictionary<int, string?>();

                default:
                    // AccountType.System is the scheduler and the seeders; there is no person behind
                    // one and nothing to reach.
                    return new Dictionary<int, string?>();
            }
        }
    }
}
