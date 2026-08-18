using Sms.Domain.Audit;

namespace Sms.Application.Audit
{
    /// <summary>Pure BR-AUM-002: only an Open hit can be dispositioned (dismiss or escalate) — a decided hit never gets a second disposition.</summary>
    public static class AnomalyDispositionPolicy
    {
        public static bool CanDispose(AnomalyHitStatus current) => current == AnomalyHitStatus.Open;
    }
}
