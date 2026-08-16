using Sms.Application.Sections;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Sections
{
    public class SectionCapacityGuardTests
    {
        [Fact]
        [BusinessRule("BR-SCN-002")]
        public void A_section_capacity_at_the_grade_plan_is_allowed()
        {
            Assert.True(SectionCapacityGuard.WithinGradePlan(sectionCapacity: 25, gradeTargetSectionSize: 25));
        }

        [Fact]
        [BusinessRule("BR-SCN-002")]
        public void A_section_capacity_over_the_grade_plan_is_rejected()
        {
            Assert.False(SectionCapacityGuard.WithinGradePlan(sectionCapacity: 26, gradeTargetSectionSize: 25));
        }

        [Fact]
        [BusinessRule("BR-SCN-002")]
        public void Assignment_below_capacity_is_allowed()
        {
            Assert.True(SectionCapacityGuard.CanAssign(currentMemberCount: 24, sectionCapacity: 25));
        }

        [Fact]
        [BusinessRule("BR-SCN-002")]
        public void Assignment_at_capacity_is_rejected()
        {
            Assert.False(SectionCapacityGuard.CanAssign(currentMemberCount: 25, sectionCapacity: 25));
        }
    }
}
