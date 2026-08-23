using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Employees;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // ppl schema per doc/Modules/12 — same schema group as Student/Parent/Application.

    public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
    {
        public void Configure(EntityTypeBuilder<Employee> builder)
        {
            builder.ToTable("Employee", "ppl");
            builder.Property(x => x.EmployeeNo).HasMaxLength(20).IsRequired();
            builder.HasIndex(x => new { x.SchoolId, x.EmployeeNo }).IsUnique();

            foreach (var name in new[]
            {
                nameof(Employee.FirstNameAr), nameof(Employee.FatherNameAr), nameof(Employee.GrandfatherNameAr), nameof(Employee.FamilyNameAr),
                nameof(Employee.FirstNameEn), nameof(Employee.FatherNameEn), nameof(Employee.GrandfatherNameEn), nameof(Employee.FamilyNameEn),
            })
            {
                builder.Property(name).HasMaxLength(60).IsRequired();
            }

            builder.Property(x => x.PrimaryIdNo).HasMaxLength(30);

            // Disbursement details (doc/Modules/12 §7 extension, owner request 2026-08-23).
            // Both nullable: a school importing an old register rarely has them for everyone, and
            // an employee who is paid in cash has neither. Lengths are generous rather than
            // country-specific — an IBAN is 34 characters at most, and the bank's own name is
            // written however the school writes it.
            builder.Property(x => x.BankName).HasMaxLength(120);
            builder.Property(x => x.BankAccountNo).HasMaxLength(40);
        }
    }

    public class OrgUnitConfiguration : IEntityTypeConfiguration<OrgUnit>
    {
        public void Configure(EntityTypeBuilder<OrgUnit> builder)
        {
            builder.ToTable("OrgUnit", "ppl");
            builder.Property(x => x.NameAr).HasMaxLength(100).IsRequired();
            builder.Property(x => x.NameEn).HasMaxLength(100).IsRequired();
        }
    }

    public class EmployeeAssignmentConfiguration : IEntityTypeConfiguration<EmployeeAssignment>
    {
        public void Configure(EntityTypeBuilder<EmployeeAssignment> builder)
        {
            builder.ToTable("EmployeeAssignment", "ppl");
            builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId);
            builder.HasOne<OrgUnit>().WithMany().HasForeignKey(x => x.OrgUnitId);

            // BR-EMP-002: at most one CURRENT assignment per employee.
            builder.HasIndex(x => x.EmployeeId, "IX_EmployeeAssignment_Employee_Current")
                .IsUnique()
                .HasFilter("[EffectiveToUtc] IS NULL");
        }
    }

    public class ContractConfiguration : IEntityTypeConfiguration<Contract>
    {
        public void Configure(EntityTypeBuilder<Contract> builder)
        {
            builder.ToTable("Contract", "ppl");
            builder.Property(x => x.SalaryBasic).HasColumnType("decimal(12,2)");
            builder.Property(x => x.SalaryAllowances).HasColumnType("decimal(12,2)");
            builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId);
            builder.HasIndex(x => x.EmployeeId);
        }
    }

    public class QualificationConfiguration : IEntityTypeConfiguration<Qualification>
    {
        public void Configure(EntityTypeBuilder<Qualification> builder)
        {
            builder.ToTable("Qualification", "ppl");
            builder.Property(x => x.TitleAr).HasMaxLength(150).IsRequired();
            builder.Property(x => x.TitleEn).HasMaxLength(150).IsRequired();
            builder.Property(x => x.InstitutionName).HasMaxLength(150);
            builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId);
        }
    }
}
