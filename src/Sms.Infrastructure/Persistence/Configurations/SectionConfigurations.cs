using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Classrooms;
using Sms.Domain.Grades;
using Sms.Domain.Sections;
using Sms.Domain.Students;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // core schema per docs/Database/03 §A — Section/HomeroomAssignment/SectionMembership (doc/Modules/06).

    public class SectionConfiguration : IEntityTypeConfiguration<Section>
    {
        public void Configure(EntityTypeBuilder<Section> builder)
        {
            builder.ToTable("Section", "core");
            builder.Property(x => x.NameAr).HasMaxLength(60).IsRequired();
            builder.Property(x => x.NameEn).HasMaxLength(60).IsRequired();
            builder.HasOne<GradeYearProfile>().WithMany().HasForeignKey(x => x.GradeYearProfileId);
            // Real FK as of E-104 (Classrooms) — was an unconstrained forward
            // reference until core.Room existed.
            builder.HasOne<Room>().WithMany().HasForeignKey(x => x.DefaultClassroomId);
            builder.HasIndex(x => new { x.GradeYearProfileId, x.NameEn }).IsUnique();
        }
    }

    public class HomeroomAssignmentConfiguration : IEntityTypeConfiguration<HomeroomAssignment>
    {
        public void Configure(EntityTypeBuilder<HomeroomAssignment> builder)
        {
            builder.ToTable("HomeroomAssignment", "core");
            builder.HasOne<Section>().WithMany().HasForeignKey(x => x.SectionId);

            // BR-SCN-004: at most one CURRENT (open-ended) assignment per section.
            builder.HasIndex(x => x.SectionId)
                .HasDatabaseName("IX_HomeroomAssignment_SectionId_Current")
                .IsUnique()
                .HasFilter("[EffectiveToUtc] IS NULL");
        }
    }

    public class SectionMembershipConfiguration : IEntityTypeConfiguration<SectionMembership>
    {
        public void Configure(EntityTypeBuilder<SectionMembership> builder)
        {
            builder.ToTable("SectionMembership", "core");
            builder.Property(x => x.TransferReasonCode).HasMaxLength(30);
            builder.HasOne<Section>().WithMany().HasForeignKey(x => x.SectionId);
            // Real FK as of E-202 (Students/Enrollment) — was an unconstrained
            // forward reference from E-103 until ppl.Enrollment existed.
            builder.HasOne<Enrollment>().WithMany().HasForeignKey(x => x.EnrollmentId).OnDelete(DeleteBehavior.Restrict);

            // BR-GLB-024/BR-SCN-005: at most one CURRENT membership per enrollment
            // (a student belongs to exactly one section at a time).
            builder.HasIndex(x => x.EnrollmentId)
                .HasDatabaseName("IX_SectionMembership_EnrollmentId_Current")
                .IsUnique()
                .HasFilter("[EffectiveToUtc] IS NULL");
            builder.HasIndex(x => x.SectionId);
        }
    }
}
