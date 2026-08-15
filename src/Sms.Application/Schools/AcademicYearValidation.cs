using System;

namespace Sms.Application.Schools
{
    /// <summary>Pure BR-AYR-001 date checks.</summary>
    public static class AcademicYearValidation
    {
        /// <summary>A year spans 6–14 months — a guard against typos, not a precise calendar rule.</summary>
        public static bool HasValidSpan(DateTime startDate, DateTime endDate)
        {
            if (endDate <= startDate)
            {
                return false;
            }

            var months = ((endDate.Year - startDate.Year) * 12) + (endDate.Month - startDate.Month);
            if (endDate.Day < startDate.Day)
            {
                months--;
            }

            return months >= 6 && months <= 14;
        }

        public static bool Overlaps(DateTime candidateStart, DateTime candidateEnd, DateTime existingStart, DateTime existingEnd)
            => candidateStart < existingEnd && existingStart < candidateEnd;
    }
}
