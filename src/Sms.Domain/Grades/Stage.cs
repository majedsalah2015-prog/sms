using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Grades
{
    /// <summary>core.Stage (doc/Modules/05 §7, BR-GRD-001): KG/Elementary/Intermediate/Secondary — ordered, per school.</summary>
    [Audited(AuditTier.T2)]
    public class Stage : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public LocalizedName Name { get; set; } = new();

        public int SequenceOrder { get; set; }

        public GenderPolicy DefaultGenderPolicy { get; set; } = GenderPolicy.Mixed;

        public bool IsActive { get; set; } = true;
    }
}
