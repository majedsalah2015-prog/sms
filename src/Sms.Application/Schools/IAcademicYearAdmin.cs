using System;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Schools;

namespace Sms.Application.Schools
{
    /// <summary>
    /// doc/Modules/03 §8 "Year definition"/status-board screens backing
    /// (screens deferred, the lifecycle operations are core). Working-year
    /// switching (BR-AYR-010) stays on the IWorkingYearContext stub
    /// (StaticTenantContext, E-002) — real per-session switching is Web/UI
    /// work, not this slice.
    /// </summary>
    public interface IAcademicYearAdmin
    {
        /// <summary>Creates a new year in Preparation. Throws <see cref="Common.Exceptions.InvalidAcademicYearDatesException"/> or <see cref="Common.Exceptions.DuplicatePreparationYearException"/>.</summary>
        Task<AcademicYear> DefineYearAsync(
            string labelAr, string labelEn, string hijriLabel, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        /// <summary>BR-AYR-004: Preparation → Active; the school's current Active year (if any) moves to Closing in the same operation.</summary>
        Task ActivateAsync(int academicYearId, CancellationToken cancellationToken = default);

        /// <summary>BR-AYR-005: Closing → Closed.</summary>
        Task CloseAsync(int academicYearId, CancellationToken cancellationToken = default);

        /// <summary>BR-AYR-006: Closed → Archived.</summary>
        Task ArchiveAsync(int academicYearId, CancellationToken cancellationToken = default);

        /// <summary>BR-AYR-007 structure builder: creates or updates (by sequence) a semester nested inside the year's dates; throws <see cref="Common.Exceptions.InvalidPeriodDatesException"/> on nesting/overlap violations (doc §9).</summary>
        Task<Semester> DefineSemesterAsync(int academicYearId, int sequenceNumber, string nameAr, string nameEn, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        /// <summary>BR-AYR-007: creates or updates (by sequence) a term nested inside its semester.</summary>
        Task<Term> DefineTermAsync(int semesterId, int sequenceNumber, string nameAr, string nameEn, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    }
}
