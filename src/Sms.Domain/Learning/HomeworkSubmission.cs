using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Learning
{
    /// <summary>
    /// lrn.HomeworkSubmission (doc/Modules/37 §7, §8.4/§8.5, BR-LRN-005/011/012/015):
    /// the <b>one live row per (homework, student)</b> BR-LRN-005 mandates —
    /// "one live submission per student per homework; a resubmission supersedes
    /// and retains the prior as a version".
    ///
    /// <para>
    /// This row is the <em>current position</em>: what was last handed in, when,
    /// whether it was late, and what it scored. The hand-ins themselves are the
    /// append-only <see cref="SubmissionVersion"/> stream. Keeping the two apart
    /// is what makes "supersedes and retains" expressible at all — one row the
    /// marking queue and the tracker read, and a log underneath it that nothing
    /// ever rewrites.
    /// </para>
    ///
    /// <para>
    /// <b>Keyed on <see cref="EnrollmentId"/>, not StudentId.</b> §7 names the
    /// column "student", but this product's academic spine keys year
    /// participation on <c>ppl.Enrollment</c> and every mark on it: the roster
    /// (§8.4) is reached by <c>SectionMembership.EnrollmentId</c>, and
    /// BR-LRN-012's handoff is <c>IGradingAdmin.EnterMarkAsync(…, enrollmentId,
    /// …)</c> against a <c>MarkEntry</c> that carries <c>EnrollmentId</c> too. A
    /// StudentId here would need translating to an enrollment at both ends, and
    /// would silently lose the year: a student re-sitting a grade has two
    /// enrollments and must have two separate homework histories, which is
    /// exactly what <see cref="AcademicYearId"/> plus this key gives. Followed
    /// <c>MarkEntry</c> rather than §7's prose, and the deviation is stated in
    /// the slice report.
    /// </para>
    ///
    /// <para>
    /// <b>T1 per BR-LRN-015</b> ("marks and mark changes are T1"), because this
    /// row carries a mark — but deliberately <b>without</b>
    /// <c>[RequiresAuditReason]</c>, for exactly <c>MarkEntry</c>'s reason: the
    /// row is created by the student's submit with <see cref="Score"/> null, so
    /// the teacher's very first real mark is an EF <c>Modified</c> transition
    /// rather than an <c>Added</c> one. <c>[RequiresAuditReason]</c> fires only
    /// on <c>Modified</c>, so it would demand a written justification for every
    /// routine first mark of every homework in the school. The mandatory-reason
    /// half of BR-LRN-015 lands where the rule actually points it — BR-LRN-012's
    /// re-release of a corrected mark is a Module 17 mark change and inherits
    /// that module's T1 change control there.
    /// </para>
    ///
    /// No <c>IActivatable</c>: <see cref="Status"/> is the lifecycle, following
    /// <see cref="Homework"/>. Carries its own <see cref="SchoolId"/> — the
    /// tenant filter must hold at every level, not only at the aggregate root.
    /// </summary>
    [Audited(AuditTier.T1)]
    public class HomeworkSubmission : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int HomeworkId { get; set; }

        /// <summary>
        /// ppl.Enrollment — the year participation pivot every mark in this
        /// product hangs off. Unique with <see cref="HomeworkId"/>: BR-LRN-005's
        /// "one live submission" is a database guarantee, not a service check.
        /// </summary>
        public int EnrollmentId { get; set; }

        /// <summary>The <em>latest</em> hand-in's timestamp — a resubmission moves it. Every previous value survives on its <see cref="SubmissionVersion"/>.</summary>
        public DateTime SubmittedAtUtc { get; set; }

        /// <summary>
        /// BR-LRN-005: late work is accepted and <em>flagged</em>, never refused.
        /// A flag rather than a status, so a late hand-in is marked and released
        /// exactly like any other; the lateness policy decides the penalty at
        /// marking, not the acceptance at submit.
        /// </summary>
        public bool IsLate { get; set; }

        /// <summary>
        /// The mark this hand-in carries, already carrying any lateness penalty
        /// (BR-LRN-005: the penalty applies at marking). Null until marked, and
        /// null is what BR-LRN-011 counts when it refuses release.
        /// <para>
        /// One number, deliberately: the teacher's pre-penalty entry is not
        /// stored beside it. Two mark columns on one row is how a school ends up
        /// with two report cards that disagree, which is the failure §1 names as
        /// this module's whole design centre. The roster carries
        /// <see cref="IsLate"/> and the homework's penalty percentage instead, so
        /// a screen can explain the number without a second copy of it.
        /// </para>
        /// </summary>
        public decimal? Score { get; set; }

        /// <summary>doc/Modules/37 §8.5 — the teacher's words to this student. Free text, one language as written; the teacher is not asked to translate their own feedback.</summary>
        public string? Feedback { get; set; }

        /// <summary>Who marked it — the account, so §10's teacher-activity and turnaround reports have their subject.</summary>
        public int? MarkedByUserAccountId { get; set; }

        public DateTime? MarkedAtUtc { get; set; }

        public SubmissionStatus Status { get; set; } = SubmissionStatus.Submitted;

        /// <summary>
        /// How many hand-ins this row has superseded, and therefore the highest
        /// <c>SubmissionVersion.VersionNumber</c> beneath it. Denormalised on
        /// purpose: §8.4's tracker shows a resubmission count for a whole
        /// section at a glance, and counting the log per row would be one query
        /// per student.
        /// </summary>
        public int VersionCount { get; set; }
    }
}
