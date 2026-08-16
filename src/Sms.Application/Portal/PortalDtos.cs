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

        public IReadOnlyList<PortalChargeLine> Charges { get; set; } = Array.Empty<PortalChargeLine>();
    }
}
