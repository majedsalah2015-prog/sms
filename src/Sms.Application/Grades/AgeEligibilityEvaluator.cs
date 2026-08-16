using System;

namespace Sms.Application.Grades
{
    /// <summary>
    /// Pure BR-GRD-005: age in years at the grade-year profile's configured
    /// cutoff date, checked against its min/max. Owned here (the rule
    /// belongs to Grades) even though its first real caller is Admissions
    /// (BR-ADM-004) — the rule owner hosts the engine, consumers reuse it.
    /// </summary>
    public static class AgeEligibilityEvaluator
    {
        public static bool IsEligible(DateTime dateOfBirth, DateTime cutoffDate, decimal? minAge, decimal? maxAge)
        {
            if (minAge == null && maxAge == null)
            {
                return true;
            }

            var ageAtCutoff = (decimal)((cutoffDate - dateOfBirth).TotalDays / 365.25);

            if (minAge.HasValue && ageAtCutoff < minAge.Value)
            {
                return false;
            }

            if (maxAge.HasValue && ageAtCutoff > maxAge.Value)
            {
                return false;
            }

            return true;
        }
    }
}
