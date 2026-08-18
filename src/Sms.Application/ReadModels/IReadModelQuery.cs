using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Cafeteria;

namespace Sms.Application.ReadModels
{
    /// <summary>vw_StudentPosition row — BR-FEE-008 single math (StudentFinancialPositionCalculator) per student × payer.</summary>
    public sealed record StudentPositionRow(int StudentId, int PayerId, decimal Charges, decimal CreditNotes, decimal Discounts, decimal Allocated, decimal Position);

    /// <summary>vw_AttendanceRates row — BR-ATD-009 canonical percentage per enrollment over a date range.</summary>
    public sealed record AttendanceRateRow(int EnrollmentId, int ScheduledDays, int ExemptedDays, int AbsentDays, decimal AttendancePercent);

    /// <summary>vw_TeacherLoad row — BR-TCH-004 current weekly periods vs the teacher's max.</summary>
    public sealed record TeacherLoadRow(int TeacherProfileId, int EmployeeId, int CurrentWeeklyPeriods, int MaxWeeklyPeriods, bool IsOverloaded);

    /// <summary>vw_SeatUtilization row — planned seats (BR-GRD-006) vs section capacity vs Active enrollment vs admissions pipeline.</summary>
    public sealed record SeatUtilizationRow(int GradeYearProfileId, int PlannedSeats, int SectionCapacity, int Enrolled, int PipelineApplications, int FreeSeats);

    /// <summary>vw_WalletBalance row — BR-CAF-007 ledger-derived balance.</summary>
    public sealed record WalletBalanceRow(int WalletId, WalletHolderKind HolderKind, int HolderId, decimal Balance);

    /// <summary>
    /// DB/04 §4 "views" — the read-model layer statements, dashboards and reports call instead of re-deriving
    /// numbers. Each method reuses the owning module's own calculator (BR-DSH-002 / BR-FEE-008 "one computation
    /// source"): these are the EF-native equivalents of vw_StudentPosition, vw_AttendanceRates, vw_TeacherLoad,
    /// vw_SeatUtilization and vw_WalletBalance (SQL Server indexed views are DB/04 Q1's implementation-time
    /// choice; the contract stays the same either way).
    /// </summary>
    public interface IReadModelQuery
    {
        /// <summary>All (student, payer) positions with at least one posted charge; <paramref name="studentId"/> narrows to one student.</summary>
        Task<IReadOnlyList<StudentPositionRow>> GetStudentPositionsAsync(int? studentId = null, CancellationToken cancellationToken = default);

        /// <summary>Attendance rates for a section's captured days in [from, to] (inclusive), one row per enrollment with any capture.</summary>
        Task<IReadOnlyList<AttendanceRateRow>> GetAttendanceRatesAsync(int sectionId, DateTime from, DateTime to, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<TeacherLoadRow>> GetTeacherLoadsAsync(int academicYearId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<SeatUtilizationRow>> GetSeatUtilizationAsync(int academicYearId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<WalletBalanceRow>> GetWalletBalancesAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// DB/04 §4 snapshot refreshers — each replaces the school's rows of one snapshot table as of "now" and returns
    /// the row count written. Called by the matching IJobHandler on the ops.JobDefinition schedule; callable ad hoc.
    /// </summary>
    public interface ISnapshotRefreshService
    {
        /// <summary>snap_AgedReceivables (RPT-FEE-004): open remainder per (payer, student, year) bucketed by posting-date age.</summary>
        Task<int> RefreshAgedReceivablesAsync(int currentDays = ReceivablesAgingBucketer.DefaultCurrentDays, CancellationToken cancellationToken = default);

        /// <summary>snap_DailyAttendanceSummary for one date, per section (Principal/VP widgets).</summary>
        Task<int> RefreshDailyAttendanceSummaryAsync(DateTime date, CancellationToken cancellationToken = default);

        /// <summary>snap_CollectionCalendar (RPT-INS-001): due-date rows over [today − lookbackDays, today + horizonDays].</summary>
        Task<int> RefreshCollectionCalendarAsync(int horizonDays = 120, int lookbackDays = 90, CancellationToken cancellationToken = default);
    }

    /// <summary>ops.JobDefinition codes for the snapshot refresh jobs (register with IJobDefinitionAdmin; handlers match on these).</summary>
    public static class SnapshotJobCodes
    {
        public const string AgedReceivables = "SnapshotAgedReceivables";
        public const string DailyAttendanceSummary = "SnapshotDailyAttendanceSummary";
        public const string CollectionCalendar = "SnapshotCollectionCalendar";
    }
}
