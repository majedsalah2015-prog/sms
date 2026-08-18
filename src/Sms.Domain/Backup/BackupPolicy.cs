using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Backup
{
    /// <summary>
    /// ops.BackupPolicy (doc/Modules/35 §7, BR-BAK-002): schedule/retention
    /// per deployment class. Deployment-wide, not ISchoolScoped — the
    /// underlying database and attachment store are shared across tenants
    /// (same reasoning as ops.JobDefinition).
    /// </summary>
    [Audited(AuditTier.T1)]
    public class BackupPolicy : AuditableEntity
    {
        public BackupDeploymentClass DeploymentClass { get; set; }

        public int RetentionDailyCount { get; set; }

        public int RetentionMonthlyCount { get; set; }

        public int RetentionYearlyCount { get; set; }

        public bool OnPremResponsibilityAcknowledged { get; set; }
    }
}
