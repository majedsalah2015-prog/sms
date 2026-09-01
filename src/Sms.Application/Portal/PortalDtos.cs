using System;
using System.Collections.Generic;

namespace Sms.Application.Portal
{
    public class PortalChildSummary
    {
        public int StudentId { get; set; }

        public string StudentNo { get; set; } = string.Empty;

        public string FirstNameAr { get; set; } = string.Empty;

        public string FirstNameEn { get; set; } = string.Empty;
    }

    /// <summary>BR-ATD-009 computation for the student's current-year enrollment.</summary>
    public class PortalAttendanceSummary
    {
        public int ScheduledDays { get; set; }

        public int ExemptedDays { get; set; }

        public int AbsentDays { get; set; }

        public decimal AttendancePercent { get; set; }
    }

    /// <summary>Only ever backed by published TermResult rows (BR-SEC-012) — drafts don't reach the portal because they don't exist until publication.</summary>
    public class PortalResultSummary
    {
        public int CurriculumOfferingId { get; set; }

        public int TermId { get; set; }

        public decimal ScorePercent { get; set; }

        public string? BandCode { get; set; }

        public DateTime PublishedAtUtc { get; set; }
    }

    public class PortalChargeLine
    {
        public string ChargeNo { get; set; } = string.Empty;

        public decimal GrossAmount { get; set; }

        public DateTime PostedAtUtc { get; set; }
    }

    /// <summary>Only ever backed by Posted charges (BR-SEC-012) — Void charges are excluded by the underlying query.</summary>
    public class PortalFeePosition
    {
        public decimal Position { get; set; }

        /// <summary>BR-DIS-010: gross and discounts shown separately - never netted invisibly.</summary>
        public decimal GrossCharges { get; set; }

        public decimal Discounts { get; set; }

        public IReadOnlyList<PortalChargeLine> Charges { get; set; } = Array.Empty<PortalChargeLine>();
    }

    /// <summary>
    /// doc/Modules/37 §8.10 — one piece of work set to the student's section.
    ///
    /// <para>
    /// Carries no submission and no mark. Both are later slices (§8.4-5), and
    /// stating that here is better than a nullable field the view would have to
    /// explain: this module's portal surface currently answers "what has been
    /// set and when is it due", not "what did I hand in".
    /// </para>
    /// </summary>
    public class PortalSetWork
    {
        public int HomeworkId { get; set; }

        public string TitleAr { get; set; } = string.Empty;

        public string TitleEn { get; set; } = string.Empty;

        public string? InstructionsAr { get; set; }

        public string? InstructionsEn { get; set; }

        public string SubjectNameAr { get; set; } = string.Empty;

        public string SubjectNameEn { get; set; } = string.Empty;

        public DateTime DueDate { get; set; }

        /// <summary>BR-LRN-004: null is ungraded practice, which the portal says plainly rather than showing a blank mark column.</summary>
        public decimal? MaxMarks { get; set; }

        /// <summary>BR-LRN-005: shown so a family knows late work is accepted and what it costs, before the date rather than after.</summary>
        public bool LatePenaltyApplies { get; set; }

        public decimal? LatePenaltyPercent { get; set; }
    }

    /// <summary>
    /// doc/Modules/37 §5 — one published lesson on the student's syllabus, with
    /// the material filed against it.
    ///
    /// <para>
    /// The module's portal contract is "read content, submit homework, sit an
    /// exam" (§5), and content is the half that needs no write surface. Only
    /// <c>Published</c> lessons ever become one of these (BR-LRN-003 /
    /// BR-SEC-012): a draft the teacher is still writing does not exist here,
    /// and a retired one has been withdrawn from the week deliberately
    /// (BR-LRN-016).
    /// </para>
    /// </summary>
    public class PortalLesson
    {
        public int LessonId { get; set; }

        /// <summary>§8.1 — the planner is an offering × week grid, so the week is how a family finds "this week's lesson".</summary>
        public int WeekNumber { get; set; }

        public string TitleAr { get; set; } = string.Empty;

        public string TitleEn { get; set; } = string.Empty;

        public string? ObjectivesAr { get; set; }

        public string? ObjectivesEn { get; set; }

        public string SubjectNameAr { get; set; } = string.Empty;

        public string SubjectNameEn { get; set; } = string.Empty;

        /// <summary>BR-LRN-003: the moment the family could first see this. Shown, because "new since I last looked" is the question a student opens the page with.</summary>
        public DateTime? PublishedAtUtc { get; set; }

        /// <summary>BR-LRN-006: scan-clean material only — see <see cref="PortalLessonResource"/>.</summary>
        public IReadOnlyList<PortalLessonResource> Resources { get; set; } = Array.Empty<PortalLessonResource>();
    }

    /// <summary>
    /// doc/Modules/37 §8.2 — one downloadable item of teaching material.
    ///
    /// <para>
    /// BR-LRN-006: "an unscanned or infected file is never served, to staff or
    /// to the portal". The portal reads that at its strictest and never even
    /// <em>lists</em> a resource whose current version is not scan-clean. A row
    /// a family cannot open is a support call; the alternative — waiting until
    /// the scan clears — is a file that appears a minute later on its own.
    /// </para>
    /// </summary>
    public class PortalLessonResource
    {
        public int ResourceId { get; set; }

        public string TitleAr { get; set; } = string.Empty;

        public string TitleEn { get; set; } = string.Empty;

        /// <summary>
        /// What kind of material this is — the document type's own name (worksheet,
        /// reading, enrichment material, slides, lesson plan).
        ///
        /// <para>
        /// Added after the owner reported the portal offering "no worksheets and
        /// no enrichment material": both were in fact reaching families, as an
        /// undifferentiated list of titles under each lesson. A student looking
        /// for this week's worksheet could not tell it from the lesson plan
        /// without opening both, which from their side is indistinguishable from
        /// the school not having set one.
        /// </para>
        ///
        /// <para>
        /// Empty when the type cannot be resolved — a retired document type still
        /// names itself, so this is genuinely "unknown" rather than "retired",
        /// and the view falls back to showing no kind rather than a wrong one.
        /// </para>
        /// </summary>
        public string TypeAr { get; set; } = string.Empty;

        /// <summary>The English half of <see cref="TypeAr"/>.</summary>
        public string TypeEn { get; set; } = string.Empty;

        /// <summary>Teacher-controlled order — material is read in a sequence, not alphabetically.</summary>
        public int DisplayOrder { get; set; }
    }
}
