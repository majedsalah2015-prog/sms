using System;

namespace Sms.Application.Messaging
{
    /// <summary>Pure BR-MSG-004: acknowledgment-required letters escalate unacknowledged after N days.</summary>
    public static class AcknowledgmentEscalationEvaluator
    {
        public static bool IsOverdue(DateTime issuedAtUtc, DateTime nowUtc, int escalationDays)
            => nowUtc >= issuedAtUtc.AddDays(escalationDays);
    }
}
