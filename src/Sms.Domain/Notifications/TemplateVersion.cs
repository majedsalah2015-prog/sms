using Sms.Domain.Common;

namespace Sms.Domain.Notifications
{
    /// <summary>
    /// msg.TemplateVersion (BR-NOT-008): append-only content snapshot. A
    /// Delivery references the exact version rendered, so later edits never
    /// rewrite history — same reasoning as PasswordHistory/AuditEntry.
    /// </summary>
    public class TemplateVersion : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int TemplateId { get; set; }

        public int VersionNumber { get; set; }

        /// <summary>Email only; null for channels without a subject line.</summary>
        public string? SubjectAr { get; set; }

        public string? SubjectEn { get; set; }

        /// <summary>BR-NOT-001 placeholder body, e.g. "{studentName} was absent on {date}." — validated against the event payload by the publisher, not stored here.</summary>
        public string BodyAr { get; set; } = string.Empty;

        public string BodyEn { get; set; } = string.Empty;

        /// <summary>S7/E-703, BR-NTF-001: added this slice — a mandatory test-send gate before a version can go live.</summary>
        public TemplatePublishStatus PublishStatus { get; set; } = TemplatePublishStatus.Draft;
    }
}
