using Sms.Application.Subjects;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Subjects
{
    public class CurriculumPlanValidatorTests
    {
        [Fact]
        [BusinessRule("BR-SUB-005")]
        public void Total_weekly_periods_sums_every_offering()
        {
            Assert.Equal(15, CurriculumPlanValidator.TotalWeeklyPeriods(new[] { 5, 4, 6 }));
        }

        [Fact]
        [BusinessRule("BR-SUB-005")]
        public void A_plan_at_the_available_slot_ceiling_is_valid()
        {
            Assert.True(CurriculumPlanValidator.IsWithinAvailableSlots(totalWeeklyPeriods: 30, availableSlotsPerWeek: 30));
        }

        [Fact]
        [BusinessRule("BR-SUB-005")]
        public void A_plan_over_the_available_slot_ceiling_is_invalid()
        {
            Assert.False(CurriculumPlanValidator.IsWithinAvailableSlots(totalWeeklyPeriods: 31, availableSlotsPerWeek: 30));
        }

        [Fact]
        [BusinessRule("BR-SUB-009")]
        public void An_assessable_offering_needs_a_positive_weight()
        {
            Assert.False(CurriculumPlanValidator.HasValidWeight(isAssessable: true, gpaWeight: 0));
            Assert.True(CurriculumPlanValidator.HasValidWeight(isAssessable: true, gpaWeight: 1));
        }

        [Fact]
        [BusinessRule("BR-SUB-003")]
        public void A_non_assessable_offering_has_no_weight_constraint()
        {
            Assert.True(CurriculumPlanValidator.HasValidWeight(isAssessable: false, gpaWeight: 0));
        }
    }
}
