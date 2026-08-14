using System;

namespace Sms.Application.Common.Interfaces
{
    /// <summary>
    /// Time abstraction (coding standard: no direct DateTime.Now/UtcNow in
    /// feature code — store UTC per ADR-4).
    /// </summary>
    public interface IClock
    {
        DateTime UtcNow { get; }
    }
}
