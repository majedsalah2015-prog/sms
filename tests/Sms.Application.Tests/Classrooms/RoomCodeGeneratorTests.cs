using System;
using System.Linq;
using Sms.Application.Classrooms;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Classrooms
{
    /// <summary>
    /// BR-ROM-001 (doc/Modules/08 §8.1): the catalog screen offers the next free
    /// room code so a registrar entering a floor of thirty rooms never types one.
    /// </summary>
    public sealed class RoomCodeGeneratorTests
    {
        [Fact]
        [BusinessRule("BR-ROM-001")]
        public void The_first_room_on_a_floor_takes_number_one()
        {
            Assert.Equal("A-101", RoomCodeGenerator.Next(buildingOrdinal: 1, floorSequenceOrder: 1, Array.Empty<string>()));
        }

        [Fact]
        [BusinessRule("BR-ROM-001")]
        public void Each_building_gets_its_own_letter_and_each_floor_its_own_digit()
        {
            Assert.Equal("B-301", RoomCodeGenerator.Next(2, 3, Array.Empty<string>()));
            Assert.Equal("Z-101", RoomCodeGenerator.Next(26, 1, Array.Empty<string>()));
            Assert.Equal("AA-101", RoomCodeGenerator.Next(27, 1, Array.Empty<string>()));
        }

        [Fact]
        [BusinessRule("BR-ROM-001")]
        public void A_ground_floor_reads_zero_and_a_basement_reads_B()
        {
            Assert.Equal("A-001", RoomCodeGenerator.Next(1, 0, Array.Empty<string>()));
            Assert.Equal("A-B101", RoomCodeGenerator.Next(1, -1, Array.Empty<string>()));
        }

        [Fact]
        [BusinessRule("BR-ROM-001")]
        public void The_next_code_skips_the_ones_already_taken()
        {
            Assert.Equal("A-103", RoomCodeGenerator.Next(1, 1, new[] { "A-101", "A-102" }));
        }

        [Fact]
        [BusinessRule("BR-ROM-001")]
        public void A_gap_left_by_a_retired_room_is_filled_before_the_end()
        {
            // The unique index still holds a deactivated room's code, so the gap
            // that gets reused is a genuinely free one, never the retired code.
            Assert.Equal("A-102", RoomCodeGenerator.Next(1, 1, new[] { "A-101", "A-103" }));
        }

        [Fact]
        [BusinessRule("BR-ROM-001")]
        public void A_code_taken_in_another_case_still_blocks()
        {
            // The screen upper-cases what it stores; a lower-case row from an import
            // is the same code to the unique index, so it must be the same here.
            Assert.Equal("A-102", RoomCodeGenerator.Next(1, 1, new[] { "a-101" }));
        }

        [Fact]
        [BusinessRule("BR-ROM-001")]
        public void Codes_on_other_floors_do_not_push_this_floor_along()
        {
            Assert.Equal("A-201", RoomCodeGenerator.Next(1, 2, new[] { "A-101", "A-102", "A-103" }));
        }

        [Fact]
        [BusinessRule("BR-ROM-001")]
        public void Past_ninety_nine_rooms_the_number_simply_widens()
        {
            var taken = Enumerable.Range(1, 99).Select(n => "A-1" + n.ToString("00")).ToList();

            Assert.Equal("A-1100", RoomCodeGenerator.Next(1, 1, taken));
        }

        [Fact]
        [BusinessRule("BR-ROM-001")]
        public void Blank_and_null_entries_in_the_taken_list_are_ignored()
        {
            Assert.Equal("A-101", RoomCodeGenerator.Next(1, 1, new[] { null, "", "   " }));
        }

        [Fact]
        [BusinessRule("BR-ROM-001")]
        public void The_generated_code_fits_the_twenty_character_column()
        {
            var taken = Enumerable.Range(1, 200).Select(n => "ZZ-99" + n.ToString("00")).ToList();

            Assert.True(RoomCodeGenerator.Next(702, 99, taken).Length <= 20);
        }
    }
}
