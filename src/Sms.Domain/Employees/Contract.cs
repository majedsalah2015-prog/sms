using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Employees
{
    /// <summary>
    /// ppl.Contract (doc/Modules/12 §7, BR-EMP-003): effective-dated
    /// employment contract. BR-EMP-003/O10: salary fields are restricted
    /// (🔒 HR + Principal only) and, per O10's binding decision, meant to
    /// be SQL Server Always Encrypted (randomized) in production — that
    /// mechanism needs a real SQL Server instance with a column master
    /// key, which doesn't exist anywhere in this environment (same
    /// category as E-006's usp_IssueNumber substitution). Modeled here as
    /// plain decimal columns with the restriction enforced at the
    /// application/permission layer (BR-GLB-072) instead; flagged
    /// explicitly rather than faking encryption Sqlite can't provide.
    /// </summary>
    [Audited(AuditTier.T1)]
    public class Contract : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int EmployeeId { get; set; }

        public ContractType Type { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        [RequiresAuditReason]
        public decimal SalaryBasic { get; set; }

        [RequiresAuditReason]
        public decimal? SalaryAllowances { get; set; }

        public ContractStatus Status { get; set; } = ContractStatus.Draft;
    }
}
