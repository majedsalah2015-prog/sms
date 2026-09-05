using System;
using System.Collections.Generic;
using Sms.Application.Learning;
using Sms.Domain.Learning;

namespace Sms.Web.Models
{
    /// <summary>doc/Modules/37 §8.7 — a Module 17 component a paper may be built to fill (BR-LRN-008).</summary>
    public sealed record PaperComponentOption(int Id, string Label, decimal MaxScore);

    /// <summary>One paper in the list, with the meter's answer beside it.</summary>
    public sealed record PaperRow(OnlinePaper Paper, string Title, PaperReconciliation Reconciliation);

    /// <summary>doc/Modules/37 §8.7, pattern P-LIST: the papers built on one bank.</summary>
    public sealed class PapersViewModel
    {
        public QuestionBank Bank { get; set; } = new();

        public string BankName { get; set; } = string.Empty;

        public IReadOnlyList<PaperRow> Papers { get; set; } = Array.Empty<PaperRow>();

        /// <summary>BR-LRN-008: only the components of this bank's own offering — a paper cannot fill another subject's component.</summary>
        public IReadOnlyList<PaperComponentOption> Components { get; set; } = Array.Empty<PaperComponentOption>();

        public bool CanBuild => Bank.IsActive && Components.Count > 0;
    }

    /// <summary>One question available to add, with what it would bring to the total.</summary>
    public sealed record PaperCandidate(Question Question, string Stem, bool AlreadyOn);

    /// <summary>
    /// doc/Modules/37 §8.7 — building one paper. The reconciliation meter is the
    /// screen's centre: BR-LRN-008 refuses approval on a mismatch, so the author
    /// sees the two numbers the whole time rather than meeting them at the end.
    /// </summary>
    public sealed class PaperViewModel
    {
        public OnlinePaper Paper { get; set; } = new();

        public string Title { get; set; } = string.Empty;

        public string BankName { get; set; } = string.Empty;

        public PaperReconciliation Reconciliation { get; set; } = new();

        public IReadOnlyList<(PaperItem Item, Question Question)> Items { get; set; }
            = Array.Empty<(PaperItem, Question)>();

        /// <summary>The bank's live questions, so the author picks without leaving the page.</summary>
        public IReadOnlyList<PaperCandidate> Candidates { get; set; } = Array.Empty<PaperCandidate>();

        /// <summary>§8.7's topic axis for the generation rule.</summary>
        public IReadOnlyList<(int Id, string Title)> Lessons { get; set; } = Array.Empty<(int, string)>();

        public bool IsEditable => OnlinePaperStatusTransitions.IsEditable(Paper.Status);

        public bool IsAwaitingApproval => Paper.Status == OnlinePaperStatus.PendingApproval;

        public bool IsApproved => Paper.Status == OnlinePaperStatus.Approved;

        public bool IsWithdrawn => Paper.Status == OnlinePaperStatus.Withdrawn;

        /// <summary>
        /// Mirrors the gate so the button is offered only when the server would
        /// take it. The gate still runs on POST — this is courtesy, never the
        /// enforcement.
        /// </summary>
        public bool CanSubmit => IsEditable
            && Reconciliation.ItemCount > 0
            && Reconciliation.WithdrawnQuestionCount == 0
            && Reconciliation.Reconciles;
    }
}
