using System.Collections.Generic;
using Sms.Application.Certificates;
using Sms.Domain.Certificates;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Certificates
{
    public class CertificateWithholdingPolicyTests
    {
        [Fact]
        [BusinessRule("BR-CRT-008")]
        public void Ksa01_default_forbids_gating_transfer_certificates_only()
        {
            Assert.False(CertificateWithholdingPolicy.MayBeGatedForFees(CertificateKind.TransferCertificate));
            Assert.True(CertificateWithholdingPolicy.MayBeGatedForFees(CertificateKind.EnrollmentProof));
            Assert.True(CertificateWithholdingPolicy.MayBeGatedForFees(CertificateKind.Transcript));
            Assert.True(CertificateWithholdingPolicy.MayBeGatedForFees(CertificateKind.Completion));
        }

        [Fact]
        [BusinessRule("BR-CRT-008")]
        public void A_different_country_pack_set_changes_the_answer()
        {
            var strictPack = new HashSet<CertificateKind> { CertificateKind.TransferCertificate, CertificateKind.Transcript };

            Assert.False(CertificateWithholdingPolicy.MayBeGatedForFees(CertificateKind.Transcript, strictPack));
            Assert.True(CertificateWithholdingPolicy.MayBeGatedForFees(CertificateKind.EnrollmentProof, strictPack));
        }
    }
}
