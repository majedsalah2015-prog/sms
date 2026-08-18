using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Setup
{
    /// <summary>
    /// core.FeatureToggle (doc/Modules/01 §7, BR-SET-006): per-school on/off
    /// for an optional module or capability (see <c>FeatureCatalog</c> for the
    /// codes and their dependencies). Off hides the module from menus and
    /// composes with deny-by-default permissions (doc 06); it never deletes
    /// data. Absent row = the catalog default for that feature. Settings
    /// tier of audit (T1, BR-SET-007) — flipping a school's feature is a
    /// configuration change that must carry a reason once past creation.
    /// </summary>
    [Audited(AuditTier.T1)]
    public class FeatureToggle : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public string FeatureCode { get; set; } = string.Empty;

        [RequiresAuditReason]
        public bool IsEnabled { get; set; }
    }
}
