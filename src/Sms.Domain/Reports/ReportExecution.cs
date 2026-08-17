using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Reports
{
    /// <summary>ppl.ReportExecution (doc/Modules/30 §7, BR-RPT-003/005): who/when/params/rows/duration/output — T0-audited for personal-data/restricted reports per BR-SEC-021 (this generic row IS that log, no separate audit event needed).</summary>
    [Audited(AuditTier.T1)]
    public class ReportExecution : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int ReportDefinitionId { get; set; }

        public int ExecutedByUserId { get; set; }

        public string ParametersJson { get; set; } = string.Empty;

        public OutputFormat Format { get; set; }

        /// <summary>False = view-only render; true = file output (BR-RPT-003's Export-permission gate applies).</summary>
        public bool WasExport { get; set; }

        public ReportExecutionStatus Status { get; set; } = ReportExecutionStatus.Completed;

        public int? RowCount { get; set; }

        public int? DurationMs { get; set; }

        public DateTime ExecutedAtUtc { get; set; }

        public DateTime? CompletedAtUtc { get; set; }
    }
}
