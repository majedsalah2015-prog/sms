using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Discounts;
using Sms.Domain.Fees;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // ppl schema per doc/Modules/22 — same schema group as Charge/CreditNote.

    public class DiscountTypeConfiguration : IEntityTypeConfiguration<DiscountType>
    {
        public void Configure(EntityTypeBuilder<DiscountType> builder)
        {
            builder.ToTable("DiscountType", "ppl");
            builder.Property(x => x.NameAr).HasMaxLength(100).IsRequired();
            builder.Property(x => x.NameEn).HasMaxLength(100).IsRequired();
            builder.Property(x => x.CapAmountPerStudent).HasColumnType("decimal(18,2)");
            builder.Property(x => x.MaxCombinedPercent).HasColumnType("decimal(5,2)");
            builder.HasOne<FeeCategory>().WithMany().HasForeignKey(x => x.FeeCategoryId);
            builder.HasMany(x => x.EligibilityRules).WithOne().HasForeignKey(x => x.DiscountTypeId);
        }
    }

    public class EligibilityRuleConfiguration : IEntityTypeConfiguration<EligibilityRule>
    {
        public void Configure(EntityTypeBuilder<EligibilityRule> builder)
        {
            builder.ToTable("EligibilityRule", "ppl");
            builder.Property(x => x.Percent).HasColumnType("decimal(5,2)");
        }
    }

    public class DiscountGrantConfiguration : IEntityTypeConfiguration<DiscountGrant>
    {
        public void Configure(EntityTypeBuilder<DiscountGrant> builder)
        {
            builder.ToTable("DiscountGrant", "ppl");
            builder.Property(x => x.BasisValue).HasColumnType("decimal(18,2)");
            builder.Property(x => x.AppliedAmount).HasColumnType("decimal(18,2)");
            builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            builder.Property(x => x.SponsorNote).HasMaxLength(500);
            builder.Property(x => x.EnvelopeOverrideReason).HasMaxLength(500);
            builder.Property(x => x.RevokedReason).HasMaxLength(500);
            builder.HasOne<DiscountType>().WithMany().HasForeignKey(x => x.DiscountTypeId);
            builder.HasOne<ScholarshipProgram>().WithMany().HasForeignKey(x => x.ScholarshipProgramId);
            builder.HasOne<DiscountGrant>().WithMany().HasForeignKey(x => x.RenewedFromGrantId);
            builder.HasIndex(x => new { x.StudentId, x.AcademicYearId });
        }
    }

    public class DiscountDocumentConfiguration : IEntityTypeConfiguration<DiscountDocument>
    {
        public void Configure(EntityTypeBuilder<DiscountDocument> builder)
        {
            builder.ToTable("DiscountDocument", "ppl");
            builder.Property(x => x.DocumentNo).HasMaxLength(30).IsRequired();
            builder.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            builder.HasOne<DiscountGrant>().WithMany().HasForeignKey(x => x.DiscountGrantId);
            builder.HasOne<Charge>().WithMany().HasForeignKey(x => x.ChargeId);
            builder.HasIndex(x => new { x.SchoolId, x.DocumentNo }).IsUnique();
            builder.HasIndex(x => x.ChargeId);
        }
    }

    public class ScholarshipProgramConfiguration : IEntityTypeConfiguration<ScholarshipProgram>
    {
        public void Configure(EntityTypeBuilder<ScholarshipProgram> builder)
        {
            builder.ToTable("ScholarshipProgram", "ppl");
            builder.Property(x => x.NameAr).HasMaxLength(100).IsRequired();
            builder.Property(x => x.NameEn).HasMaxLength(100).IsRequired();
            builder.Property(x => x.MaxTotalAmount).HasColumnType("decimal(18,2)");
            builder.HasOne<DiscountType>().WithMany().HasForeignKey(x => x.DiscountTypeId);
        }
    }

    public class WaiverConfiguration : IEntityTypeConfiguration<Waiver>
    {
        public void Configure(EntityTypeBuilder<Waiver> builder)
        {
            builder.ToTable("Waiver", "ppl");
            builder.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            builder.HasOne<Charge>().WithMany().HasForeignKey(x => x.ChargeId);
            builder.HasOne<CreditNote>().WithMany().HasForeignKey(x => x.CreditNoteId);
        }
    }

    public class RenewalQueueItemConfiguration : IEntityTypeConfiguration<RenewalQueueItem>
    {
        public void Configure(EntityTypeBuilder<RenewalQueueItem> builder)
        {
            builder.ToTable("RenewalQueueItem", "ppl");
            builder.Property(x => x.AdjustedBasisValue).HasColumnType("decimal(18,2)");
            builder.HasOne<DiscountGrant>().WithMany().HasForeignKey(x => x.PriorGrantId);
            builder.HasIndex(x => new { x.PriorGrantId, x.NewAcademicYearId }).IsUnique();
        }
    }

    public class StatementIssueConfiguration : IEntityTypeConfiguration<StatementIssue>
    {
        public void Configure(EntityTypeBuilder<StatementIssue> builder)
        {
            builder.ToTable("StatementIssue", "ppl");
            builder.Property(x => x.StatementNo).HasMaxLength(30).IsRequired();
            builder.Property(x => x.ClosingBalance).HasColumnType("decimal(18,2)");
            builder.Property(x => x.SnapshotJson).IsRequired();
            builder.HasOne<Payer>().WithMany().HasForeignKey(x => x.PayerId);
            builder.HasIndex(x => new { x.SchoolId, x.StatementNo }).IsUnique();
        }
    }
}
