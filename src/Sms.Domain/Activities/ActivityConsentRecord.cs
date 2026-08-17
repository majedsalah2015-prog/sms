using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Activities
{
    /// <summary>
    /// ppl.ConsentRecord (doc/Modules/29 §7, BR-ACT-005) — named
    /// <c>ActivityConsentRecord</c> because <c>Sms.Domain.Health.ConsentRecord</c>
    /// already exists (E-602's medical consent). Versioned — what was
    /// consented, when, by whom. No consent means no participation, hard,
    /// no override (product safeguarding stance) — enforced by the
    /// Application layer's ConsentGate at the caller, not by this
    /// entity's mere existence.
    /// </summary>
    [Audited(AuditTier.T1)]
    public class ActivityConsentRecord : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int ProgramEnrollmentId { get; set; }

        public string ConsentTextSnapshot { get; set; } = string.Empty;

        public int GrantedByUserId { get; set; }

        public DateTime GrantedAtUtc { get; set; }
    }
}
