namespace Sms.Application.Notifications
{
    public sealed class ChannelSendOutcome
    {
        public static ChannelSendOutcome Success(string? providerReference = null)
            => new() { Succeeded = true, ProviderReference = providerReference };

        public static ChannelSendOutcome Failure(string reason)
            => new() { Succeeded = false, FailureReason = reason };

        public bool Succeeded { get; private init; }

        public string? ProviderReference { get; private init; }

        public string? FailureReason { get; private init; }
    }
}
