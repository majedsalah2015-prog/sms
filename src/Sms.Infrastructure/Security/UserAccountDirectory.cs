using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Security;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Security
{
    /// <summary>
    /// EF-backed <see cref="IUserAccountDirectory"/>. A staff account's display
    /// name comes from the employee that claims it — <c>Employee.UserAccountId</c>
    /// points at the account, not the other way round — and a parent, student or
    /// system account has none, so callers fall back to the user name.
    /// <para>
    /// The name is picked by the request's UI culture, matching how every screen
    /// in this system chooses between the Arabic and English name fields. That
    /// is a display decision living in a data adapter, which is not ideal; it is
    /// here because the consumer (the ERP's <c>UserInfo.DisplayName</c>) is a
    /// single string and there is nowhere later to make the choice.
    /// </para>
    /// <para>
    /// Reads go through the ambient school filter, so an id belonging to another
    /// school does not resolve (ADR-2, BR-GLB-010).
    /// </para>
    /// </summary>
    public class UserAccountDirectory : IUserAccountDirectory
    {
        private readonly AppDbContext _db;

        public UserAccountDirectory(AppDbContext db)
        {
            _db = db;
        }

        public async Task<UserAccountInfo?> FindAsync(int userId, CancellationToken cancellationToken = default)
        {
            var rows = await QueryFor(a => a.Id == userId, activeOnly: false).ToListAsync(cancellationToken);
            return rows.Select(Project).FirstOrDefault();
        }

        public async Task<IReadOnlyDictionary<int, UserAccountInfo>> GetByIdsAsync(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default)
        {
            if (userIds == null || userIds.Count == 0)
            {
                return new Dictionary<int, UserAccountInfo>();
            }

            var ids = userIds.Distinct().ToList();
            var rows = await QueryFor(a => ids.Contains(a.Id), activeOnly: false).ToListAsync(cancellationToken);
            return rows.Select(Project).ToDictionary(u => u.Id);
        }

        public async Task<IReadOnlyList<UserAccountInfo>> ListAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
        {
            var rows = await QueryFor(_ => true, activeOnly).OrderBy(r => r.UserName).ToListAsync(cancellationToken);
            return rows.Select(Project).ToList();
        }

        // IsActive is read explicitly rather than relying on the soft-active filter: FindAsync must be
        // able to report "this account exists but is inactive", which a filtered query cannot say.
        private IQueryable<Row> QueryFor(
            System.Linq.Expressions.Expression<System.Func<Sms.Domain.Security.UserAccount, bool>> predicate,
            bool activeOnly)
        {
            // IgnoreQueryFilters lifts filters for every entity in the query, not just the root, so the
            // school scope both sides would have got for free is restated explicitly here (ADR-2).
            var accounts = _db.UserAccounts.IgnoreQueryFilters()
                .Where(a => a.SchoolId == _db.CurrentSchoolId)
                .Where(predicate);

            if (activeOnly)
            {
                accounts = accounts.Where(a => a.IsActive);
            }

            var employees = _db.Employees.IgnoreQueryFilters()
                .Where(e => e.SchoolId == _db.CurrentSchoolId);

            return from a in accounts
                   join e in employees on a.Id equals e.UserAccountId into staff
                   from e in staff.DefaultIfEmpty()
                   select new Row
                   {
                       Id = a.Id,
                       UserName = a.UserName,
                       IsActive = a.IsActive,
                       FirstNameAr = e == null ? null : e.FirstNameAr,
                       FamilyNameAr = e == null ? null : e.FamilyNameAr,
                       FirstNameEn = e == null ? null : e.FirstNameEn,
                       FamilyNameEn = e == null ? null : e.FamilyNameEn,
                   };
        }

        private static UserAccountInfo Project(Row row)
            => new UserAccountInfo(row.Id, row.UserName, DisplayName(row), row.IsActive);

        private static string? DisplayName(Row row)
        {
            var arabic = Join(row.FirstNameAr, row.FamilyNameAr);
            var english = Join(row.FirstNameEn, row.FamilyNameEn);

            return CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft
                ? arabic ?? english
                : english ?? arabic;
        }

        private static string? Join(string? first, string? family)
        {
            var name = $"{first} {family}".Trim();
            return name.Length == 0 ? null : name;
        }

        private sealed class Row
        {
            public int Id { get; set; }

            public string UserName { get; set; } = string.Empty;

            public bool IsActive { get; set; }

            public string? FirstNameAr { get; set; }

            public string? FamilyNameAr { get; set; }

            public string? FirstNameEn { get; set; }

            public string? FamilyNameEn { get; set; }
        }
    }
}
