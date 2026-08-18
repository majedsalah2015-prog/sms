using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Backup;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // ops schema per doc/Modules/35 — deployment-wide, same schema group as E-011's JobDefinition/JobRun.

    public class BackupPolicyConfiguration : IEntityTypeConfiguration<BackupPolicy>
    {
        public void Configure(EntityTypeBuilder<BackupPolicy> builder)
        {
            builder.ToTable("BackupPolicy", "ops");
        }
    }

    public class BackupRunConfiguration : IEntityTypeConfiguration<BackupRun>
    {
        public void Configure(EntityTypeBuilder<BackupRun> builder)
        {
            builder.ToTable("BackupRun", "ops");
            builder.HasOne<BackupPolicy>().WithMany().HasForeignKey(x => x.BackupPolicyId);
        }
    }

    public class BackupVerificationRunConfiguration : IEntityTypeConfiguration<BackupVerificationRun>
    {
        public void Configure(EntityTypeBuilder<BackupVerificationRun> builder)
        {
            builder.ToTable("BackupVerificationRun", "ops");
            builder.HasOne<BackupRun>().WithMany().HasForeignKey(x => x.BackupRunId);
        }
    }

    public class SnapshotEventConfiguration : IEntityTypeConfiguration<SnapshotEvent>
    {
        public void Configure(EntityTypeBuilder<SnapshotEvent> builder)
        {
            builder.ToTable("SnapshotEvent", "ops");
            builder.Property(x => x.Label).HasMaxLength(100).IsRequired();
            builder.Property(x => x.TriggerOperation).HasMaxLength(50).IsRequired();
        }
    }

    public class RestoreCaseConfiguration : IEntityTypeConfiguration<RestoreCase>
    {
        public void Configure(EntityTypeBuilder<RestoreCase> builder)
        {
            builder.ToTable("RestoreCase", "ops");
            builder.Property(x => x.CertificateNo).HasMaxLength(20);
        }
    }
}
