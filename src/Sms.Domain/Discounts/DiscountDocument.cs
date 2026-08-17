using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Discounts
{
    /// <summary>
    /// ppl.DiscountDocument (doc/Modules/22 §7 "application lines live as
    /// discount documents against charges (Module 19 model)", BR-DIS-005/
    /// 010): a numbered (doc 08 "DSC" series), auditable line reducing one
    /// charge. Distinct from a CreditNote on purpose — BR-DIS-010 says
    /// discounts are never netted invisibly, so every position/statement
    /// reader subtracts credit notes and discount documents as separate
    /// figures. Immutable once issued (revocation is a grant-level event;
    /// claw-back posts a new charge).
    /// </summary>
    [Audited(AuditTier.T1)]
    public class DiscountDocument : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int DiscountGrantId { get; set; }

        public int ChargeId { get; set; }

        public string DocumentNo { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public DateTime IssuedAtUtc { get; set; }
    }
}
