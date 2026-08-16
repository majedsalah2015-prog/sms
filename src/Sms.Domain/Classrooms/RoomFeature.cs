using Sms.Domain.Common;

namespace Sms.Domain.Classrooms
{
    /// <summary>core.RoomFeature (BR-ROM-005): room × equipment/feature lookup (projector, smartboard, lab benches, AC…).</summary>
    public class RoomFeature : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int RoomId { get; set; }

        /// <summary>References core.LookupValue, category "RoomFeature".</summary>
        public int FeatureLookupId { get; set; }
    }
}
