using System.Collections.Generic;
using Sms.Application.Backup;
using Sms.Domain.Backup;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Backup
{
    public class BackupFailureEscalationEvaluatorTests
    {
        [Fact]
        [BusinessRule("BR-BAK-006")]
        public void Two_consecutive_failures_are_incident_severity()
        {
            var runs = new List<BackupRunStatus> { BackupRunStatus.Failed, BackupRunStatus.Failed, BackupRunStatus.Complete };

            Assert.True(BackupFailureEscalationEvaluator.IsIncidentSeverity(runs));
        }

        [Fact]
        [BusinessRule("BR-BAK-006")]
        public void A_single_failure_is_not_incident_severity()
        {
            var runs = new List<BackupRunStatus> { BackupRunStatus.Failed, BackupRunStatus.Complete };

            Assert.False(BackupFailureEscalationEvaluator.IsIncidentSeverity(runs));
        }

        [Fact]
        [BusinessRule("BR-BAK-006")]
        public void A_failure_followed_by_a_success_resets_the_streak()
        {
            var runs = new List<BackupRunStatus> { BackupRunStatus.Complete, BackupRunStatus.Failed };

            Assert.False(BackupFailureEscalationEvaluator.IsIncidentSeverity(runs));
        }
    }
}
