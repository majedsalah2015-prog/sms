using System;
using System.Collections.Generic;
using Sms.Domain.Grades;
using Sms.Domain.Grading;
using Sms.Domain.Schools;
using Sms.Domain.Sections;
using Sms.Domain.Students;
using Sms.Domain.Subjects;

namespace Sms.Web.Models
{
    // ---------------------------------------------------------------- Grading (doc/Modules/17 §8, E-302 basic subset)

    /// <summary>Year picker + the working year's structure every grading screen needs.</summary>
    public abstract class GradingPageViewModel
    {
        public sealed record ProfileOption(int ProfileId, GradeLevel Grade, Stage Stage);

        public IReadOnlyList<AcademicYear> Years { get; set; } = Array.Empty<AcademicYear>();

        public AcademicYear? Year { get; set; }

        public IReadOnlyList<ProfileOption> Profiles { get; set; } = Array.Empty<ProfileOption>();

        public IReadOnlyList<Term> Terms { get; set; } = Array.Empty<Term>();
    }

    // ---- 8.4 Marksheet workspace: list + create ----

    public sealed class MarksheetListViewModel : GradingPageViewModel
    {
        public sealed record Row(Marksheet Sheet, Subject Subject, GradeLevel Grade, Term Term, Section Section, int Total, int Resolved);

        public sealed record BlueprintOption(int BlueprintId, Subject Subject, GradeLevel Grade, Term Term, int ProfileId);

        public sealed record SectionOption(Section Section, GradeLevel Grade);

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        public MarksheetStatus? Status { get; set; }

        public int? SectionId { get; set; }

        public IReadOnlyList<BlueprintOption> LockedBlueprints { get; set; } = Array.Empty<BlueprintOption>();

        public IReadOnlyList<SectionOption> Sections { get; set; } = Array.Empty<SectionOption>();

        public Dictionary<MarksheetStatus, int> CountsByStatus { get; set; } = new();
    }

    // ---- 8.4 Marksheet workspace: the grid ----

    public sealed class MarksheetWorkspaceViewModel
    {
        public sealed record Cell(MarkEntry Entry);

        public sealed record StudentRow(Enrollment Enrollment, Student Student, IReadOnlyList<MarkEntry> Entries, decimal? PreviewPercent, ScaleBand? PreviewBand, bool Resolved);

        public Marksheet Sheet { get; set; } = null!;

        public Blueprint Blueprint { get; set; } = null!;

        public IReadOnlyList<BlueprintComponent> Components { get; set; } = Array.Empty<BlueprintComponent>();

        public IReadOnlyList<ScaleBand> Bands { get; set; } = Array.Empty<ScaleBand>();

        public GradingScale Scale { get; set; } = null!;

        public Subject Subject { get; set; } = null!;

        public GradeLevel Grade { get; set; } = null!;

        public Term Term { get; set; } = null!;

        public Section Section { get; set; } = null!;

        public AcademicYear Year { get; set; } = null!;

        public IReadOnlyList<StudentRow> Students { get; set; } = Array.Empty<StudentRow>();

        public IReadOnlyList<MarksheetStatus> AllowedTransitions { get; set; } = Array.Empty<MarksheetStatus>();

        public bool IsEditable => Sheet.Status == MarksheetStatus.Draft;

        public bool HasAnyMark { get; set; }

        public int Resolved { get; set; }

        public int Total { get; set; }

        /// <summary>BR-GRA-005 WF-08: only Published sheets can be reopened for correction.</summary>
        public bool CanCorrect => Sheet.Status == MarksheetStatus.Published;

        public IReadOnlyList<(string Action, string? Field, string? Old, string? New, DateTime At, int Actor, string? Reason)> Audit { get; set; } = Array.Empty<(string, string?, string?, string?, DateTime, int, string?)>();
    }

    // ---- 8.1 Scale designer ----

    public sealed class ScaleDesignerViewModel : GradingPageViewModel
    {
        public sealed record ScaleRow(GradingScale Scale, Stage Stage, int BandCount, int BlueprintCount);

        public IReadOnlyList<ScaleRow> Scales { get; set; } = Array.Empty<ScaleRow>();

        public IReadOnlyList<Stage> Stages { get; set; } = Array.Empty<Stage>();

        public IReadOnlyList<(int Id, string Ar, string En)> Curricula { get; set; } = Array.Empty<(int, string, string)>();

        public GradingScale? Selected { get; set; }

        public Stage? SelectedStage { get; set; }

        public IReadOnlyList<ScaleBand> Bands { get; set; } = Array.Empty<ScaleBand>();

        /// <summary>Gaps/overlaps between bands over 0–100 — the designer's visual-preview warnings.</summary>
        public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();

        public int SelectedBlueprintCount { get; set; }
    }

    // ---- 8.2 Blueprint & weights editor ----

    public sealed class BlueprintListViewModel : GradingPageViewModel
    {
        public sealed record OfferingRow(CurriculumOffering Offering, Subject Subject, Blueprint? Blueprint, int ComponentCount, decimal WeightSum, int MarksheetCount);

        public ProfileOption? Profile { get; set; }

        public Term? Term { get; set; }

        public IReadOnlyList<OfferingRow> Offerings { get; set; } = Array.Empty<OfferingRow>();

        public IReadOnlyList<GradingScale> Scales { get; set; } = Array.Empty<GradingScale>();
    }

