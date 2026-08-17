using System;
using System.Collections.Generic;
using Sms.Domain.Discounts;

namespace Sms.Application.Discounts
{
    /// <summary>
    /// Pure BR-DIS-005: turn a grant (basis + value + stage + cap) into
    /// per-charge discount amounts. Charges arrive in posting order with
    /// their unpaid, undiscounted remainder; a discount never exceeds
    /// that remainder ("discounts never produce negative positions") and
    /// the per-student cap trims the tail. Percentage of net × (1+VAT)
    /// equals the same percentage of gross, so the stage only changes
    /// FixedAmount: BeforeVat means the fixed value is a net figure whose
    /// gross effect is value × (1+VAT).
    /// </summary>
    public static class DiscountAmountCalculator
    {
        public sealed record ChargeInput(int ChargeId, decimal GrossAmount, decimal VatRate, decimal Remaining);

        public sealed record ChargeDiscount(int ChargeId, decimal Amount);

        public static IReadOnlyList<ChargeDiscount> Compute(
            DiscountBasis basis, DiscountComputationStage stage, decimal basisValue, decimal? capPerStudent, IReadOnlyList<ChargeInput> charges)
        {
            var result = new List<ChargeDiscount>();
            var capRemaining = capPerStudent ?? decimal.MaxValue;
            var fixedRemainingNet = basisValue;

            foreach (var charge in charges)
            {
                if (capRemaining <= 0m || charge.Remaining <= 0m)
                {
                    break;
                }

                decimal amount;
                if (basis == DiscountBasis.Percentage)
                {
                    amount = Math.Round(charge.GrossAmount * basisValue / 100m, 2, MidpointRounding.AwayFromZero);
                }
                else
                {
                    if (fixedRemainingNet <= 0m)
                    {
                        break;
                    }

                    var factor = stage == DiscountComputationStage.BeforeVat ? 1m + charge.VatRate : 1m;
                    var maxNet = charge.Remaining / factor;
                    var takeNet = Math.Min(fixedRemainingNet, maxNet);
                    amount = Math.Round(takeNet * factor, 2, MidpointRounding.AwayFromZero);
                    fixedRemainingNet -= takeNet;
                }

                amount = Math.Min(amount, charge.Remaining);
                amount = Math.Min(amount, capRemaining);
                if (amount <= 0m)
                {
                    continue;
                }

                capRemaining -= amount;
                result.Add(new ChargeDiscount(charge.ChargeId, amount));
            }

            return result;
        }
    }
}
