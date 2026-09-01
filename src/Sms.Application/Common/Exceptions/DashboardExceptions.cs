using System;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>The widget registry was asked to change a row that is not there.</summary>
    public class WidgetDefinitionNotFoundException : InvalidOperationException
    {
        public WidgetDefinitionNotFoundException(int widgetDefinitionId)
            : base($"Widget definition {widgetDefinitionId} does not exist.")
        {
        }
    }

    /// <summary>doc/Modules/31 §9: personalization cannot add a widget the user isn't permitted to see — server-enforced regardless of what the client requests.</summary>
    public class WidgetNotPermittedException : InvalidOperationException
    {
        public WidgetNotPermittedException(int userAccountId, int widgetDefinitionId)
            : base($"User {userAccountId} does not hold the permission required for widget {widgetDefinitionId} (doc/Modules/31 §9).")
        {
        }
    }
}
