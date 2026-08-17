using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Timetable
{
    /// <summary>Pure BR-TTB-004 hard constraints: teacher/room/section double-booking within the same period slot. Availability/wing/gender hard constraints need Module 13's TeacherAvailability (deferred) and Module 08's room-type strictness config — not checked here.</summary>
    public static class PlacementConflictDetector
    {
        public readonly struct ExistingPlacement
        {
            public ExistingPlacement(int periodSlotId, int sectionId, int teacherProfileId, int? roomId)
            {
                PeriodSlotId = periodSlotId;
                SectionId = sectionId;
                TeacherProfileId = teacherProfileId;
                RoomId = roomId;
            }

            public int PeriodSlotId { get; }

            public int SectionId { get; }

            public int TeacherProfileId { get; }

            public int? RoomId { get; }
        }

        public static bool HasTeacherConflict(int periodSlotId, int teacherProfileId, IEnumerable<ExistingPlacement> existing)
            => existing.Any(p => p.PeriodSlotId == periodSlotId && p.TeacherProfileId == teacherProfileId);

        public static bool HasRoomConflict(int periodSlotId, int? roomId, IEnumerable<ExistingPlacement> existing)
            => roomId.HasValue && existing.Any(p => p.PeriodSlotId == periodSlotId && p.RoomId == roomId);

        public static bool HasSectionConflict(int periodSlotId, int sectionId, IEnumerable<ExistingPlacement> existing)
            => existing.Any(p => p.PeriodSlotId == periodSlotId && p.SectionId == sectionId);
    }
}
