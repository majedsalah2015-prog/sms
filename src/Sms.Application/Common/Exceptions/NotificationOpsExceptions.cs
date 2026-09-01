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

    /// <summary>No template or template version with that id belongs to this school.</summary>
    public class UnknownTemplateException : InvalidOperationException
    {
        public UnknownTemplateException(int id)
            : base($"No notification template exists with id {id} (doc/Modules/33 §8.2).")
        {
        }
    }

    /// <summary>
    /// BR-NTF-003: the console was handed a gateway code <c>ProviderCatalog</c> does not
    /// define. Storing it would register a provider no <c>IChannelSender</c> answers to.
    /// </summary>
    public class UnknownProviderCodeException : InvalidOperationException
    {
        public UnknownProviderCodeException(string? code)
            : base($"'{code}' is not a gateway this deployment can send through (BR-NTF-003).")
        {
            Code = code;
        }

        public string? Code { get; }
    }

    /// <summary>BR-NTF-003: the gateway is real, but it does not serve the channel it was registered on.</summary>
    public class ProviderChannelMismatchException : InvalidOperationException
    {
        public ProviderChannelMismatchException(string code, NotificationChannel channel)
            : base($"Gateway '{code}' does not serve the {channel} channel (BR-NTF-003).")
        {
            Code = code;
            Channel = channel;
        }

        public string Code { get; }

        public NotificationChannel Channel { get; }
    }

    /// <summary>
    /// BR-NTF-003: retiring this gateway would leave a channel with enabled subscription
    /// rules and no way to send them — the school would stop reaching parents on it and
    /// nothing would say so.
    /// </summary>
    public class ProviderInUseException : InvalidOperationException
    {
        public ProviderInUseException(NotificationChannel channel, int activeRuleCount)
            : base($"This is the only active gateway for {channel}, and {activeRuleCount} subscription rule(s) still send on it (BR-NTF-003).")
        {
            Channel = channel;
            ActiveRuleCount = activeRuleCount;
        }

        public NotificationChannel Channel { get; }

        public int ActiveRuleCount { get; }
    }

    /// <summary>
    /// The recipient has no address on the channel being sent to — no mobile for
    /// WhatsApp, no mailbox for email. BR-NTF-005's data-quality case, raised where a
    /// person is waiting for an answer (a test send) rather than swallowed into the
    /// failure queue.
    /// </summary>
    public class RecipientUnreachableException : InvalidOperationException
    {
        public RecipientUnreachableException(int userId, NotificationChannel channel)
            : base($"User {userId} has no {channel} address on file (BR-NTF-005).")
        {
            UserId = userId;
            Channel = channel;
        }

        public int UserId { get; }

        public NotificationChannel Channel { get; }
    }

    /// <summary>
    /// The gateway is registered but not usable — no credentials entered, or the row
    /// deactivated. Separated from a send failure because the fix is a screen, not a retry.
    /// </summary>
    public class ProviderNotConfiguredException : InvalidOperationException
    {
        public ProviderNotConfiguredException(NotificationChannel channel)
            : base($"No configured, active gateway is registered for the {channel} channel (BR-NTF-003).")
        {
            Channel = channel;
        }

        public NotificationChannel Channel { get; }
    }
}
