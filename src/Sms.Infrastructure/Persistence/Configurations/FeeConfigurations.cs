using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Fees;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // ppl schema per doc/Modules/19 — same schema group as Student/Parent/Application.
    // Money columns are decimal(18,4) per doc/Database/01 DB-4; VAT rate is decimal(6,4) (a fraction, not a percentage).

    public class FeeCategoryConfiguration : IEntityTypeConfiguration<FeeCategory>
    {
        public void Configure(EntityTypeBuilder<FeeCategory> builder)
        {
            builder.ToTable("FeeCategory", "ppl");
            builder.Property(x => x.NameAr).HasMaxLength(100).IsRequired();
            builder.Property(x => x.NameEn).HasMaxLength(100).IsRequired();
            builder.Property(x => x.VatRate).HasColumnType("decimal(6,4)");
            builder.Property(x => x.GlExportCode).HasMaxLength(30);
        }
    }

    public class FeeStructureLineConfiguration : IEntityTypeConfiguration<FeeStructureLine>
    {
        public void Configure(EntityTypeBuilder<FeeStructureLine> builder)
        {
            builder.ToTable("FeeStructureLine", "ppl");
            builder.Property(x => x.Amount).HasColumnType("decimal(18,4)");
            builder.HasOne<FeeCategory>().WithMany().HasForeignKey(x => x.FeeCategoryId);

            // Doc §9: no duplicate active structure per grade-year-category.
            builder.HasIndex(x => new { x.GradeYearProfileId, x.FeeCategoryId }, "IX_FeeStructureLine_Profile_Category").IsUnique();
        }
    }

    public class PayerConfiguration : IEntityTypeConfiguration<Payer>
    {
        public void Configure(EntityTypeBuilder<Payer> builder)
        {
            builder.ToTable("Payer", "ppl");
        }
    }

    public class ChargeConfiguration : IEntityTypeConfiguration<Charge>
    {
        public void Configure(EntityTypeBuilder<Charge> builder)
        {
            builder.ToTable("Charge", "ppl");
            builder.Property(x => x.ChargeNo).HasMaxLength(30).IsRequired();
            builder.HasIndex(x => new { x.SchoolId, x.ChargeNo }).IsUnique();
            builder.Property(x => x.NetAmount).HasColumnType("decimal(18,4)");
            builder.Property(x => x.VatRateSnapshot).HasColumnType("decimal(6,4)");
            builder.Property(x => x.VatAmount).HasColumnType("decimal(18,4)");
            builder.Property(x => x.GrossAmount).HasColumnType("decimal(18,4)");
            builder.Property(x => x.InvoiceHash).HasMaxLength(64);
            builder.Property(x => x.PreviousInvoiceHash).HasMaxLength(64);
            builder.HasOne<FeeCategory>().WithMany().HasForeignKey(x => x.FeeCategoryId);
            builder.HasOne<Payer>().WithMany().HasForeignKey(x => x.PayerId);
            builder.HasIndex(x => x.StudentId);
        }
    }

    public class CreditNoteConfiguration : IEntityTypeConfiguration<CreditNote>
    {
        public void Configure(EntityTypeBuilder<CreditNote> builder)
        {
            builder.ToTable("CreditNote", "ppl");
            builder.Property(x => x.CreditNoteNo).HasMaxLength(30).IsRequired();
            builder.HasIndex(x => new { x.SchoolId, x.CreditNoteNo }).IsUnique();
            builder.Property(x => x.Amount).HasColumnType("decimal(18,4)");
            builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            builder.HasOne<Charge>().WithMany().HasForeignKey(x => x.ChargeId);
        }
    }
}
