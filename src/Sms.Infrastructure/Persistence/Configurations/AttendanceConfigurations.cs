using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Attendance;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // core schema per doc/Modules/14 — same schema group as Section/CalendarDay.

    public class AttendanceDayConfiguration : IEntityTypeConfiguration<AttendanceDay>
    {
        public void Configure(EntityTypeBuilder<AttendanceDay> builder)
        {
            builder.ToTable("AttendanceDay", "core");
            builder.HasIndex(x => x.SectionId);

            // BR-ATD-003: one record per enrollment per day.
            builder.HasIndex(x => new { x.EnrollmentId, x.Date }, "IX_AttendanceDay_Enrollment_Date").IsUnique();
        }
    }

    public class GateEventConfiguration : IEntityTypeConfiguration<GateEvent>
    {
        public void Configure(EntityTypeBuilder<GateEvent> builder)
        {
            builder.ToTable("GateEvent", "core");
            builder.Property(x => x.PickupPersonName).HasMaxLength(150);
            builder.HasIndex(x => x.EnrollmentId);
        }
    }

    public class JustificationConfiguration : IEntityTypeConfiguration<Justification>
    {
        public void Configure(EntityTypeBuilder<Justification> builder)
        {
            builder.ToTable("Justification", "core");
            builder.Property(x => x.RejectionReason).HasMaxLength(500);
            builder.HasOne<AttendanceDay>().WithMany().HasForeignKey(x => x.AttendanceDayId);
        }
    }

    public class LeavePassConfiguration : IEntityTypeConfiguration<LeavePass>
    {
        public void Configure(EntityTypeBuilder<LeavePass> builder)
        {
            builder.ToTable("LeavePass", "core");
            builder.Property(x => x.Reason).HasMaxLength(300).IsRequired();
            builder.HasIndex(x => x.EnrollmentId);
        }
    }
}
