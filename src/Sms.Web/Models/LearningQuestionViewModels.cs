using System;
using System.Collections.Generic;
using Sms.Application.Learning;
using Sms.Domain.Learning;

namespace Sms.Web.Models
{
    /// <summary>One bank in the list, with the counts that say whether it is worth opening.</summary>
    public sealed record QuestionBankRow(
        QuestionBank Bank,
        string Name,
        int LiveQuestionCount,
        int DeprecatedQuestionCount,
        bool IsMine);

    /// <summary>
    /// doc/Modules/37 §8.6, pattern P-LIST: choose the subject, see its banks,
    /// make another.
    /// </summary>
    public sealed class QuestionBanksViewModel
    {
        public IReadOnlyList<OfferingOption> Offerings { get; set; } = Array.Empty<OfferingOption>();

        public int? SelectedOfferingId { get; set; }

        public IReadOnlyList<QuestionBankRow> Banks { get; set; } = Array.Empty<QuestionBankRow>();

        public bool IncludeRetired { get; set; }

        /// <summary>True when the user holds no placement and heads no department — the screen explains BR-LRN-002 instead of showing an empty picker that looks broken.</summary>
        public bool HasNoReach => Offerings.Count == 0;
    }

    /// <summary>One question in a bank's list. Options travel with it so the list can show what the choice actually was without a query per row.</summary>
    public sealed record QuestionRow(
        Question Question,
        string Stem,
        int OptionCount,
        int VersionCount);

    /// <summary>
    /// doc/Modules/37 §8.6, pattern P-LIST: the questions in one bank, filtered by
    /// the two axes §8.7's generation rule will later draw on.
    /// </summary>
    public sealed class QuestionBankViewModel
    {
        public QuestionBank Bank { get; set; } = new();

        public string BankName { get; set; } = string.Empty;

        public string OfferingLabel { get; set; } = string.Empty;

        public IReadOnlyList<QuestionRow> Questions { get; set; } = Array.Empty<QuestionRow>();

        public QuestionType? FilterType { get; set; }

        public QuestionDifficulty? FilterDifficulty { get; set; }

        public bool IncludeDeprecated { get; set; }

        /// <summary>A retired bank is readable and takes nothing new (BR-GLB-006).</summary>
        public bool IsRetired => !Bank.IsActive;
    }

    /// <summary>
    /// doc/Modules/37 §8.6 — authoring one question. The same model serves adding
    /// and revising, because BR-LRN-007 makes a revision a new version of the same
    /// shape rather than a different operation with different fields.
    /// </summary>
    public sealed class QuestionEditViewModel
    {
        public int QuestionBankId { get; set; }

        public string BankName { get; set; } = string.Empty;

        /// <summary>Null when adding; the version being revised when editing.</summary>
        public Question? Revising { get; set; }

        public IReadOnlyList<QuestionOption> Options { get; set; } = Array.Empty<QuestionOption>();

        public IReadOnlyList<QuestionAcceptedAnswer> AcceptedAnswers { get; set; } = Array.Empty<QuestionAcceptedAnswer>();

        /// <summary>§8.7's topic axis — the lessons of this bank's offering (BR-LRN-001).</summary>
        public IReadOnlyList<(int Id, string Title)> Lessons { get; set; } = Array.Empty<(int, string)>();

        public bool IsRevision => Revising != null;

        public QuestionType Type => Revising?.Type ?? QuestionType.SingleChoice;

        /// <summary>
        /// Every version of this question, so an author revising one can see what
        /// they are stepping away from. Empty when adding.
        /// </summary>
        public IReadOnlyList<Question> Versions { get; set; } = Array.Empty<Question>();
    }
}
