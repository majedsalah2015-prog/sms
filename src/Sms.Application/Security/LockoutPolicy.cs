namespace Sms.Application.Security
{
    /// <summary>BR-SEC-002 defaults (doc 06 §3): 5 failures, timed unlock, CAPTCHA hint after 3.</summary>
    public sealed class LockoutPolicy
    {
        public static readonly LockoutPolicy Default = new();

        public int FailureThreshold { get; init; } = 5;

        public int LockoutDurationMinutes { get; init; } = 15;

        /// <summary>Portal CAPTCHA trigger point; Web layer acts on <see cref="LockoutStatus.RequiresCaptcha"/>.</summary>
        public int CaptchaThreshold { get; init; } = 3;
    }
}
