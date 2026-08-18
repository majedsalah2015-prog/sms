using Sms.Application.Backup;
using Sms.Domain.Backup;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Backup
{
    public class BackupTrustEvaluatorTests
    {
        [Fact]
        [BusinessRule("BR-BAK-003")]
        public void Trusted_only_when_complete_and_last_verification_passed()
        {
            Assert.True(BackupTrustEvaluator.IsTrusted(BackupRunStatus.Complete, true));
        }

        [Fact]
        [BusinessRule("BR-BAK-003")]
        public void Not_trusted_without_a_passed_verification()
        {
            Assert.False(BackupTrustEvaluator.IsTrusted(BackupRunStatus.Complete, false));
            Assert.False(BackupTrustEvaluator.IsTrusted(BackupRunStatus.Complete, null));
        }

        [Fact]
        [BusinessRule("BR-BAK-003")]
        public void Not_trusted_when_the_run_itself_is_not_complete()
        {
            Assert.False(BackupTrustEvaluator.IsTrusted(BackupRunStatus.Degraded, true));
            Assert.False(BackupTrustEvaluator.IsTrusted(BackupRunStatus.Failed, true));
        }
    }
}
