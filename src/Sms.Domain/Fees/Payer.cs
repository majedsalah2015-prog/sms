using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Fees
{
    /// <summary>
    /// ppl.Payer (doc/Modules/19 §7, BR-FEE-004): billing abstraction from
    /// day one, so sponsor billing (Gulf reality) can activate later
    /// without a schema change. v1 is Parent-backed only — the Sponsor
    /// entity itself (company/embassy/charity) is a deferred future
    /// enhancement (doc §13), so SponsorId stays reserved and unused.
    /// </summary>
    [Audited(AuditTier.T3)]
    public class Payer : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public PayerType Type { get; set; } = PayerType.Parent;

        public int? ParentId { get; set; }
    }
}
