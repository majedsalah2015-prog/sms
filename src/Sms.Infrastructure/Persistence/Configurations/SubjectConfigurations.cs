using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Grades;
using Sms.Domain.Subjects;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // core schema per docs/Database/03 §A5 (CurriculumOffering pivotal spec) + doc/Modules/07.

    public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
    {
        public void Configure(EntityTypeBuilder<Department> builder)
        {
            builder.ToTable("Department", "core");
            builder.OwnsOne(x => x.Name, name =>
            {
                name.Property(n => n.NameAr).HasColumnName("NameAr").HasMaxLength(100).IsRequired();
                name.Property(n => n.NameEn).HasColumnName("NameEn").HasMaxLength(100).IsRequired();
            });
        }
    }

    public class SubjectConfiguration : IEntityTypeConfiguration<Subject>
    {
        public void Configure(EntityTypeBuilder<Subject> builder)
        {
            builder.ToTable("Subject", "core");
            builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
            builder.Property(x => x.Category).HasMaxLength(30).IsRequired();
            builder.HasIndex(x => new { x.SchoolId, x.Code }).IsUnique();
            builder.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId);
            builder.OwnsOne(x => x.Name, name =>
            {
                name.Property(n => n.NameAr).HasColumnName("NameAr").HasMaxLength(100).IsRequired();
                name.Property(n => n.NameEn).HasColumnName("NameEn").HasMaxLength(100).IsRequired();
            });
        }
    }

    public class CurriculumOfferingConfiguration : IEntityTypeConfiguration<CurriculumOffering>
    {
        public void Configure(EntityTypeBuilder<CurriculumOffering> builder)
        {
            builder.ToTable("CurriculumOffering", "core");
            builder.Property(x => x.GpaWeight).HasColumnType("decimal(6,3)");
            builder.Property(x => x.ElectiveGroupTag).HasMaxLength(30);
            builder.HasOne<GradeYearProfile>().WithMany().HasForeignKey(x => x.GradeYearProfileId);
            builder.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId);

            // BR-SUB §9 uniqueness only applies to the CURRENT (not end-dated) offering —
            // a historical, end-dated row for the same pair must stay insertable-adjacent,
            // matching Section's "at most one current" filtered-index pattern.
            builder.HasIndex(x => new { x.GradeYearProfileId, x.SubjectId })
                .HasDatabaseName("IX_CurriculumOffering_GradeYearSubject_Current")
                .IsUnique()
                .HasFilter("[EffectiveToUtc] IS NULL");
        }
    }

    public class TeacherSubjectQualificationConfiguration : IEntityTypeConfiguration<TeacherSubjectQualification>
    {
        public void Configure(EntityTypeBuilder<TeacherSubjectQualification> builder)
        {
            builder.ToTable("TeacherSubjectQualification", "core");
            builder.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId);
            builder.HasIndex(x => new { x.TeacherUserId, x.SubjectId, x.StageId }).IsUnique();
        }
    }
}
