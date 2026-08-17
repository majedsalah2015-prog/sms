using Sms.Application.Certificates;
using Sms.Domain.Certificates;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Certificates
{
    public class CertificateRequestStatusTransitionsTests
    {
        [Theory]
        [InlineData(CertificateRequestStatus.Requested, CertificateRequestStatus.Approved)]
        [InlineData(CertificateRequestStatus.Requested, CertificateRequestStatus.Rejected)]
        [InlineData(CertificateRequestStatus.Approved, CertificateRequestStatus.Issued)]
        [BusinessRule("BR-CRT-003")]
        public void Legal_moves_are_allowed(CertificateRequestStatus from, CertificateRequestStatus to)
        {
            Assert.True(CertificateRequestStatusTransitions.CanTransition(from, to));
        }

        [Theory]
        [InlineData(CertificateRequestStatus.Requested, CertificateRequestStatus.Issued)]
        [InlineData(CertificateRequestStatus.Approved, CertificateRequestStatus.Rejected)]
        [InlineData(CertificateRequestStatus.Issued, CertificateRequestStatus.Approved)]
        [BusinessRule("BR-CRT-003")]
        public void Illegal_moves_are_rejected(CertificateRequestStatus from, CertificateRequestStatus to)
        {
            Assert.False(CertificateRequestStatusTransitions.CanTransition(from, to));
        }
    }
}
