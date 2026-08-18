using Sms.Domain.Backup;

namespace Sms.Application.Backup
{
    /// <summary>Pure BR-BAK-003 (NF-A4): a generation is Trusted only once it is Complete and its last verification passed.</summary>
    public static class BackupTrustEvaluator
    {
        public static bool IsTrusted(BackupRunStatus runStatus, bool? lastVerificationPassed)
            => runStatus == BackupRunStatus.Complete && lastVerificationPassed == true;
    }
}
