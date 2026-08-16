using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Students
{
    /// <summary>ppl.EmergencyContact (BR-STU-003): ≥ 1 required beyond parents.</summary>
    [Audited(AuditTier.T2)]
    public class EmergencyContact : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int StudentId { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        /// <summary>References core.LookupValue, category "RelationshipType".</summary>
        public int? RelationshipLookupId { get; set; }

        public string Phone { get; set; } = string.Empty;

        public bool IsPickupAuthorized { get; set; }
    }
}
