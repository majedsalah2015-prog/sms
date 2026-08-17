using System;
using Sms.Domain.Notifications;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-NTF-001: the requested publish-status pair isn't a legal move (most commonly: publishing a version that was never test-sent).</summary>
    public class InvalidTemplatePublishTransitionException : InvalidOperationException
    {
        public InvalidTemplatePublishTransitionException(TemplatePublishStatus from, TemplatePublishStatus to)
            : base($"Template version publish-status cannot move from '{from}' to '{to}' (BR-NTF-001).")
        {
        }
    }

    /// <summary>BR-NTF-002: a statutory subscription rule can't be disabled without a Principal approval flag.</summary>
    public class StatutorySubscriptionChangeDeniedException : InvalidOperationException
    {
        public StatutorySubscriptionChangeDeniedException(int subscriptionRuleId)
            : base($"Subscription rule {subscriptionRuleId} is statutory and cannot be disabled without Principal approval (BR-NTF-002).")
        {
        }
    }

    /// <summary>BR-NTF-004: the channel's period budget hard-stop is in effect — non-safety-class sends are blocked.</summary>
    public class BudgetHardStopException : InvalidOperationException
    {
        public BudgetHardStopException(string periodKey)
            : base($"The notification budget hard-stop is in effect for period '{periodKey}' (BR-NTF-004).")
        {
        }
    }
}
