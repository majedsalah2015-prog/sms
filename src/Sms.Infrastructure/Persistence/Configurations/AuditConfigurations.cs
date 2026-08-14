using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Audit;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // aud schema per docs/Database/01 §3 and 03 §A12; explorer indexes per
    // DB/04 §2. Append-only is enforced in the context (BR-AUD-001) and by
    // deny-UPDATE/DELETE grants in the deployment scripts (DB/04 §3).

    public class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
    {
        public void Configure(EntityTypeBuilder<AuditEntry> builder)
        {
            builder.ToTable("AuditEntry", "aud");
            builder.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
            builder.Property(x => x.BusinessKey).HasMaxLength(60);
            builder.Property(x => x.FieldName).HasMaxLength(128);
            builder.Property(x => x.Reason).HasMaxLength(500);
            builder.Property(x => x.SourceScreen).HasMaxLength(128);
            builder.Property(x => x.ClientIp).HasMaxLength(45);
            builder.HasIndex(x => new { x.EntityType, x.EntityId, x.OccurredAtUtc });
            builder.HasIndex(x => new { x.ActorUserId, x.OccurredAtUtc });
            builder.HasIndex(x => new { x.SchoolId, x.OccurredAtUtc });
        }
    }

    public class IntegrityCheckpointConfiguration : IEntityTypeConfiguration<IntegrityCheckpoint>
    {
        public void Configure(EntityTypeBuilder<IntegrityCheckpoint> builder)
        {
            builder.ToTable("IntegrityCheckpoint", "aud");
            builder.Property(x => x.EntriesHash).HasMaxLength(64).IsRequired();
            builder.Property(x => x.ChainHash).HasMaxLength(64).IsRequired();
            builder.Property(x => x.PreviousChainHash).HasMaxLength(64);
            builder.HasIndex(x => x.PeriodStartUtc);
        }
    }
}
