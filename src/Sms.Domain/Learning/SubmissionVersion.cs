using System;
using Sms.Domain.Common;

namespace Sms.Domain.Learning
{
    /// <summary>
    /// lrn.SubmissionVersion (doc/Modules/37 §7, BR-LRN-005/015): the snapshot of
    /// <em>one act of handing work in</em>. Append-only — written once at submit
    /// and never updated, never superseded in place, never removed.
    ///
    /// <para>
    /// BR-LRN-005: "a resubmission supersedes and retains the prior as a
    /// version". <see cref="HomeworkSubmission"/> is the live position; this is
    /// the history under it. A student who hands in at 21:00, notices a mistake
    /// and hands in again at 22:00 leaves two rows here and one row there.
    /// </para>
    ///
    /// <para>
    /// <b>Never <c>[Audited]</c>, by design.</b> BR-LRN-015 excludes the
    /// submission stream from audit explicitly, and BR-LRN-005 states the reason
    /// in one line: it is already an append-only log, and auditing a log is
    /// circular — the audit entry would carry the same bytes as the row that
    /// caused it, at twice the storage and none of the extra information.
    /// </para>
    ///
    /// Carries its own <see cref="SchoolId"/> and <see cref="AcademicYearId"/> —
    /// the tenant filter must hold at every level, not only at the aggregate
    /// root. No <c>IActivatable</c> and no <c>ISoftActiveFiltered</c>: a
    /// superseded version is not inactive, it is history, and history that
    /// vanishes from a query is history a teacher cannot open when a family asks
    /// what was actually handed in.
    /// </summary>
    public class SubmissionVersion : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int HomeworkSubmissionId { get; set; }

        /// <summary>1 for the first hand-in and up by one per resubmission. Unique with <see cref="HomeworkSubmissionId"/> — an append-only log with a repeated sequence number is not one.</summary>
        public int VersionNumber { get; set; }

        /// <summary>What the student typed. Optional: a hand-in may be files alone (<see cref="SubmissionAttachment"/>), typed work alone, or both.</summary>
        public string? TextResponse { get; set; }

        /// <summary>When <em>this</em> hand-in arrived. The live row's copy moves on resubmission; this one never does.</summary>
        public DateTime SubmittedAtUtc { get; set; }

        /// <summary>
        /// Whether <em>this</em> hand-in was late, decided by
        /// <c>SubmissionLatenessEvaluator</c> at the moment it arrived and frozen
        /// here. Kept per version rather than only on the live row so that a
        /// student who submitted on time and then revised after the deadline is
        /// visibly a different case from one who never handed in until after it.
        /// </summary>
        public bool IsLate { get; set; }
    }
}
