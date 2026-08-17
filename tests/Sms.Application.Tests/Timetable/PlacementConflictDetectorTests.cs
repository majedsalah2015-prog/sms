using System.Collections.Generic;
using Sms.Application.Timetable;
using Sms.TestSupport;
using Xunit;
using ExistingPlacement = Sms.Application.Timetable.PlacementConflictDetector.ExistingPlacement;

namespace Sms.Application.Tests.Timetable
{
    public class PlacementConflictDetectorTests
    {
        [Fact]
        [BusinessRule("BR-TTB-004")]
        public void Same_teacher_at_same_slot_is_a_conflict()
        {
            var existing = new[] { new ExistingPlacement(periodSlotId: 1, sectionId: 10, teacherProfileId: 5, roomId: null) };

            Assert.True(PlacementConflictDetector.HasTeacherConflict(1, 5, existing));
        }

        [Fact]
        [BusinessRule("BR-TTB-004")]
        public void Same_teacher_at_a_different_slot_is_not_a_conflict()
        {
            var existing = new[] { new ExistingPlacement(periodSlotId: 1, sectionId: 10, teacherProfileId: 5, roomId: null) };

            Assert.False(PlacementConflictDetector.HasTeacherConflict(2, 5, existing));
        }

        [Fact]
        [BusinessRule("BR-TTB-004")]
        public void Same_room_at_same_slot_is_a_conflict()
        {
            var existing = new[] { new ExistingPlacement(periodSlotId: 1, sectionId: 10, teacherProfileId: 5, roomId: 7) };

            Assert.True(PlacementConflictDetector.HasRoomConflict(1, 7, existing));
        }

        [Fact]
        [BusinessRule("BR-TTB-004")]
        public void No_room_requested_is_never_a_room_conflict()
        {
            var existing = new[] { new ExistingPlacement(periodSlotId: 1, sectionId: 10, teacherProfileId: 5, roomId: 7) };

            Assert.False(PlacementConflictDetector.HasRoomConflict(1, null, existing));
        }

        [Fact]
        [BusinessRule("BR-TTB-004")]
        public void Same_section_at_same_slot_is_a_conflict()
        {
            var existing = new[] { new ExistingPlacement(periodSlotId: 1, sectionId: 10, teacherProfileId: 5, roomId: null) };

            Assert.True(PlacementConflictDetector.HasSectionConflict(1, 10, existing));
        }

        [Fact]
        [BusinessRule("BR-TTB-004")]
        public void No_existing_placements_means_no_conflicts()
        {
            var existing = new List<ExistingPlacement>();

            Assert.False(PlacementConflictDetector.HasTeacherConflict(1, 5, existing));
            Assert.False(PlacementConflictDetector.HasRoomConflict(1, 7, existing));
            Assert.False(PlacementConflictDetector.HasSectionConflict(1, 10, existing));
        }
    }
}
