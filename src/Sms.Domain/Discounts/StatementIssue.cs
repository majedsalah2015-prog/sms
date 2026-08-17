using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Discounts
{
    /// <summary>
    /// A formally issued payer statement of account (doc/Modules/19 §8.7,
    /// BR-DIS-010, and E-501's dunning "statement letter (numbered,
    /// Module 18 pattern)"): doc 08 "STM" number + frozen snapshot of the
    /// statement lines and totals as of issue. Ad-hoc statement views are
    /// computed on demand (IStatementService.BuildAsync); only formal
    /// letters are numbered and kept.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class StatementIssue : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int PayerId { get; set; }

        public string StatementNo { get; set; } = string.Empty;

        public DateTime AsOfUtc { get; set; }

        public string SnapshotJson { get; set; } = string.Empty;

        public decimal ClosingBalance { get; set; }
    }
}
