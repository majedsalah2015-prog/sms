using System;
using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Classrooms
{
    /// <summary>
    /// Pure BR-ROM-004: a room is unavailable for a candidate window if it
    /// overlaps any RoomAvailabilityException (maintenance/reserved). Same
    /// half-open-interval overlap semantics as
    /// Sms.Application.Schools.AcademicYearValidation.Overlaps — touching
    /// boundaries are not an overlap.
    /// </summary>
    public static class RoomAvailabilityChecker
    {
        public static bool IsAvailable(DateTime candidateStart, DateTime candidateEnd, IEnumerable<(DateTime Start, DateTime End)> exceptions)
            => !exceptions.Any(e => candidateStart < e.End && e.Start < candidateEnd);
    }
}
