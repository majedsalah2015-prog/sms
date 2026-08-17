namespace Sms.Domain.Messaging
{
    /// <summary>BR-MSG-001: section-level sends need no approval; grade/stage/school-wide do (P2 VP/Principal, not enforced here — the approval TIMESTAMP gate is).</summary>
    public enum AnnouncementStatus : short
    {
        Draft = 1,
        PendingApproval = 2,
        Sent = 3,
    }
}
