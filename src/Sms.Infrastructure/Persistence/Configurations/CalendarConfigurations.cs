using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Calendar;
using Sms.Domain.Schools;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // core schema per docs/Database/03 §A — CalendarDay/CalendarEvent/CalendarVersion (doc/Modules/04).

    public class CalendarDayConfiguration : IEntityTypeConfiguration<CalendarDay>
    {
        public void Configure(EntityTypeBuilder<CalendarDay> builder)
        {
            builder.ToTable("CalendarDay", "core");
            builder.HasOne<AcademicYear>().WithMany().HasForeignKey(x => x.AcademicYearId);
            builder.HasIndex(x => new { x.AcademicYearId, x.Date }).IsUnique();
        }
    }

    public class CalendarEventConfiguration : IEntityTypeConfiguration<CalendarEvent>
    {
        public void Configure(EntityTypeBuilder<CalendarEvent> builder)
        {
            builder.ToTable("CalendarEvent", "core");
            builder.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
            builder.Property(x => x.NameEn).HasMaxLength(200).IsRequired();
            builder.HasOne<AcademicYear>().WithMany().HasForeignKey(x => x.AcademicYearId);
            builder.HasIndex(x => new { x.AcademicYearId, x.StartDate });
        }
    }

    public class CalendarVersionConfiguration : IEntityTypeConfiguration<CalendarVersion>
    {
        public void Configure(EntityTypeBuilder<CalendarVersion> builder)
        {
            builder.ToTable("CalendarVersion", "core");
            builder.HasOne<AcademicYear>().WithMany().HasForeignKey(x => x.AcademicYearId);
            builder.HasIndex(x => new { x.AcademicYearId, x.VersionNumber }).IsUnique();
        }
    }
}
