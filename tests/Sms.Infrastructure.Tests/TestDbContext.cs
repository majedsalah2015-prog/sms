using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Audit;
using Sms.Domain.Common;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Tests
{
    /// <summary>Test-only entity shaped like tenant-owned master data (T3: reference lists).</summary>
    [Audited(AuditTier.T3)]
    public class MasterItem : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public bool IsActive { get; set; } = true;

        public string Name { get; set; } = string.Empty;
    }

    /// <summary>Test-only entity shaped like a year-scoped transaction row (unaudited).</summary>
    public class TransactionItem : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public string Reference { get; set; } = string.Empty;
    }

    /// <summary>Test-only entity shaped like T1 data (marks after submission, doc 07 §3).</summary>
    [Audited(AuditTier.T1)]
    public class SensitiveRecord : AuditableEntity, ISchoolScoped, IAuditBusinessKey
    {
        public int SchoolId { get; set; }

        public string StudentNo { get; set; } = string.Empty;

        [RequiresAuditReason]
        public decimal Mark { get; set; }

        public string? Note { get; set; }

        public string AuditBusinessKey => StudentNo;
    }

    /// <summary>Test-only entity shaped like T2 data (non-identity student data).</summary>
    [Audited(AuditTier.T2)]
    public class StandardRecord : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public string Phone { get; set; } = string.Empty;
    }

    public class TestDbContext : SmsDbContext
    {
        public TestDbContext(DbContextOptions options, ITenantContext tenant, ICurrentUser user, IClock clock, IAuditContext? audit = null)
            : base(options, tenant, user, clock, audit)
        {
        }

        public DbSet<MasterItem> MasterItems => Set<MasterItem>();

        public DbSet<TransactionItem> TransactionItems => Set<TransactionItem>();

        public DbSet<SensitiveRecord> SensitiveRecords => Set<SensitiveRecord>();

        public DbSet<StandardRecord> StandardRecords => Set<StandardRecord>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // NOT NULL column so the BR-AUD-003 atomicity test can force a
            // database-level failure inside an audited save.
            modelBuilder.Entity<SensitiveRecord>().Property(x => x.StudentNo).IsRequired();
        }
    }
}
