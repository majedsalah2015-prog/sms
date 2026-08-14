using System;
using Sms.Application.Common.Interfaces;

namespace Sms.Infrastructure.Common
{
    public sealed class SystemClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
