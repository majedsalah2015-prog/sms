using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Schools;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // core schema per docs/Database/03 §A1 (AcademicYear pivotal spec) + doc/Modules/03.

    public class AcademicYearConfiguration : IEntityTypeConfiguration<AcademicYear>
    {
        public void Configure(EntityTypeBuilder<AcademicYear> builder)
        {
            builder.ToTable("AcademicYear", "core");
            builder.Property(x => x.LabelEn).HasMaxLength(40).IsRequired();
            builder.Property(x => x.LabelAr).HasMaxLength(40).IsRequired();
            builder.Property(x => x.HijriLabel).HasMaxLength(40).IsRequired();

            // BR-AYR-002: exactly one Active, at most one Preparation, per school.
            // [Status] bracket-quoting is valid in both SQL Server and Sqlite.
            // Two indexes over the SAME property (SchoolId) must be created via the
            // (expression, name) overload — plain HasIndex(x => x.SchoolId) called
            // twice keys by property list, and the second call silently overwrites
            // the first (verified: only one of the two showed up in sqlite_master).
            builder.HasIndex(x => x.SchoolId, "IX_AcademicYear_SchoolId_Active")
                .IsUnique()
                .HasFilter("[Status] = 1");
            builder.HasIndex(x => x.SchoolId, "IX_AcademicYear_SchoolId_Preparation")
                .IsUnique()
                .HasFilter("[Status] = 0");
        }
    }

    public class SemesterConfiguration : IEntityTypeConfiguration<Semester>
    {
        public void Configure(EntityTypeBuilder<Semester> builder)
        {
            builder.ToTable("Semester", "core");
            builder.Property(x => x.NameAr).HasMaxLength(60).IsRequired();
            builder.Property(x => x.NameEn).HasMaxLength(60).IsRequired();
            builder.HasOne<AcademicYear>().WithMany().HasForeignKey(x => x.AcademicYearId);
            builder.HasIndex(x => new { x.AcademicYearId, x.SequenceNumber }).IsUnique();
        }
    }

    public class TermConfiguration : IEntityTypeConfiguration<Term>
    {
        public void Configure(EntityTypeBuilder<Term> builder)
        {
            builder.ToTable("Term", "core");
            builder.Property(x => x.NameAr).HasMaxLength(60).IsRequired();
            builder.Property(x => x.NameEn).HasMaxLength(60).IsRequired();
            builder.HasOne<Semester>().WithMany().HasForeignKey(x => x.SemesterId);
            builder.HasIndex(x => new { x.SemesterId, x.SequenceNumber }).IsUnique();
        }
    }
}
