using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Admissions;
using AdmissionApplication = Sms.Domain.Admissions.Application;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // ppl schema per doc/Modules/09 — same schema group as Student/Parent (E-202).

    public class AdmissionCampaignConfiguration : IEntityTypeConfiguration<AdmissionCampaign>
    {
        public void Configure(EntityTypeBuilder<AdmissionCampaign> builder)
        {
            builder.ToTable("AdmissionCampaign", "ppl");
            builder.Property(x => x.ApplicationFeeAmount).HasColumnType("decimal(18,2)");
            builder.HasIndex(x => new { x.SchoolId, x.AcademicYearId, x.GradeYearProfileId });
        }
    }

    public class ApplicationConfiguration : IEntityTypeConfiguration<AdmissionApplication>
    {
        public void Configure(EntityTypeBuilder<AdmissionApplication> builder)
        {
            builder.ToTable("Application", "ppl");
            builder.Property(x => x.ApplicationNo).HasMaxLength(20).IsRequired();
            builder.HasIndex(x => new { x.SchoolId, x.ApplicationNo }).IsUnique();
            builder.HasIndex(x => x.CampaignId);
            builder.HasIndex(x => x.ParentId);

            foreach (var name in new[]
            {
                nameof(AdmissionApplication.FirstNameAr), nameof(AdmissionApplication.FatherNameAr),
                nameof(AdmissionApplication.GrandfatherNameAr), nameof(AdmissionApplication.FamilyNameAr),
                nameof(AdmissionApplication.FirstNameEn), nameof(AdmissionApplication.FatherNameEn),
                nameof(AdmissionApplication.GrandfatherNameEn), nameof(AdmissionApplication.FamilyNameEn),
            })
            {
                builder.Property(name).HasMaxLength(60).IsRequired();
            }
        }
    }

    public class ApplicationAssessmentConfiguration : IEntityTypeConfiguration<ApplicationAssessment>
    {
        public void Configure(EntityTypeBuilder<ApplicationAssessment> builder)
        {
            builder.ToTable("ApplicationAssessment", "ppl");
            builder.Property(x => x.Score).HasColumnType("decimal(5,2)");
            builder.Property(x => x.Notes).HasMaxLength(500);
            builder.HasOne<AdmissionApplication>().WithMany().HasForeignKey(x => x.ApplicationId);
        }
    }

    public class WaitingListEntryConfiguration : IEntityTypeConfiguration<WaitingListEntry>
    {
        public void Configure(EntityTypeBuilder<WaitingListEntry> builder)
        {
            builder.ToTable("WaitingListEntry", "ppl");
            builder.HasOne<AdmissionApplication>().WithMany().HasForeignKey(x => x.ApplicationId);
            // BR-ADM-006: submission-order rank, unique per grade-year profile.
            builder.HasIndex(x => new { x.GradeYearProfileId, x.OrderRank }, "IX_WaitingListEntry_Profile_Rank").IsUnique();
        }
    }
}
