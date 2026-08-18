using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Rollover;
using Sms.Domain.Schools;
using Sms.Domain.Students;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // core schema — doc/Modules/03 §7 lists RolloverBatch/RolloverStudentState alongside AcademicYear.

    public class RolloverBatchConfiguration : IEntityTypeConfiguration<RolloverBatch>
    {
        public void Configure(EntityTypeBuilder<RolloverBatch> builder)
        {
            builder.ToTable("RolloverBatch", "core");
            builder.Property(x => x.TimetableDeferredReason).HasMaxLength(500);
            builder.Property(x => x.CarryForwardTotal).HasColumnType("decimal(18,4)");
            builder.HasOne<AcademicYear>().WithMany().HasForeignKey(x => x.SourceAcademicYearId);
            builder.HasOne<AcademicYear>().WithMany().HasForeignKey(x => x.TargetAcademicYearId);
            // One batch per source→target pair; a source year rolls forward once.
            builder.HasIndex(x => new { x.SchoolId, x.SourceAcademicYearId, x.TargetAcademicYearId }).IsUnique();
        }
    }

    public class RolloverStudentStateConfiguration : IEntityTypeConfiguration<RolloverStudentState>
    {
        public void Configure(EntityTypeBuilder<RolloverStudentState> builder)
        {
            builder.ToTable("RolloverStudentState", "core");
            builder.Property(x => x.DecisionReason).HasMaxLength(500);
            builder.Property(x => x.CarryForwardAmount).HasColumnType("decimal(18,4)");
            builder.HasOne<RolloverBatch>().WithMany().HasForeignKey(x => x.RolloverBatchId);
            builder.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId);
            builder.HasOne<Enrollment>().WithMany().HasForeignKey(x => x.SourceEnrollmentId);
            // BR-AYR-008 "idempotent per student": exactly one state row per student per batch.
            builder.HasIndex(x => new { x.RolloverBatchId, x.StudentId }).IsUnique();
            builder.HasIndex(x => x.TargetEnrollmentId);
        }
    }
}
