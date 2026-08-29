using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Domain.Security;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Security
{
    /// <summary>
    /// The person behind an account: the name they are actually known by, and the file number the
    /// office looks them up under (doc/Modules/06 §2, §8).
    /// <para>
    /// Shared by the two screens that list accounts — the directory behind
    /// <see cref="UserAccountAdmin"/> and the role-assignment list behind <see cref="SecurityAdmin"/>
    /// — because a user name on its own answers nobody's question. Nobody in a school office knows
    /// who <c>emp-1042</c> is, and a list of user names is a list an administrator has to translate
    /// before they can use it.
    /// </para>
    /// <para>
    /// Read <b>past</b> the soft-active filter, with the school scope restated by hand because
    /// <c>IgnoreQueryFilters</c> lifts every filter and not only the one meant (ADR-2, BR-GLB-010).
    /// A withdrawn student's or a terminated employee's account is exactly the row an administrator
    /// has come here to deal with, and a list that could not name them would go anonymous at the
    /// least useful moment.
    /// </para>
    /// </summary>
    internal static class AccountPeople
    {
        /// <summary>
        /// One lookup for a whole page of accounts — three queries at most, and none at all for a
        /// kind of account the page does not contain. Keyed by (type, person id) rather than by
        /// person id alone: employee 7, parent 7 and student 7 are three different people.
        /// </summary>
        internal static async Task<IReadOnlyDictionary<(AccountType Type, int PersonId), PersonName>> LoadAsync(
            AppDbContext db, IReadOnlyCollection<UserAccount> accounts, CancellationToken cancellationToken)
        {
            var people = new Dictionary<(AccountType, int), PersonName>();

            var staffIds = PersonIds(accounts, AccountType.Staff);
            if (staffIds.Count > 0)
            {
                var employees = await db.Employees.IgnoreQueryFilters().AsNoTracking()
                    .Where(e => e.SchoolId == db.CurrentSchoolId && staffIds.Contains(e.Id))
                    .ToListAsync(cancellationToken);
                foreach (var employee in employees)
                {
                    people[(AccountType.Staff, employee.Id)] = new PersonName(
                        Join(employee.FirstNameAr, employee.FatherNameAr, employee.FamilyNameAr),
                        Join(employee.FirstNameEn, employee.FatherNameEn, employee.FamilyNameEn),
                        employee.EmployeeNo);
                }
            }

            var parentIds = PersonIds(accounts, AccountType.Parent);
            if (parentIds.Count > 0)
            {
                var parents = await db.Parents.IgnoreQueryFilters().AsNoTracking()
                    .Where(p => p.SchoolId == db.CurrentSchoolId && parentIds.Contains(p.Id))
                    .ToListAsync(cancellationToken);
                foreach (var parent in parents)
                {
                    people[(AccountType.Parent, parent.Id)] = new PersonName(parent.NameAr, parent.NameEn, parent.ParentFileNo);
                }
            }

            var studentIds = PersonIds(accounts, AccountType.Student);
            if (studentIds.Count > 0)
            {
                var students = await db.Students.IgnoreQueryFilters().AsNoTracking()
                    .Where(s => s.SchoolId == db.CurrentSchoolId && studentIds.Contains(s.Id))
                    .ToListAsync(cancellationToken);
                foreach (var student in students)
                {
                    people[(AccountType.Student, student.Id)] = new PersonName(
                        Join(student.FirstNameAr, student.FatherNameAr, student.FamilyNameAr),
                        Join(student.FirstNameEn, student.FatherNameEn, student.FamilyNameEn),
                        student.StudentNo);
                }
            }

            return people;
        }

        /// <summary>
        /// The person this account belongs to, or an empty name. Empty is a real answer and not a
        /// fault: an <see cref="AccountType.System"/> integration account belongs to nobody, and a
        /// person-linked account whose row has gone is a defect the screen should show rather than
        /// throw over.
        /// </summary>
        internal static PersonName Of(
            this IReadOnlyDictionary<(AccountType Type, int PersonId), PersonName> people, UserAccount account)
            => account.PersonId is { } personId && people.TryGetValue((account.AccountType, personId), out var found)
                ? found
                : default;

        /// <summary>Case-insensitive, accent-sensitive substring match — what a search box does.</summary>
        internal static bool Contains(string? value, string term)
            => value != null && value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>Composes a display name from the parts that were filled in, skipping the ones that were not.</summary>
        internal static string Join(params string?[] parts)
            => string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p))).Trim();

        private static List<int> PersonIds(IEnumerable<UserAccount> accounts, AccountType type)
            => accounts
                .Where(a => a.AccountType == type && a.PersonId != null)
                .Select(a => a.PersonId!.Value)
                .Distinct()
                .ToList();

        internal readonly struct PersonName
        {
            public PersonName(string nameAr, string nameEn, string reference)
            {
                NameAr = nameAr;
                NameEn = nameEn;
                Reference = reference;
            }

            public string? NameAr { get; }

            public string? NameEn { get; }

            public string? Reference { get; }
        }
    }
}
