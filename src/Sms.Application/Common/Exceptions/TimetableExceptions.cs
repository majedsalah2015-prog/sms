using System;
using Sms.Domain.Timetable;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-TTB-004: a hard constraint (teacher/room/section double-booking) blocks the placement.</summary>
    public class PlacementConflictException : InvalidOperationException
    {
        public PlacementConflictException(string conflictType)
            : base($"Placement conflict: {conflictType} double-booking at this period slot (BR-TTB-004).")
        {
        }
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
        }
    }

    /// <summary>BR-TTB-002: the requested version status pair isn't a legal WF-12 move.</summary>
    public class InvalidTimetableVersionStatusTransitionException : InvalidOperationException
    {
        public InvalidTimetableVersionStatusTransitionException(TimetableVersionStatus from, TimetableVersionStatus to)
            : base($"Timetable version status cannot move from '{from}' to '{to}' (BR-TTB-002).")
        {
        }
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
