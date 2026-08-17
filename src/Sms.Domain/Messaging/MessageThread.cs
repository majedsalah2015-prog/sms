using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Messaging
{
    /// <summary>
    /// msg.Thread (doc/Modules/32 §7, BR-MSG-002) — named <c>MessageThread</c>
    /// because bare <c>Thread</c> collides with <c>System.Threading.Thread</c>,
    /// which nearly every file in this codebase already imports via
    /// <c>System.Threading</c>/<c>System.Threading.Tasks</c> (same
    /// collision-avoidance discipline as E-607's ActivityProgram/ActivityTrip).
    /// School record, never deletable by participants (BR-GLB-032 spirit).
    /// </summary>
    [Audited(AuditTier.T2)]
    public class MessageThread : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public string TopicCode { get; set; } = string.Empty;

        public int InitiatedByUserId { get; set; }

        public int RoutedToRoleId { get; set; }

        public ThreadStatus Status { get; set; } = ThreadStatus.Open;
    }
}
