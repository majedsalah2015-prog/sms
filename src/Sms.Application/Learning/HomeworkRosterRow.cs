using System;
using Sms.Domain.Learning;

namespace Sms.Application.Learning
{
    /// <summary>
    /// doc/Modules/37 §8.4 — one line of the submission tracker: "submitted /
    /// late / missing roster with one-click chase".
    ///
    /// <para>
    /// The roster is <b>every student in the homework's section</b>, not every
    /// submission — which is the whole point of the screen. A student who handed
    /// nothing in is the row a teacher is looking for, and a query over
    /// <c>HomeworkSubmission</c> alone cannot produce them. So "missing" is
    /// <see cref="HasSubmitted"/> being false, and is derived here rather than
    /// stored (see <see cref="SubmissionStatus"/>'s remarks on why it is not a
    /// status).
    /// </para>
    ///
    /// <para>
    /// Carries <see cref="IsLate"/> and <see cref="LatePenaltyPercent"/> beside
    /// <see cref="Score"/> so the screen can explain the mark without a second
    /// stored copy of it: "13.50 — 25% late penalty applied" is one row's worth
    /// of context, and BR-LRN-005 requires a family to be able to see what
    /// lateness cost.
    /// </para>
    /// </summary>
    public class HomeworkRosterRow
    {
        /// <summary>ppl.Enrollment — the key the submission, the mark entry and the section membership all agree on.</summary>
        public int EnrollmentId { get; set; }

        public int StudentId { get; set; }

        public string StudentNo { get; set; } = string.Empty;

        public string StudentNameAr { get; set; } = string.Empty;

        public string StudentNameEn { get; set; } = string.Empty;

        /// <summary>Null when nothing was handed in — the "missing" column of §8.4's tracker.</summary>
        public int? SubmissionId { get; set; }

        /// <summary>§8.4: false is "missing". Nothing was submitted, so no row exists (BR-LRN-005).</summary>
        public bool HasSubmitted => SubmissionId is not null;

        /// <summary>The latest hand-in's timestamp; null when missing.</summary>
        public DateTime? SubmittedAtUtc { get; set; }

        /// <summary>BR-LRN-005: accepted and flagged. False for a missing hand-in — absent work is not "late", it is absent, and the chase is a different conversation.</summary>
        public bool IsLate { get; set; }

        /// <summary>The mark that counts, lateness penalty already applied (BR-LRN-005). Null is unmarked — what BR-LRN-011 counts when it refuses release.</summary>
        public decimal? Score { get; set; }

        public string? Feedback { get; set; }

        /// <summary>Null when missing.</summary>
        public SubmissionStatus? Status { get; set; }

        /// <summary>How many hand-ins this student has superseded (BR-LRN-005). 0 when missing, 1 for a single hand-in.</summary>
        public int VersionCount { get; set; }

        /// <summary>The homework's penalty percentage, repeated on each row so the screen can explain a reduced <see cref="Score"/> without a second query. Null unless the policy uses one.</summary>
        public decimal? LatePenaltyPercent { get; set; }
    }
}
