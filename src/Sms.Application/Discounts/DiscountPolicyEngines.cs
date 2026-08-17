using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Domain.Discounts;

namespace Sms.Application.Discounts
{
    /// <summary>Pure BR-DIS-001 stacking: a non-stackable grant can't sit beside any other; stackable grants can't exceed the type's combined cap (global default 100%).</summary>
    public static class StackingPolicyEvaluator
    {
        public sealed record ExistingGrant(bool IsStackable, decimal PercentEquivalent);

        public static bool CanStack(bool newIsStackable, decimal newPercentEquivalent, IReadOnlyCollection<ExistingGrant> existing, decimal maxCombinedPercent)
        {
            if (existing.Count == 0)
            {
                return true;
            }

            if (!newIsStackable || existing.Any(e => !e.IsStackable))
            {
                return false;
            }

            return existing.Sum(e => e.PercentEquivalent) + newPercentEquivalent <= Math.Min(maxCombinedPercent, 100m);
        }
    }

    /// <summary>Pure BR-DIS-003 WF-04 threshold routing (P4): ≤10% Finance Manager; ≤25% + Principal; >25% + Owner/Board. Scholarship awards go to the committee (BR-DIS-004).</summary>
    public static class GrantApprovalRouter
    {
        public static ApprovalTier Route(DiscountGrantSource source, decimal percentEquivalent)
        {
            if (source == DiscountGrantSource.Scholarship)
            {
                return ApprovalTier.Committee;
            }

            if (percentEquivalent <= 10m)
            {
                return ApprovalTier.FinanceManager;
            }

            return percentEquivalent <= 25m ? ApprovalTier.Principal : ApprovalTier.Owner;
        }
    }

    /// <summary>Pure BR-DIS-006: waivers are WF-06 family — P2 (Finance Manager) up to the school's threshold, P4 (Principal) above.</summary>
    public static class WaiverApprovalRouter
    {
        public static ApprovalTier Route(decimal amount, decimal principalThreshold)
            => amount <= principalThreshold ? ApprovalTier.FinanceManager : ApprovalTier.Principal;
    }

    /// <summary>Pure BR-DIS-002 sibling ladder: children ranked eldest-first among the family's enrolled students; the highest ladder step whose ordinal ≤ the child's ordinal applies ("3rd child 10%, 4th+ 15%").</summary>
    public static class SiblingLadderEvaluator
    {
        public sealed record Sibling(int StudentId, DateTime DateOfBirth);

        public sealed record LadderStep(int ChildOrdinal, decimal Percent);

        public static IReadOnlyDictionary<int, int> Ordinals(IReadOnlyCollection<Sibling> siblings)
            => siblings.OrderBy(s => s.DateOfBirth).ThenBy(s => s.StudentId)
                .Select((s, i) => (s.StudentId, Ordinal: i + 1))
                .ToDictionary(x => x.StudentId, x => x.Ordinal);

        public static decimal Percent(int ordinal, IReadOnlyCollection<LadderStep> ladder)
            => ladder.Where(s => s.ChildOrdinal <= ordinal).OrderByDescending(s => s.ChildOrdinal).Select(s => s.Percent).FirstOrDefault();
    }

    /// <summary>Pure BR-DIS-004 envelope: exceeding either cap blocks the award (Owner override is the caller's T1 + reason path).</summary>
    public static class EnvelopeEvaluator
    {
        public static bool HasRoom(int? maxAwards, decimal? maxTotalAmount, int approvedCount, decimal approvedAmount, decimal newAmount)
        {
            if (maxAwards.HasValue && approvedCount + 1 > maxAwards.Value)
            {
                return false;
            }

            return !maxTotalAmount.HasValue || approvedAmount + newAmount <= maxTotalAmount.Value;
        }
    }

    /// <summary>Pure BR-DIS-008: the forward (unconsumed) fraction of the year from the revocation's effective date — what a claw-back charges back. Default policy forgives the past, so only this slice is ever recovered.</summary>
    public static class ClawbackCalculator
    {
        public static decimal ForwardFraction(DateTime effectiveDate, DateTime yearStart, DateTime yearEnd)
        {
            var total = (yearEnd.Date - yearStart.Date).Days + 1;
            if (total <= 0)
            {
                return 0m;
            }

            var forward = (yearEnd.Date - effectiveDate.Date).Days + 1;
            return Math.Clamp((decimal)forward / total, 0m, 1m);
        }

        public static decimal Amount(decimal appliedAmount, DateTime effectiveDate, DateTime yearStart, DateTime yearEnd)
            => Math.Round(appliedAmount * ForwardFraction(effectiveDate, yearStart, yearEnd), 2, MidpointRounding.AwayFromZero);
    }
}
