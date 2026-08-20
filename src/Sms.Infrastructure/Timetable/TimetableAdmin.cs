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

        public async Task RemovePeriodSlotAsync(int periodSlotId, CancellationToken cancellationToken = default)
        {
            var slot = await _db.PeriodSlots.SingleAsync(s => s.Id == periodSlotId, cancellationToken);
            var inUse = await _db.Placements.CountAsync(p => p.PeriodSlotId == periodSlotId, cancellationToken);
            if (inUse > 0)
            {
                throw new PeriodSlotInUseException(periodSlotId, inUse);
            }

            _db.PeriodSlots.Remove(slot);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<TimetableVersion> DefineVersionAsync(int academicYearId, int? termId = null, CancellationToken cancellationToken = default)
        {
            var version = new TimetableVersion { AcademicYearId = academicYearId, TermId = termId };
            _db.TimetableVersions.Add(version);
            await _db.SaveChangesAsync(cancellationToken);
            return version;
        }

        public async Task ReopenVersionAsync(int timetableVersionId, CancellationToken cancellationToken = default)
        {
            var version = await _db.TimetableVersions.SingleAsync(v => v.Id == timetableVersionId, cancellationToken);
            if (!TimetableVersionStatusTransitions.CanTransition(version.Status, TimetableVersionStatus.Draft))
            {
                throw new InvalidTimetableVersionStatusTransitionException(version.Status, TimetableVersionStatus.Draft);
            }

            version.Status = TimetableVersionStatus.Draft;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task RemovePlacementAsync(int placementId, CancellationToken cancellationToken = default)
        {
            var placement = await _db.Placements.SingleAsync(p => p.Id == placementId, cancellationToken);
            await EnsureDraftAsync(placement.TimetableVersionId, cancellationToken);
            _db.Placements.Remove(placement);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<Placement> PlaceAsync(
            int timetableVersionId, int sectionId, int periodSlotId, int curriculumOfferingId, int teacherProfileId,
            int? roomId = null, CancellationToken cancellationToken = default)
        {
            await EnsureDraftAsync(timetableVersionId, cancellationToken);

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

            // BR-TTB-003 "every offering fully placed": every section that appears in the version is
            // checked against ALL current offerings of its grade-year profile — an offering with zero
            // placements is a shortfall too, not invisible. Sections with no placement at all are out of
            // the version's scope (e.g. a stage that is not timetabled) and are not blocked here; the
            // validation board lists them so the owner sees the gap.
            var sectionIds = placements.Select(p => p.SectionId).Distinct().ToList();
            var sectionProfiles = await _db.Sections
                .Where(s => sectionIds.Contains(s.Id))
                .Select(s => new { s.Id, s.GradeYearProfileId })
                .ToListAsync(cancellationToken);
            var profileIds = sectionProfiles.Select(s => s.GradeYearProfileId).Distinct().ToList();
            var offerings = await _db.CurriculumOfferings
                .Where(o => profileIds.Contains(o.GradeYearProfileId) && o.EffectiveToUtc == null)
                .Select(o => new { o.Id, o.GradeYearProfileId, o.WeeklyPeriods })
                .ToListAsync(cancellationToken);

            foreach (var section in sectionProfiles)
            {
                foreach (var offering in offerings.Where(o => o.GradeYearProfileId == section.GradeYearProfileId))
                {
                    var placed = placements.FirstOrDefault(p => p.SectionId == section.Id && p.CurriculumOfferingId == offering.Id)?.PlacedCount ?? 0;
                    if (!PlacementCompletenessEvaluator.IsComplete(placed, offering.WeeklyPeriods))
                    {
                        throw new IncompletePlacementException(
                            offering.Id, section.Id, PlacementCompletenessEvaluator.Shortfall(placed, offering.WeeklyPeriods));
                    }
                }
            }

            // Placements against an ended offering (not current any more) are still counted against the plan they were made for.
            foreach (var group in placements.Where(p => !offerings.Any(o => o.Id == p.CurriculumOfferingId)))
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

        /// <summary>doc/Modules/15 §9: "version editing locked while under WF-12 review" — only Draft accepts placement edits.</summary>
        private async Task EnsureDraftAsync(int timetableVersionId, CancellationToken cancellationToken)
        {
            var status = await _db.TimetableVersions
                .Where(v => v.Id == timetableVersionId)
                .Select(v => v.Status)
                .SingleAsync(cancellationToken);
            if (status != TimetableVersionStatus.Draft)
            {
                throw new TimetableVersionLockedException(status);
            }
        }
    }
}
