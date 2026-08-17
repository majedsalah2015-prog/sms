using Sms.Application.Certificates;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Certificates
{
    public class CertificatePrerequisiteEvaluatorTests
    {
        [Fact]
        [BusinessRule("BR-CRT-003")]
        public void No_requirements_always_passes()
        {
            Assert.True(CertificatePrerequisiteEvaluator.AreMet(false, false, false, false));
        }

        [Fact]
        [BusinessRule("BR-CRT-003")]
        public void Missing_published_results_fails_when_required()
        {
            Assert.False(CertificatePrerequisiteEvaluator.AreMet(requiresPublishedResults: true, hasPublishedResults: false, false, false));
        }

        [Fact]
        [BusinessRule("BR-CRT-008")]
        public void Fee_not_clear_fails_when_clearance_required()
        {
            Assert.False(CertificatePrerequisiteEvaluator.AreMet(false, false, requiresFeeClearance: true, isFeeClear: false));
        }

        [Fact]
        [BusinessRule("BR-CRT-003")]
        public void Both_requirements_satisfied_passes()
        {
            Assert.True(CertificatePrerequisiteEvaluator.AreMet(true, true, true, true));
        }
    }
}
