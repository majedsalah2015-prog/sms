using System.Linq;
using Sms.Application.Rollover;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Rollover
{
    public class ChecklistAndCarryForwardTests
    {
        private static OpeningChecklistFacts GreenOpening() => new()
        {
            CalendarDayCount = 180, GradeYearProfileCount = 3, SectionCount = 6, FeeStructureLineCount = 3, UnapprovedFeeStructureLineCount = 0,
            GradingScaleCount = 1, TimetablePublished = true, UndecidedPromotionCount = 0, ConfirmedWithoutSectionCount = 0,
        };

        [Fact]
        [BusinessRule("BR-AYR-004")]
        public void Opening_checklist_is_green_only_when_every_item_holds()
        {
            Assert.True(OpeningChecklistEvaluator.IsGreen(OpeningChecklistEvaluator.Evaluate(GreenOpening())));

            var f = GreenOpening();
            f.UnapprovedFeeStructureLineCount = 1;
            var items = OpeningChecklistEvaluator.Evaluate(f);
            Assert.False(OpeningChecklistEvaluator.IsGreen(items));
            Assert.Equal(OpeningChecklistEvaluator.Fees, items.Single(i => !i.IsSatisfied).Code);
        }

        [Fact]
        [BusinessRule("BR-AYR-004")]
        public void Timetable_may_be_published_or_explicitly_deferred()
        {
            var f = GreenOpening();
            f.TimetablePublished = false;
            Assert.False(OpeningChecklistEvaluator.IsGreen(OpeningChecklistEvaluator.Evaluate(f)));

            f.TimetableExplicitlyDeferred = true;
            Assert.True(OpeningChecklistEvaluator.IsGreen(OpeningChecklistEvaluator.Evaluate(f)));
        }

        [Fact]
        [BusinessRule("BR-AYR-008")]
        public void Undecided_students_block_activation()
        {
            var f = GreenOpening();
            f.UndecidedPromotionCount = 2;
            var items = OpeningChecklistEvaluator.Evaluate(f);
            Assert.Equal(OpeningChecklistEvaluator.Promotions, items.Single(i => !i.IsSatisfied).Code);
        }

        [Fact]
        [BusinessRule("BR-AYR-005")]
        public void Closing_checklist_needs_marksheets_workflows_and_a_reconciled_carry_forward()
        {
            var green = new ClosingChecklistFacts { UnresolvedMarksheetCount = 0, OpenWorkflowInstanceCount = 0, CarryForwardPosted = true, CarryForwardReconciled = true };
            Assert.True(ClosingChecklistEvaluator.IsGreen(ClosingChecklistEvaluator.Evaluate(green)));

            green.CarryForwardReconciled = false;
            Assert.Equal(ClosingChecklistEvaluator.Reconciled, ClosingChecklistEvaluator.Evaluate(green).Single(i => !i.IsSatisfied).Code);
        }

        [Fact]
        [BusinessRule("BR-AYR-009")]
        public void Carry_forward_plans_one_opening_balance_per_payer_from_positive_remainders_only()
        {
            var plan = CarryForwardCalculator.PlanForStudent(new[]
            {
                new ChargeRemainder(chargeId: 1, payerId: 7, gross: 1150m, credited: 0m, discounted: 150m, allocated: 500m),   // 500 left
                new ChargeRemainder(chargeId: 2, payerId: 7, gross: 300m, credited: 0m, discounted: 0m, allocated: 300m),      // settled
                new ChargeRemainder(chargeId: 3, payerId: 9, gross: 200m, credited: 50m, discounted: 0m, allocated: 0m),       // 150 left, other payer
            });

            Assert.Equal(2, plan.Count);
            Assert.Equal(500m, plan[7].Total);
            Assert.Single(plan[7].Lines);
            Assert.Equal(150m, plan[9].Total);
        }

        [Fact]
        [BusinessRule("BR-FEE-009")]
        public void Fully_settled_students_have_nothing_to_carry()
        {
            var plan = CarryForwardCalculator.PlanForStudent(new[] { new ChargeRemainder(1, 7, 100m, 100m, 0m, 0m) });
            Assert.Empty(plan);
        }

        [Fact]
        [BusinessRule("BR-AYR-009")]
        public void Reconciliation_is_an_exact_equality()
        {
            Assert.True(CarryForwardCalculator.Reconciles(650m, 650m));
            Assert.False(CarryForwardCalculator.Reconciles(650m, 649.99m));
        }
    }
}
