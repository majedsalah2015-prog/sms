using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Employees;
using Sms.Domain.Teachers;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // core schema per doc/Modules/13 — same schema group as Section/Subjects.

    public class TeacherProfileConfiguration : IEntityTypeConfiguration<TeacherProfile>
    {
        public void Configure(EntityTypeBuilder<TeacherProfile> builder)
        {
            builder.ToTable("TeacherProfile", "core");
            builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId);
            builder.HasIndex(x => x.EmployeeId).IsUnique();
        }
    }

    public class TeacherAssignmentConfiguration : IEntityTypeConfiguration<TeacherAssignment>
    {
        public void Configure(EntityTypeBuilder<TeacherAssignment> builder)
        {
            builder.ToTable("TeacherAssignment", "core");
            builder.HasOne<TeacherProfile>().WithMany().HasForeignKey(x => x.TeacherProfileId);
            builder.HasIndex(x => new { x.CurriculumOfferingId, x.SectionId });

            // BR-TCH-005: at most one CURRENT primary teacher per offering x section.
            builder.HasIndex(x => new { x.CurriculumOfferingId, x.SectionId, x.Role }, "IX_TeacherAssignment_Offering_Section_Primary")
                .IsUnique()
                .HasFilter("[EffectiveToUtc] IS NULL AND [Role] = 1");
        }
    }
}
