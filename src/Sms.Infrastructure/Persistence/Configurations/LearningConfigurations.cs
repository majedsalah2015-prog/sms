using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Attachments;
using Sms.Domain.Grading;
using Sms.Domain.Learning;
using Sms.Domain.Sections;
using Sms.Domain.Students;
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

    public class HomeworkSubmissionConfiguration : IEntityTypeConfiguration<HomeworkSubmission>
    {
        public void Configure(EntityTypeBuilder<HomeworkSubmission> builder)
        {
            builder.ToTable("HomeworkSubmission", "lrn");
            builder.Property(x => x.Feedback).HasMaxLength(2000);

            // Marks, not money: the same decimal(7,2) as Homework.MaxMarks and
            // Module 17's MarkEntry.Score, so the number this row carries and the
            // number the marksheet receives cannot disagree on shape.
            builder.Property(x => x.Score).HasColumnType("decimal(7,2)");

            builder.HasOne<Homework>().WithMany().HasForeignKey(x => x.HomeworkId);

            // Keyed on the enrollment, not the student — the roster joins through
            // SectionMembership.EnrollmentId and BR-LRN-012's handoff writes a
            // MarkEntry that carries EnrollmentId too. See the entity's remarks.
            builder.HasOne<Enrollment>().WithMany().HasForeignKey(x => x.EnrollmentId);

            // BR-LRN-005: "one live submission per student per homework". A
            // DATABASE guarantee, not a service check — a resubmission racing
            // itself through two requests would otherwise leave two live rows and
            // two marks for one piece of work, and the tracker would silently
            // show whichever it read first.
            builder.HasIndex(x => new { x.HomeworkId, x.EnrollmentId }, "UQ_HomeworkSubmission_Homework_Enrollment").IsUnique();

            // §8.5's marking queue and BR-LRN-011's unscored count both ask one
            // homework for its submissions by state. Distinct column list from
            // the unique index above, so the two do not collapse into one.
            builder.HasIndex(x => new { x.HomeworkId, x.Status }, "IX_HomeworkSubmission_HomeworkId_Status");

            // The portal and §10's missing-work register ask the opposite
            // question: everything this one student has handed in.
            builder.HasIndex(x => x.EnrollmentId, "IX_HomeworkSubmission_EnrollmentId");
        }
    }

    public class SubmissionVersionConfiguration : IEntityTypeConfiguration<SubmissionVersion>
    {
        public void Configure(EntityTypeBuilder<SubmissionVersion> builder)
        {
            builder.ToTable("SubmissionVersion", "lrn");

            // 4000, the same ceiling Homework.Instructions* carry — and a real
            // nvarchar(4000) the database enforces, where 8000 would silently
            // become nvarchar(max) and enforce nothing. A typed answer longer
            // than two pages is a file, which is what SubmissionAttachment is for.
            builder.Property(x => x.TextResponse).HasMaxLength(4000);

            builder.HasOne<HomeworkSubmission>().WithMany().HasForeignKey(x => x.HomeworkSubmissionId);

            // BR-LRN-005: an append-only log with a repeated sequence number is
            // not one. Unique so "version 2" names exactly one hand-in even if
            // two requests append at once.
            builder.HasIndex(x => new { x.HomeworkSubmissionId, x.VersionNumber }, "UQ_SubmissionVersion_Submission_Version").IsUnique();
        }
    }

    public class SubmissionAttachmentConfiguration : IEntityTypeConfiguration<SubmissionAttachment>
    {
        public void Configure(EntityTypeBuilder<SubmissionAttachment> builder)
        {
            builder.ToTable("SubmissionAttachment", "lrn");

            // Hung off the version, never off the live submission — that is what
            // keeps a superseded hand-in's files with the hand-in they belonged
            // to (BR-LRN-005). See the entity's remarks for the §7 deviation.
            builder.HasOne<SubmissionVersion>().WithMany().HasForeignKey(x => x.SubmissionVersionId);

            // BR-LRN-006: the bytes, the typing, the size limit and the scan all
            // live in doc.Attachment. This row only links.
            builder.HasOne<Attachment>().WithMany().HasForeignKey(x => x.AttachmentId);

            // The same file twice on one hand-in is a double-click, not a second
            // file.
            builder.HasIndex(x => new { x.SubmissionVersionId, x.AttachmentId }, "UQ_SubmissionAttachment_Version_Attachment").IsUnique();
        }
    }

    /// <summary>
    /// doc/Modules/37 §7 lrn.QuestionBank. Outside the soft-active filter on
    /// purpose: §7 keeps versioned catalogs loadable, because a bank retired this
    /// term still owns the questions on last term's paper.
    /// </summary>
    public class QuestionBankConfiguration : IEntityTypeConfiguration<QuestionBank>
    {
        public void Configure(EntityTypeBuilder<QuestionBank> builder)
        {
            builder.ToTable("QuestionBank", "lrn");

            builder.Property(x => x.NameAr).IsRequired().HasMaxLength(200);
            builder.Property(x => x.NameEn).IsRequired().HasMaxLength(200);

            // BR-LRN-001: banks hang off the offering, never off a raw subject.
            builder.HasOne<CurriculumOffering>().WithMany().HasForeignKey(x => x.CurriculumOfferingId);

            builder.HasIndex(x => x.CurriculumOfferingId, "IX_QuestionBank_Offering");
        }
    }

    /// <summary>
    /// doc/Modules/37 §7 lrn.Question, BR-LRN-007. Every version is a row; the
    /// unique index over (root, version) is what makes "version 2 of this
    /// question" a thing the database guarantees rather than a convention the
    /// service hopes it kept.
    /// </summary>
    public class QuestionConfiguration : IEntityTypeConfiguration<Question>
    {
        public void Configure(EntityTypeBuilder<Question> builder)
        {
            builder.ToTable("Question", "lrn");

            builder.Property(x => x.StemAr).IsRequired();
            builder.Property(x => x.StemEn).IsRequired();
            builder.Property(x => x.Marks).HasColumnType("decimal(6,2)");
            builder.Property(x => x.NumericTolerance).HasColumnType("decimal(12,4)");
            builder.Property(x => x.DeprecatedReason).HasMaxLength(500);

            builder.HasOne<QuestionBank>().WithMany().HasForeignKey(x => x.QuestionBankId);

            // §8.7 generates by topic, and the topic is the lesson. Optional: a
            // question may span the course.
            builder.HasOne<Lesson>().WithMany().HasForeignKey(x => x.LessonId);

            builder.HasIndex(x => new { x.RootQuestionId, x.Version }, "UQ_Question_Root_Version").IsUnique();

            // The pick query: current, undeprecated, filtered by type and
            // difficulty. Indexed on what it filters, per docs/Database/01.
            builder.HasIndex(x => new { x.QuestionBankId, x.IsCurrentVersion, x.IsDeprecated }, "IX_Question_Bank_Live");
        }
    }

    /// <summary>
    /// doc/Modules/37 §7 lrn.QuestionOption. Bound to the question <em>version</em>
    /// rather than to its root: a revision brings its own options, so the choices
    /// a student was shown last term cannot be rewritten by this term's edit
    /// (BR-LRN-007).
    /// </summary>
    public class QuestionOptionConfiguration : IEntityTypeConfiguration<QuestionOption>
    {
        public void Configure(EntityTypeBuilder<QuestionOption> builder)
        {
            builder.ToTable("QuestionOption", "lrn");

            builder.Property(x => x.TextAr).IsRequired();
            builder.Property(x => x.TextEn).IsRequired();

            builder.HasOne<Question>().WithMany().HasForeignKey(x => x.QuestionId);

            builder.HasIndex(x => new { x.QuestionId, x.DisplayOrder }, "IX_QuestionOption_Question_Order");
        }
    }

    /// <summary>
    /// doc/Modules/37 §7, BR-LRN-011 lrn.QuestionAcceptedAnswer: the spellings a
    /// short-text or numeric question accepts, per question version for the same
    /// reason options are.
    /// </summary>
    public class QuestionAcceptedAnswerConfiguration : IEntityTypeConfiguration<QuestionAcceptedAnswer>
    {
        public void Configure(EntityTypeBuilder<QuestionAcceptedAnswer> builder)
        {
            builder.ToTable("QuestionAcceptedAnswer", "lrn");

            builder.Property(x => x.Text).IsRequired().HasMaxLength(400);

            builder.HasOne<Question>().WithMany().HasForeignKey(x => x.QuestionId);

            builder.HasIndex(x => x.QuestionId, "IX_QuestionAcceptedAnswer_Question");
        }
    }
}
