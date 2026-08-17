using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Dashboards
{
    /// <summary>
    /// core.WidgetDefinition (doc/Modules/31 §7, BR-DSH-001): the registry
    /// row a persona layout or personalization references — widget
    /// *content* (the consolidated widget→data-source→drill-path
    /// specification) is a separate Phase 9 deliverable per the doc's own
    /// framing, same "platform vs catalog" split as E-701's Reports.
    /// </summary>
    [Audited(AuditTier.T3)]
    public class WidgetDefinition : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        /// <summary>doc's own "DSH-&lt;MOD&gt;-###" registry code convention.</summary>
        public string Code { get; set; } = string.Empty;

        public string OwningModuleCode { get; set; } = string.Empty;

        public string TitleAr { get; set; } = string.Empty;

        public string TitleEn { get; set; } = string.Empty;

        /// <summary>References sec.Permission — the View action gates whether this widget renders at all (BR-DSH-001 deny-by-default).</summary>
        public int RequiredPermissionId { get; set; }

        public WidgetRefreshClass RefreshClass { get; set; }

        /// <summary>Free-text screen/report reference — every number must click through (doc §1), but no screen registry exists to point at yet.</summary>
        public string DrillTargetCode { get; set; } = string.Empty;

        /// <summary>BR-DSH-006: false = a staff widget, never portal-reachable.</summary>
        public bool IsPortalEligible { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
