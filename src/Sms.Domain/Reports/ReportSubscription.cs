using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Reports
{
    /// <summary>ppl.ReportSubscription (doc/Modules/30 §7, BR-RPT-006): recipients limited to users holding the report's permission — verified at save (VerifySubscriptionRecipientsAsync's "each send" half needs the actual dispatch job, E-011, not wired).</summary>
    [Audited(AuditTier.T2)]
    public class ReportSubscription : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int ReportDefinitionId { get; set; }

        public int SubscriberUserId { get; set; }

        public SubscriptionFrequency Frequency { get; set; }

        public string ParametersJson { get; set; } = string.Empty;

        public OutputFormat Format { get; set; }

        public DeliveryChannel DeliveryChannel { get; set; } = DeliveryChannel.Portal;

        public bool IsActive { get; set; } = true;
    }
}
