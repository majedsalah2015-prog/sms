using System;
using Sms.Domain.Timetable;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-TTB-004: a hard constraint (teacher/room/section double-booking) blocks the placement.</summary>
    /// <summary>Who is double-booked — the three hard constraints BR-TTB-004 enforces.</summary>
    public enum PlacementConflictKind
    {
        /// <summary>The teacher is already teaching something else in that period.</summary>
        Teacher = 1,

        /// <summary>The section is already sitting in another lesson in that period.</summary>
        Section = 2,

        /// <summary>The room is already occupied in that period.</summary>
        Room = 3,
    }

    /// <summary>BR-TTB-004: a hard constraint (teacher/room/section double-booking) blocks the placement.</summary>
    public class PlacementConflictException : InvalidOperationException
    {
        public PlacementConflictException(PlacementConflictKind conflict)
            : base($"Placement conflict: {conflict.ToString().ToLowerInvariant()} double-booking at this period slot (BR-TTB-004).")
        {
            Conflict = conflict;
        }

        public PlacementConflictKind Conflict { get; }
    }

    /// <summary>BR-TCH-002: the placement's teacher has no matching TeacherAssignment for this offering x section.</summary>
    public class TeacherNotAssignedException : InvalidOperationException
    {
        public TeacherNotAssignedException(int teacherProfileId, int curriculumOfferingId, int sectionId)
            : base($"Teacher profile {teacherProfileId} has no assignment for offering {curriculumOfferingId} / section {sectionId} (BR-TCH-002).")
        {
        }
    }

    /// <summary>BR-TTB-003: not every offering is fully placed (placed periods != weekly-periods plan).</summary>
    public class IncompletePlacementException : InvalidOperationException
    {
        public IncompletePlacementException(int curriculumOfferingId, int sectionId, int shortfall)
            : base($"Offering {curriculumOfferingId} / section {sectionId} is short {shortfall} period(s) of its weekly plan (BR-TTB-003).")
        {
            Shortfall = shortfall;
        }

        /// <summary>How many periods are still unplaced — the number the scheduler has to close.</summary>
        public int Shortfall { get; }
    }

    /// <summary>BR-TTB-002: the requested version status pair isn't a legal WF-12 move.</summary>
    public class InvalidTimetableVersionStatusTransitionException : InvalidOperationException
    {
        public InvalidTimetableVersionStatusTransitionException(TimetableVersionStatus from, TimetableVersionStatus to)
            : base($"Timetable version status cannot move from '{from}' to '{to}' (BR-TTB-002).")
        {
        }
    }

    /// <summary>doc/Modules/15 §9: placements can only be edited while the version is Draft — Validated (under WF-12 review) and Published versions are locked.</summary>
    public class TimetableVersionLockedException : InvalidOperationException
    {
        public TimetableVersionLockedException(TimetableVersionStatus status)
            : base($"Timetable version is {status} — placements are locked; reopen a Validated version or create a new one for an amendment (BR-TTB-002/009).")
        {
            Status = status;
        }

        /// <summary>Which stage locked it — validated versions reopen, published ones are amended by a new version.</summary>
        public TimetableVersionStatus Status { get; }
    }

    /// <summary>BR-TTB-001: a period slot that placements already reference cannot be removed (the placements go first).</summary>
    public class PeriodSlotInUseException : InvalidOperationException
    {
        public PeriodSlotInUseException(int periodSlotId, int placementCount)
            : base($"Period slot {periodSlotId} is referenced by {placementCount} placement(s) and cannot be removed (BR-TTB-001).")
        {
            PlacementCount = placementCount;
        }

        /// <summary>How many lessons sit in the slot — what has to be moved out before it can go.</summary>
        public int PlacementCount { get; }
    }

    /// <summary>BR-TTB-007: the candidate substitute is neither qualified nor free at the session's slot.</summary>
    public class SubstituteNotEligibleException : InvalidOperationException
    {
        public SubstituteNotEligibleException(int substituteTeacherProfileId)
            : base($"Teacher profile {substituteTeacherProfileId} is not eligible to substitute this session (BR-TTB-007).")
        {
        }
    }
}
