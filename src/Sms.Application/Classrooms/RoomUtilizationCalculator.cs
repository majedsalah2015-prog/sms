using System;
using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Classrooms
{
    /// <summary>
    /// doc/Modules/08 §8.5: rooms × periods occupancy, read off the published
    /// timetable. Pure — it is handed the placements and the week's shape and returns
    /// the grid, so the same arithmetic answers the screen, a report and a widget
    /// without three of them drifting apart.
    /// <para>
    /// <b>What "utilised" counts.</b> A teaching period the room is placed in, once,
    /// for the whole week. Breaks are excluded from the denominator: a school does not
    /// use its rooms during break and counting those slots would make every room look
    /// half idle by construction, which is the kind of number that gets a heat map
    /// ignored.
    /// </para>
    /// <para>
    /// <b>Double bookings are shown, not hidden.</b> Two placements in one room in one
    /// slot is a hard constraint violation the timetable validator refuses — but a
    /// published version can still hold one where a room was assigned after
    /// validation, and a grid that silently showed "occupied" either way would be the
    /// last place anyone found it. The cell keeps the real count.
    /// </para>
    /// </summary>
    public static class RoomUtilizationCalculator
    {
        /// <summary>One placement, reduced to what occupancy depends on.</summary>
        public sealed record RoomPlacement(int RoomId, int PeriodSlotId);

        /// <summary>A teaching period in the week's shape.</summary>
        public sealed record TeachingSlot(int PeriodSlotId, DayOfWeek Day, int SequenceNumber);

        /// <summary>What one room does across the week.</summary>
        public sealed record RoomRow(
            int RoomId,
            IReadOnlyDictionary<int, int> BySlot,
            int OccupiedSlots,
            int TeachingSlots)
        {
            /// <summary>Occupied teaching periods as a percentage of the ones on offer; 0 when the week has no teaching periods at all.</summary>
            public int PercentUsed => TeachingSlots == 0 ? 0 : (int)Math.Round(100.0 * OccupiedSlots / TeachingSlots, MidpointRounding.AwayFromZero);

            /// <summary>A slot holding more than one placement — a clash the timetable should not have published.</summary>
            public bool HasDoubleBooking => BySlot.Values.Any(n => n > 1);
        }

        /// <summary>
        /// The grid. <paramref name="roomIds"/> decides the rows and their order —
        /// including rooms nothing is placed in, because an empty row is the most
        /// useful thing on this screen for the person deciding where to put a class.
        /// </summary>
        public static IReadOnlyList<RoomRow> Build(
            IReadOnlyCollection<int> roomIds,
            IReadOnlyCollection<TeachingSlot> teachingSlots,
            IReadOnlyCollection<RoomPlacement> placements)
        {
            var slotIds = teachingSlots.Select(s => s.PeriodSlotId).ToHashSet();
            var byRoom = placements
                .Where(p => slotIds.Contains(p.PeriodSlotId))
                .GroupBy(p => p.RoomId)
                .ToDictionary(g => g.Key, g => g.GroupBy(p => p.PeriodSlotId).ToDictionary(x => x.Key, x => x.Count()));

            return roomIds.Select(roomId =>
            {
                var bySlot = byRoom.TryGetValue(roomId, out var slots)
                    ? slots
                    : new Dictionary<int, int>();

                return new RoomRow(roomId, bySlot, bySlot.Count, teachingSlots.Count);
            }).ToList();
        }

        /// <summary>
        /// How busy the whole building is: occupied room-periods over available ones.
        /// The single number a facilities decision is argued with — "we are at 46%" is
        /// what makes "we need another room" a claim rather than an impression.
        /// </summary>
        public static int OverallPercent(IReadOnlyCollection<RoomRow> rows)
        {
            var available = rows.Sum(r => r.TeachingSlots);
            return available == 0 ? 0 : (int)Math.Round(100.0 * rows.Sum(r => r.OccupiedSlots) / available, MidpointRounding.AwayFromZero);
        }
    }
}
