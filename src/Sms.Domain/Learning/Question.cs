using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Learning
{
    /// <summary>
    /// lrn.Question (doc/Modules/37 §7, §8.6, BR-LRN-007): one item in a bank,
    /// bilingual, versioned, and never edited out from under a paper somebody has
    /// already sat.
    ///
    /// <para>
    /// <b>How versioning is shaped.</b> A revision is a new row, not a mutation:
    /// it keeps the <see cref="RootQuestionId"/> of the original, takes the next
    /// <see cref="Version"/>, and becomes the one <see cref="IsCurrentVersion"/>.
    /// Picks read the current version; a paper item stores the exact row it
    /// froze. That is what makes BR-LRN-007's "a past paper always renders as it
    /// was answered" true by construction rather than by a rule somebody has to
    /// remember — the old row is still there, unchanged, with its own options.
    /// </para>
    ///
    /// <para>
    /// <b>Deprecation is not deletion and not a version.</b>
    /// <see cref="IsDeprecated"/> takes the question out of future picks and out
    /// of nothing already sat (BR-GLB-006, BR-LRN-016). A deprecated question
    /// still renders on the paper that used it, which is the whole point.
    /// </para>
    ///
    /// <para>
    /// No <see cref="IActivatable"/> and deliberately no
    /// <c>ISoftActiveFiltered</c>: §7 puts versioned catalogs outside the filter,
    /// because a frozen past paper must stay loadable. Deprecation is the
    /// lifecycle here, following <see cref="Homework"/>'s status.
    /// </para>
    ///
    /// T2 per BR-LRN-015 — a definition. What a student later scores on it is
    /// Module 17's T1 concern, not this row's.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class Question : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int QuestionBankId { get; set; }

        /// <summary>
        /// The identity that survives revision: every version of one question
        /// shares it, and version 1 carries its own id. A paper that wants "the
        /// current wording of this question" asks by root; a paper that has been
        /// sat holds the row id and never looks it up again.
        /// </summary>
        public int RootQuestionId { get; set; }

        /// <summary>1 for the original, incrementing per revision (BR-LRN-007).</summary>
        public int Version { get; set; } = 1;

        /// <summary>Exactly one version of a root is current. Picks read this; frozen paper items ignore it.</summary>
        public bool IsCurrentVersion { get; set; } = true;

        public QuestionType Type { get; set; }

        /// <summary>BR-GLB-001: both languages, because a bilingual school sits both.</summary>
        public string StemAr { get; set; } = string.Empty;

        public string StemEn { get; set; } = string.Empty;

        /// <summary>The mark this question is worth by default. A paper may weight it differently; the bank states what the author intended.</summary>
        public decimal Marks { get; set; }

        public QuestionDifficulty Difficulty { get; set; } = QuestionDifficulty.Medium;

        /// <summary>
        /// §8.7's third generation axis, expressed as the lesson the question
        /// belongs to rather than as free text: the product already owns the
        /// syllabus structure, and a typed topic list would be a second one that
        /// drifts from it. Null for a question that spans the course.
        /// </summary>
        public int? LessonId { get; set; }

        /// <summary>
        /// BR-LRN-011: how close a numeric answer must be. Null on every other
        /// type. Zero means exact — which is a real answer to "how many
        /// electrons", and different from null.
        /// </summary>
        public decimal? NumericTolerance { get; set; }

        /// <summary>Shown after marking where the paper allows it — the author explaining why, not the system.</summary>
        public string? ExplanationAr { get; set; }

        public string? ExplanationEn { get; set; }

        /// <summary>BR-LRN-007/BR-GLB-006: out of future picks, still on every paper that used it.</summary>
        public bool IsDeprecated { get; set; }

        public string? DeprecatedReason { get; set; }
    }
}
