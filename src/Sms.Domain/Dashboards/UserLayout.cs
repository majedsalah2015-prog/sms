using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Dashboards
{
    /// <summary>
    /// core.UserLayout (doc/Modules/31 §7, BR-DSH-003): a user's
    /// personalization on top of their role's LayoutTemplate — add/remove/
    /// arrange within permitted widgets only (server-enforced, doc §9).
    /// Absence of any row for a user means "use the template default";
    /// ResetToDefaultAsync just deletes the user's rows rather than
    /// needing a separate "reset" flag.
    /// </summary>
    [Audited(AuditTier.T3)]
    public class UserLayout : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int UserAccountId { get; set; }

        public int WidgetDefinitionId { get; set; }

        public int SortOrder { get; set; }

        public bool IsVisible { get; set; } = true;
    }
}
