using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Activities;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // ppl schema per doc/Modules/29 — same schema group as Student/Application.

    public class ActivityTypeConfiguration : IEntityTypeConfiguration<ActivityType>
    {
        public void Configure(EntityTypeBuilder<ActivityType> builder)
        {
            builder.ToTable("ActivityType", "ppl");
            builder.Property(x => x.NameAr).HasMaxLength(100).IsRequired();
            builder.Property(x => x.NameEn).HasMaxLength(100).IsRequired();
        }
    }

    public class ActivityProgramConfiguration : IEntityTypeConfiguration<ActivityProgram>
    {
        public void Configure(EntityTypeBuilder<ActivityProgram> builder)
        {
            builder.ToTable("Program", "ppl");
            builder.Property(x => x.NameAr).HasMaxLength(150).IsRequired();
            builder.Property(x => x.NameEn).HasMaxLength(150).IsRequired();
            builder.Property(x => x.CostAmount).HasColumnType("decimal(12,2)");
            builder.HasOne<ActivityType>().WithMany().HasForeignKey(x => x.ActivityTypeId);
        }
    }

    public class ProgramEnrollmentConfiguration : IEntityTypeConfiguration<ProgramEnrollment>
    {
        public void Configure(EntityTypeBuilder<ProgramEnrollment> builder)
        {
            builder.ToTable("ProgramEnrollment", "ppl");
            builder.HasOne<ActivityProgram>().WithMany().HasForeignKey(x => x.ProgramId);
            builder.HasIndex(x => new { x.ProgramId, x.StudentId });
        }
    }

    public class ActivityConsentRecordConfiguration : IEntityTypeConfiguration<ActivityConsentRecord>
    {
        public void Configure(EntityTypeBuilder<ActivityConsentRecord> builder)
        {
            builder.ToTable("ActivityConsentRecord", "ppl");
            builder.Property(x => x.ConsentTextSnapshot).IsRequired();
            builder.HasOne<ProgramEnrollment>().WithMany().HasForeignKey(x => x.ProgramEnrollmentId);
        }
    }

    public class ActivitySessionConfiguration : IEntityTypeConfiguration<ActivitySession>
    {
        public void Configure(EntityTypeBuilder<ActivitySession> builder)
        {
            builder.ToTable("ActivitySession", "ppl");
            builder.HasOne<ActivityProgram>().WithMany().HasForeignKey(x => x.ProgramId);
        }
    }

    public class ActivityAttendanceConfiguration : IEntityTypeConfiguration<ActivityAttendance>
    {
        public void Configure(EntityTypeBuilder<ActivityAttendance> builder)
        {
            builder.ToTable("ActivityAttendance", "ppl");
            builder.HasOne<ActivitySession>().WithMany().HasForeignKey(x => x.ActivitySessionId);
            builder.HasOne<ProgramEnrollment>().WithMany().HasForeignKey(x => x.ProgramEnrollmentId);
            builder.HasIndex(x => new { x.ActivitySessionId, x.ProgramEnrollmentId }).IsUnique();
        }
    }

    public class ActivityTripConfiguration : IEntityTypeConfiguration<ActivityTrip>
    {
        public void Configure(EntityTypeBuilder<ActivityTrip> builder)
        {
            builder.ToTable("ActivityTrip", "ppl");
            builder.Property(x => x.ItineraryText).IsRequired();
            builder.HasOne<ActivityProgram>().WithMany().HasForeignKey(x => x.ProgramId);
            builder.HasIndex(x => x.ProgramId).IsUnique();
        }
    }

    public class CompetitionEventConfiguration : IEntityTypeConfiguration<CompetitionEvent>
    {
        public void Configure(EntityTypeBuilder<CompetitionEvent> builder)
        {
            builder.ToTable("CompetitionEvent", "ppl");
            builder.Property(x => x.NameAr).HasMaxLength(150).IsRequired();
            builder.Property(x => x.NameEn).HasMaxLength(150).IsRequired();
        }
    }

    public class AchievementConfiguration : IEntityTypeConfiguration<Achievement>
    {
        public void Configure(EntityTypeBuilder<Achievement> builder)
        {
            builder.ToTable("Achievement", "ppl");
            builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
            builder.HasOne<ActivityProgram>().WithMany().HasForeignKey(x => x.ProgramId);
            builder.HasOne<CompetitionEvent>().WithMany().HasForeignKey(x => x.CompetitionEventId);
        }
    }
}
