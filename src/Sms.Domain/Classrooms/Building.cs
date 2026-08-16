using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Classrooms
{
    /// <summary>core.Building (doc/Modules/08 §7): light hierarchy root — Building → Floor → Room.</summary>
    [Audited(AuditTier.T3)]
    public class Building : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public LocalizedName Name { get; set; } = new();

        public bool IsActive { get; set; } = true;
    }
}
