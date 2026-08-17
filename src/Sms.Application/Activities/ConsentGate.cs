namespace Sms.Application.Activities
{
    /// <summary>Pure BR-ACT-005: no consent, no participation — hard, no override (product safeguarding stance).</summary>
    public static class ConsentGate
    {
        public static bool CanParticipate(bool requiresConsent, bool hasConsentRecord) => !requiresConsent || hasConsentRecord;
    }
}
