using System;
using Sms.Domain.Common;

namespace Sms.Domain.ReadModels
{
    /// <summary>
    /// DB/04 §4 read models: heavy statutory/analytical reports (RPT class An/Stat) and D/C15 dashboard
    /// widgets read these snapshot tables, never the hot OLTP tables (NF-P5 without contention). Every row
    /// carries <c>AsOfUtc</c> (BR-DSH-002 as-of display); a refresh replaces the school's rows wholesale
    /// via the ops.JobDefinition schedule. Deliberately plain rows: not audited (derived data), not
    /// IActivatable (a refresh hard-deletes and re-inserts), tenant-scoped like everything else.
    /// </summary>
    public abstract class SnapshotRow : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        /// <summary>When the underlying data was read — the "as of" every consumer must display.</summary>
        public DateTime AsOfUtc { get; set; }
    }

    /// <summary>snap_AgedReceivables (daily) — RPT-FEE-004 aged receivables by payer/student/grade; Finance donut (C15/D).</summary>
    public class AgedReceivablesSnapshot : SnapshotRow
    {
        public int PayerId { get; set; }

        public int StudentId { get; set; }

        public int AcademicYearId { get; set; }

        /// <summary>Grade-year profile of the student's Active enrollment in that year (null when the charge outlived the enrollment).</summary>
        public int? GradeYearProfileId { get; set; }

        public decimal Current { get; set; }

        public decimal Days1To30 { get; set; }

        public decimal Days31To60 { get; set; }

        public decimal Days61To90 { get; set; }

        public decimal Over90 { get; set; }

        public decimal Total { get; set; }
    }

    /// <summary>snap_DailyAttendanceSummary — Principal/VP widgets (Today's attendance % by stage/grade/section, C15/D).</summary>
    public class DailyAttendanceSummarySnapshot : SnapshotRow
    {
        public DateTime Date { get; set; }

        public int AcademicYearId { get; set; }

        public int SectionId { get; set; }

        public int GradeYearProfileId { get; set; }

        public int StageId { get; set; }

        public int ScheduledCount { get; set; }

        public int AbsentCount { get; set; }

        public int ExemptedCount { get; set; }

        public int LateCount { get; set; }

        /// <summary>BR-ATD-009 canonical percentage (AttendancePercentageCalculator) — never recomputed by a consumer.</summary>
        public decimal PresentPercent { get; set; }
    }

    /// <summary>snap_CollectionCalendar — RPT-INS-001 cashflow forecast: what is due per day, what of it is already collected.</summary>
    public class CollectionCalendarSnapshot : SnapshotRow
    {
        public DateTime DueDate { get; set; }

        public int AcademicYearId { get; set; }

        public int InstallmentCount { get; set; }

        public decimal ScheduledAmount { get; set; }

        public decimal PaidAmount { get; set; }

        public decimal OutstandingAmount { get; set; }

        /// <summary>Installments on this due date whose derived status is Overdue as of the snapshot.</summary>
        public int OverdueCount { get; set; }
    }
}
