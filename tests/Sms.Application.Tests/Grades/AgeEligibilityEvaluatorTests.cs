using System;
using Sms.Application.Grades;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Grades
{
    public class AgeEligibilityEvaluatorTests
    {
        private static readonly DateTime Cutoff = new DateTime(2020, 9, 1);

        [Fact]
        [BusinessRule("BR-GRD-005")]
        public void No_configured_range_is_always_eligible()
        {
            Assert.True(AgeEligibilityEvaluator.IsEligible(new DateTime(2019, 1, 1), Cutoff, null, null));
        }

        [Fact]
        [BusinessRule("BR-GRD-005")]
        public void Within_range_is_eligible()
        {
            // ~5.08 years at cutoff
            Assert.True(AgeEligibilityEvaluator.IsEligible(new DateTime(2015, 8, 1), Cutoff, 5m, 6m));
        }

        [Fact]
        [BusinessRule("BR-GRD-005")]
        public void Just_under_max_is_eligible()
        {
            // ~5.92 years at cutoff
            Assert.True(AgeEligibilityEvaluator.IsEligible(new DateTime(2014, 10, 1), Cutoff, 5m, 6m));
        }

        [Fact]
        [BusinessRule("BR-GRD-005")]
        public void Too_young_is_ineligible()
        {
            // ~4.67 years at cutoff
            Assert.False(AgeEligibilityEvaluator.IsEligible(new DateTime(2016, 1, 1), Cutoff, 5m, 6m));
        }

        [Fact]
        [BusinessRule("BR-GRD-005")]
        public void Too_old_is_ineligible()
        {
            // ~7.67 years at cutoff
            Assert.False(AgeEligibilityEvaluator.IsEligible(new DateTime(2013, 1, 1), Cutoff, 5m, 6m));
        }
    }
}
