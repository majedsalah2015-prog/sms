using System;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>doc/Modules/31 §9: personalization cannot add a widget the user isn't permitted to see — server-enforced regardless of what the client requests.</summary>
    public class WidgetNotPermittedException : InvalidOperationException
    {
        public WidgetNotPermittedException(int userAccountId, int widgetDefinitionId)
            : base($"User {userAccountId} does not hold the permission required for widget {widgetDefinitionId} (doc/Modules/31 §9).")
        {
        }
    }
}
