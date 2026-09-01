using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Application.Discounts;
using Sms.Application.Statements;
using Sms.Domain.Discounts;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Discounts
{
    public class DiscountAmountCalculatorTests
    {
        private static DiscountAmountCalculator.ChargeInput Charge(int id, decimal gross, decimal remaining, decimal vat = 0.15m) => new(id, gross, vat, remaining);

        [Fact]
        [BusinessRule("BR-DIS-005")]
        public void Percentage_applies_per_charge_and_never_exceeds_the_remaining_balance()
        {
            var charges = new[] { Charge(1, 1150m, 1150m), Charge(2, 575m, 100m) };

            var result = DiscountAmountCalculator.Compute(DiscountBasis.Percentage, DiscountComputationStage.BeforeVat, 20m, null, charges);

            Assert.Equal(230m, result.Single(r => r.ChargeId == 1).Amount);
            Assert.Equal(100m, result.Single(r => r.ChargeId == 2).Amount);   // 115 capped at the 100 remaining
        }

        [Fact]
        [BusinessRule("BR-DIS-001")]
        public void Fixed_amount_before_VAT_has_a_gross_effect_of_value_times_one_plus_VAT()
        {
            var charges = new[] { Charge(1, 1150m, 1150m) };

            var before = DiscountAmountCalculator.Compute(DiscountBasis.FixedAmount, DiscountComputationStage.BeforeVat, 100m, null, charges);
            var after = DiscountAmountCalculator.Compute(DiscountBasis.FixedAmount, DiscountComputationStage.AfterVat, 100m, null, charges);

            Assert.Equal(115m, before.Single().Amount);
            Assert.Equal(100m, after.Single().Amount);
        }

        [Fact]
        [BusinessRule("BR-DIS-005")]
        public void Fixed_amount_walks_charges_in_order_and_the_student_cap_trims_the_tail()
        {
            var charges = new[] { Charge(1, 500m, 500m, 0m), Charge(2, 500m, 500m, 0m) };

            var uncapped = DiscountAmountCalculator.Compute(DiscountBasis.FixedAmount, DiscountComputationStage.AfterVat, 700m, null, charges);
            var capped = DiscountAmountCalculator.Compute(DiscountBasis.FixedAmount, DiscountComputationStage.AfterVat, 700m, capPerStudent: 600m, charges);

            Assert.Equal(new[] { 500m, 200m }, uncapped.Select(r => r.Amount));
            Assert.Equal(new[] { 500m, 100m }, capped.Select(r => r.Amount));
        }
    }

    public class StackingPolicyEvaluatorTests
    {
        [Fact]
        [BusinessRule("BR-DIS-001")]
        public void Non_stackable_types_never_combine_and_stackable_ones_respect_the_combined_cap()
        {
            var existing = new[] { new StackingPolicyEvaluator.ExistingGrant(true, 15m) };

            Assert.True(StackingPolicyEvaluator.CanStack(true, 10m, existing, 50m));
            Assert.False(StackingPolicyEvaluator.CanStack(true, 40m, existing, 50m));
            Assert.False(StackingPolicyEvaluator.CanStack(false, 10m, existing, 100m));
            Assert.False(StackingPolicyEvaluator.CanStack(true, 10m, new[] { new StackingPolicyEvaluator.ExistingGrant(false, 15m) }, 100m));
            Assert.True(StackingPolicyEvaluator.CanStack(false, 100m, Array.Empty<StackingPolicyEvaluator.ExistingGrant>(), 100m));
        }
    }

    public class ApprovalRoutersTests
    {
        [Theory]
        [InlineData(10, ApprovalTier.FinanceManager)]
        [InlineData(10.01, ApprovalTier.Principal)]
        [InlineData(25, ApprovalTier.Principal)]
        [InlineData(26, ApprovalTier.Owner)]
        [BusinessRule("BR-DIS-003")]
        public void Manual_grants_route_by_percentage_thresholds(decimal percent, ApprovalTier expected)
        {
            Assert.Equal(expected, GrantApprovalRouter.Route(DiscountGrantSource.Manual, percent));
        }

        [Fact]
        [BusinessRule("BR-DIS-004")]
        public void Scholarships_route_to_the_committee()
        {
            Assert.Equal(ApprovalTier.Committee, GrantApprovalRouter.Route(DiscountGrantSource.Scholarship, 100m));
        }

        [Fact]
        [BusinessRule("BR-DIS-006")]
        public void Waivers_route_by_amount()
        {
            Assert.Equal(ApprovalTier.FinanceManager, WaiverApprovalRouter.Route(500m, 500m));
            Assert.Equal(ApprovalTier.Principal, WaiverApprovalRouter.Route(500.01m, 500m));
        }
    }

    public class SiblingLadderEvaluatorTests
    {
        [Fact]
        [BusinessRule("BR-DIS-002")]
        public void Children_rank_eldest_first_and_the_highest_reached_step_applies()
        {
            var siblings = new[]
            {
                new SiblingLadderEvaluator.Sibling(10, new DateTime(2015, 1, 1)),
                new SiblingLadderEvaluator.Sibling(11, new DateTime(2012, 1, 1)),
                new SiblingLadderEvaluator.Sibling(12, new DateTime(2018, 1, 1)),
                new SiblingLadderEvaluator.Sibling(13, new DateTime(2020, 1, 1)),
            };
            var ladder = new[] { new SiblingLadderEvaluator.LadderStep(3, 10m), new SiblingLadderEvaluator.LadderStep(4, 15m) };

            var ordinals = SiblingLadderEvaluator.Ordinals(siblings);

            Assert.Equal(1, ordinals[11]);
            Assert.Equal(0m, SiblingLadderEvaluator.Percent(ordinals[11], ladder));
            Assert.Equal(0m, SiblingLadderEvaluator.Percent(ordinals[10], ladder));
            Assert.Equal(10m, SiblingLadderEvaluator.Percent(ordinals[12], ladder));
            Assert.Equal(15m, SiblingLadderEvaluator.Percent(ordinals[13], ladder));
        }

        [Fact]
        [BusinessRule("BR-DIS-002")]
        public void A_child_holds_a_position_in_every_family_they_are_linked_to()
        {
            // 20 and 21 share a guardian; 21 is also linked to a second guardian, alone.
            var links = new[]
            {
                new SiblingLadderEvaluator.FamilyLink(1, 20, new DateTime(2015, 1, 1)),
                new SiblingLadderEvaluator.FamilyLink(1, 21, new DateTime(2018, 1, 1)),
                new SiblingLadderEvaluator.FamilyLink(2, 21, new DateTime(2018, 1, 1)),
            };

            var positions = SiblingLadderEvaluator.Positions(links);

            Assert.Equal(new[] { (1, 1, 2) }, positions[20].Select(p => (p.ParentId, p.Ordinal, p.SiblingCount)));
            Assert.Equal(new[] { (1, 2, 2), (2, 1, 1) }, positions[21].Select(p => (p.ParentId, p.Ordinal, p.SiblingCount)).OrderBy(x => x.ParentId));
        }

        [Fact]
        [BusinessRule("BR-DIS-002")]
        public void Two_links_to_the_same_guardian_do_not_make_a_child_its_own_sibling()
        {
            var links = new[]
            {
                new SiblingLadderEvaluator.FamilyLink(1, 30, new DateTime(2015, 1, 1)),
                new SiblingLadderEvaluator.FamilyLink(1, 30, new DateTime(2015, 1, 1)),
            };

            var positions = SiblingLadderEvaluator.Positions(links);

            var only = Assert.Single(positions[30]);
            Assert.Equal(1, only.Ordinal);
            Assert.Equal(1, only.SiblingCount);
        }
    }

    public class EnvelopeAndClawbackTests
    {
        [Fact]
        [BusinessRule("BR-DIS-004")]
        public void Envelope_blocks_on_either_cap()
        {
            Assert.True(EnvelopeEvaluator.HasRoom(maxAwards: 2, maxTotalAmount: 10000m, approvedCount: 1, approvedAmount: 4000m, newAmount: 5000m));
            Assert.False(EnvelopeEvaluator.HasRoom(maxAwards: 2, maxTotalAmount: null, approvedCount: 2, approvedAmount: 0m, newAmount: 1m));
            Assert.False(EnvelopeEvaluator.HasRoom(maxAwards: null, maxTotalAmount: 10000m, approvedCount: 0, approvedAmount: 6000m, newAmount: 5000m));
        }

        [Fact]
        [BusinessRule("BR-DIS-008")]
        public void Clawback_recovers_only_the_forward_fraction_of_the_year()
        {
            var start = new DateTime(2026, 9, 1);
            var end = new DateTime(2027, 6, 30);   // 303 days

            Assert.Equal(1m, ClawbackCalculator.ForwardFraction(start, start, end));
            Assert.Equal(0m, ClawbackCalculator.ForwardFraction(end.AddDays(1), start, end));
            Assert.Equal(Math.Round(1000m * 100m / 303m, 2), ClawbackCalculator.Amount(1000m, new DateTime(2027, 3, 23), start, end));   // 100 days left incl.
        }
    }

    public class StatementBuilderTests
    {
        [Fact]
        [BusinessRule("BR-DIS-010")]
        public void Statement_separates_gross_discounts_credit_notes_and_payments_with_a_running_balance()
        {
            var lines = new List<StatementLine>
            {
                new(new DateTime(2026, 9, 5), StatementLineKind.Payment, "RCP-1", "Payment", 0m, 300m),
                new(new DateTime(2026, 9, 1), StatementLineKind.Charge, "INV-1", "Charge", 1000m, 0m),
                new(new DateTime(2026, 9, 2), StatementLineKind.Discount, "DSC-1", "Discount", 0m, 100m),
                new(new DateTime(2026, 9, 3), StatementLineKind.CreditNote, "CRN-1", "Credit note", 0m, 50m),
            };

            var statement = StatementBuilder.Build(lines);

            Assert.Equal(new[] { "INV-1", "DSC-1", "CRN-1", "RCP-1" }, statement.Lines.Select(l => l.DocumentNo));
            Assert.Equal(new[] { 1000m, 900m, 850m, 550m }, statement.Lines.Select(l => l.RunningBalance));
            Assert.Equal(1000m, statement.GrossCharges);
            Assert.Equal(100m, statement.Discounts);
            Assert.Equal(50m, statement.CreditNotes);
            Assert.Equal(850m, statement.NetCharges);
            Assert.Equal(550m, statement.ClosingBalance);
        }

        [Fact]
        [BusinessRule("BR-DIS-010")]
        public void As_of_date_cuts_later_documents()
        {
            var lines = new[]
            {
                new StatementLine(new DateTime(2026, 9, 1), StatementLineKind.Charge, "INV-1", "Charge", 1000m, 0m),
                new StatementLine(new DateTime(2026, 10, 1), StatementLineKind.Payment, "RCP-1", "Payment", 0m, 1000m),
            };

            Assert.Equal(1000m, StatementBuilder.Build(lines, new DateTime(2026, 9, 15)).ClosingBalance);
        }
    }
}
