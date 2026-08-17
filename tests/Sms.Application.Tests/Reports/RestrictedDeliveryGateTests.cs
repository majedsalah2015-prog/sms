using Sms.Application.Reports;
using Sms.Domain.Reports;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Reports
{
    public class RestrictedDeliveryGateTests
    {
        [Fact]
        [BusinessRule("BR-RPT-003")]
        public void Restricted_reports_refuse_email_delivery()
        {
            Assert.False(RestrictedDeliveryGate.IsChannelAllowed(ReportSensitivity.Restricted, DeliveryChannel.Email));
            Assert.True(RestrictedDeliveryGate.IsChannelAllowed(ReportSensitivity.Restricted, DeliveryChannel.Portal));
        }

        [Theory]
        [InlineData(ReportSensitivity.Normal)]
        [InlineData(ReportSensitivity.PersonalData)]
        [BusinessRule("BR-RPT-003")]
        public void Non_restricted_reports_allow_any_channel(ReportSensitivity sensitivity)
        {
            Assert.True(RestrictedDeliveryGate.IsChannelAllowed(sensitivity, DeliveryChannel.Email));
            Assert.True(RestrictedDeliveryGate.IsChannelAllowed(sensitivity, DeliveryChannel.Portal));
        }
    }
}
