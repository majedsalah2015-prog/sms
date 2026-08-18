using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.ReadModels;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // rpt schema — DB/04 §4 snapshot tables (the doc's snap_* read models). Plain rows: refreshed wholesale, no FKs
    // (a snapshot must survive the hot row it summarizes being archived), indexed for the report/widget read paths only.

    public class AgedReceivablesSnapshotConfiguration : IEntityTypeConfiguration<AgedReceivablesSnapshot>
    {
        public void Configure(EntityTypeBuilder<AgedReceivablesSnapshot> builder)
        {
            builder.ToTable("AgedReceivablesSnapshot", "rpt");
            foreach (var money in new[] { nameof(AgedReceivablesSnapshot.Current), nameof(AgedReceivablesSnapshot.Days1To30), nameof(AgedReceivablesSnapshot.Days31To60), nameof(AgedReceivablesSnapshot.Days61To90), nameof(AgedReceivablesSnapshot.Over90), nameof(AgedReceivablesSnapshot.Total) })
            {
                builder.Property(money).HasColumnType("decimal(18,4)");
            }

            builder.HasIndex(x => new { x.SchoolId, x.PayerId }, "IX_AgedReceivablesSnapshot_School_Payer");
            builder.HasIndex(x => new { x.SchoolId, x.GradeYearProfileId }, "IX_AgedReceivablesSnapshot_School_Profile");
        }
    }

    public class DailyAttendanceSummarySnapshotConfiguration : IEntityTypeConfiguration<DailyAttendanceSummarySnapshot>
    {
        public void Configure(EntityTypeBuilder<DailyAttendanceSummarySnapshot> builder)
        {
            builder.ToTable("DailyAttendanceSummarySnapshot", "rpt");
            builder.Property(x => x.PresentPercent).HasColumnType("decimal(5,2)");
            builder.HasIndex(x => new { x.SchoolId, x.Date, x.SectionId }, "IX_DailyAttendanceSummarySnapshot_School_Date_Section").IsUnique();
            builder.HasIndex(x => new { x.SchoolId, x.Date, x.StageId }, "IX_DailyAttendanceSummarySnapshot_School_Date_Stage");
        }
    }

    public class CollectionCalendarSnapshotConfiguration : IEntityTypeConfiguration<CollectionCalendarSnapshot>
    {
        public void Configure(EntityTypeBuilder<CollectionCalendarSnapshot> builder)
        {
            builder.ToTable("CollectionCalendarSnapshot", "rpt");
            builder.Property(x => x.ScheduledAmount).HasColumnType("decimal(18,2)");
            builder.Property(x => x.PaidAmount).HasColumnType("decimal(18,2)");
            builder.Property(x => x.OutstandingAmount).HasColumnType("decimal(18,2)");
            builder.HasIndex(x => new { x.SchoolId, x.DueDate }, "IX_CollectionCalendarSnapshot_School_DueDate");
        }
    }
}
