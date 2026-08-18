using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Rollover
{
    /// <summary>One source-year charge with everything already netted against it.</summary>
    public sealed class ChargeRemainder
    {
        public ChargeRemainder(int chargeId, int payerId, decimal gross, decimal credited, decimal discounted, decimal allocated)
        {
            ChargeId = chargeId;
            PayerId = payerId;
            Remaining = gross - credited - discounted - allocated;
        }

        public int ChargeId { get; }

        public int PayerId { get; }

        /// <summary>What is still owed on this charge; ≤ 0 means fully settled (or over-credited — nothing to carry).</summary>
        public decimal Remaining { get; }
    }

    /// <summary>
    /// BR-AYR-009 / BR-FEE-009: the carry-forward is a receivable→receivable
    /// transfer. Per payer: Σ positive remainders of the student's source-year
    /// charges = one OpeningBalance charge in the target year, and each source
    /// charge gets a carry-forward credit note for its own remainder so the
    /// source year nets to zero and the student's overall position (which
    /// spans years, BR-GLB-064) is unchanged by the transfer. Same E-502
    /// discipline: every reader already subtracts credit notes, so nothing
    /// downstream needs to learn a new document type.
    /// </summary>
    public static class CarryForwardCalculator
    {
        /// <summary>payerId → (opening balance total, the charge remainders that make it up).</summary>
        public static IReadOnlyDictionary<int, (decimal Total, IReadOnlyList<ChargeRemainder> Lines)> PlanForStudent(IEnumerable<ChargeRemainder> remainders)
        {
            return remainders
                .Where(r => r.Remaining > 0m)
                .GroupBy(r => r.PayerId)
                .ToDictionary(g => g.Key, g => (g.Sum(r => r.Remaining), (IReadOnlyList<ChargeRemainder>)g.OrderBy(r => r.ChargeId).ToList()));
        }

        /// <summary>doc/Modules/03 §9 hard check: closing receivables = opening balances posted.</summary>
        public static bool Reconciles(decimal closingReceivablesTransferred, decimal openingBalancesPosted)
            => closingReceivablesTransferred == openingBalancesPosted;
    }
}
