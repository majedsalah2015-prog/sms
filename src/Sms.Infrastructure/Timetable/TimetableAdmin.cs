using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Calendar;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Timetable;
using Sms.Domain.Calendar;
using Sms.Domain.Timetable;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Timetable
{
    /// <summary>Standalone admin operations — save themselves, no larger transaction to ride.</summary>
    public class TimetableAdmin : ITimetableAdmin
    {
        private readonly AppDbContext _db;
        private readonly IClock _clock;

        public TimetableAdmin(AppDbContext db, IClock clock)
        {
            _db = db;
            _clock = clock;
        }

        public async Task<TimetableShape> DefineShapeAsync(int stageId, int academicYearId, CancellationToken cancellationToken = default)
        {
            var shape = new TimetableShape { StageId = stageId, AcademicYearId = academicYearId };
            _db.TimetableShapes.Add(shape);
            await _db.SaveChangesAsync(cancellationToken);
            return shape;
        }

        public async Task<PeriodSlot> AddPeriodSlotAsync(
            int shapeId, DayOfWeek dayOfWeek, int sequenceNumber, TimeSpan startTime, TimeSpan endTime,
            bool isBreak = false, CancellationToken cancellationToken = default)
        {
            var slot = new PeriodSlot
            {
                TimetableShapeId = shapeId, DayOfWeek = dayOfWeek, SequenceNumber = sequenceNumber,
                StartTime = startTime, EndTime = endTime, IsBreak = isBreak,
            };
            _db.PeriodSlots.Add(slot);
            await _db.SaveChangesAsync(cancellationToken);
            return slot;
        }

        public async Task<TimetableVersion> DefineVersionAsync(int academicYearId, int? termId = null, CancellationToken cancellationToken = default)
        {
            var version = new TimetableVersion { AcademicYearId = academicYearId, TermId = termId };
            _db.TimetableVersions.Add(version);
            await _db.SaveChangesAsync(cancellationToken);
            return version;
        }

        public async Task<Placement> PlaceAsync(
            int timetableVersionId, int sectionId, int periodSlotId, int curriculumOfferingId, int teacherProfileId,
            int? roomId = null, CancellationToken cancellationToken = default)
        {
            var hasAssignment = await _db.TeacherAssignments.AnyAsync(
                a => a.TeacherProfileId == teacherProfileId && a.CurriculumOfferingId == curriculumOfferingId
                    && a.SectionId == sectionId && a.EffectiveToUtc == null,
                cancellationToken);
            if (!hasAssignment)
            {
                throw new TeacherNotAssignedException(teacherProfileId, curriculumOfferingId, sectionId);
            }

            var existing = await _db.Placements
                .Where(p => p.TimetableVersionId == timetableVersionId)
                .Select(p => new { p.PeriodSlotId, p.SectionId, p.TeacherProfileId, p.RoomId })
                .ToListAsync(cancellationToken);
            var existingConflicts = existing
                .Select(p => new PlacementConflictDetector.ExistingPlacement(p.PeriodSlotId, p.SectionId, p.TeacherProfileId, p.RoomId))
                .ToList();

            if (PlacementConflictDetector.HasTeacherConflict(periodSlotId, teacherProfileId, existingConflicts))
            {
                throw new PlacementConflictException("teacher");
            }

            if (PlacementConflictDetector.HasSectionConflict(periodSlotId, sectionId, existingConflicts))
            {
                throw new PlacementConflictException("section");
            }

            if (PlacementConflictDetector.HasRoomConflict(periodSlotId, roomId, existingConflicts))
            {
                throw new PlacementConflictException("room");
            }

            var placement = new Placement
            {
                TimetableVersionId = timetableVersionId, SectionId = sectionId, PeriodSlotId = periodSlotId,
                CurriculumOfferingId = curriculumOfferingId, TeacherProfileId = teacherProfileId, RoomId = roomId,
            };
            _db.Placements.Add(placement);
            await _db.SaveChangesAsync(cancellationToken);
            return placement;
        }

        public async Task ValidateVersionAsync(int timetableVersionId, CancellationToken cancellationToken = default)
        {
            var version = await _db.TimetableVersions.SingleAsync(v => v.Id == timetableVersionId, cancellationToken);
            if (!TimetableVersionStatusTransitions.CanTransition(version.Status, TimetableVersionStatus.Validated))
            {
                throw new InvalidTimetableVersionStatusTransitionException(version.Status, TimetableVersionStatus.Validated);
            }

            var placements = await _db.Placements
                .Where(p => p.TimetableVersionId == timetableVersionId)
                .GroupBy(p => new { p.SectionId, p.CurriculumOfferingId })
                .Select(g => new { g.Key.SectionId, g.Key.CurriculumOfferingId, PlacedCount = g.Count() })
                .ToListAsync(cancellationToken);

            foreach (var group in placements)
            {
                var offering = await _db.CurriculumOfferings.SingleAsync(o => o.Id == group.CurriculumOfferingId, cancellationToken);
                if (!PlacementCompletenessEvaluator.IsComplete(group.PlacedCount, offering.WeeklyPeriods))
                {
                    throw new IncompletePlacementException(
                        group.CurriculumOfferingId, group.SectionId,
                        PlacementCompletenessEvaluator.Shortfall(group.PlacedCount, offering.WeeklyPeriods));
                }
            }

            version.Status = TimetableVersionStatus.Validated;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task PublishAsync(
            int timetableVersionId, int publishedByUserId, DateTime rangeStart, DateTime rangeEnd,
            ISet<DayOfWeek> weekendDays, CancellationToken cancellationToken = default)
        {
            var version = await _db.TimetableVersions.SingleAsync(v => v.Id == timetableVersionId, cancellationToken);
            if (!TimetableVersionStatusTransitions.CanTransition(version.Status, TimetableVersionStatus.Published))
            {
                throw new InvalidTimetableVersionStatusTransitionException(version.Status, TimetableVersionStatus.Published);
            }

            var placementSlots = await (
                from p in _db.Placements
                join s in _db.PeriodSlots on p.PeriodSlotId equals s.Id
                where p.TimetableVersionId == timetableVersionId
                select new SessionGenerator.PlacementSlot(p.Id, s.DayOfWeek)).ToListAsync(cancellationToken);

            var overrides = await _db.CalendarDays
                .Where(d => d.AcademicYearId == version.AcademicYearId)
                .ToDictionaryAsync(d => d.Date.Date, d => d.DayType, cancellationToken);

            bool IsWorkingDay(DateTime date) => CalendarDayResolver.Resolve(date, weekendDays, overrides) == DayType.Working;

            foreach (var (placementId, date) in SessionGenerator.Generate(rangeStart, rangeEnd, placementSlots, IsWorkingDay))
            {
                _db.Sessions.Add(new Session
                {
                    AcademicYearId = version.AcademicYearId, PlacementId = placementId, Date = date, Status = SessionStatus.Held,
                });
            }

            version.Status = TimetableVersionStatus.Published;
            version.PublishedAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<Substitution> AssignSubstituteAsync(
            int sessionId, int substituteTeacherProfileId, string reason, bool allowSuperviseOnly = false,
            bool isCountedForPayroll = true, CancellationToken cancellationToken = default)
        {
            var session = await _db.Sessions.SingleAsync(s => s.Id == sessionId, cancellationToken);
            var placement = await _db.Placements.SingleAsync(p => p.Id == session.PlacementId, cancellationToken);

            // Qualification proxy (doc's real BR-SUB-006 matrix keys off sec.UserAccount, not TeacherProfile -
            // the same identity-bridging gap flagged in E-203/E-304): a teacher already assigned to teach this
            // same offering elsewhere is treated as qualified to substitute it.
            var isQualified = await _db.TeacherAssignments.AnyAsync(
                a => a.TeacherProfileId == substituteTeacherProfileId && a.CurriculumOfferingId == placement.CurriculumOfferingId,
                cancellationToken);

            var alreadyPlaced = await (
                from p in _db.Placements
                join s in _db.Sessions on p.Id equals s.PlacementId
                where p.TeacherProfileId == substituteTeacherProfileId && p.PeriodSlotId == placement.PeriodSlotId
                    && s.Date == session.Date && s.Status != SessionStatus.Cancelled
                select s.Id).AnyAsync(cancellationToken);
            var alreadySubstituting = await (
                from sub in _db.Substitutions
                join s in _db.Sessions on sub.SessionId equals s.Id
                join p in _db.Placements on s.PlacementId equals p.Id
                where sub.SubstituteTeacherProfileId == substituteTeacherProfileId && p.PeriodSlotId == placement.PeriodSlotId && s.Date == session.Date
                select sub.Id).AnyAsync(cancellationToken);
            var isFreeAtSlot = !alreadyPlaced && !alreadySubstituting;

            if (!SubstituteEligibilityEvaluator.IsEligible(isFreeAtSlot, isQualified, allowSuperviseOnly))
            {
                throw new SubstituteNotEligibleException(substituteTeacherProfileId);
            }

            var substitution = new Substitution
            {
                SessionId = sessionId, SubstituteTeacherProfileId = substituteTeacherProfileId, Reason = reason,
                IsCountedForPayroll = isCountedForPayroll, AssignedAtUtc = _clock.UtcNow,
            };
            _db.Substitutions.Add(substitution);

            session.Status = SessionStatus.Substituted;
            await _db.SaveChangesAsync(cancellationToken);
            return substitution;
        }

        public async Task ChangeSessionRoomAsync(int sessionId, int newRoomId, string reason, CancellationToken cancellationToken = default)
        {
            var session = await _db.Sessions.SingleAsync(s => s.Id == sessionId, cancellationToken);
            session.Status = SessionStatus.RoomChanged;
            session.OverrideRoomId = newRoomId;
            session.ChangeReason = reason;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task CancelSessionAsync(int sessionId, string reason, CancellationToken cancellationToken = default)
        {
            var session = await _db.Sessions.SingleAsync(s => s.Id == sessionId, cancellationToken);
            session.Status = SessionStatus.Cancelled;
            session.ChangeReason = reason;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
