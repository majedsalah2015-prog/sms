using System;
using System.Text;
using Sms.Application.Fees;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Fees
{
    public class ZatcaQrCodeBuilderTests
    {
        [Fact]
        [BusinessRule("BR-FEE-005")]
        public void Payload_decodes_back_to_the_five_expected_tlv_fields()
        {
            var timestamp = new DateTime(2027, 3, 1, 10, 30, 0, DateTimeKind.Utc);
            var payload = ZatcaQrCodeBuilder.BuildBase64Payload("Al-Andalus School", "300012345600003", timestamp, 1150.00m, 150.00m);

            var bytes = Convert.FromBase64String(payload);
            var fields = DecodeTlv(bytes);

            Assert.Equal("Al-Andalus School", fields[1]);
            Assert.Equal("300012345600003", fields[2]);
            Assert.Equal("2027-03-01T10:30:00Z", fields[3]);
            Assert.Equal("1150.00", fields[4]);
            Assert.Equal("150.00", fields[5]);
        }

        [Fact]
        [BusinessRule("BR-FEE-005")]
        public void Value_over_255_bytes_is_rejected()
        {
            var longName = new string('س', 300);

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ZatcaQrCodeBuilder.BuildBase64Payload(longName, "300012345600003", DateTime.UtcNow, 100m, 0m));
        }

        private static System.Collections.Generic.Dictionary<byte, string> DecodeTlv(byte[] bytes)
        {
            var result = new System.Collections.Generic.Dictionary<byte, string>();
            var i = 0;
            while (i < bytes.Length)
            {
                var tag = bytes[i];
                var length = bytes[i + 1];
                var value = Encoding.UTF8.GetString(bytes, i + 2, length);
                result[tag] = value;
                i += 2 + length;
            }

            return result;
        }
    }
}
