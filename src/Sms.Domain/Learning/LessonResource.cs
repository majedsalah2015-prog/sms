using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Learning
{
    /// <summary>
    /// lrn.LessonResource (doc/Modules/37 §7, §8.2): teaching material hung on a
    /// <see cref="Lesson"/> through the existing attachment pipeline.
    ///
    /// BR-LRN-006: this row carries no bytes and no file metadata of its own. The
    /// typing (<c>DocumentTypeId</c>), the size limit and the virus scan all stay
    /// in <c>doc.Attachment</c>, which already versions itself — so "versioned"
    /// in §7 is satisfied by the attachment pipeline rather than by a second
    /// version mechanism competing with it. The scan gate is a serving concern:
    /// a resource whose attachment is not scan-clean is never served, to staff or
    /// to the portal.
    ///
    /// Carries its own <see cref="SchoolId"/> — the tenant filter must hold at
    /// every level, not only at the aggregate root.
    ///
    /// Unlike <see cref="Lesson"/> this is <c>IActivatable</c>: a mis-attached
    /// file is withdrawn from the lesson without retiring the lesson, and the
    /// hard-delete guard then makes a physical removal throw (BR-GLB-005).
    /// </summary>
    [Audited(AuditTier.T2)]
    public class LessonResource : AuditableEntity, ISchoolScoped, IYearScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int LessonId { get; set; }

        /// <summary>doc/Modules/37 §7 — the link into doc.Attachment (doc 10). Owns no bytes itself.</summary>
        public int AttachmentId { get; set; }

        public string TitleAr { get; set; } = string.Empty;

        public string TitleEn { get; set; } = string.Empty;

        /// <summary>Teacher-controlled order within the lesson; material is read in a sequence, not alphabetically.</summary>
        public int DisplayOrder { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
