using Sms.Application.Activities;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Activities
{
    public class TripStaffRatioEvaluatorTests
    {
        [Theory]
        [InlineData(10, 10, 1)]
        [InlineData(11, 10, 2)]
        [InlineData(20, 10, 2)]
        [InlineData(21, 10, 3)]
        [BusinessRule("BR-ACT-004")]
        public void RequiredStaff_rounds_up_a_partial_group(int studentCount, int ratio, int expected)
        {
            Assert.Equal(expected, TripStaffRatioEvaluator.RequiredStaff(studentCount, ratio));
        }

        [Theory]
        [InlineData(20, 2, 10, true)]
        [InlineData(20, 1, 10, false)]
        [BusinessRule("BR-ACT-004")]
        public void IsSatisfied_matches_required_staff(int studentCount, int assignedStaff, int ratio, bool expected)
        {
            Assert.Equal(expected, TripStaffRatioEvaluator.IsSatisfied(studentCount, assignedStaff, ratio));
        }
    }
}
