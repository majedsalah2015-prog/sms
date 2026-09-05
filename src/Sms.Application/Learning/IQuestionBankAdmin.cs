using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Learning;

namespace Sms.Application.Learning
{
    /// <summary>
    /// doc/Modules/37 §8.6 — the question bank.
    ///
    /// <para>
    /// Standalone shape: every method saves itself, like <see cref="ILessonAdmin"/>
    /// and <see cref="IHomeworkAdmin"/>.
    /// </para>
    ///
    /// <para>
    /// Reach (BR-LRN-002) is enforced here rather than in the screen, because
    /// "whose questions are these" is a business rule and a second caller must
    /// not be able to skip it. §6 gives the bank to the head of department with
    /// the teacher authoring within their own offerings, and that is exactly what
    /// the offering-level reach already answers.
    /// </para>
    /// </summary>
    public interface IQuestionBankAdmin
    {
        /// <summary>
        /// BR-LRN-002: the offerings this user may author against — the (offering,
        /// section) reach with the section dropped, because a bank belongs to a
        /// subject-year and not to one class of it.
        /// </summary>
        Task<IReadOnlyList<int>> ReachableOfferingsAsync(
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// The banks this user may open for an offering: their own, plus the ones
        /// shared to the offering or the department (BR-LRN-007). Retired banks
        /// are included only when asked for — they stay loadable because their
        /// questions may sit on a paper already answered (§7).
        /// </summary>
        Task<IReadOnlyList<QuestionBank>> BanksAsync(
            int curriculumOfferingId,
            bool includeRetired = false,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Throws <see cref="Common.Exceptions.TeachingReachException"/> when the
        /// author holds no placement on the offering and heads no department over
        /// it (BR-LRN-002).
        /// </summary>
        Task<QuestionBank> CreateBankAsync(
            int curriculumOfferingId,
            string nameAr,
            string nameEn,
            QuestionShareScope shareScope = QuestionShareScope.AuthorOnly,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default);

        Task<QuestionBank> UpdateBankAsync(
            int questionBankId,
            string nameAr,
            string nameEn,
            QuestionShareScope shareScope,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-GLB-005/BR-LRN-016: retired, never deleted. Its questions stop
        /// appearing in future picks and keep rendering on every paper that
        /// already used them.
        /// </summary>
        Task RetireBankAsync(
            int questionBankId,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// The current version of every question in the bank, newest first.
        /// Deprecated ones are excluded unless asked for (BR-LRN-007): an author
        /// picking questions wants the live bank, an author reviewing it wants the
        /// whole of it.
        /// </summary>
        Task<IReadOnlyList<Question>> QuestionsAsync(
            int questionBankId,
            QuestionType? type = null,
            QuestionDifficulty? difficulty = null,
            bool includeDeprecated = false,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default);

        /// <summary>Every version of one question, oldest first — the history BR-LRN-007 exists to keep.</summary>
        Task<IReadOnlyList<Question>> VersionsAsync(
            int rootQuestionId,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default);

        /// <summary>The options and accepted answers belonging to one question version.</summary>
        Task<(IReadOnlyList<QuestionOption> Options, IReadOnlyList<QuestionAcceptedAnswer> AcceptedAnswers)> DetailAsync(
            int questionId,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds version 1 of a new question. The shape is checked by
        /// <see cref="QuestionTypeRules"/> before anything is written — a question
        /// that cannot be marked must not reach a bank a paper will draw on.
        /// Throws <see cref="Common.Exceptions.QuestionShapeException"/> carrying
        /// the specific refusal, and
        /// <see cref="Common.Exceptions.TeachingReachException"/> without reach.
        /// </summary>
        Task<Question> AddQuestionAsync(
            int questionBankId,
            QuestionDraft draft,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-LRN-007: a revision is a new version, never an edit in place. The
        /// previous version stops being current, keeps its own options and
        /// accepted answers untouched, and goes on rendering exactly as it was
        /// answered on any paper that froze it.
        /// <para>
        /// Revising a deprecated question is refused — bringing back a question
        /// somebody withdrew, under a new wording, is a decision that belongs to
        /// creating one rather than to editing.
        /// </para>
        /// </summary>
        Task<Question> ReviseQuestionAsync(
            int questionId,
            QuestionDraft draft,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-LRN-007/BR-GLB-006: out of future picks, out of nothing already sat.
        /// A reason is required because a withdrawn question is a judgement about
        /// the item, and the next author to wonder why deserves the answer.
        /// </summary>
        Task DeprecateQuestionAsync(
            int questionId,
            string reason,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// What an author states about a question, in one shape, so
    /// <see cref="IQuestionBankAdmin.AddQuestionAsync"/> and
    /// <see cref="IQuestionBankAdmin.ReviseQuestionAsync"/> cannot drift apart —
    /// a revision that accepted a different set of fields from a creation is how a
    /// question ends up unable to express, on version two, something it could say
    /// on version one.
    /// </summary>
    public sealed class QuestionDraft
    {
        public QuestionType Type { get; set; }

        public string StemAr { get; set; } = string.Empty;

        public string StemEn { get; set; } = string.Empty;

        public decimal Marks { get; set; }

        public QuestionDifficulty Difficulty { get; set; } = QuestionDifficulty.Medium;

        public int? LessonId { get; set; }

        public decimal? NumericTolerance { get; set; }

        public string? ExplanationAr { get; set; }

        public string? ExplanationEn { get; set; }

        /// <summary>Bilingual text and the correct flag, in authoring order.</summary>
        public IReadOnlyList<QuestionDraftOption> Options { get; set; } = new List<QuestionDraftOption>();

        /// <summary>The spellings a short-text or numeric question accepts (BR-LRN-011).</summary>
        public IReadOnlyList<string> AcceptedAnswers { get; set; } = new List<string>();
    }

    public sealed record QuestionDraftOption(string TextAr, string TextEn, bool IsCorrect);
}
