using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Dashboards
{
    /// <summary>core.LayoutTemplateWidget — one widget's position within a LayoutTemplate.</summary>
    [Audited(AuditTier.T3)]
    public class LayoutTemplateWidget : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int LayoutTemplateId { get; set; }

        public int WidgetDefinitionId { get; set; }

        public int SortOrder { get; set; }
    }
}
