namespace Sms.Application.Activities
{
    /// <summary>Pure BR-ACT-004: departure checklist — ratio satisfied, every active enrollment's consent current, transport plan confirmed. Return confirmation reuses BR-TRN-005's sweep pattern: the returned headcount must match the departed count exactly.</summary>
    public static class TripDepartureChecklistEvaluator
    {
        public static bool CanDepart(bool ratioSatisfied, bool allConsentsCurrent, bool transportConfirmed)
            => ratioSatisfied && allConsentsCurrent && transportConfirmed;

        public static bool HeadcountMatches(int departedCount, int returnedCount) => departedCount == returnedCount;
    }
}
