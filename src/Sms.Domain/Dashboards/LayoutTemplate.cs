using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Dashboards
{
    /// <summary>core.LayoutTemplate (doc/Modules/31 §7, BR-DSH-003): a role's default widget set, school-adjustable. Users personalize on top of this via UserLayout, not by editing the template.</summary>
    [Audited(AuditTier.T3)]
    public class LayoutTemplate : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int RoleId { get; set; }
    }
}
