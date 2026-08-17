using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Health;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // svc schema per docs/Database/02-ER-Model §6.

    public class MedicalFileConfiguration : IEntityTypeConfiguration<MedicalFile>
    {
        public void Configure(EntityTypeBuilder<MedicalFile> builder)
        {
            builder.ToTable("MedicalFile", "svc");
            builder.Property(x => x.BloodType).HasMaxLength(5);
            builder.Property(x => x.EmergencyBannerAr).HasMaxLength(1000);
            builder.Property(x => x.EmergencyBannerEn).HasMaxLength(1000);
            builder.HasMany(x => x.Allergies).WithOne().HasForeignKey(x => x.MedicalFileId);
            builder.HasMany(x => x.Conditions).WithOne().HasForeignKey(x => x.MedicalFileId);
            builder.HasIndex(x => new { x.SchoolId, x.StudentId }).IsUnique();
        }
    }

    public class AllergyConfiguration : IEntityTypeConfiguration<Allergy>
    {
        public void Configure(EntityTypeBuilder<Allergy> builder)
        {
            builder.ToTable("Allergy", "svc");
            builder.Property(x => x.Substance).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Notes).HasMaxLength(1000);
        }
    }

    public class MedicalConditionConfiguration : IEntityTypeConfiguration<MedicalCondition>
    {
        public void Configure(EntityTypeBuilder<MedicalCondition> builder)
        {
            builder.ToTable("MedicalCondition", "svc");
            builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Notes).HasMaxLength(1000);
        }
    }

    public class CarePlanConfiguration : IEntityTypeConfiguration<CarePlan>
    {
        public void Configure(EntityTypeBuilder<CarePlan> builder)
        {
            builder.ToTable("CarePlan", "svc");
            builder.Property(x => x.ConditionName).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Triggers).HasMaxLength(2000).IsRequired();
            builder.Property(x => x.ResponseSteps).HasMaxLength(4000).IsRequired();
            builder.Property(x => x.EmergencyContactsNote).HasMaxLength(1000);
            builder.HasOne<MedicalFile>().WithMany().HasForeignKey(x => x.MedicalFileId);
        }
    }

    public class ClinicVisitConfiguration : IEntityTypeConfiguration<ClinicVisit>
    {
        public void Configure(EntityTypeBuilder<ClinicVisit> builder)
        {
            builder.ToTable("ClinicVisit", "svc");
            builder.Property(x => x.VisitNo).HasMaxLength(30).IsRequired();
            builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            builder.Property(x => x.TriageNotes).HasMaxLength(4000);
            builder.Property(x => x.BloodPressure).HasMaxLength(20);
            builder.Property(x => x.TemperatureC).HasColumnType("decimal(4,1)");
            builder.Property(x => x.PickupVerifiedByName).HasMaxLength(150);
            builder.Property(x => x.PickupExceptionNote).HasMaxLength(1000);
            builder.HasOne<MedicalFile>().WithMany().HasForeignKey(x => x.MedicalFileId);
            builder.HasIndex(x => new { x.SchoolId, x.VisitNo }).IsUnique();
        }
    }

    public class MedicationAuthorizationConfiguration : IEntityTypeConfiguration<MedicationAuthorization>
    {
        public void Configure(EntityTypeBuilder<MedicationAuthorization> builder)
        {
            builder.ToTable("MedicationAuthorization", "svc");
            builder.Property(x => x.MedicationName).HasMaxLength(200).IsRequired();
            builder.Property(x => x.DoseUnit).HasMaxLength(20).IsRequired();
            builder.Property(x => x.ScheduleTimes).HasMaxLength(200).IsRequired();
            builder.Property(x => x.DosePerAdministration).HasColumnType("decimal(9,3)");
            builder.HasOne<MedicalFile>().WithMany().HasForeignKey(x => x.MedicalFileId);
        }
    }

    public class AdministrationLogConfiguration : IEntityTypeConfiguration<AdministrationLog>
    {
        public void Configure(EntityTypeBuilder<AdministrationLog> builder)
        {
            builder.ToTable("AdministrationLog", "svc");
            builder.Property(x => x.DoseGiven).HasColumnType("decimal(9,3)");
            builder.Property(x => x.DeviationReason).HasMaxLength(500);
            builder.HasOne<MedicationAuthorization>().WithMany().HasForeignKey(x => x.MedicationAuthorizationId);
        }
    }

    public class VaccinationScheduleEntryConfiguration : IEntityTypeConfiguration<VaccinationScheduleEntry>
    {
        public void Configure(EntityTypeBuilder<VaccinationScheduleEntry> builder)
        {
            builder.ToTable("VaccinationScheduleEntry", "svc");
            builder.Property(x => x.VaccineCode).HasMaxLength(30).IsRequired();
            builder.HasIndex(x => new { x.SchoolId, x.VaccineCode, x.DoseNumber }).IsUnique();
        }
    }

    public class VaccinationRecordConfiguration : IEntityTypeConfiguration<VaccinationRecord>
    {
        public void Configure(EntityTypeBuilder<VaccinationRecord> builder)
        {
            builder.ToTable("VaccinationRecord", "svc");
            builder.Property(x => x.VaccineCode).HasMaxLength(30).IsRequired();
            builder.HasOne<MedicalFile>().WithMany().HasForeignKey(x => x.MedicalFileId);
            builder.HasOne<VaccinationCampaign>().WithMany().HasForeignKey(x => x.VaccinationCampaignId);
        }
    }

    public class VaccinationCampaignConfiguration : IEntityTypeConfiguration<VaccinationCampaign>
    {
        public void Configure(EntityTypeBuilder<VaccinationCampaign> builder)
        {
            builder.ToTable("VaccinationCampaign", "svc");
            builder.Property(x => x.NameAr).HasMaxLength(100).IsRequired();
            builder.Property(x => x.NameEn).HasMaxLength(100).IsRequired();
            builder.Property(x => x.VaccineCode).HasMaxLength(30).IsRequired();
        }
    }

    public class ConsentRecordConfiguration : IEntityTypeConfiguration<ConsentRecord>
    {
        public void Configure(EntityTypeBuilder<ConsentRecord> builder)
        {
            builder.ToTable("ConsentRecord", "svc");
            builder.HasOne<VaccinationCampaign>().WithMany().HasForeignKey(x => x.VaccinationCampaignId);
            builder.HasIndex(x => new { x.VaccinationCampaignId, x.StudentId });
        }
    }

    public class ScreeningCampaignConfiguration : IEntityTypeConfiguration<ScreeningCampaign>
    {
        public void Configure(EntityTypeBuilder<ScreeningCampaign> builder)
        {
            builder.ToTable("ScreeningCampaign", "svc");
        }
    }

    public class ScreeningResultConfiguration : IEntityTypeConfiguration<ScreeningResult>
    {
        public void Configure(EntityTypeBuilder<ScreeningResult> builder)
        {
            builder.ToTable("ScreeningResult", "svc");
            builder.Property(x => x.Value1).HasColumnType("decimal(9,2)");
            builder.Property(x => x.Value2).HasColumnType("decimal(9,2)");
            builder.Property(x => x.Notes).HasMaxLength(1000);
            builder.HasOne<ScreeningCampaign>().WithMany().HasForeignKey(x => x.ScreeningCampaignId);
            builder.HasIndex(x => new { x.ScreeningCampaignId, x.StudentId }).IsUnique();
        }
    }

    public class InfectiousCaseConfiguration : IEntityTypeConfiguration<InfectiousCase>
    {
        public void Configure(EntityTypeBuilder<InfectiousCase> builder)
        {
            builder.ToTable("InfectiousCase", "svc");
            builder.Property(x => x.DiseaseName).HasMaxLength(200).IsRequired();
            builder.HasOne<MedicalFile>().WithMany().HasForeignKey(x => x.MedicalFileId);
        }
    }

    public class ExposureNoticeConfiguration : IEntityTypeConfiguration<ExposureNotice>
    {
        public void Configure(EntityTypeBuilder<ExposureNotice> builder)
        {
            builder.ToTable("ExposureNotice", "svc");
            builder.Property(x => x.DiseaseName).HasMaxLength(200).IsRequired();
        }
    }
}
