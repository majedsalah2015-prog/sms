using System;
using System.Collections.Generic;
using Sms.Application.Payments;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Payments
{
    /// <summary>
    /// BR-FEE-004 / BR-PAR-005 over the case that put this engine here: a child with a
    /// father and a mother on the same card, only one of whom the school bills. The
    /// cashier screen used to show both guardians as a bare name over the same student
    /// number, so a receipt could be issued against the parent with no open invoice —
    /// allocating to nothing, held as advance balance, while the fees stayed open on the
    /// other one.
    /// </summary>
    public class PayerResponsibilityEvaluatorTests
    {
        private const int Child = 10;
        private const int Sibling = 11;
        private const int Father = 1;
        private const int Mother = 2;

        /// <summary>Mother is billed for the child; father is a live guardian who is not.</summary>
        private static readonly IReadOnlyList<PayerResponsibilityEvaluator.GuardianLink> Family = new[]
        {
            new PayerResponsibilityEvaluator.GuardianLink(Child, Father, false),
            new PayerResponsibilityEvaluator.GuardianLink(Child, Mother, true),
        };

        [Fact]
        [BusinessRule("BR-FEE-004")]
        public void The_billed_guardian_is_responsible()
        {
            Assert.True(PayerResponsibilityEvaluator.IsResponsibleFor(Mother, Child, Family));
        }

        [Fact]
        [BusinessRule("BR-FEE-004")]
        public void A_guardian_who_is_not_billed_is_not_responsible_however_real_the_link_is()
        {
            Assert.False(PayerResponsibilityEvaluator.IsResponsibleFor(Father, Child, Family));
        }

        [Fact]
        [BusinessRule("BR-PAR-005")]
        public void Responsibility_is_per_child_not_per_family()
        {
            // BR-PAR-005's own case: divorced parents each covering specific children.
            var split = new[]
            {
                new PayerResponsibilityEvaluator.GuardianLink(Child, Mother, true),
                new PayerResponsibilityEvaluator.GuardianLink(Sibling, Mother, false),
                new PayerResponsibilityEvaluator.GuardianLink(Sibling, Father, true),
            };

            Assert.True(PayerResponsibilityEvaluator.IsResponsibleFor(Mother, Child, split));
            Assert.False(PayerResponsibilityEvaluator.IsResponsibleFor(Mother, Sibling, split));
            Assert.True(PayerResponsibilityEvaluator.IsResponsibleFor(Father, Sibling, split));
        }

        [Fact]
        [BusinessRule("BR-PAR-005")]
        public void An_ended_link_carries_no_responsibility()
        {
            // Only live links reach the evaluator, and the child still appears on the card
            // because the old charges name that payer — the moment a cashier is most likely
            // to take money for a family this person no longer pays for.
            var afterCustodyChanged = new[]
            {
                new PayerResponsibilityEvaluator.GuardianLink(Child, Mother, true),
            };

            Assert.False(PayerResponsibilityEvaluator.IsResponsibleFor(Father, Child, afterCustodyChanged));
            Assert.True(PayerResponsibilityEvaluator.IsResponsibleForNothing(Father, new[] { Child }, afterCustodyChanged));
        }

        [Fact]
        [BusinessRule("BR-FEE-004")]
        public void A_payer_with_no_parent_behind_it_is_responsible_for_nobody()
        {
            // BR-FEE-004's reserved sponsor path: Payer.ParentId is null and must not
            // resolve to whatever guardian happens to be first in the list.
            Assert.False(PayerResponsibilityEvaluator.IsResponsibleFor(null, Child, Family));
            Assert.True(PayerResponsibilityEvaluator.IsResponsibleForNothing(null, new[] { Child }, Family));
        }

        [Fact]
        [BusinessRule("BR-FEE-004")]
        public void The_warning_fires_for_the_guardian_the_school_does_not_bill()
        {
            Assert.True(PayerResponsibilityEvaluator.IsResponsibleForNothing(Father, new[] { Child }, Family));
            Assert.False(PayerResponsibilityEvaluator.IsResponsibleForNothing(Mother, new[] { Child }, Family));
        }

        [Fact]
        [BusinessRule("BR-PAR-005")]
        public void The_warning_stays_silent_when_responsibility_is_merely_split()
        {
            // Billed for one of the two children is not the wrong-payer case — the per-child
            // marks carry that, and a warning here would be noise on a busy counter.
            var split = new[]
            {
                new PayerResponsibilityEvaluator.GuardianLink(Child, Mother, true),
                new PayerResponsibilityEvaluator.GuardianLink(Sibling, Mother, false),
            };

            Assert.False(PayerResponsibilityEvaluator.IsResponsibleForNothing(Mother, new[] { Child, Sibling }, split));
        }

        [Fact]
        [BusinessRule("BR-FEE-004")]
        public void A_card_with_no_children_states_nothing_either_way()
        {
            Assert.False(PayerResponsibilityEvaluator.IsResponsibleForNothing(Father, Array.Empty<int>(), Family));
        }
    }
}
