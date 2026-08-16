using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Employees
{
    /// <summary>
    /// ppl.OrgUnit (doc/Modules/12 §7, BR-EMP-002): administrative org tree
    /// — distinct from Module 07's academic Department, though linkable
    /// (not linked in this slice; no consumer needs it yet).
    /// </summary>
    [Audited(AuditTier.T3)]
    public class OrgUnit : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int? ParentOrgUnitId { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;
    }
}
