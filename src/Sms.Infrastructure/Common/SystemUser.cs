using Sms.Application.Common.Interfaces;

namespace Sms.Infrastructure.Common
{
    /// <summary>
    /// Placeholder acting user until the security framework (E-003) wires
    /// ICurrentUser to the authenticated principal. UserId 0 is reserved for
    /// system/background operations.
    /// </summary>
    public sealed class SystemUser : ICurrentUser
    {
        public int UserId => 0;
    }
}
