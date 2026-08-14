using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Common;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Tests
{
    /// <summary>Test-only entity shaped like tenant-owned master data.</summary>
    public class MasterItem : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public bool IsActive { get; set; } = true;

        public string Name { get; set; } = string.Empty;
    }

    /// <summary>Test-only entity shaped like a year-scoped transaction row.</summary>
    public class TransactionItem : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public string Reference { get; set; } = string.Empty;
    }

    public class TestDbContext : SmsDbContext
    {
        public TestDbContext(DbContextOptions options, ITenantContext tenant, ICurrentUser user, IClock clock)
            : base(options, tenant, user, clock)
        {
        }

        public DbSet<MasterItem> MasterItems => Set<MasterItem>();

        public DbSet<TransactionItem> TransactionItems => Set<TransactionItem>();
    }
}
