using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Grades;
using Sms.Domain.Schools;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // core schema per docs/Database/03 §A — Stage/GradeLevel/GradeYearProfile (doc/Modules/05).

    public class StageConfiguration : IEntityTypeConfiguration<Stage>
    {
        public void Configure(EntityTypeBuilder<Stage> builder)
        {
            builder.ToTable("Stage", "core");
            builder.OwnsOne(x => x.Name, name =>
            {
                name.Property(n => n.NameAr).HasColumnName("NameAr").HasMaxLength(100).IsRequired();
                name.Property(n => n.NameEn).HasColumnName("NameEn").HasMaxLength(100).IsRequired();
            });
        }
    }

    public class GradeLevelConfiguration : IEntityTypeConfiguration<GradeLevel>
    {
        public void Configure(EntityTypeBuilder<GradeLevel> builder)
        {
            builder.ToTable("GradeLevel", "core");
            builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
            builder.HasIndex(x => new { x.SchoolId, x.Code }).IsUnique();
            builder.HasOne<Stage>().WithMany().HasForeignKey(x => x.StageId);
            // Self-referencing FK, restricted so a promotion target can't be
            // deleted out from under a grade that points to it (it can only
            // ever be deactivated, BR-GRD-007, which doesn't touch the FK).
            builder.HasOne<GradeLevel>().WithMany().HasForeignKey(x => x.PromotionTargetGradeLevelId).OnDelete(DeleteBehavior.Restrict);
            builder.OwnsOne(x => x.Name, name =>
            {
                name.Property(n => n.NameAr).HasColumnName("NameAr").HasMaxLength(100).IsRequired();
                name.Property(n => n.NameEn).HasColumnName("NameEn").HasMaxLength(100).IsRequired();
            });
        }
    }

    public class GradeYearProfileConfiguration : IEntityTypeConfiguration<GradeYearProfile>
    {
        public void Configure(EntityTypeBuilder<GradeYearProfile> builder)
        {
            builder.ToTable("GradeYearProfile", "core");
            builder.Property(x => x.MinAgeAtCutoff).HasColumnType("decimal(4,2)");
            builder.Property(x => x.MaxAgeAtCutoff).HasColumnType("decimal(4,2)");
            builder.HasOne<GradeLevel>().WithMany().HasForeignKey(x => x.GradeLevelId);
            builder.HasOne<AcademicYear>().WithMany().HasForeignKey(x => x.AcademicYearId);
            builder.HasIndex(x => new { x.GradeLevelId, x.AcademicYearId }).IsUnique();
        }
    }
}
