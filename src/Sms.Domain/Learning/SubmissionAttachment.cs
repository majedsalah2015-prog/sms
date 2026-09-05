using Sms.Domain.Common;

namespace Sms.Domain.Learning
{
    /// <summary>
    /// lrn.SubmissionAttachment (doc/Modules/37 §8.10, BR-LRN-005/006): one file
    /// filed against one hand-in.
    ///
    /// <para>
    /// <b>Deviation from §7, stated deliberately.</b> §7's entity list names
    /// <c>HomeworkSubmission</c> and <c>SubmissionVersion</c> and stops there —
    /// it does not name this third table. It is added anyway, for two reasons
    /// the doc's own text supplies:
    /// </para>
    /// <list type="number">
    /// <item><description>
    /// §8.10 requires the portal to "submit with upload". Without a join table
    /// the only place to put a file is a single <c>AttachmentId</c> column on the
    /// version, which caps a hand-in at exactly one file. A student told to
    /// photograph four pages of working would have to be told to submit four
    /// times — a product defect a school reports in its first week, not a
    /// simplification.
    /// </description></item>
    /// <item><description>
    /// The files hang off the <see cref="SubmissionVersion"/>, not off the live
    /// <see cref="HomeworkSubmission"/>, and that is what makes BR-LRN-005 true
    /// of attachments as well as of text: when a resubmission supersedes, the
    /// superseded version keeps the files that were actually handed in with it.
    /// Hung off the live row instead, a resubmission would either orphan the
    /// earlier files or silently re-attribute them to work they were never part
    /// of.
    /// </description></item>
    /// </list>
    ///
    /// <para>
    /// BR-LRN-006: this row carries no bytes and no file metadata of its own —
    /// the typing (<c>DocumentTypeId</c>), the size limit and the virus scan all
    /// stay in <c>doc.Attachment</c>, exactly as <see cref="LessonResource"/>
    /// does on the teacher's side. The scan gate is a <em>serving</em> concern:
    /// an unscanned file is accepted from the student and simply never served to
    /// the teacher until it is clean. There is no serving surface in this slice
    /// (screens are the next one), so no gate is written here; the check belongs
    /// with the download action that will need it.
    /// </para>
    ///
    /// Never <c>[Audited]</c>: it is part of the append-only hand-in snapshot
    /// BR-LRN-015 excludes from audit, written once with its version and never
    /// updated. Carries its own <see cref="SchoolId"/> and
    /// <see cref="AcademicYearId"/> — the tenant filter must hold at every level.
    /// </summary>
    public class SubmissionAttachment : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        /// <summary>The hand-in these bytes were part of — the version, never the live row (see the class remarks).</summary>
        public int SubmissionVersionId { get; set; }

        /// <summary>doc/Modules/37 §7 / BR-LRN-006 — the link into doc.Attachment (doc 10). Owns no bytes itself.</summary>
        public int AttachmentId { get; set; }
    }
}
