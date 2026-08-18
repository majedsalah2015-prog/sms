using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Cafeteria;
using Sms.Domain.Fees;
using Sms.Domain.Payments;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // svc schema per docs/Database/02-ER-Model §6.

    public class CafeteriaItemConfiguration : IEntityTypeConfiguration<CafeteriaItem>
    {
        public void Configure(EntityTypeBuilder<CafeteriaItem> builder)
        {
            builder.ToTable("CafeteriaItem", "svc");
            builder.Property(x => x.NameAr).HasMaxLength(150).IsRequired();
            builder.Property(x => x.NameEn).HasMaxLength(150).IsRequired();
            builder.Property(x => x.Category).HasMaxLength(50).IsRequired();
            builder.Property(x => x.Price).HasColumnType("decimal(9,2)");
            builder.Property(x => x.AllergenTags).HasMaxLength(300);
        }
    }

    public class MenuConfiguration : IEntityTypeConfiguration<Menu>
    {
        public void Configure(EntityTypeBuilder<Menu> builder)
        {
            builder.ToTable("Menu", "svc");
            builder.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.MenuId);
            builder.HasIndex(x => new { x.SchoolId, x.Date }).IsUnique();
        }
    }

    public class MenuLineConfiguration : IEntityTypeConfiguration<MenuLine>
    {
        public void Configure(EntityTypeBuilder<MenuLine> builder)
        {
            builder.ToTable("MenuLine", "svc");
            builder.HasOne<CafeteriaItem>().WithMany().HasForeignKey(x => x.CafeteriaItemId);
        }
    }

    public class WalletConfiguration : IEntityTypeConfiguration<Wallet>
    {
        public void Configure(EntityTypeBuilder<Wallet> builder)
        {
            builder.ToTable("Wallet", "svc");
            builder.Property(x => x.OverdraftAllowance).HasColumnType("decimal(9,2)");
            builder.HasIndex(x => new { x.SchoolId, x.HolderKind, x.HolderId }).IsUnique();
        }
    }

    public class WalletLedgerEntryConfiguration : IEntityTypeConfiguration<WalletLedgerEntry>
    {
        public void Configure(EntityTypeBuilder<WalletLedgerEntry> builder)
        {
            builder.ToTable("WalletLedger", "svc");
            builder.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            builder.Property(x => x.Reason).HasMaxLength(500);
            builder.HasOne<Wallet>().WithMany().HasForeignKey(x => x.WalletId);
            builder.HasOne<Receipt>().WithMany().HasForeignKey(x => x.ReceiptId);
            builder.HasOne<RefundVoucher>().WithMany().HasForeignKey(x => x.RefundVoucherId);
            // DB/04 §1 (S8/E-802): balance derivation walks a wallet's ledger in insertion order (BR-CAF-007, ledger-derived balance).
            builder.HasIndex(x => new { x.WalletId, x.Id }, "IX_WalletLedger_Wallet_Id");
        }
    }

    public class SpendControlConfiguration : IEntityTypeConfiguration<SpendControl>
    {
        public void Configure(EntityTypeBuilder<SpendControl> builder)
        {
            builder.ToTable("SpendControl", "svc");
            builder.Property(x => x.DailyLimit).HasColumnType("decimal(9,2)");
            builder.Property(x => x.BlockedCategories).HasMaxLength(300);
            builder.HasIndex(x => new { x.SchoolId, x.StudentId }).IsUnique();
        }
    }

    public class SaleConfiguration : IEntityTypeConfiguration<Sale>
    {
        public void Configure(EntityTypeBuilder<Sale> builder)
        {
            builder.ToTable("Sale", "svc");
            builder.Property(x => x.Total).HasColumnType("decimal(18,2)");
            builder.Property(x => x.VoidReason).HasMaxLength(500);
            builder.HasOne<TillSession>().WithMany().HasForeignKey(x => x.TillSessionId);
            builder.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.SaleId);
            builder.HasIndex(x => new { x.HolderKind, x.HolderId, x.AtUtc });
        }
    }

    public class SaleLineConfiguration : IEntityTypeConfiguration<SaleLine>
    {
        public void Configure(EntityTypeBuilder<SaleLine> builder)
        {
            builder.ToTable("SaleLine", "svc");
            builder.Property(x => x.UnitPrice).HasColumnType("decimal(9,2)");
            builder.Property(x => x.LineTotal).HasColumnType("decimal(18,2)");
            builder.HasOne<CafeteriaItem>().WithMany().HasForeignKey(x => x.CafeteriaItemId);
        }
    }

    public class MealPlanConfiguration : IEntityTypeConfiguration<MealPlan>
    {
        public void Configure(EntityTypeBuilder<MealPlan> builder)
        {
            builder.ToTable("MealPlan", "svc");
            builder.Property(x => x.NameAr).HasMaxLength(100).IsRequired();
            builder.Property(x => x.NameEn).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Price).HasColumnType("decimal(18,2)");
            builder.Property(x => x.DailyValueCap).HasColumnType("decimal(9,2)");
            builder.HasOne<FeeCategory>().WithMany().HasForeignKey(x => x.FeeCategoryId);
        }
    }

    public class MealPlanSubscriptionConfiguration : IEntityTypeConfiguration<MealPlanSubscription>
    {
        public void Configure(EntityTypeBuilder<MealPlanSubscription> builder)
        {
            builder.ToTable("MealPlanSubscription", "svc");
            builder.HasOne<MealPlan>().WithMany().HasForeignKey(x => x.MealPlanId);
            builder.HasOne<Charge>().WithMany().HasForeignKey(x => x.ChargeId);
        }
    }

    public class RedemptionConfiguration : IEntityTypeConfiguration<Redemption>
    {
        public void Configure(EntityTypeBuilder<Redemption> builder)
        {
            builder.ToTable("Redemption", "svc");
            builder.HasOne<MealPlanSubscription>().WithMany().HasForeignKey(x => x.MealPlanSubscriptionId);
            builder.HasOne<Sale>().WithMany().HasForeignKey(x => x.SaleId);
            builder.HasIndex(x => new { x.MealPlanSubscriptionId, x.Date }).IsUnique();
        }
    }

    public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
    {
        public void Configure(EntityTypeBuilder<StockMovement> builder)
        {
            builder.ToTable("StockMovement", "svc");
            builder.Property(x => x.Reason).HasMaxLength(500);
            builder.HasOne<CafeteriaItem>().WithMany().HasForeignKey(x => x.CafeteriaItemId);
            builder.HasIndex(x => x.CafeteriaItemId);
        }
    }
}
