using Sms.Domain.Backup;

namespace Sms.Application.Backup
{
    /// <summary>Pure BR-BAK-005 chain: request -> scope definition -> support execution -> post-restore verification -> closed. One-way, no skipping.</summary>
    public static class RestoreCaseStatusTransitions
    {
        public static bool CanTransition(RestoreCaseStatus from, RestoreCaseStatus to)
        {
            return (from, to) switch
            {
                (RestoreCaseStatus.Requested, RestoreCaseStatus.ScopeDefined) => true,
                (RestoreCaseStatus.ScopeDefined, RestoreCaseStatus.Executed) => true,
                (RestoreCaseStatus.Executed, RestoreCaseStatus.Verified) => true,
                (RestoreCaseStatus.Verified, RestoreCaseStatus.Closed) => true,
                _ => false,
            };
        }
    }
}
