using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Classrooms;
using Sms.Domain.Examinations;
using Sms.Domain.Grading;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // core schema per doc/Modules/16 — same schema group as Section/Subjects/Grading/Timetable.

    public class ExamTypeConfiguration : IEntityTypeConfiguration<ExamType>
    {
        public void Configure(EntityTypeBuilder<ExamType> builder)
        {
            builder.ToTable("ExamType", "core");
            builder.Property(x => x.NameAr).HasMaxLength(80).IsRequired();
            builder.Property(x => x.NameEn).HasMaxLength(80).IsRequired();
        }
    }

    public class ExamRoundConfiguration : IEntityTypeConfiguration<ExamRound>
    {
        public void Configure(EntityTypeBuilder<ExamRound> builder)
        {
            builder.ToTable("ExamRound", "core");
            builder.Property(x => x.NameAr).HasMaxLength(100).IsRequired();
            builder.Property(x => x.NameEn).HasMaxLength(100).IsRequired();
        }
    }

    public class ExamConfiguration : IEntityTypeConfiguration<Exam>
    {
        public void Configure(EntityTypeBuilder<Exam> builder)
        {
            builder.ToTable("Exam", "core");
            builder.HasOne<ExamRound>().WithMany().HasForeignKey(x => x.ExamRoundId);
            builder.HasOne<ExamType>().WithMany().HasForeignKey(x => x.ExamTypeId);
            builder.HasOne<BlueprintComponent>().WithMany().HasForeignKey(x => x.BlueprintComponentId);
            builder.HasIndex(x => new { x.GradeYearProfileId, x.Date });
        }
    }

    public class ExamSittingConfiguration : IEntityTypeConfiguration<ExamSitting>
    {
        public void Configure(EntityTypeBuilder<ExamSitting> builder)
        {
            builder.ToTable("ExamSitting", "core");
            builder.HasOne<Exam>().WithMany().HasForeignKey(x => x.ExamId);
            builder.HasOne<Room>().WithMany().HasForeignKey(x => x.RoomId);
        }
    }

    public class ExamAttendanceConfiguration : IEntityTypeConfiguration<ExamAttendance>
    {
        public void Configure(EntityTypeBuilder<ExamAttendance> builder)
        {
            builder.ToTable("ExamAttendance", "core");
            builder.HasOne<ExamSitting>().WithMany().HasForeignKey(x => x.ExamSittingId);
            builder.HasIndex(x => new { x.ExamSittingId, x.EnrollmentId }).IsUnique();
        }
    }

    public class ExamIncidentConfiguration : IEntityTypeConfiguration<ExamIncident>
    {
        public void Configure(EntityTypeBuilder<ExamIncident> builder)
        {
            builder.ToTable("ExamIncident", "core");
            builder.HasOne<ExamSitting>().WithMany().HasForeignKey(x => x.ExamSittingId);
            builder.Property(x => x.Category).HasMaxLength(50).IsRequired();
            builder.Property(x => x.Narrative).HasMaxLength(2000).IsRequired();
        }
    }

    public class MakeupEligibilityConfiguration : IEntityTypeConfiguration<MakeupEligibility>
    {
        public void Configure(EntityTypeBuilder<MakeupEligibility> builder)
        {
            builder.ToTable("MakeupEligibility", "core");
            builder.HasOne<Exam>().WithMany().HasForeignKey(x => x.ExamId);
            builder.HasIndex(x => new { x.ExamId, x.EnrollmentId }).IsUnique();
        }
    }
}
