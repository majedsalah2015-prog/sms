using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Grades;
using Sms.Domain.Schools;
using Sms.Domain.Students;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // ppl schema per docs/Database/03 §A2-A4 pivotal specs + doc/Modules/10.

    public class StudentConfiguration : IEntityTypeConfiguration<Student>
    {
        public void Configure(EntityTypeBuilder<Student> builder)
        {
            builder.ToTable("Student", "ppl");
            builder.Property(x => x.StudentNo).HasMaxLength(20).IsRequired();
            builder.HasIndex(x => new { x.SchoolId, x.StudentNo }).IsUnique();

            foreach (var name in new[]
            {
                nameof(Student.FirstNameAr), nameof(Student.FatherNameAr), nameof(Student.GrandfatherNameAr), nameof(Student.FamilyNameAr),
                nameof(Student.FirstNameEn), nameof(Student.FatherNameEn), nameof(Student.GrandfatherNameEn), nameof(Student.FamilyNameEn),
            })
            {
                builder.Property(name).HasMaxLength(60).IsRequired();
            }

            builder.Property(x => x.PrimaryIdNo).HasMaxLength(30);

            builder.Property(x => x.PlaceOfBirth).HasMaxLength(100);
            builder.Property(x => x.RationCardNo).HasMaxLength(30);

            // 20 to match ppl.Parent.PrimaryMobile: the same number is often typed into both, and a
            // column that silently truncated on one side of the family would be worse than one
            // that refuses.
            builder.Property(x => x.Mobile).HasMaxLength(20);

            // BR-GLB-003: unique per (school, id type, id number) when a primary ID is recorded.
            builder.HasIndex(x => new { x.SchoolId, x.PrimaryIdTypeLookupId, x.PrimaryIdNo })
                .HasDatabaseName("IX_Student_PrimaryId")
                .IsUnique()
                .HasFilter("[PrimaryIdNo] IS NOT NULL");
        }
    }

    public class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
    {
        public void Configure(EntityTypeBuilder<Enrollment> builder)
        {
            builder.ToTable("Enrollment", "ppl");
            builder.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId);
            builder.HasOne<GradeYearProfile>().WithMany().HasForeignKey(x => x.GradeYearProfileId);
            builder.HasOne<AcademicYear>().WithMany().HasForeignKey(x => x.AcademicYearId);

            // BR-GLB-024: at most one CURRENT (Active) enrollment per student per year.
            builder.HasIndex(x => new { x.StudentId, x.AcademicYearId })
                .HasDatabaseName("IX_Enrollment_Student_Year_Active")
                .IsUnique()
                .HasFilter("[Status] = 1");
            // DB/04 §1 (S8/E-802): tenant-first roster path — rosters, seat utilization, rollover state seeding.
            builder.HasIndex(x => new { x.SchoolId, x.AcademicYearId, x.GradeYearProfileId }, "IX_Enrollment_School_Year_Profile");
        }
    }

    public class StudentGuardianLinkConfiguration : IEntityTypeConfiguration<StudentGuardianLink>
    {
        public void Configure(EntityTypeBuilder<StudentGuardianLink> builder)
        {
            builder.ToTable("StudentGuardianLink", "ppl");
            builder.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId);
            builder.HasIndex(x => x.StudentId);
            builder.HasIndex(x => x.ParentId);
        }
    }

    public class EmergencyContactConfiguration : IEntityTypeConfiguration<EmergencyContact>
    {
        public void Configure(EntityTypeBuilder<EmergencyContact> builder)
        {
            builder.ToTable("EmergencyContact", "ppl");
            builder.Property(x => x.NameAr).HasMaxLength(100).IsRequired();
            builder.Property(x => x.NameEn).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Phone).HasMaxLength(30).IsRequired();
            builder.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId);
        }
    }
}
