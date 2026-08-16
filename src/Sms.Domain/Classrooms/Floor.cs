using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Classrooms
{
    /// <summary>core.Floor (doc/Modules/08 §7): light hierarchy child of Building.</summary>
    [Audited(AuditTier.T3)]
    public class Floor : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public int BuildingId { get; set; }

        public LocalizedName Name { get; set; } = new();

        public int SequenceOrder { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
