using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Activities
{
    /// <summary>ppl.ActivityType (doc/Modules/29 §7, BR-ACT-001): club/sport/competition/trip/event catalog.</summary>
    [Audited(AuditTier.T3)]
    public class ActivityType : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        public ActivityCategory Category { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
