using System;
using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Library
{
    /// <summary>Pure BR-LIB-003: checkout needs an available copy, a member within limits and no blocking flags — override is a librarian permission that gets logged, not a policy exception.</summary>
    public static class CheckoutPolicy
    {
        public sealed record Verdict(bool CopyAvailable, bool WithinLoanLimit, bool HasBlockingFlags)
        {
            public bool Allowed => CopyAvailable && WithinLoanLimit && !HasBlockingFlags;
        }

        public static Verdict Evaluate(bool copyAvailable, int activeLoans, int maxConcurrentLoans, bool hasUnpaidFines, bool hasClearanceHold)
            => new(copyAvailable, activeLoans < maxConcurrentLoans, hasUnpaidFines || hasClearanceHold);
    }

    /// <summary>Pure BR-LIB-003: renewals within policy unless another member has reserved the title.</summary>
    public static class RenewalPolicy
    {
        public static bool CanRenew(int renewalCount, int maxRenewals, bool reservedByAnother) => renewalCount < maxRenewals && !reservedByAnother;
    }

    /// <summary>Pure BR-LIB-005: per-day fine with a cap; zero when fines are disabled or nothing is overdue.</summary>
    public static class FineCalculator
    {
        public static int OverdueDays(DateTime dueDate, DateTime asOf) => Math.Max(0, (asOf.Date - dueDate.Date).Days);

        public static decimal Compute(int overdueDays, bool finesEnabled, decimal perDay, decimal cap)
        {
            if (!finesEnabled || overdueDays <= 0)
            {
                return 0m;
            }

            var raw = overdueDays * perDay;
            return cap > 0m ? Math.Min(raw, cap) : raw;
        }
    }

    /// <summary>Pure BR-LIB-006: replacement charge = copy cost, else the policy price; neither → cannot charge (doc §9).</summary>
    public static class ReplacementChargePolicy
    {
        public static decimal? Amount(decimal? copyCost, decimal? policyPrice) => copyCost ?? policyPrice;
    }

    /// <summary>Pure BR-LIB-004: the reservation queue is FIFO; an offer holds for the window then passes on.</summary>
    public static class ReservationQueuePolicy
    {
        public sealed record Queued(int ReservationId, DateTime QueuedAtUtc);

        public static int? NextToOffer(IEnumerable<Queued> queued) => queued.OrderBy(q => q.QueuedAtUtc).ThenBy(q => q.ReservationId).Select(q => (int?)q.ReservationId).FirstOrDefault();

        public static bool HoldExpired(DateTime holdExpiresAtUtc, DateTime nowUtc) => nowUtc > holdExpiresAtUtc;
    }

    /// <summary>Pure BR-LIB-008: a copy expected on the shelf (Available/Repair) that was not scanned is Missing; a scanned copy expected elsewhere (Loaned/Lost/Withdrawn) is Misplaced.</summary>
    public static class StocktakeFindingEvaluator
    {
        public static Sms.Domain.Library.StocktakeFinding Evaluate(Sms.Domain.Library.CopyStatus expected, bool wasScanned)
        {
            var onShelf = expected is Sms.Domain.Library.CopyStatus.Available or Sms.Domain.Library.CopyStatus.Repair or Sms.Domain.Library.CopyStatus.Reserved;
            if (onShelf && !wasScanned)
            {
                return Sms.Domain.Library.StocktakeFinding.Missing;
            }

            if (!onShelf && wasScanned)
            {
                return Sms.Domain.Library.StocktakeFinding.Misplaced;
            }

            return Sms.Domain.Library.StocktakeFinding.Ok;
        }
    }
}
