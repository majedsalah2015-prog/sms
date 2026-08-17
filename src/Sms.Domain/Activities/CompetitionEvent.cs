using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Activities
{
    /// <summary>ppl.CompetitionEvent (doc/Modules/29 §7): an external competition an Achievement can reference.</summary>
    [Audited(AuditTier.T3)]
    public class CompetitionEvent : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        public DateTime Date { get; set; }

        public string? ExternalBodyRef { get; set; }
    }
}
