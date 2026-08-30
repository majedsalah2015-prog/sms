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
}
