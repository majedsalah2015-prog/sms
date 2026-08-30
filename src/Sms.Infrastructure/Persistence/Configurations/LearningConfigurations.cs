using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Attachments;
using Sms.Domain.Learning;
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
}
