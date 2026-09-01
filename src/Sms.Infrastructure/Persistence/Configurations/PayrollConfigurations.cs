using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Employees;
using Sms.Domain.Payroll;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // ppl schema, with ppl.Employee and ppl.Contract — payroll is Module 12's people, not the fin
    // schema's students and payers, and docs/Database/01 §2 puts employees here. Owner request,
    // 2026-08-28; see Sms.Domain.Payroll.PayrollRun for the deviation from doc/Modules/12 §2.
    //
    // Money columns are decimal(12,2) to match ppl.Contract.SalaryBasic, which is where every one
    // of them ultimately comes from; run totals get (14,2) because a school's whole payroll is two
    // orders of magnitude larger than one salary.

    public class PayrollRunConfiguration : IEntityTypeConfiguration<PayrollRun>
    {
        public void Configure(EntityTypeBuilder<PayrollRun> builder)
        {
            builder.ToTable("PayrollRun", "ppl");

            builder.Property(x => x.PayrollRunNo).HasMaxLength(30).IsRequired();
            builder.Property(x => x.Notes).HasMaxLength(500);

            builder.Property(x => x.TotalGross).HasColumnType("decimal(14,2)");
            builder.Property(x => x.TotalDeductions).HasColumnType("decimal(14,2)");
            builder.Property(x => x.TotalNet).HasColumnType("decimal(14,2)");

            builder.HasIndex(x => new { x.SchoolId, x.PayrollRunNo }).IsUnique();

            // One payroll per school per month. Filtered so a run opened by mistake can be
            // cancelled and the month re-opened — without the filter, a school's only way past a
            // typo would be to pay the wrong month or delete a numbered document.
            builder.HasIndex(x => new { x.SchoolId, x.PeriodYear, x.PeriodMonth })
                .IsUnique()
                .HasFilter("[Status] <> 4");
        }
    }

    public class PayrollRunLineConfiguration : IEntityTypeConfiguration<PayrollRunLine>
    {
        public void Configure(EntityTypeBuilder<PayrollRunLine> builder)
        {
            builder.ToTable("PayrollRunLine", "ppl");

            builder.Property(x => x.Notes).HasMaxLength(500);

            builder.Property(x => x.BasicSalary).HasColumnType("decimal(12,2)");
            builder.Property(x => x.Allowances).HasColumnType("decimal(12,2)");
            builder.Property(x => x.AdditionsTotal).HasColumnType("decimal(12,2)");
            builder.Property(x => x.DeductionsTotal).HasColumnType("decimal(12,2)");
            builder.Property(x => x.AdvanceDeduction).HasColumnType("decimal(12,2)");
            builder.Property(x => x.GrossPay).HasColumnType("decimal(12,2)");
            builder.Property(x => x.NetPay).HasColumnType("decimal(12,2)");

            builder.HasOne<PayrollRun>().WithMany().HasForeignKey(x => x.PayrollRunId);
            builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId);
            builder.HasOne<Contract>().WithMany().HasForeignKey(x => x.ContractId);

            // One line per employee per run. The database enforces it as well as the service,
            // because "it compiled" proves nothing about a uniqueness guarantee.
            builder.HasIndex(x => new { x.PayrollRunId, x.EmployeeId }).IsUnique();

            // The payslip history screen reads down one employee across runs.
            builder.HasIndex(x => new { x.SchoolId, x.EmployeeId }, "IX_PayrollRunLine_Employee");
        }
    }

    public class PayrollLineAdjustmentConfiguration : IEntityTypeConfiguration<PayrollLineAdjustment>
    {
        public void Configure(EntityTypeBuilder<PayrollLineAdjustment> builder)
        {
            builder.ToTable("PayrollLineAdjustment", "ppl");

            builder.Property(x => x.Description).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Amount).HasColumnType("decimal(12,2)");

            builder.HasOne<PayrollRunLine>().WithMany().HasForeignKey(x => x.PayrollRunLineId);

            builder.HasIndex(x => x.PayrollRunLineId);
        }
    }

    public class SalaryAdvanceConfiguration : IEntityTypeConfiguration<SalaryAdvance>
    {
        public void Configure(EntityTypeBuilder<SalaryAdvance> builder)
        {
            builder.ToTable("SalaryAdvance", "ppl");

            builder.Property(x => x.AdvanceNo).HasMaxLength(30).IsRequired();
            builder.Property(x => x.Reason).HasMaxLength(500);
            builder.Property(x => x.DecisionNote).HasMaxLength(500);
            builder.Property(x => x.DisbursementRefNo).HasMaxLength(60);

            builder.Property(x => x.Amount).HasColumnType("decimal(12,2)");

            builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId);

            builder.HasIndex(x => new { x.SchoolId, x.AdvanceNo }).IsUnique();

            // "Does this employee already owe us something" is asked on every new request and on
            // every payroll generation.
            builder.HasIndex(x => new { x.SchoolId, x.EmployeeId, x.Status }, "IX_SalaryAdvance_Employee_Status");
        }
    }

    public class SalaryAdvanceInstallmentConfiguration : IEntityTypeConfiguration<SalaryAdvanceInstallment>
    {
        public void Configure(EntityTypeBuilder<SalaryAdvanceInstallment> builder)
        {
            builder.ToTable("SalaryAdvanceInstallment", "ppl");

            builder.Property(x => x.Amount).HasColumnType("decimal(12,2)");
            builder.Property(x => x.WaiverNote).HasMaxLength(500);

            builder.HasOne<SalaryAdvance>().WithMany().HasForeignKey(x => x.SalaryAdvanceId);
            builder.HasOne<PayrollRunLine>().WithMany().HasForeignKey(x => x.PayrollRunLineId);

            builder.HasIndex(x => new { x.SalaryAdvanceId, x.SequenceNo }).IsUnique();

            // The payroll generator asks "what falls due in this month, still scheduled".
            builder.HasIndex(x => new { x.SchoolId, x.DueYear, x.DueMonth, x.Status }, "IX_SalaryAdvanceInstallment_Due");
        }
    }
}
