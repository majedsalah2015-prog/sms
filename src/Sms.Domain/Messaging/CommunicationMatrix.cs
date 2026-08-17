using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Messaging
{
    /// <summary>msg.CommunicationMatrix (doc/Modules/32 §7, BR-MSG-002): topic -> role routing — "absence -> homeroom, fees -> finance", never to a named inbox.</summary>
    [Audited(AuditTier.T2)]
    public class CommunicationMatrix : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public string TopicCode { get; set; } = string.Empty;

        public int RoutedToRoleId { get; set; }
    }
}
