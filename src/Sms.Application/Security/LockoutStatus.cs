using System;

namespace Sms.Application.Security
{
    public sealed class LockoutStatus
    {
        public bool IsLockedOut { get; init; }

        public DateTime? UnlocksAtUtc { get; init; }

        public bool RequiresCaptcha { get; init; }
    }
}
