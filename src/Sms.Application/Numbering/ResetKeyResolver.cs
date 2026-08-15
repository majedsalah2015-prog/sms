using Sms.Domain.Numbering;

namespace Sms.Application.Numbering
{
    /// <summary>
    /// Pure BR-NUM-004 partitioning: "Never" collapses every school-scoped
    /// series into one lifelong counter ("ALL") — the same key regardless of
    /// year, which is exactly what keeps a student number permanent across
    /// withdrawal/re-admission.
    /// </summary>
    public static class ResetKeyResolver
    {
        public const string LifelongKey = "ALL";

        public static string Resolve(ResetPolicy policy, string academicYearLabel, int gregorianYear)
        {
            return policy switch
            {
                ResetPolicy.PerAcademicYear => academicYearLabel,
                ResetPolicy.PerCalendarYear => gregorianYear.ToString(),
                _ => LifelongKey,
            };
        }
    }
}
