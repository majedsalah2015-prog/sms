using System;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-NUM-001: no series is registered — or active — under this code.</summary>
    public class NoActiveNumberingSeriesException : InvalidOperationException
    {
        public NoActiveNumberingSeriesException(string seriesCode)
            : base($"No active numbering series for code '{seriesCode}' (BR-NUM-001).")
        {
        }
    }
}
