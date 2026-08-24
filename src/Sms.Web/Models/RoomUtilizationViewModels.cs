using System;
using System.Collections.Generic;
using Sms.Domain.Classrooms;

namespace Sms.Web.Models
{
    /// <summary>One teaching period in a room's week, with what is placed in it.</summary>
    public sealed record RoomWeekSlot(
        DayOfWeek Day,
        int SequenceNumber,
        TimeSpan StartTime,
        TimeSpan EndTime,
        string? SectionName,
        string? SubjectName,
        string? TeacherName);

    /// <summary>
    /// doc/Modules/08 §8.5 — rooms × periods, read off the published timetable.
    /// <para>
    /// The screen exists to answer one question a facilities decision turns on: is the
    /// school short of rooms, or short of scheduling? A grid of every room against
    /// every period answers it in a glance in a way a list of numbers does not.
    /// </para>
    /// </summary>
    public sealed class RoomUtilizationViewModel
    {
        public sealed record Column(int PeriodSlotId, DayOfWeek Day, int SequenceNumber, TimeSpan StartTime);

        public sealed record Row(Room Room, string BuildingName, string FloorName, IReadOnlyDictionary<int, int> BySlot, int PercentUsed, bool HasDoubleBooking);

        public IReadOnlyList<Column> Columns { get; set; } = Array.Empty<Column>();

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        public int OverallPercent { get; set; }

        /// <summary>Null when no version is published — the grid has nothing to read and says so rather than showing an empty week as if every room were free.</summary>
        public int? PublishedVersionId { get; set; }

        public IReadOnlyList<Sms.Domain.Schools.AcademicYear> Years { get; set; } = Array.Empty<Sms.Domain.Schools.AcademicYear>();

        public int? YearId { get; set; }

        /// <summary>The busiest and the emptiest rooms — what somebody opens this screen to find.</summary>
        public IReadOnlyList<Row> Busiest { get; set; } = Array.Empty<Row>();

        public IReadOnlyList<Row> Idlest { get; set; } = Array.Empty<Row>();
    }
}
