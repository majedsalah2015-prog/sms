using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Timetable;

namespace Sms.Application.Timetable
{
    /// <summary>
    /// doc/Modules/15 §8 Shape designer / Timetable builder / Publication
    /// console / Daily cover console screens backing (screens deferred,
    /// the operations are core). The auto-generation solver (doc §13) is
    /// deliberately Future — v1 is assisted-manual (PlaceAsync + live
    /// conflict checks), matching the doc's own scope decision, not a
    /// missed feature.
    /// </summary>
    public interface ITimetableAdmin
    {
        Task<TimetableShape> DefineShapeAsync(int stageId, int academicYearId, CancellationToken cancellationToken = default);

        Task<PeriodSlot> AddPeriodSlotAsync(
            int shapeId, DayOfWeek dayOfWeek, int sequenceNumber, TimeSpan startTime, TimeSpan endTime,
            bool isBreak = false, CancellationToken cancellationToken = default);

        Task<TimetableVersion> DefineVersionAsync(int academicYearId, int? termId = null, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.TeacherNotAssignedException"/> (BR-TCH-002) or <see cref="Common.Exceptions.PlacementConflictException"/> (BR-TTB-004).</summary>
        Task<Placement> PlaceAsync(
            int timetableVersionId, int sectionId, int periodSlotId, int curriculumOfferingId, int teacherProfileId,
            int? roomId = null, CancellationToken cancellationToken = default);

        /// <summary>Draft -> Validated (BR-TTB-002). Throws <see cref="Common.Exceptions.IncompletePlacementException"/> if a placed offering falls short of its weekly-periods plan (BR-TTB-003).</summary>
        Task ValidateVersionAsync(int timetableVersionId, CancellationToken cancellationToken = default);

        /// <summary>Validated -> Published (BR-TTB-002); generates Session rows for the date range (BR-TTB-006, working days only).</summary>
        Task PublishAsync(
            int timetableVersionId, int publishedByUserId, DateTime rangeStart, DateTime rangeEnd,
            ISet<DayOfWeek> weekendDays, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.SubstituteNotEligibleException"/> unless free at the slot and (qualified or allowSuperviseOnly).</summary>
        Task<Substitution> AssignSubstituteAsync(
            int sessionId, int substituteTeacherProfileId, string reason, bool allowSuperviseOnly = false,
            bool isCountedForPayroll = true, CancellationToken cancellationToken = default);

        Task ChangeSessionRoomAsync(int sessionId, int newRoomId, string reason, CancellationToken cancellationToken = default);

        Task CancelSessionAsync(int sessionId, string reason, CancellationToken cancellationToken = default);
    }
}
