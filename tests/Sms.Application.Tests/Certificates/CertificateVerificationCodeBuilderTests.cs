using System;
using Sms.Application.Certificates;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Certificates
{
    public class CertificateVerificationCodeBuilderTests
    {
        [Fact]
        [BusinessRule("BR-CRT-005")]
        public void Same_inputs_produce_the_same_code()
        {
            var issuedAtUtc = new DateTime(2027, 6, 1, 8, 0, 0, DateTimeKind.Utc);

            var code1 = CertificateVerificationCodeBuilder.Build("CERT-000001", issuedAtUtc);
            var code2 = CertificateVerificationCodeBuilder.Build("CERT-000001", issuedAtUtc);

            Assert.Equal(code1, code2);
        }

        [Fact]
        [BusinessRule("BR-CRT-005")]
        public void Different_certificate_numbers_produce_different_codes()
        {
            var issuedAtUtc = new DateTime(2027, 6, 1, 8, 0, 0, DateTimeKind.Utc);

            var code1 = CertificateVerificationCodeBuilder.Build("CERT-000001", issuedAtUtc);
            var code2 = CertificateVerificationCodeBuilder.Build("CERT-000002", issuedAtUtc);

            Assert.NotEqual(code1, code2);
        }

        [Fact]
        [BusinessRule("BR-CRT-005")]
        public void Code_is_a_fixed_length_hex_string()
        {
            var code = CertificateVerificationCodeBuilder.Build("CERT-000001", new DateTime(2027, 6, 1, 8, 0, 0, DateTimeKind.Utc));

            Assert.Equal(16, code.Length);
            Assert.Matches("^[0-9A-F]+$", code);
        }
    }
}
