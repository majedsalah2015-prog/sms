using System;
using Sms.Domain.Schools;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-SCH-005: the requested status pair isn't a legal move (e.g. Closed is terminal).</summary>
    public class InvalidSchoolStatusTransitionException : InvalidOperationException
    {
        public InvalidSchoolStatusTransitionException(SchoolStatus from, SchoolStatus to)
            : base($"School status cannot move from '{from}' to '{to}' (BR-SCH-005).")
        {
        }
    }
}
