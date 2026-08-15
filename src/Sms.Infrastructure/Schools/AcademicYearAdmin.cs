using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Schools;
using Sms.Domain.Schools;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Schools
{
    /// <summary>Standalone admin operations — save themselves, no larger transaction to ride.</summary>
    public class AcademicYearAdmin : IAcademicYearAdmin
    {
        private readonly AppDbContext _db;

        public AcademicYearAdmin(AppDbContext db)
        {
            _db = db;
        }

        public async Task<AcademicYear> DefineYearAsync(
            string labelAr, string labelEn, string hijriLabel, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            if (!AcademicYearValidation.HasValidSpan(startDate, endDate))
            {
                throw new InvalidAcademicYearDatesException("span must be 6-14 months with an end date after the start date");
            }

            // Proactive check for a clear error on the expected path; the filtered
            // unique indexes are the concurrency-safe backstop (same pattern as E-006).
            var existingYears = await _db.AcademicYears.ToListAsync(cancellationToken);
            if (existingYears.Any(y => AcademicYearValidation.Overlaps(startDate, endDate, y.StartDate, y.EndDate)))
            {
                throw new InvalidAcademicYearDatesException("overlaps an existing academic year for this school");
            }

            if (existingYears.Any(y => y.Status == AcademicYearStatus.Preparation))
            {
                throw new DuplicatePreparationYearException();
            }

            var year = new AcademicYear
            {
                LabelAr = labelAr,
                LabelEn = labelEn,
                HijriLabel = hijriLabel,
                StartDate = startDate,
                EndDate = endDate,
                Status = AcademicYearStatus.Preparation,
            };
            _db.AcademicYears.Add(year);
            await _db.SaveChangesAsync(cancellationToken);
            return year;
        }

        public async Task ActivateAsync(int academicYearId, CancellationToken cancellationToken = default)
        {
            var year = await _db.AcademicYears.SingleAsync(y => y.Id == academicYearId, cancellationToken);
            RequireTransition(year.Status, AcademicYearStatus.Active);

            // BR-AYR-004: activating the new year moves the prior Active to Closing.
            var currentActive = await _db.AcademicYears.SingleOrDefaultAsync(y => y.Status == AcademicYearStatus.Active, cancellationToken);
            if (currentActive != null)
            {
                currentActive.Status = AcademicYearStatus.Closing;
            }

            year.Status = AcademicYearStatus.Active;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task CloseAsync(int academicYearId, CancellationToken cancellationToken = default)
        {
            var year = await _db.AcademicYears.SingleAsync(y => y.Id == academicYearId, cancellationToken);
            RequireTransition(year.Status, AcademicYearStatus.Closed);

            year.Status = AcademicYearStatus.Closed;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task ArchiveAsync(int academicYearId, CancellationToken cancellationToken = default)
        {
            var year = await _db.AcademicYears.SingleAsync(y => y.Id == academicYearId, cancellationToken);
            RequireTransition(year.Status, AcademicYearStatus.Archived);

            year.Status = AcademicYearStatus.Archived;
            await _db.SaveChangesAsync(cancellationToken);
        }

        private static void RequireTransition(AcademicYearStatus from, AcademicYearStatus to)
        {
            if (!AcademicYearStatusTransitions.CanTransition(from, to))
            {
                throw new InvalidAcademicYearStatusTransitionException(from, to);
            }
        }
    }
}
