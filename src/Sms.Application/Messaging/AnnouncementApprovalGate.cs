using Sms.Domain.Messaging;

namespace Sms.Application.Messaging
{
    /// <summary>Pure BR-MSG-001: section-level sends (homeroom, within scope) need no approval; grade/stage/school-wide do (P2 VP/Principal, not enforced here — only the approval-timestamp gate is).</summary>
    public static class AnnouncementApprovalGate
    {
        public static bool RequiresApproval(AudienceScope scope) => scope != AudienceScope.Section;
    }
}
