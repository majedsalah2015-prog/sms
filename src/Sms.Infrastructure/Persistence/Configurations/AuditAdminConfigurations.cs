using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Audit;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // aud schema per doc/Modules/34 — same schema as E-004's AuditEntry/IntegrityCheckpoint.

    public class AnomalyRuleConfiguration : IEntityTypeConfiguration<AnomalyRule>
    {
        public void Configure(EntityTypeBuilder<AnomalyRule> builder)
        {
            builder.ToTable("AnomalyRule", "aud");
            builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
            builder.Property(x => x.DescriptionAr).HasMaxLength(300).IsRequired();
            builder.Property(x => x.DescriptionEn).HasMaxLength(300).IsRequired();
            builder.HasIndex(x => x.Code).IsUnique();
        }
    }

    public class AnomalyHitConfiguration : IEntityTypeConfiguration<AnomalyHit>
    {
        public void Configure(EntityTypeBuilder<AnomalyHit> builder)
        {
            builder.ToTable("AnomalyHit", "aud");
            builder.Property(x => x.ContextJson).IsRequired();
            builder.HasOne<AnomalyRule>().WithMany().HasForeignKey(x => x.AnomalyRuleId);
        }
    }

    public class IntegrityVerificationRunConfiguration : IEntityTypeConfiguration<IntegrityVerificationRun>
    {
        public void Configure(EntityTypeBuilder<IntegrityVerificationRun> builder)
        {
            builder.ToTable("IntegrityVerificationRun", "aud");
        }
    }
}
