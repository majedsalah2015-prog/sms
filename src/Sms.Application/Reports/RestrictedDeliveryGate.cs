using Sms.Domain.Reports;

namespace Sms.Application.Reports
{
    /// <summary>Pure BR-RPT-003: restricted reports never schedule to email — portal-delivery only.</summary>
    public static class RestrictedDeliveryGate
    {
        public static bool IsChannelAllowed(ReportSensitivity sensitivity, DeliveryChannel channel)
            => sensitivity != ReportSensitivity.Restricted || channel != DeliveryChannel.Email;
    }
}
