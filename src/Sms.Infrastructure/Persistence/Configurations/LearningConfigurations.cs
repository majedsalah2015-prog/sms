using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Attachments;
using Sms.Domain.Grading;
using Sms.Domain.Learning;
using Sms.Domain.Sections;
using Sms.Domain.Subjects;
using Sms.Domain.Timetable;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // lrn schema per doc/Modules/37 §7. This is a NEW schema: docs/Database/01 §2's
    // cluster table covers modules 01-36 only and will need `lrn` appended when
    // module 37 is approved. Flagged rather than silently assumed.

    public class LessonConfiguration : IEntityTypeConfiguration<Lesson>
    {
        public void Configure(EntityTypeBuilder<Lesson> builder)
        {
            builder.ToTable("Lesson", "lrn");
            builder.Property(x => x.TitleAr).HasMaxLength(200).IsRequired();
            builder.Property(x => x.TitleEn).HasMaxLength(200).IsRequired();
            builder.Property(x => x.ObjectivesAr).HasMaxLength(2000);
            builder.Property(x => x.ObjectivesEn).HasMaxLength(2000);
            builder.Property(x => x.RetiredReason).HasMaxLength(500);

            // BR-LRN-001: anchored on the offering, never on a raw Subject.
            builder.HasOne<CurriculumOffering>().WithMany().HasForeignKey(x => x.CurriculumOfferingId);

            // Optional bind to a dated Module 15 session — "what happened that period".
            builder.HasOne<Session>().WithMany().HasForeignKey(x => x.SessionId);

            // The planner is an offering x week grid (§8.1); this is the grid's read path.
            builder.HasIndex(x => new { x.CurriculumOfferingId, x.WeekNumber });
        }
    }

    public class LessonResourceConfiguration : IEntityTypeConfiguration<LessonResource>
    {
        public void Configure(EntityTypeBuilder<LessonResource> builder)
        {
            builder.ToTable("LessonResource", "lrn");
            builder.Property(x => x.TitleAr).HasMaxLength(200).IsRequired();
            builder.Property(x => x.TitleEn).HasMaxLength(200).IsRequired();

            builder.HasOne<Lesson>().WithMany().HasForeignKey(x => x.LessonId);

            // BR-LRN-006: the bytes, the typing, the size limit and the scan all
            // live in doc.Attachment. This row only links and orders.
            builder.HasOne<Attachment>().WithMany().HasForeignKey(x => x.AttachmentId);

            builder.HasIndex(x => new { x.LessonId, x.DisplayOrder });
        }
    }

    public class HomeworkConfiguration : IEntityTypeConfiguration<Homework>
    {
        public void Configure(EntityTypeBuilder<Homework> builder)
        {
            builder.ToTable("Homework", "lrn");
            builder.Property(x => x.TitleAr).HasMaxLength(200).IsRequired();
            builder.Property(x => x.TitleEn).HasMaxLength(200).IsRequired();
            builder.Property(x => x.InstructionsAr).HasMaxLength(4000);
            builder.Property(x => x.InstructionsEn).HasMaxLength(4000);
            builder.Property(x => x.WithdrawnReason).HasMaxLength(500);

            // Marks, not money: same precision as Grading's MaxScore/Score so a
            // homework mark and the component it feeds cannot disagree on shape.
            builder.Property(x => x.MaxMarks).HasColumnType("decimal(7,2)");
            builder.Property(x => x.LatePenaltyPercent).HasColumnType("decimal(5,2)");

            // BR-LRN-001: anchored on the offering, never on a raw Subject.
            builder.HasOne<CurriculumOffering>().WithMany().HasForeignKey(x => x.CurriculumOfferingId);

            // BR-LRN-002: work is set to one named section.
            builder.HasOne<Section>().WithMany().HasForeignKey(x => x.SectionId);

            // BR-LRN-004/012: the Module 17 component a graded homework feeds.
            // Optional — ungraded practice names none and never reaches Module 17.
            builder.HasOne<BlueprintComponent>().WithMany().HasForeignKey(x => x.BlueprintComponentId);

            // The desk lists one section's work in due-date order (§8.3).
            builder.HasIndex(x => new { x.SectionId, x.DueDate }, "IX_Homework_SectionId_DueDate");

            // The portal asks the opposite question — what is set for this
            // student's sections, and is it visible yet (BR-LRN-003).
            builder.HasIndex(x => new { x.SectionId, x.Status }, "IX_Homework_SectionId_Status");
        }
    }
}
