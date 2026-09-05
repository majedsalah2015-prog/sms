using Sms.Application.Learning;
using Sms.Domain.Learning;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Learning
{
    /// <summary>
    /// BR-LRN-008: a paper matches the Module 17 component it fills, or it is not
    /// approved. Plus §4's paper spine.
    /// </summary>
    public class PaperReconciliationGateTests
    {
        [Fact]
        [BusinessRule("BR-LRN-008")]
        public void A_paper_matching_its_component_reconciles()
            => Assert.True(PaperReconciliationGate.Reconciles(20m, 20m));

        [Fact]
        [BusinessRule("BR-LRN-008")]
        public void A_paper_worth_more_than_its_component_does_not_reconcile()
            => Assert.False(PaperReconciliationGate.Reconciles(22m, 20m));

        [Fact]
        [BusinessRule("BR-LRN-008")]
        public void A_paper_worth_less_than_its_component_does_not_reconcile()
            => Assert.False(PaperReconciliationGate.Reconciles(18m, 20m));

        [Fact]
        [BusinessRule("BR-LRN-008")]
        public void The_variance_is_signed_so_the_meter_can_say_over_or_short()
        {
            Assert.Equal(3m, PaperReconciliationGate.Variance(23m, 20m));
            Assert.Equal(-2m, PaperReconciliationGate.Variance(18m, 20m));
            Assert.Equal(0m, PaperReconciliationGate.Variance(20m, 20m));
        }

        [Fact]
        [BusinessRule("BR-LRN-008")]
        public void Fractional_marks_reconcile_exactly_like_whole_ones()
            => Assert.True(PaperReconciliationGate.Reconciles(7.5m + 7.5m + 5m, 20m));

        // ---------------------------------------------------------------- the gate

        [Fact]
        [BusinessRule("BR-LRN-008")]
        public void A_paper_in_the_wrong_state_is_refused_before_anything_is_counted()
            => Assert.Equal(
                PaperRefusal.WrongStatus,
                PaperReconciliationGate.Check(
                    OnlinePaperStatus.Draft, OnlinePaperStatus.PendingApproval, 5, 20m, 20m, 0));

        [Fact]
        [BusinessRule("BR-LRN-008")]
        public void An_empty_paper_cannot_be_sent_for_approval()
            => Assert.Equal(
                PaperRefusal.NoItems,
                PaperReconciliationGate.Check(
                    OnlinePaperStatus.Draft, OnlinePaperStatus.Draft, 0, 0m, 20m, 0));

        [Fact]
        [BusinessRule("BR-LRN-007")]
        public void A_question_withdrawn_after_it_was_added_blocks_approval()
            => Assert.Equal(
                PaperRefusal.ContainsWithdrawnQuestion,
                PaperReconciliationGate.Check(
                    OnlinePaperStatus.Draft, OnlinePaperStatus.Draft, 5, 20m, 20m, 1));

        [Fact]
        [BusinessRule("BR-LRN-008")]
        public void A_paper_that_does_not_add_up_is_refused()
            => Assert.Equal(
                PaperRefusal.MarksDoNotReconcile,
                PaperReconciliationGate.Check(
                    OnlinePaperStatus.Draft, OnlinePaperStatus.Draft, 5, 19m, 20m, 0));

        [Fact]
        [BusinessRule("BR-LRN-008")]
        public void A_paper_that_adds_up_passes()
            => Assert.Equal(
                PaperRefusal.None,
                PaperReconciliationGate.Check(
                    OnlinePaperStatus.Draft, OnlinePaperStatus.Draft, 5, 20m, 20m, 0));

        [Fact]
        [BusinessRule("BR-LRN-008")]
        public void The_item_count_is_information_and_never_a_refusal_of_its_own()
        {
            // One question carrying the whole component is unusual and is not
            // wrong: the blueprint states a weight, never a question count.
            Assert.Equal(
                PaperRefusal.None,
                PaperReconciliationGate.Check(
                    OnlinePaperStatus.Draft, OnlinePaperStatus.Draft, 1, 20m, 20m, 0));

            Assert.Equal(
                PaperRefusal.None,
                PaperReconciliationGate.Check(
                    OnlinePaperStatus.Draft, OnlinePaperStatus.Draft, 40, 20m, 20m, 0));
        }

        // ---------------------------------------------------------------- the spine

        [Theory]
        [BusinessRule("BR-LRN-008")]
        [InlineData(OnlinePaperStatus.Draft, OnlinePaperStatus.PendingApproval, true)]
        [InlineData(OnlinePaperStatus.PendingApproval, OnlinePaperStatus.Approved, true)]
        [InlineData(OnlinePaperStatus.PendingApproval, OnlinePaperStatus.Draft, true)]
        [InlineData(OnlinePaperStatus.Approved, OnlinePaperStatus.Withdrawn, true)]
        [InlineData(OnlinePaperStatus.Draft, OnlinePaperStatus.Approved, false)]
        [InlineData(OnlinePaperStatus.Withdrawn, OnlinePaperStatus.Draft, false)]
        public void The_paper_spine_offers_only_the_moves_section_4_names(
            OnlinePaperStatus from, OnlinePaperStatus to, bool allowed)
            => Assert.Equal(allowed, OnlinePaperStatusTransitions.CanTransition(from, to));

        [Fact]
        [BusinessRule("BR-LRN-008")]
        public void An_approved_paper_never_returns_to_draft()
        {
            // Reopening it would leave the head of department's approval standing
            // on a document that no longer exists.
            Assert.False(OnlinePaperStatusTransitions.CanTransition(
                OnlinePaperStatus.Approved, OnlinePaperStatus.Draft));
            Assert.False(OnlinePaperStatusTransitions.CanTransition(
                OnlinePaperStatus.Approved, OnlinePaperStatus.PendingApproval));
        }

        [Fact]
        [BusinessRule("BR-LRN-016")]
        public void A_withdrawn_paper_is_history_rather_than_a_draft()
        {
            Assert.False(OnlinePaperStatusTransitions.CanTransition(
                OnlinePaperStatus.Withdrawn, OnlinePaperStatus.PendingApproval));
            Assert.False(OnlinePaperStatusTransitions.CanTransition(
                OnlinePaperStatus.Withdrawn, OnlinePaperStatus.Approved));
        }

        [Theory]
        [BusinessRule("BR-LRN-008")]
        [InlineData(OnlinePaperStatus.Draft, true)]
        [InlineData(OnlinePaperStatus.PendingApproval, false)]
        [InlineData(OnlinePaperStatus.Approved, false)]
        [InlineData(OnlinePaperStatus.Withdrawn, false)]
        public void Items_move_only_while_the_paper_is_a_draft(OnlinePaperStatus status, bool editable)
            => Assert.Equal(editable, OnlinePaperStatusTransitions.IsEditable(status));

        [Fact]
        [BusinessRule("BR-LRN-008")]
        public void Only_an_approved_paper_can_be_scheduled_to_a_sitting()
        {
            Assert.True(OnlinePaperStatusTransitions.CanBeScheduled(OnlinePaperStatus.Approved));
            Assert.False(OnlinePaperStatusTransitions.CanBeScheduled(OnlinePaperStatus.Draft));
            Assert.False(OnlinePaperStatusTransitions.CanBeScheduled(OnlinePaperStatus.PendingApproval));
        }
    }
}
