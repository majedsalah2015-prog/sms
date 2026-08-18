using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Fees;
using Sms.Domain.Payments;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // ppl schema per doc/Modules/21 — same schema group as Fees.

    public class TillSessionConfiguration : IEntityTypeConfiguration<TillSession>
    {
        public void Configure(EntityTypeBuilder<TillSession> builder)
        {
            builder.ToTable("TillSession", "ppl");
            builder.Property(x => x.TillCode).HasMaxLength(20).IsRequired();
            builder.Property(x => x.FloatAmount).HasColumnType("decimal(18,4)");
            builder.Property(x => x.SystemTotal).HasColumnType("decimal(18,4)");
            builder.Property(x => x.CountedTotal).HasColumnType("decimal(18,4)");
            builder.Property(x => x.VarianceReason).HasMaxLength(500);
        }
    }

    public class ReceiptConfiguration : IEntityTypeConfiguration<Receipt>
    {
        public void Configure(EntityTypeBuilder<Receipt> builder)
        {
            builder.ToTable("Receipt", "ppl");
            builder.Property(x => x.ReceiptNo).HasMaxLength(30).IsRequired();
            builder.HasIndex(x => new { x.SchoolId, x.ReceiptNo }).IsUnique();
            // DB/04 §1 (S8/E-802): payer history / day close (doc's PostedAtUtc is IssuedAtUtc here). TillSessionId is FK-indexed by convention.
            builder.HasIndex(x => new { x.PayerId, x.IssuedAtUtc }, "IX_Receipt_Payer_IssuedAt");
            builder.Property(x => x.MethodRefNo).HasMaxLength(60);
            builder.Property(x => x.Amount).HasColumnType("decimal(18,4)");
            builder.HasOne<Payer>().WithMany().HasForeignKey(x => x.PayerId);
            builder.HasOne<TillSession>().WithMany().HasForeignKey(x => x.TillSessionId);
        }
    }

    public class PaymentAllocationConfiguration : IEntityTypeConfiguration<PaymentAllocation>
    {
        public void Configure(EntityTypeBuilder<PaymentAllocation> builder)
        {
            builder.ToTable("PaymentAllocation", "ppl");
            builder.Property(x => x.AllocatedAmount).HasColumnType("decimal(18,4)");
            builder.HasOne<Receipt>().WithMany().HasForeignKey(x => x.ReceiptId);
            builder.HasOne<Charge>().WithMany().HasForeignKey(x => x.ChargeId);
        }
    }

    public class PdcConfiguration : IEntityTypeConfiguration<Pdc>
    {
        public void Configure(EntityTypeBuilder<Pdc> builder)
        {
            builder.ToTable("Pdc", "ppl");
            builder.Property(x => x.BankName).HasMaxLength(100).IsRequired();
            builder.Property(x => x.ChequeNo).HasMaxLength(30).IsRequired();
            builder.Property(x => x.Amount).HasColumnType("decimal(18,4)");
            builder.HasOne<Payer>().WithMany().HasForeignKey(x => x.PayerId);
            builder.HasOne<Receipt>().WithMany().HasForeignKey(x => x.ClearedReceiptId);
        }
    }

    public class RefundVoucherConfiguration : IEntityTypeConfiguration<RefundVoucher>
    {
        public void Configure(EntityTypeBuilder<RefundVoucher> builder)
        {
            builder.ToTable("RefundVoucher", "ppl");
            builder.Property(x => x.VoucherNo).HasMaxLength(30).IsRequired();
            builder.HasIndex(x => new { x.SchoolId, x.VoucherNo }).IsUnique();
            builder.Property(x => x.Amount).HasColumnType("decimal(18,4)");
            builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            builder.HasOne<Payer>().WithMany().HasForeignKey(x => x.PayerId);
        }
    }
}
