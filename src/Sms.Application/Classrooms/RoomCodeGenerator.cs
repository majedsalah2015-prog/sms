using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Sms.Application.Classrooms
{
    /// <summary>
    /// doc/Modules/08 §8.1, BR-ROM-001: the room code is unique per school, and
    /// the doc never says who types it — so the catalog screen offers the next
    /// free one rather than making a registrar invent "A-101" thirty times.
    /// <para>
    /// Shape is <c>{building}-{floor}{nn}</c>: the building's letter by its
    /// creation order (A, B, … Z, AA), the floor's sequence order, then a
    /// two-digit room number. A basement (negative sequence order) reads
    /// <c>A-B101</c>.
    /// </para>
    /// <para>
    /// Uniqueness is the caller's <paramref name="takenCodes"/>, which must
    /// carry <b>every</b> code in the school including deactivated rooms — the
    /// unique index does not forget a retired room's code, so neither can this.
    /// </para>
    /// </summary>
    public static class RoomCodeGenerator
    {
        /// <summary>
        /// The first code of the form <c>{building}-{floor}{nn}</c> that no room
        /// in <paramref name="takenCodes"/> already holds (compared case-insensitively,
        /// as the screen upper-cases what it stores).
        /// </summary>
        /// <param name="buildingOrdinal">1-based position of the building in creation order.</param>
        /// <param name="floorSequenceOrder">The floor's own <c>SequenceOrder</c>; negative reads as a basement.</param>
        public static string Next(int buildingOrdinal, int floorSequenceOrder, IEnumerable<string?> takenCodes)
        {
            var taken = new HashSet<string>(
                (takenCodes ?? Enumerable.Empty<string?>()).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!.Trim()),
                StringComparer.OrdinalIgnoreCase);

            var prefix = BuildingLetter(buildingOrdinal) + "-" + FloorPart(floorSequenceOrder);

            // Bounded by taken.Count + 1: the candidates are distinct, so at most
            // taken.Count of them can be blocked and one is always left over.
            for (var number = 1; number <= taken.Count + 1; number++)
            {
                var candidate = prefix + number.ToString("00", CultureInfo.InvariantCulture);
                if (!taken.Contains(candidate))
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Unreachable: more blocked candidates than taken codes.");
        }

        /// <summary>A, B, … Z, AA, AB — spreadsheet-column style, so a 27th building still gets its own letter.</summary>
        private static string BuildingLetter(int ordinal)
        {
            if (ordinal < 1)
            {
                ordinal = 1;
            }

            var letters = new StringBuilder();
            while (ordinal > 0)
            {
                ordinal--;
                letters.Insert(0, (char)('A' + (ordinal % 26)));
                ordinal /= 26;
            }

            return letters.ToString();
        }

        private static string FloorPart(int sequenceOrder)
            => sequenceOrder < 0
                ? "B" + (-sequenceOrder).ToString(CultureInfo.InvariantCulture)
                : sequenceOrder.ToString(CultureInfo.InvariantCulture);
    }
}
