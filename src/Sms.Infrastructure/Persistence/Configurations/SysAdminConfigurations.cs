using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.SysAdmin;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // doc/Modules/36 — LicenseState/ImportBatch are per-school (core schema); MaintenanceWindow/PurgeExecution/LegalHold/DiagnosticsBundle are deployment-wide (ops schema, same group as JobDefinition/JobRun).

    public class LicenseStateConfiguration : IEntityTypeConfiguration<LicenseState>
    {
        public void Configure(EntityTypeBuilder<LicenseState> builder)
        {
            builder.ToTable("LicenseState", "core");
            builder.HasIndex(x => x.SchoolId).IsUnique();
        }
    }

    public class MaintenanceWindowConfiguration : IEntityTypeConfiguration<MaintenanceWindow>
    {
        public void Configure(EntityTypeBuilder<MaintenanceWindow> builder)
        {
            builder.ToTable("MaintenanceWindow", "ops");
            builder.Property(x => x.MessageAr).HasMaxLength(300).IsRequired();
            builder.Property(x => x.MessageEn).HasMaxLength(300).IsRequired();
        }
    }

    public class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
    {
        public void Configure(EntityTypeBuilder<ImportBatch> builder)
        {
            builder.ToTable("ImportBatch", "core");
            builder.Property(x => x.TemplateCode).HasMaxLength(50).IsRequired();
        }
    }

    public class PurgeExecutionConfiguration : IEntityTypeConfiguration<PurgeExecution>
    {
        public void Configure(EntityTypeBuilder<PurgeExecution> builder)
        {
            builder.ToTable("PurgeExecution", "ops");
            builder.Property(x => x.CertificateNo).HasMaxLength(20);
        }
    }

    public class LegalHoldConfiguration : IEntityTypeConfiguration<LegalHold>
    {
        public void Configure(EntityTypeBuilder<LegalHold> builder)
        {
            builder.ToTable("LegalHold", "ops");
            builder.Property(x => x.SubjectReference).HasMaxLength(100).IsRequired();
        }
    }

    public class DiagnosticsBundleConfiguration : IEntityTypeConfiguration<DiagnosticsBundle>
    {
        public void Configure(EntityTypeBuilder<DiagnosticsBundle> builder)
        {
            builder.ToTable("DiagnosticsBundle", "ops");
            builder.Property(x => x.Reference).HasMaxLength(64).IsRequired();
        }
    }
}
