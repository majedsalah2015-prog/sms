using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Grading;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // core schema per doc/Modules/17 — same schema group as Section/Subjects/Grades.

    public class GradingScaleConfiguration : IEntityTypeConfiguration<GradingScale>
    {
        public void Configure(EntityTypeBuilder<GradingScale> builder)
        {
            builder.ToTable("GradingScale", "core");
            builder.Property(x => x.NameAr).HasMaxLength(100).IsRequired();
            builder.Property(x => x.NameEn).HasMaxLength(100).IsRequired();
        }
    }

    public class ScaleBandConfiguration : IEntityTypeConfiguration<ScaleBand>
    {
        public void Configure(EntityTypeBuilder<ScaleBand> builder)
        {
            builder.ToTable("ScaleBand", "core");
            builder.Property(x => x.BandCode).HasMaxLength(10).IsRequired();
            builder.Property(x => x.LabelAr).HasMaxLength(60).IsRequired();
            builder.Property(x => x.LabelEn).HasMaxLength(60).IsRequired();
            builder.Property(x => x.MinPercent).HasColumnType("decimal(5,2)");
            builder.Property(x => x.MaxPercent).HasColumnType("decimal(5,2)");
            builder.Property(x => x.GpaPoints).HasColumnType("decimal(4,2)");
            builder.HasOne<GradingScale>().WithMany().HasForeignKey(x => x.GradingScaleId);
        }
    }

    public class BlueprintConfiguration : IEntityTypeConfiguration<Blueprint>
    {
        public void Configure(EntityTypeBuilder<Blueprint> builder)
        {
            builder.ToTable("Blueprint", "core");
            builder.HasOne<GradingScale>().WithMany().HasForeignKey(x => x.GradingScaleId);
            builder.HasIndex(x => new { x.CurriculumOfferingId, x.TermId }).IsUnique();
        }
    }

    public class BlueprintComponentConfiguration : IEntityTypeConfiguration<BlueprintComponent>
    {
        public void Configure(EntityTypeBuilder<BlueprintComponent> builder)
        {
            builder.ToTable("BlueprintComponent", "core");
            builder.Property(x => x.NameAr).HasMaxLength(80).IsRequired();
            builder.Property(x => x.NameEn).HasMaxLength(80).IsRequired();
            builder.Property(x => x.Weight).HasColumnType("decimal(5,2)");
            builder.Property(x => x.MaxScore).HasColumnType("decimal(7,2)");
            builder.HasOne<Blueprint>().WithMany().HasForeignKey(x => x.BlueprintId);
        }
    }

    public class MarksheetConfiguration : IEntityTypeConfiguration<Marksheet>
    {
        public void Configure(EntityTypeBuilder<Marksheet> builder)
        {
            builder.ToTable("Marksheet", "core");
            builder.HasOne<Blueprint>().WithMany().HasForeignKey(x => x.BlueprintId);
            builder.HasIndex(x => new { x.BlueprintId, x.SectionId }).IsUnique();
        }
    }

    public class MarkEntryConfiguration : IEntityTypeConfiguration<MarkEntry>
    {
        public void Configure(EntityTypeBuilder<MarkEntry> builder)
        {
            builder.ToTable("MarkEntry", "core");
            builder.Property(x => x.Score).HasColumnType("decimal(7,2)");
            builder.HasOne<Marksheet>().WithMany().HasForeignKey(x => x.MarksheetId);
            builder.HasIndex(x => new { x.MarksheetId, x.BlueprintComponentId, x.EnrollmentId }, "IX_MarkEntry_Marksheet_Component_Enrollment").IsUnique();
        }
    }

    public class TermResultConfiguration : IEntityTypeConfiguration<TermResult>
    {
        public void Configure(EntityTypeBuilder<TermResult> builder)
        {
            builder.ToTable("TermResult", "core");
            builder.Property(x => x.ScorePercent).HasColumnType("decimal(5,2)");
            builder.Property(x => x.CalculationSnapshotJson).IsRequired();
            builder.HasIndex(x => new { x.EnrollmentId, x.CurriculumOfferingId, x.TermId }, "IX_TermResult_Enrollment_Offering_Term").IsUnique();
        }
    }

    public class PromotionCriteriaConfiguration : IEntityTypeConfiguration<PromotionCriteria>
    {
        public void Configure(EntityTypeBuilder<PromotionCriteria> builder)
        {
            builder.ToTable("PromotionCriteria", "core");
            builder.Property(x => x.OverallPassMark).HasColumnType("decimal(5,2)");
            builder.HasIndex(x => x.GradeYearProfileId).IsUnique();
        }
    }

    public class YearResultConfiguration : IEntityTypeConfiguration<YearResult>
    {
        public void Configure(EntityTypeBuilder<YearResult> builder)
        {
            builder.ToTable("YearResult", "core");
            builder.Property(x => x.Gpa).HasColumnType("decimal(4,2)");
            builder.HasIndex(x => new { x.EnrollmentId, x.AcademicYearId }, "IX_YearResult_Enrollment_Year").IsUnique();
        }
    }
}
