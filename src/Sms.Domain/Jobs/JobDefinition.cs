using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Jobs
{
    /// <summary>
    /// ops.JobDefinition (doc 02 T-6). A system-level registry of job
    /// types — deliberately NOT ISchoolScoped: jobs like the audit
    /// checkpoint or notification dispatch run once globally, not once per
    /// school, matching AuditEntry's identical reasoning for staying
    /// unfiltered.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class JobDefinition : AuditableEntity
    {
        /// <summary>e.g. "AuditIntegrityCheckpoint", matched against IJobHandler.JobCode.</summary>
        public string Code { get; set; } = string.Empty;

        public LocalizedName Name { get; set; } = new();

        /// <summary>Standard 5-field cron; interpreted by the Hangfire scheduler, not by this record.</summary>
        public string CronExpression { get; set; } = string.Empty;

        public bool IsEnabled { get; set; } = true;
    }
}