    public sealed class BlueprintEditorViewModel
    {
        public Blueprint Blueprint { get; set; } = null!;

        public CurriculumOffering Offering { get; set; } = null!;

        public Subject Subject { get; set; } = null!;

        public GradeLevel Grade { get; set; } = null!;

        public Term Term { get; set; } = null!;

        public AcademicYear Year { get; set; } = null!;

        public GradingScale Scale { get; set; } = null!;

        public IReadOnlyList<BlueprintComponent> Components { get; set; } = Array.Empty<BlueprintComponent>();

        public decimal WeightSum { get; set; }

        public int MarksheetCount { get; set; }

        public int ProfileId { get; set; }
    }

    // ---- 8.3 Criteria designer ----

    public sealed class CriteriaDesignerViewModel : GradingPageViewModel
    {
        public sealed record Row(ProfileOption Profile, PromotionCriteria? Criteria, int OfferingCount, int YearResultCount);

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();
    }

    // ---- 8.5 Results explorer ----

    public sealed class ResultsExplorerViewModel : GradingPageViewModel
    {
        public sealed record OfferingCol(CurriculumOffering Offering, Subject Subject);

        public sealed record StudentRow(Enrollment Enrollment, Student Student, IReadOnlyDictionary<int, (TermResult Result, ScaleBand? Band)> ByOffering, decimal? Average, int? Rank, YearResult? YearResult, int FailedCount);

        public sealed record SectionOption(Section Section, GradeLevel Grade);

        public IReadOnlyList<SectionOption> Sections { get; set; } = Array.Empty<SectionOption>();

        public Section? Section { get; set; }

        public GradeLevel? Grade { get; set; }

        public int? ProfileId { get; set; }

        public Term? Term { get; set; }

        public IReadOnlyList<OfferingCol> Offerings { get; set; } = Array.Empty<OfferingCol>();

        public IReadOnlyList<StudentRow> Students { get; set; } = Array.Empty<StudentRow>();

        public bool HasCriteria { get; set; }
    }

    // ---- 8.6 Report card (HTML render; PDF blocked on O6) ----

    public sealed class ReportCardViewModel
    {
        public sealed record Line(Subject Subject, CurriculumOffering Offering, TermResult? Result, ScaleBand? Band);

        public Student Student { get; set; } = null!;

        public Enrollment Enrollment { get; set; } = null!;

        public AcademicYear Year { get; set; } = null!;

        public Term Term { get; set; } = null!;

        public IReadOnlyList<Term> Terms { get; set; } = Array.Empty<Term>();

        public GradeLevel Grade { get; set; } = null!;

        public Section? Section { get; set; }

        public string SchoolNameAr { get; set; } = string.Empty;

        public string SchoolNameEn { get; set; } = string.Empty;

        public IReadOnlyList<Line> Lines { get; set; } = Array.Empty<Line>();

        public decimal? Average { get; set; }

        public ScaleBand? AverageBand { get; set; }

        public int? Rank { get; set; }

        public int? RankOf { get; set; }

        /// <summary>BR-GRA-004 / BR-ATD-009: the single central attendance % (null when no attendance rows exist).</summary>
        public decimal? AttendancePercent { get; set; }

        public int ScheduledDays { get; set; }

        public int AbsentDays { get; set; }

        public bool AllPublished { get; set; }

        public bool IsReprint { get; set; }
    }
}

namespace Sms.Web.Models
{
    /// <summary>Bilingual labels for the grading enums the screens print.</summary>
    public static class GradingLabels
    {
        public static string MarksheetStatus(Sms.Domain.Grading.MarksheetStatus s, bool arabic) => s switch
        {
            Sms.Domain.Grading.MarksheetStatus.Draft => arabic ? "مسودة" : "Draft",
            Sms.Domain.Grading.MarksheetStatus.Submitted => arabic ? "مُقدَّم" : "Submitted",
            Sms.Domain.Grading.MarksheetStatus.HoDReviewed => arabic ? "راجعه رئيس القسم" : "HoD reviewed",
            Sms.Domain.Grading.MarksheetStatus.Approved => arabic ? "معتمد" : "Approved",
            Sms.Domain.Grading.MarksheetStatus.Published => arabic ? "منشور" : "Published",
            _ => s.ToString(),
        };

        public static string MarksheetBadge(Sms.Domain.Grading.MarksheetStatus s) => s switch
        {
            Sms.Domain.Grading.MarksheetStatus.Draft => "text-bg-secondary",
            Sms.Domain.Grading.MarksheetStatus.Submitted => "text-bg-info",
            Sms.Domain.Grading.MarksheetStatus.HoDReviewed => "text-bg-primary",
            Sms.Domain.Grading.MarksheetStatus.Approved => "text-bg-warning",
            Sms.Domain.Grading.MarksheetStatus.Published => "text-bg-success",
            _ => "text-bg-light",
        };

        public static string PromotionOutcome(Sms.Domain.Grading.PromotionOutcome o, bool arabic) => o switch
        {
            Sms.Domain.Grading.PromotionOutcome.Promote => arabic ? "ينتقل" : "Promote",
            Sms.Domain.Grading.PromotionOutcome.Conditional => arabic ? "انتقال مشروط" : "Conditional",
            Sms.Domain.Grading.PromotionOutcome.Retain => arabic ? "يُعيد" : "Retain",
            _ => o.ToString(),
        };
    }
}
