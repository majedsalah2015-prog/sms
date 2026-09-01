using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Guards;
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
                throw new InvalidAcademicYearDatesException(AcademicYearDateFault.SpanOutOfRange);
            }

            // Proactive check for a clear error on the expected path; the filtered
            // unique indexes are the concurrency-safe backstop (same pattern as E-006).
            var existingYears = await _db.AcademicYears.ToListAsync(cancellationToken);
            if (existingYears.Any(y => AcademicYearValidation.Overlaps(startDate, endDate, y.StartDate, y.EndDate)))
            {
                throw new InvalidAcademicYearDatesException(AcademicYearDateFault.OverlapsAnotherYear);
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

        public async Task<AcademicYear> UpdateYearAsync(
            int academicYearId, string labelAr, string labelEn, string hijriLabel, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            var year = await _db.AcademicYears.SingleAsync(y => y.Id == academicYearId, cancellationToken);
            await RequireNoEnrollmentsAsync(academicYearId, cancellationToken);

            if (!AcademicYearValidation.HasValidSpan(startDate, endDate))
            {
                throw new InvalidAcademicYearDatesException(AcademicYearDateFault.SpanOutOfRange);
            }

            var otherYears = await _db.AcademicYears.AsNoTracking().Where(y => y.Id != academicYearId).ToListAsync(cancellationToken);
            if (otherYears.Any(y => AcademicYearValidation.Overlaps(startDate, endDate, y.StartDate, y.EndDate)))
            {
                throw new InvalidAcademicYearDatesException(AcademicYearDateFault.OverlapsAnotherYear);
            }

            // Shrinking the year must keep its semesters nested (BR-AYR-007).
            var semesters = await _db.Semesters.AsNoTracking().Where(s => s.AcademicYearId == academicYearId).ToListAsync(cancellationToken);
            foreach (var s in semesters)
            {
                RequireNested(s.StartDate, s.EndDate, startDate, endDate, SchoolPeriodKind.Semester);
            }

            year.LabelAr = labelAr;
            year.LabelEn = labelEn;
            year.HijriLabel = hijriLabel;
            year.StartDate = startDate;
            year.EndDate = endDate;
            await _db.SaveChangesAsync(cancellationToken);
            return year;
        }

        public async Task<AcademicYear> RelabelYearAsync(
            int academicYearId, string labelAr, string labelEn, string hijriLabel, CancellationToken cancellationToken = default)
        {
            // No RequireNoEnrollmentsAsync, and no date validation: nothing here touches the span the
            // rest of the schema nests inside. See the contract for why that exemption is the point of
            // this method rather than an oversight in it.
            var year = await _db.AcademicYears.SingleAsync(y => y.Id == academicYearId, cancellationToken);

            year.LabelAr = labelAr;
            year.LabelEn = labelEn;
            year.HijriLabel = hijriLabel;
            await _db.SaveChangesAsync(cancellationToken);
            return year;
        }

        public async Task DeleteYearAsync(int academicYearId, CancellationToken cancellationToken = default)
        {
            var year = await _db.AcademicYears.SingleAsync(y => y.Id == academicYearId, cancellationToken);
            await RequireNoEnrollmentsAsync(academicYearId, cancellationToken);

            // Proactive checks for the M03-adjacent dependents so the user gets a
            // clear reason; every other FK is Restrict and surfaces as DbUpdateException.
            if (await _db.RolloverBatches.AnyAsync(b => b.SourceAcademicYearId == academicYearId || b.TargetAcademicYearId == academicYearId, cancellationToken))
            {
                throw new AcademicYearInUseException(UsageReport.From(new UsageReference("rollover batch(es)", "دفعة ترحيل", 1)));
            }

            if (await _db.GradeYearProfiles.AnyAsync(p => p.AcademicYearId == academicYearId, cancellationToken)
                || await _db.Sections.AnyAsync(s => s.AcademicYearId == academicYearId, cancellationToken))
            {
                throw new AcademicYearInUseException(UsageReport.From(new UsageReference("grade or section definition(s) for this year", "تعريف صف أو شعبة في هذا العام", 1)));
            }

            if (await _db.CalendarDays.AnyAsync(d => d.AcademicYearId == academicYearId, cancellationToken)
                || await _db.CalendarEvents.AnyAsync(e => e.AcademicYearId == academicYearId, cancellationToken)
                || await _db.CalendarVersions.AnyAsync(v => v.AcademicYearId == academicYearId, cancellationToken))
            {
                throw new AcademicYearInUseException(UsageReport.From(new UsageReference("calendar day(s) or event(s)", "يوم أو حدث في التقويم", 1)));
            }

            _db.Terms.RemoveRange(await _db.Terms.Where(t => t.AcademicYearId == academicYearId).ToListAsync(cancellationToken));
            _db.Semesters.RemoveRange(await _db.Semesters.Where(s => s.AcademicYearId == academicYearId).ToListAsync(cancellationToken));
            _db.AcademicYears.Remove(year);

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                // The database refused on a foreign key the proactive checks above do not name.
                // The provider's own message is English SQL and belongs in the log, not on a screen,
                // so it goes to the exception's inner slot while the user is told, in their own
                // language, that something still points at the year.
                throw new AcademicYearInUseException(
                    UsageReport.From(new UsageReference("record(s) elsewhere in the system", "سجل في مواضع أخرى من النظام", 1)),
                    ex);
            }
        }

        private async Task RequireNoEnrollmentsAsync(int academicYearId, CancellationToken cancellationToken)
        {
            var enrolled = await _db.Enrollments.CountAsync(e => e.AcademicYearId == academicYearId, cancellationToken);
            if (enrolled > 0)
            {
                throw new AcademicYearInUseException(UsageReport.From(new UsageReference("student enrollment(s)", "قيد طالب", enrolled)));
            }
        }

        public async Task ActivateAsync(int academicYearId, CancellationToken cancellationToken = default)
        {
            var year = await _db.AcademicYears.SingleAsync(y => y.Id == academicYearId, cancellationToken);
            RequireTransition(year.Status, AcademicYearStatus.Active);

            // E-101 / BR-SET-003: the school's FIRST activation is gated on the
            // Setup Wizard being declared complete. "First" = no year of this
            // school has ever left Preparation. Later activations (rollover)
            // aren't re-gated. Evaluated against the tenant's School row; if the
            // row is absent (only possible in fixtures — a real tenant always
            // has one) there is no wizard to gate on.
            var everActivated = await _db.AcademicYears.AnyAsync(y => y.Status != AcademicYearStatus.Preparation, cancellationToken);
            if (!everActivated)
            {
                var school = await _db.Schools.AsNoTracking().SingleOrDefaultAsync(s => s.Id == year.SchoolId, cancellationToken);
                if (school != null && school.SetupCompletedAtUtc == null)
                {
                    var completed = await _db.SetupChecklists.AsNoTracking()
                        .Where(c => c.Status == Sms.Domain.Setup.SetupStepStatus.Completed)
                        .Select(c => c.StepCode)
                        .ToListAsync(cancellationToken);
                    var pending = Sms.Application.Setup.SetupWizardSteps.All
                        .Where(s => s.IsMandatory && !completed.Contains(s.Code))
                        .Select(s => s.Code)
                        .ToList();
                    throw new SetupIncompleteException(pending);
                }
            }

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

        public async Task<Semester> DefineSemesterAsync(
            int academicYearId, int sequenceNumber, string nameAr, string nameEn, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            var year = await _db.AcademicYears.SingleAsync(y => y.Id == academicYearId, cancellationToken);
            RequireNested(startDate, endDate, year.StartDate, year.EndDate, SchoolPeriodKind.Semester);

            var siblings = await _db.Semesters.Where(s => s.AcademicYearId == academicYearId && s.SequenceNumber != sequenceNumber).ToListAsync(cancellationToken);
            RequireNoOverlap(startDate, endDate, siblings.Select(s => (s.StartDate, s.EndDate)), SchoolPeriodKind.Semester);

            var semester = await _db.Semesters.SingleOrDefaultAsync(s => s.AcademicYearId == academicYearId && s.SequenceNumber == sequenceNumber, cancellationToken);
            if (semester == null)
            {
                semester = new Semester { AcademicYearId = academicYearId, SequenceNumber = sequenceNumber };
                _db.Semesters.Add(semester);
            }
            else
            {
                // Shrinking a semester must keep its terms nested.
                var terms = await _db.Terms.Where(t => t.SemesterId == semester.Id).ToListAsync(cancellationToken);
                foreach (var t in terms)
                {
                    RequireNested(t.StartDate, t.EndDate, startDate, endDate, SchoolPeriodKind.Term);
                }
            }

            semester.NameAr = nameAr;
            semester.NameEn = nameEn;
            semester.StartDate = startDate;
            semester.EndDate = endDate;
            await _db.SaveChangesAsync(cancellationToken);
            return semester;
        }

        public async Task<Term> DefineTermAsync(
            int semesterId, int sequenceNumber, string nameAr, string nameEn, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            var semester = await _db.Semesters.SingleAsync(s => s.Id == semesterId, cancellationToken);
            RequireNested(startDate, endDate, semester.StartDate, semester.EndDate, SchoolPeriodKind.Term);

            var siblings = await _db.Terms.Where(t => t.SemesterId == semesterId && t.SequenceNumber != sequenceNumber).ToListAsync(cancellationToken);
            RequireNoOverlap(startDate, endDate, siblings.Select(t => (t.StartDate, t.EndDate)), SchoolPeriodKind.Term);

            var term = await _db.Terms.SingleOrDefaultAsync(t => t.SemesterId == semesterId && t.SequenceNumber == sequenceNumber, cancellationToken);
            if (term == null)
            {
                term = new Term { AcademicYearId = semester.AcademicYearId, SemesterId = semesterId, SequenceNumber = sequenceNumber };
                _db.Terms.Add(term);
            }

            term.NameAr = nameAr;
            term.NameEn = nameEn;
            term.StartDate = startDate;
            term.EndDate = endDate;
            await _db.SaveChangesAsync(cancellationToken);
            return term;
        }

        private static void RequireNested(DateTime start, DateTime end, DateTime outerStart, DateTime outerEnd, SchoolPeriodKind kind)
        {
            if (end <= start)
            {
                throw new InvalidPeriodDatesException(PeriodDateFault.EndsBeforeItStarts, kind);
            }

            if (start < outerStart || end > outerEnd)
            {
                throw new InvalidPeriodDatesException(PeriodDateFault.NotInsideItsParent, kind);
            }
        }

        private static void RequireNoOverlap(DateTime start, DateTime end, System.Collections.Generic.IEnumerable<(DateTime Start, DateTime End)> siblings, SchoolPeriodKind kind)
        {
            if (siblings.Any(s => AcademicYearValidation.Overlaps(start, end, s.Start, s.End)))
            {
                throw new InvalidPeriodDatesException(PeriodDateFault.OverlapsASibling, kind);
            }
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
