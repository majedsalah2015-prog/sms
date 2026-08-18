using System.Collections.Generic;
using Sms.Domain.Backup;

namespace Sms.Application.Backup
{
    /// <summary>Pure BR-BAK-006: two consecutive failed runs escalate to product-incident severity.</summary>
    public static class BackupFailureEscalationEvaluator
    {
        public static bool IsIncidentSeverity(IReadOnlyList<BackupRunStatus> recentRunsNewestFirst)
            => recentRunsNewestFirst.Count >= 2
               && recentRunsNewestFirst[0] == BackupRunStatus.Failed
               && recentRunsNewestFirst[1] == BackupRunStatus.Failed;
    }
}
