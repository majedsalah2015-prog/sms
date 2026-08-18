using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Schools;
using Sms.Domain.Setup;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // core schema per docs/Database/03 §B (core (34)) — SchoolSetting, CountryPack, FeatureToggle, SetupChecklist.

    public class CountryPackConfiguration : IEntityTypeConfiguration<CountryPack>
    {
        public void Configure(EntityTypeBuilder<CountryPack> builder)
        {
            builder.ToTable("CountryPack", "core");
            builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
            builder.Property(x => x.CountryIsoCode).HasMaxLength(2).IsRequired();
            builder.Property(x => x.DefaultCurrencyCode).HasMaxLength(3).IsRequired();
            builder.Property(x => x.DefaultTimeZoneId).HasMaxLength(100).IsRequired();
            builder.Property(x => x.DefaultVatRate).HasColumnType("decimal(9,6)");
            builder.Property(x => x.RequiredIdTypeCodes).HasMaxLength(200);
            builder.Property(x => x.StatutoryReportCodes).HasMaxLength(1000);
            builder.Property(x => x.DefaultWorkingDays).HasMaxLength(100);
            builder.HasIndex(x => new { x.Code, x.Version }).IsUnique();
            builder.OwnsOne(x => x.Name, name =>
            {
                name.Property(n => n.NameAr).HasColumnName("NameAr").HasMaxLength(200).IsRequired();
                name.Property(n => n.NameEn).HasColumnName("NameEn").HasMaxLength(200).IsRequired();
            });
        }
    }

    public class SchoolSettingConfiguration : IEntityTypeConfiguration<SchoolSetting>
    {
        public void Configure(EntityTypeBuilder<SchoolSetting> builder)
        {
            builder.ToTable("SchoolSetting", "core");
            builder.Property(x => x.Key).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Value).HasMaxLength(2000).IsRequired();
            builder.HasOne<AcademicYear>().WithMany().HasForeignKey(x => x.AcademicYearId);
            // One default row and at most one row per year for a key. SQL Server
            // treats NULLs as equal in unique indexes, so the (SchoolId, Key,
            // AcademicYearId) uniqueness covers the single default row too.
            builder.HasIndex(x => new { x.SchoolId, x.Key, x.AcademicYearId }).IsUnique();
        }
    }

    public class FeatureToggleConfiguration : IEntityTypeConfiguration<FeatureToggle>
    {
        public void Configure(EntityTypeBuilder<FeatureToggle> builder)
        {
            builder.ToTable("FeatureToggle", "core");
            builder.Property(x => x.FeatureCode).HasMaxLength(50).IsRequired();
            builder.HasIndex(x => new { x.SchoolId, x.FeatureCode }).IsUnique();
        }
    }

    public class SetupChecklistConfiguration : IEntityTypeConfiguration<SetupChecklist>
    {
        public void Configure(EntityTypeBuilder<SetupChecklist> builder)
        {
            builder.ToTable("SetupChecklist", "core");
            builder.Property(x => x.StepCode).HasMaxLength(50).IsRequired();
            builder.Property(x => x.Notes).HasMaxLength(500);
            builder.HasIndex(x => new { x.SchoolId, x.StepCode }).IsUnique();
        }
    }
}
