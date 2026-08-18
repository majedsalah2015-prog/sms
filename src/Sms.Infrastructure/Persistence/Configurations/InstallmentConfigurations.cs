using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Fees;
using Sms.Domain.Installments;
using Sms.Domain.Payments;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // ppl schema per doc/Modules/20 — same schema group as Charge/Receipt.

    public class PlanTemplateConfiguration : IEntityTypeConfiguration<PlanTemplate>
    {
        public void Configure(EntityTypeBuilder<PlanTemplate> builder)
        {
            builder.ToTable("PlanTemplate", "ppl");
            builder.Property(x => x.NameAr).HasMaxLength(100).IsRequired();
            builder.Property(x => x.NameEn).HasMaxLength(100).IsRequired();
            builder.Property(x => x.DownPaymentPercent).HasColumnType("decimal(5,2)");
            builder.HasOne<FeeCategory>().WithMany().HasForeignKey(x => x.FeeCategoryId);
            builder.HasMany(x => x.Installments).WithOne().HasForeignKey(x => x.PlanTemplateId);
            builder.HasIndex(x => new { x.SchoolId, x.AcademicYearId });
        }
    }

    public class TemplateInstallmentConfiguration : IEntityTypeConfiguration<TemplateInstallment>
    {
        public void Configure(EntityTypeBuilder<TemplateInstallment> builder)
        {
            builder.ToTable("TemplateInstallment", "ppl");
            builder.Property(x => x.SplitPercent).HasColumnType("decimal(5,2)");
            builder.HasIndex(x => new { x.PlanTemplateId, x.SequenceNumber }).IsUnique();
        }
    }

    public class PlanAssignmentConfiguration : IEntityTypeConfiguration<PlanAssignment>
    {
        public void Configure(EntityTypeBuilder<PlanAssignment> builder)
        {
            builder.ToTable("PlanAssignment", "ppl");
            builder.Property(x => x.ExceptionReason).HasMaxLength(500);
            builder.HasOne<PlanTemplate>().WithMany().HasForeignKey(x => x.PlanTemplateId);
            builder.HasOne<Payer>().WithMany().HasForeignKey(x => x.PayerId);
            builder.HasMany(x => x.Installments).WithOne().HasForeignKey(x => x.PlanAssignmentId);
            builder.HasIndex(x => new { x.StudentId, x.AcademicYearId, x.FeeCategoryId }).IsUnique();
        }
    }

    public class InstallmentConfiguration : IEntityTypeConfiguration<Installment>
    {
        public void Configure(EntityTypeBuilder<Installment> builder)
        {
            builder.ToTable("Installment", "ppl");
            builder.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            builder.Property(x => x.WriteOffReason).HasMaxLength(500);
            builder.HasOne<Pdc>().WithMany().HasForeignKey(x => x.CoveringPdcId);
            builder.HasMany(x => x.ChargeLines).WithOne().HasForeignKey(x => x.InstallmentId);
            builder.HasIndex(x => new { x.PlanAssignmentId, x.DueDate });
            // DB/04 §1 (S8/E-802): dunning + collection calendar. The doc's "(PayerId, DueDate, Status) / WHERE unpaid" can't be
            // written literally — Installment has no PayerId (it's on PlanAssignment) and no stored status (BR-INS-007 derives it) —
            // so the closest honest form is school + due date over the rows that can still be owed.
            builder.HasIndex(x => new { x.SchoolId, x.DueDate }, "IX_Installment_School_DueDate_Open")
                .HasFilter("[IsSuperseded] = 0 AND [IsWrittenOff] = 0");
        }
    }

    public class InstallmentChargeLineConfiguration : IEntityTypeConfiguration<InstallmentChargeLine>
    {
        public void Configure(EntityTypeBuilder<InstallmentChargeLine> builder)
        {
            builder.ToTable("InstallmentChargeLine", "ppl");
            builder.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            builder.HasOne<Charge>().WithMany().HasForeignKey(x => x.ChargeId);
            builder.HasIndex(x => x.ChargeId);
        }
    }

    public class ScheduleRevisionConfiguration : IEntityTypeConfiguration<ScheduleRevision>
    {
        public void Configure(EntityTypeBuilder<ScheduleRevision> builder)
        {
            builder.ToTable("ScheduleRevision", "ppl");
            builder.Property(x => x.Reason).HasMaxLength(500);
            builder.Property(x => x.BeforeJson).IsRequired();
            builder.Property(x => x.AfterJson).IsRequired();
            builder.HasOne<PlanAssignment>().WithMany().HasForeignKey(x => x.PlanAssignmentId);
        }
    }

    public class RescheduleCaseConfiguration : IEntityTypeConfiguration<RescheduleCase>
    {
        public void Configure(EntityTypeBuilder<RescheduleCase> builder)
        {
            builder.ToTable("RescheduleCase", "ppl");
            builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            builder.Property(x => x.DecisionReason).HasMaxLength(500);
            builder.Property(x => x.RemainderAmount).HasColumnType("decimal(18,2)");
            builder.Property(x => x.ProposedScheduleJson).IsRequired();
            builder.HasOne<PlanAssignment>().WithMany().HasForeignKey(x => x.PlanAssignmentId);
        }
    }

    public class PromiseToPayConfiguration : IEntityTypeConfiguration<PromiseToPay>
    {
        public void Configure(EntityTypeBuilder<PromiseToPay> builder)
        {
            builder.ToTable("PromiseToPay", "ppl");
            builder.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            builder.HasOne<Installment>().WithMany().HasForeignKey(x => x.InstallmentId);
        }
    }

    public class DunningEventConfiguration : IEntityTypeConfiguration<DunningEvent>
    {
        public void Configure(EntityTypeBuilder<DunningEvent> builder)
        {
            builder.ToTable("DunningEvent", "ppl");
            builder.HasOne<Installment>().WithMany().HasForeignKey(x => x.InstallmentId);
            builder.HasIndex(x => new { x.InstallmentId, x.Step }).IsUnique();
        }
    }
}
