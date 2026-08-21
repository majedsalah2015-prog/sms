using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.GlExport;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // fin schema: the accounting hand-off tables (E-503).

    public class GlAccountMappingConfiguration : IEntityTypeConfiguration<GlAccountMapping>
    {
        public void Configure(EntityTypeBuilder<GlAccountMapping> builder)
        {
            builder.ToTable("GlAccountMapping", "fin");
            builder.Property(x => x.Key).HasMaxLength(60).IsRequired();
            builder.Property(x => x.AccountCode).HasMaxLength(40).IsRequired();
            builder.Property(x => x.AccountNameAr).HasMaxLength(100).IsRequired();
            builder.Property(x => x.AccountNameEn).HasMaxLength(100).IsRequired();
            builder.HasIndex(x => new { x.SchoolId, x.Key }).IsUnique();
        }
    }

    public class GlExportBatchConfiguration : IEntityTypeConfiguration<GlExportBatch>
    {
        public void Configure(EntityTypeBuilder<GlExportBatch> builder)
        {
            builder.ToTable("GlExportBatch", "fin");
            builder.Property(x => x.BatchNo).HasMaxLength(30).IsRequired();
            builder.Property(x => x.ContentHash).HasMaxLength(64).IsRequired();
            builder.Property(x => x.VoidReason).HasMaxLength(500);
            // 30 is the ledger's own cap on a document number (its DocumentSequence refuses longer).
            builder.Property(x => x.PostedJournalNo).HasMaxLength(30);
            builder.Property(x => x.ReversalJournalNo).HasMaxLength(30);
            builder.Property(x => x.TotalDebit).HasColumnType("decimal(18,2)");
            builder.Property(x => x.TotalCredit).HasColumnType("decimal(18,2)");
            builder.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.GlExportBatchId);
            builder.HasIndex(x => new { x.SchoolId, x.BatchNo }).IsUnique();
        }
    }

    public class GlJournalLineConfiguration : IEntityTypeConfiguration<GlJournalLine>
    {
        public void Configure(EntityTypeBuilder<GlJournalLine> builder)
        {
            builder.ToTable("GlJournalLine", "fin");
            builder.Property(x => x.AccountKey).HasMaxLength(60).IsRequired();
            builder.Property(x => x.AccountCode).HasMaxLength(40).IsRequired();
            builder.Property(x => x.Description).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Debit).HasColumnType("decimal(18,2)");
            builder.Property(x => x.Credit).HasColumnType("decimal(18,2)");
        }
    }
}
