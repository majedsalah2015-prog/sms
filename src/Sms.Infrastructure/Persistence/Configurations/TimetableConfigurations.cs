using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Classrooms;
using Sms.Domain.Timetable;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // core schema per doc/Modules/15 — same schema group as Section/Subjects/Grades.

    public class TimetableShapeConfiguration : IEntityTypeConfiguration<TimetableShape>
    {
        public void Configure(EntityTypeBuilder<TimetableShape> builder)
        {
            builder.ToTable("TimetableShape", "core");
        }
    }

    public class PeriodSlotConfiguration : IEntityTypeConfiguration<PeriodSlot>
    {
        public void Configure(EntityTypeBuilder<PeriodSlot> builder)
        {
            builder.ToTable("PeriodSlot", "core");
            builder.HasOne<TimetableShape>().WithMany().HasForeignKey(x => x.TimetableShapeId);
            builder.HasIndex(x => new { x.TimetableShapeId, x.DayOfWeek, x.SequenceNumber }).IsUnique();
        }
    }

    public class TimetableVersionConfiguration : IEntityTypeConfiguration<TimetableVersion>
    {
        public void Configure(EntityTypeBuilder<TimetableVersion> builder)
        {
            builder.ToTable("TimetableVersion", "core");
        }
    }

    public class PlacementConfiguration : IEntityTypeConfiguration<Placement>
    {
        public void Configure(EntityTypeBuilder<Placement> builder)
        {
            builder.ToTable("Placement", "core");
            builder.HasOne<TimetableVersion>().WithMany().HasForeignKey(x => x.TimetableVersionId);
            builder.HasOne<Room>().WithMany().HasForeignKey(x => x.RoomId);
            builder.HasIndex(x => new { x.TimetableVersionId, x.PeriodSlotId });
        }
    }

    public class SessionConfiguration : IEntityTypeConfiguration<Session>
    {
        public void Configure(EntityTypeBuilder<Session> builder)
        {
            builder.ToTable("Session", "core");
            builder.HasOne<Placement>().WithMany().HasForeignKey(x => x.PlacementId);
            builder.HasOne<Room>().WithMany().HasForeignKey(x => x.OverrideRoomId);
            builder.Property(x => x.ChangeReason).HasMaxLength(500);
            builder.HasIndex(x => new { x.PlacementId, x.Date }).IsUnique();
        }
    }

    public class SubstitutionConfiguration : IEntityTypeConfiguration<Substitution>
    {
        public void Configure(EntityTypeBuilder<Substitution> builder)
        {
            builder.ToTable("Substitution", "core");
            builder.HasOne<Session>().WithMany().HasForeignKey(x => x.SessionId);
            builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        }
    }
}
