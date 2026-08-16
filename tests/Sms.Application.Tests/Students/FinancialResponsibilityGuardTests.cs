using Sms.Application.Students;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Students
{
    public class FinancialResponsibilityGuardTests
    {
        [Fact]
        [BusinessRule("BR-STU-003")]
        public void At_least_one_responsible_flag_passes()
        {
            Assert.True(FinancialResponsibilityGuard.HasAtLeastOneResponsible(new[] { false, true, false }));
        }

        [Fact]
        [BusinessRule("BR-STU-003")]
        public void No_responsible_flags_fails()
        {
            Assert.False(FinancialResponsibilityGuard.HasAtLeastOneResponsible(new[] { false, false }));
        }

        [Fact]
        [BusinessRule("BR-STU-003")]
        public void No_links_at_all_fails()
        {
            Assert.False(FinancialResponsibilityGuard.HasAtLeastOneResponsible(System.Array.Empty<bool>()));
        }
    }
}
