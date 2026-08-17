namespace Sms.Application.Notifications
{
    /// <summary>Pure BR-NTF-002: a school cannot silently disable a statutory/safety-class subscription rule below the product floor — disabling one requires a Principal approval flag.</summary>
    public static class StatutorySubscriptionGuard
    {
        public static bool CanDisable(bool isStatutory, bool hasPrincipalApproval) => !isStatutory || hasPrincipalApproval;
    }
}
