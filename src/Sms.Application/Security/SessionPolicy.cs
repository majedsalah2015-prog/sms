namespace Sms.Application.Security
{
    /// <summary>BR-SEC-004 defaults (doc 06 §3): idle timeout differs by portal vs. staff, absolute is shared.</summary>
    public sealed class SessionPolicy
    {
        public static readonly SessionPolicy Default = new();

        public int StaffIdleTimeoutMinutes { get; init; } = 30;

        public int PortalIdleTimeoutMinutes { get; init; } = 20;

        public int AbsoluteTimeoutHours { get; init; } = 12;
    }
}
