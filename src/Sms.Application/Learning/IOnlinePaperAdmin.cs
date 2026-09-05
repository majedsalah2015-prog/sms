using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Learning;

namespace Sms.Application.Learning
{
    /// <summary>
    /// doc/Modules/37 §8.7 — the paper builder. Standalone shape: every method
    /// saves itself.
    ///
    /// <para>
    /// Reach (BR-LRN-002) is inherited from the bank the paper draws on rather
    /// than checked again here: a paper belongs to a bank, a bank belongs to an
    /// offering, and a second copy of "may this user touch this offering" would
    /// eventually disagree with the first.
    /// </para>
    ///
    /// <para>
    /// <b>Approval authority is the permission's, not this port's.</b> §6 gives
    /// the Approve verb to the head of department and Create/Edit to the teacher,
    /// and the screen's <c>[RequirePermission]</c> is what enforces that split —
    /// the same deny-by-default gate every other screen in this product uses.
    /// What this port owns is BR-LRN-008: whether the paper is in a state to be
    /// approved at all.
    /// </para>
    /// </summary>
    public interface IOnlinePaperAdmin
    {
        Task<IReadOnlyList<OnlinePaper>> PapersAsync(
            int questionBankId,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-LRN-008: the component is named when the paper is created, not when
        /// it is scheduled, so the reconciliation meter can be live while the
        /// paper is being built instead of a surprise at the end.
        /// Throws <see cref="Common.Exceptions.TeachingReachException"/> without
        /// reach over the bank's offering, and
        /// <see cref="Common.Exceptions.QuestionBankRetiredException"/> for a
        /// retired bank.
        /// </summary>
        Task<OnlinePaper> CreateAsync(
            int questionBankId,
            int blueprintComponentId,
            string titleAr,
            string titleEn,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default);

        /// <summary>The paper's items in order, with the question version each one froze.</summary>
        Task<IReadOnlyList<(PaperItem Item, Question Question)>> ItemsAsync(
            int onlinePaperId,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Everything BR-LRN-008's meter needs, in one read: how many items, what
        /// they add up to, what the component expects, and how many of them have
        /// since been withdrawn from the bank.
        /// </summary>
        Task<PaperReconciliation> ReconciliationAsync(
            int onlinePaperId,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds one question to the paper, pinned to that exact version
        /// (BR-LRN-007). <paramref name="marks"/> defaults to the question's own.
        /// Throws <see cref="Common.Exceptions.PaperNotEditableException"/> unless
        /// the paper is a draft, and
        /// <see cref="Common.Exceptions.QuestionNotInBankException"/> for a
        /// question from another bank — a paper draws on one bank, which is what
        /// keeps it inside one offering.
        /// </summary>
        Task<PaperItem> AddItemAsync(
            int onlinePaperId,
            int questionId,
            decimal? marks = null,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default);

        Task RemoveItemAsync(
            int paperItemId,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// §8.7's generation rule: take up to <paramref name="count"/> questions
        /// from the bank matching the given topic, difficulty and type, skipping
        /// what is already on the paper and anything withdrawn.
        ///
        /// <para>
        /// Returns how many were actually added, which is often fewer than asked
        /// for — a bank of six easy questions cannot supply ten. Saying so is the
        /// point: silently adding six would leave an author believing the paper
        /// was built.
        /// </para>
        /// </summary>
        Task<int> GenerateAsync(
            int onlinePaperId,
            int count,
            int? lessonId = null,
            QuestionDifficulty? difficulty = null,
            QuestionType? type = null,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// §4: draft -> pending approval, gated by BR-LRN-008 so the head of
        /// department is never handed a paper that cannot be approved.
        /// Throws <see cref="Common.Exceptions.PaperRefusedException"/> carrying
        /// the refusal and both totals.
        /// </summary>
        Task SubmitForApprovalAsync(
            int onlinePaperId,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// §4 P2: the head of department approves. Re-runs BR-LRN-008 rather than
        /// trusting the submit-time answer — a question can be withdrawn from the
        /// bank between the two, and the approval is the signature that matters.
        /// </summary>
        Task ApproveAsync(
            int onlinePaperId,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default);

        /// <summary>Hands it back to the author as a draft, with the reason recorded in the audit trail (BR-LRN-015 T2).</summary>
        Task RejectAsync(
            int onlinePaperId,
            string reason,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default);

        /// <summary>BR-LRN-016: withdrawn with a stated reason, never deleted.</summary>
        Task WithdrawAsync(
            int onlinePaperId,
            string reason,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// BR-LRN-008's meter, as one value. Carries the component's own number so the
    /// screen and the refusal are quoting the same figure — a meter computed from
    /// a second query is a meter that eventually disagrees with the gate.
    /// </summary>
    public sealed class PaperReconciliation
    {
        public int ItemCount { get; set; }

        public decimal TotalMarks { get; set; }

        /// <summary>What Module 17 expects this component to be worth.</summary>
        public decimal ComponentMaxScore { get; set; }

        public string ComponentName { get; set; } = string.Empty;

        /// <summary>Items whose question has since been withdrawn from the bank (BR-LRN-007).</summary>
        public int WithdrawnQuestionCount { get; set; }

        public bool Reconciles => PaperReconciliationGate.Reconciles(TotalMarks, ComponentMaxScore);

        /// <summary>Positive is over, negative is short.</summary>
        public decimal Variance => PaperReconciliationGate.Variance(TotalMarks, ComponentMaxScore);
    }
}
