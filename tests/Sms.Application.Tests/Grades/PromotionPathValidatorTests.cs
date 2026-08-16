using System.Collections.Generic;
using Sms.Application.Grades;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Grades
{
    public class PromotionPathValidatorTests
    {
        [Fact]
        [BusinessRule("BR-GRD-002")]
        public void A_complete_ladder_has_no_missing_targets()
        {
            var grades = new List<GradeSnapshot>
            {
                new(1, 2, isGraduating: false),
                new(2, 3, isGraduating: false),
                new(3, null, isGraduating: true),
            };

            Assert.Empty(PromotionPathValidator.FindGradesMissingPromotionTarget(grades));
        }

        [Fact]
        [BusinessRule("BR-GRD-002")]
        public void A_non_graduating_grade_without_a_target_is_flagged()
        {
            var grades = new List<GradeSnapshot>
            {
                new(1, 2, isGraduating: false),
                new(2, null, isGraduating: false), // missing target, not flagged graduating
            };

            var missing = PromotionPathValidator.FindGradesMissingPromotionTarget(grades);
            Assert.Contains(2, missing);
        }

        [Fact]
        [BusinessRule("BR-GRD-009")]
        public void A_linear_ladder_to_a_graduating_grade_has_no_cycle()
        {
            var grades = new List<GradeSnapshot>
            {
                new(1, 2, isGraduating: false),
                new(2, 3, isGraduating: false),
                new(3, null, isGraduating: true),
            };

            Assert.False(PromotionPathValidator.HasCycle(grades));
        }

        [Fact]
        [BusinessRule("BR-GRD-009")]
        public void A_grade_promoting_back_to_an_earlier_grade_is_a_cycle()
        {
            var grades = new List<GradeSnapshot>
            {
                new(1, 2, isGraduating: false),
                new(2, 3, isGraduating: false),
                new(3, 1, isGraduating: false), // cycles back to grade 1
            };

            Assert.True(PromotionPathValidator.HasCycle(grades));
        }

        [Fact]
        [BusinessRule("BR-GRD-009")]
        public void A_grade_promoting_to_itself_is_a_cycle()
        {
            var grades = new List<GradeSnapshot> { new(1, 1, isGraduating: false) };

            Assert.True(PromotionPathValidator.HasCycle(grades));
        }
    }
}
