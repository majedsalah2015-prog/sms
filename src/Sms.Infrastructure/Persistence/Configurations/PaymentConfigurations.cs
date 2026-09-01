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
            builder.HasOne<CollectionAccount>().WithMany().HasForeignKey(x => x.CollectionAccountId);
            // The daily collection report reads by account (doc/Modules/21 §10 "by till/method"), and
            // reconciliation reads one account's receipts against one bank statement.
            builder.HasIndex(x => new { x.CollectionAccountId, x.IssuedAtUtc }, "IX_Receipt_CollectionAccount_IssuedAt");
        }
    }

    public class CollectionAccountConfiguration : IEntityTypeConfiguration<CollectionAccount>
    {
        public void Configure(EntityTypeBuilder<CollectionAccount> builder)
        {
            builder.ToTable("CollectionAccount", "ppl");
            builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
            builder.Property(x => x.NameAr).HasMaxLength(120).IsRequired();
            builder.Property(x => x.NameEn).HasMaxLength(120).IsRequired();
            builder.Property(x => x.BankName).HasMaxLength(120);
            // An IBAN is 34 characters at its longest (ISO 13616); the account number is the bank's own
            // format and is stored as typed, spaces and all, because that is how it gets read out loud.
            builder.Property(x => x.AccountNo).HasMaxLength(40);
            builder.Property(x => x.Iban).HasMaxLength(40);
            builder.Property(x => x.GlExportCode).HasMaxLength(30);
            builder.HasIndex(x => new { x.SchoolId, x.Code }).IsUnique();
            // BankLookupId deliberately gets no FK, as every lookup reference in this model does
            // (see EmployeeConfiguration): a real FK would make retiring a catalogue value a
            // migration rather than a deactivation.
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
