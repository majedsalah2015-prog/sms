using Sms.Application.Rollover;
using Sms.Domain.Grading;
using Sms.Domain.Rollover;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Rollover
{
    public class PromotionDecisionMapperTests
    {
        [Theory]
        [BusinessRule("BR-AYR-008")]
        [InlineData(PromotionOutcome.Promote, false, PromotionDecision.Promote)]
        [InlineData(PromotionOutcome.Conditional, false, PromotionDecision.Conditional)]
        [InlineData(PromotionOutcome.Retain, false, PromotionDecision.Retain)]
        [InlineData(PromotionOutcome.Promote, true, PromotionDecision.Graduate)]
        [InlineData(PromotionOutcome.Conditional, true, PromotionDecision.Graduate)]
        [InlineData(PromotionOutcome.Retain, true, PromotionDecision.Retain)]
        public void Grading_outcome_maps_to_rollover_decision(PromotionOutcome outcome, bool graduating, PromotionDecision expected)
        {
            Assert.Equal(expected, PromotionDecisionMapper.Propose(outcome, graduating));
        }

        [Fact]
        [BusinessRule("BR-GRA-006")]
        public void No_year_result_leaves_the_student_undecided()
        {
            Assert.Equal(PromotionDecision.Undecided, PromotionDecisionMapper.Propose(null, isGraduatingGrade: false));
            Assert.Equal(PromotionDecision.Undecided, PromotionDecisionMapper.Propose(null, isGraduatingGrade: true));
        }

        [Theory]
        [BusinessRule("BR-GRD-002")]
        [InlineData(PromotionDecision.Promote, 10, 11, 11)]
        [InlineData(PromotionDecision.Conditional, 10, 11, 11)]
        [InlineData(PromotionDecision.Retain, 10, 11, 10)]
        [InlineData(PromotionDecision.Graduate, 10, null, null)]
        [InlineData(PromotionDecision.Undecided, 10, 11, null)]
        [InlineData(PromotionDecision.Promote, 10, null, null)]   // grade without target: surfaced as null, caller raises
        public void Target_grade_follows_the_decision(PromotionDecision decision, int current, int? target, int? expected)
        {
            Assert.Equal(expected, PromotionDecisionMapper.ResolveTargetGradeLevelId(decision, current, target));
        }

        [Fact]
        public void Only_promote_conditional_and_retain_need_a_seat()
        {
            Assert.True(PromotionDecisionMapper.RequiresTargetSeat(PromotionDecision.Promote));
            Assert.True(PromotionDecisionMapper.RequiresTargetSeat(PromotionDecision.Conditional));
            Assert.True(PromotionDecisionMapper.RequiresTargetSeat(PromotionDecision.Retain));
            Assert.False(PromotionDecisionMapper.RequiresTargetSeat(PromotionDecision.Graduate));
            Assert.False(PromotionDecisionMapper.RequiresTargetSeat(PromotionDecision.Undecided));
        }
    }
}
