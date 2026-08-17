using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Fees;
using Sms.Domain.Payments;
using Sms.Domain.Store;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // svc schema per docs/Database/02-ER-Model §6.

    public class StoreItemConfiguration : IEntityTypeConfiguration<StoreItem>
    {
        public void Configure(EntityTypeBuilder<StoreItem> builder)
        {
            builder.ToTable("StoreItem", "svc");
            builder.Property(x => x.NameAr).HasMaxLength(150).IsRequired();
            builder.Property(x => x.NameEn).HasMaxLength(150).IsRequired();
            builder.HasOne<FeeCategory>().WithMany().HasForeignKey(x => x.FeeCategoryId);
            builder.HasMany(x => x.Variants).WithOne().HasForeignKey(x => x.StoreItemId);
        }
    }

    public class StoreVariantConfiguration : IEntityTypeConfiguration<StoreVariant>
    {
        public void Configure(EntityTypeBuilder<StoreVariant> builder)
        {
            builder.ToTable("Variant", "svc");
            builder.Property(x => x.Sku).HasMaxLength(40).IsRequired();
            builder.Property(x => x.Barcode).HasMaxLength(40);
            builder.Property(x => x.Size).HasMaxLength(20);
            builder.Property(x => x.Color).HasMaxLength(30);
            builder.HasIndex(x => new { x.SchoolId, x.Sku }).IsUnique();
        }
    }

    public class PriceListConfiguration : IEntityTypeConfiguration<PriceList>
    {
        public void Configure(EntityTypeBuilder<PriceList> builder)
        {
            builder.ToTable("PriceList", "svc");
            builder.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.PriceListId);
            builder.HasIndex(x => new { x.SchoolId, x.Version }).IsUnique();
        }
    }

    public class PriceListLineConfiguration : IEntityTypeConfiguration<PriceListLine>
    {
        public void Configure(EntityTypeBuilder<PriceListLine> builder)
        {
            builder.ToTable("PriceListLine", "svc");
            builder.Property(x => x.Price).HasColumnType("decimal(9,2)");
            builder.HasOne<StoreItem>().WithMany().HasForeignKey(x => x.StoreItemId);
        }
    }

    public class BundleConfiguration : IEntityTypeConfiguration<Bundle>
    {
        public void Configure(EntityTypeBuilder<Bundle> builder)
        {
            builder.ToTable("Bundle", "svc");
            builder.Property(x => x.NameAr).HasMaxLength(150).IsRequired();
            builder.Property(x => x.NameEn).HasMaxLength(150).IsRequired();
            builder.Property(x => x.Price).HasColumnType("decimal(18,2)");
            builder.HasOne<FeeCategory>().WithMany().HasForeignKey(x => x.FeeCategoryId);
            builder.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.BundleId);
        }
    }

    public class BundleLineConfiguration : IEntityTypeConfiguration<BundleLine>
    {
        public void Configure(EntityTypeBuilder<BundleLine> builder)
        {
            builder.ToTable("BundleLine", "svc");
            builder.HasOne<StoreItem>().WithMany().HasForeignKey(x => x.StoreItemId);
        }
    }

    public class BundleAssignmentConfiguration : IEntityTypeConfiguration<BundleAssignment>
    {
        public void Configure(EntityTypeBuilder<BundleAssignment> builder)
        {
            builder.ToTable("BundleAssignment", "svc");
            builder.HasOne<Bundle>().WithMany().HasForeignKey(x => x.BundleId);
            builder.HasOne<Charge>().WithMany().HasForeignKey(x => x.ChargeId);
            builder.HasOne<CreditNote>().WithMany().HasForeignKey(x => x.CreditNoteId);
            builder.HasIndex(x => new { x.BundleId, x.StudentId }).IsUnique();
        }
    }

    public class DistributionSessionConfiguration : IEntityTypeConfiguration<DistributionSession>
    {
        public void Configure(EntityTypeBuilder<DistributionSession> builder)
        {
            builder.ToTable("DistributionSession", "svc");
            builder.HasOne<Bundle>().WithMany().HasForeignKey(x => x.BundleId);
        }
    }

    public class HandoutRecordConfiguration : IEntityTypeConfiguration<HandoutRecord>
    {
        public void Configure(EntityTypeBuilder<HandoutRecord> builder)
        {
            builder.ToTable("HandoutRecord", "svc");
            builder.HasOne<DistributionSession>().WithMany().HasForeignKey(x => x.DistributionSessionId);
            builder.HasOne<BundleAssignment>().WithMany().HasForeignKey(x => x.BundleAssignmentId);
            builder.HasOne<StoreVariant>().WithMany().HasForeignKey(x => x.StoreVariantId);
        }
    }

    public class StoreSaleConfiguration : IEntityTypeConfiguration<StoreSale>
    {
        public void Configure(EntityTypeBuilder<StoreSale> builder)
        {
            builder.ToTable("StoreSale", "svc");
            builder.Property(x => x.Total).HasColumnType("decimal(18,2)");
            builder.Property(x => x.VoidReason).HasMaxLength(500);
            builder.Property(x => x.FinanceOverrideReason).HasMaxLength(500);
            builder.HasOne<Payer>().WithMany().HasForeignKey(x => x.PayerId);
            builder.HasOne<TillSession>().WithMany().HasForeignKey(x => x.TillSessionId);
            builder.HasOne<Charge>().WithMany().HasForeignKey(x => x.ChargeId);
            builder.HasOne<Receipt>().WithMany().HasForeignKey(x => x.ReceiptId);
            builder.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.StoreSaleId);
        }
    }

    public class StoreSaleLineConfiguration : IEntityTypeConfiguration<StoreSaleLine>
    {
        public void Configure(EntityTypeBuilder<StoreSaleLine> builder)
        {
            builder.ToTable("StoreSaleLine", "svc");
            builder.Property(x => x.UnitPrice).HasColumnType("decimal(9,2)");
            builder.Property(x => x.LineTotal).HasColumnType("decimal(18,2)");
            builder.HasOne<StoreVariant>().WithMany().HasForeignKey(x => x.StoreVariantId);
        }
    }

    public class ReturnExchangeConfiguration : IEntityTypeConfiguration<ReturnExchange>
    {
        public void Configure(EntityTypeBuilder<ReturnExchange> builder)
        {
            builder.ToTable("ReturnExchange", "svc");
            builder.HasOne<StoreSaleLine>().WithMany().HasForeignKey(x => x.StoreSaleLineId);
            builder.HasOne<StoreVariant>().WithMany().HasForeignKey(x => x.NewStoreVariantId);
            builder.HasOne<CreditNote>().WithMany().HasForeignKey(x => x.CreditNoteId);
        }
    }

    public class ReturnPolicyConfiguration : IEntityTypeConfiguration<ReturnPolicy>
    {
        public void Configure(EntityTypeBuilder<ReturnPolicy> builder)
        {
            builder.ToTable("StoreReturnPolicy", "svc");
            builder.HasIndex(x => new { x.SchoolId, x.Category }).IsUnique();
        }
    }

    public class AccountChargePolicyConfiguration : IEntityTypeConfiguration<AccountChargePolicy>
    {
        public void Configure(EntityTypeBuilder<AccountChargePolicy> builder)
        {
            builder.ToTable("StoreAccountChargePolicy", "svc");
            builder.Property(x => x.CapPerSale).HasColumnType("decimal(9,2)");
            builder.HasIndex(x => new { x.SchoolId, x.Category }).IsUnique();
        }
    }

    public class StoreStockMovementConfiguration : IEntityTypeConfiguration<StoreStockMovement>
    {
        public void Configure(EntityTypeBuilder<StoreStockMovement> builder)
        {
            builder.ToTable("StoreStockMovement", "svc");
            builder.Property(x => x.Reason).HasMaxLength(500);
            builder.HasOne<StoreVariant>().WithMany().HasForeignKey(x => x.StoreVariantId);
            builder.HasIndex(x => x.StoreVariantId);
        }
    }
}
