using System;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>The code doesn't map to any registered JobDefinition — a caller bug, not a scheduling race.</summary>
    public class UnknownJobException : InvalidOperationException
    {
        public UnknownJobException(string jobCode)
            : base($"No JobDefinition registered for code '{jobCode}'.")
        {
        }
    }
}
