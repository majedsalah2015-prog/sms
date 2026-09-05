using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Application.Learning;
using Sms.Domain.Learning;

namespace Sms.Web.Models
{
    /// <summary>
    /// doc/Modules/37 §8.4 and §8.5 — one model for both faces of the marking
    /// screen, because they answer the same question about the same class from
    /// two sides: the tracker asks who handed in, the queue asks what it is
    /// worth.
    ///
    /// <para>
    /// Every counter here is derived from <see cref="Roster"/> rather than stored
    /// or queried separately, and that is deliberate. The one that matters is
    /// <see cref="Unscored"/>: it is the exact number
    /// <c>HomeworkSubmissionAdmin.ReleaseAsync</c> computes before
    /// <c>HomeworkReleaseGate</c> refuses on it (BR-LRN-011), so what the
    /// completeness meter shows and what the refusal says can never drift apart.
    /// A screen that counted "unmarked" its own way would eventually offer a
    /// release button the server refuses, which is the worst of both.
    /// </para>
    /// </summary>
    public sealed class HomeworkMarkingViewModel
    {
        public int HomeworkId { get; set; }

        public string Title { get; set; } = string.Empty;

        /// <summary>"Mathematics · 3-A" — the pair BR-LRN-002 measures reach in.</summary>
        public string ClassLabel { get; set; } = string.Empty;

        public DateTime DueDate { get; set; }

        /// <summary>BR-LRN-004: null is ungraded practice, which is marked with feedback and never released.</summary>
        public decimal? MaxMarks { get; set; }

        public int? BlueprintComponentId { get; set; }

        /// <summary>The Module 17 component a released mark lands in (BR-LRN-012). Null on ungraded practice.</summary>
        public string? ComponentLabel { get; set; }

        public HomeworkStatus Status { get; set; }

        public LatenessPolicy LatenessPolicy { get; set; }

        public decimal? LatePenaltyPercent { get; set; }

        public IReadOnlyList<HomeworkRosterRow> Roster { get; set; } = Array.Empty<HomeworkRosterRow>();

        // ---------------------------------------------------------------- the counters

        public int Total => Roster.Count;

        public int Submitted => Roster.Count(r => r.HasSubmitted);

        /// <summary>BR-LRN-005: handed in after the due date, accepted and flagged.</summary>
        public int Late => Roster.Count(r => r.IsLate);

        /// <summary>§8.4's third column. Nobody handed in, so no row exists — this is the count the chase is aimed at.</summary>
        public int Missing => Total - Submitted;

        public int Scored => Roster.Count(r => r.HasSubmitted && r.Score is not null);

        /// <summary>
        /// BR-LRN-011, counted exactly as <c>ReleaseAsync</c> counts it: hand-ins
        /// carrying no score. A student who never handed in is NOT counted —
        /// otherwise one absence would make a homework unreleasable forever.
        /// </summary>
        public int Unscored => Roster.Count(r => r.HasSubmitted && r.Score is null);

        public bool IsGraded => MaxMarks is > 0m;

        /// <summary>True once every hand-in carries a score — the meter is full.</summary>
        public bool IsComplete => Submitted > 0 && Unscored == 0;

        /// <summary>
        /// Mirrors <c>HomeworkReleaseGate.Check</c> so the button is offered only
        /// when the server would accept it. The gate still runs on POST — this is
        /// courtesy, never the enforcement.
        /// </summary>
        public bool CanRelease =>
            Status == HomeworkStatus.Marking
            && IsGraded
            && BlueprintComponentId is not null
            && Unscored == 0;

        public bool IsReleased => Status == HomeworkStatus.Released;

        /// <summary>Marks are entered while the homework is collecting or being marked, never after release (BR-LRN-012).</summary>
        public bool CanScore => Status is HomeworkStatus.Issued or HomeworkStatus.Collecting or HomeworkStatus.Marking;

        /// <summary>The queue is closed to new work only once the teacher says so — §4's Collecting -> Marking step.</summary>
        public bool CanBeginMarking => Status is HomeworkStatus.Issued or HomeworkStatus.Collecting;
    }
}
