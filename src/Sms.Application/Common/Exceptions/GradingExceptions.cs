using System;
using Sms.Domain.Grading;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-GRA-001: the scale is locked (a published TermResult already references it) and cannot accept new bands.</summary>
    public class GradingScaleLockedException : InvalidOperationException
    {
        public GradingScaleLockedException(int gradingScaleId)
            : base($"Grading scale {gradingScaleId} is locked and cannot be edited (BR-GRA-001).")
        {
        }
    }

    /// <summary>BR-GRA-003 validation rule: a Blueprint's component weights must sum to exactly 100 before it can be finalized.</summary>
    public class BlueprintWeightMismatchException : InvalidOperationException
    {
        public BlueprintWeightMismatchException(int blueprintId, decimal actualSum)
            : base($"Blueprint {blueprintId}'s component weights sum to {actualSum}, not 100 (BR-GRA-003).")
        {
            ActualSum = actualSum;
        }

        /// <summary>What the weights actually add up to, so the designer is shown the gap rather than left to find it.</summary>
        public decimal ActualSum { get; }
    }

    /// <summary>The blueprint is not finalized (locked) yet — marksheets can't be created against an in-progress weight design.</summary>
    public class BlueprintNotFinalizedException : InvalidOperationException
    {
        public BlueprintNotFinalizedException(int blueprintId)
            : base($"Blueprint {blueprintId} is not finalized yet (BR-GRA-003).")
        {
        }
    }

    /// <summary>The blueprint is already finalized (locked) — its component weight design can no longer change.</summary>
    public class BlueprintLockedException : InvalidOperationException
    {
        public BlueprintLockedException(int blueprintId)
            : base($"Blueprint {blueprintId} is finalized and its components can no longer change (BR-GRA-003).")
        {
        }
    }

    /// <summary>BR-GRA-005: the requested marksheet status pair isn't a legal WF-07 move.</summary>
    public class InvalidMarksheetStatusTransitionException : InvalidOperationException
    {
        public InvalidMarksheetStatusTransitionException(MarksheetStatus from, MarksheetStatus to)
            : base($"Marksheet status cannot move from '{from}' to '{to}' (BR-GRA-005).")
        {
        }
    }

    /// <summary>Doc §9 validation rule: submission/publication requires every student in the batch resolved (marked, absent, or exempt).</summary>
    public class UnresolvedMarkEntriesException : InvalidOperationException
    {
        public UnresolvedMarkEntriesException(int marksheetId, int unresolvedCount)
            : base($"Marksheet {marksheetId} has {unresolvedCount} unresolved mark entries (BR-GRA §9).")
        {
            UnresolvedCount = unresolvedCount;
        }

        /// <summary>How many students still have neither a mark, an absence, nor an exemption.</summary>
        public int UnresolvedCount { get; }
    }

    /// <summary>BR-GRA-001: a scale referenced by a blueprint cannot be deleted.</summary>
    public class GradingScaleInUseException : InvalidOperationException
    {
        public GradingScaleInUseException(int gradingScaleId, int blueprintCount)
            : base($"Grading scale {gradingScaleId} is referenced by {blueprintCount} blueprint(s) and cannot be deleted (BR-GRA-001).")
        {
            BlueprintCount = blueprintCount;
        }

        /// <summary>How many mark designs point at the scale — what has to be moved off it first.</summary>
        public int BlueprintCount { get; }
    }

    /// <summary>BR-GRA-003: a blueprint with marksheets cannot be deleted.</summary>
    public class BlueprintInUseException : InvalidOperationException
    {
        public BlueprintInUseException(int blueprintId, int marksheetCount)
            : base($"Blueprint {blueprintId} has {marksheetCount} marksheet(s) and cannot be deleted (BR-GRA-003).")
        {
            MarksheetCount = marksheetCount;
        }

        /// <summary>How many marksheets were built on this design.</summary>
        public int MarksheetCount { get; }
    }

    /// <summary>Why a marksheet survived a delete — the two ways BR-GRA-011 protects it.</summary>
    public enum MarksheetDeleteBlocker
    {
        /// <summary>It has left Draft, so it is a submitted or published document rather than a form.</summary>
        NotDraft = 1,

        /// <summary>Marks are already in it, and a mark is audited from the moment it is first typed.</summary>
        MarksEntered = 2,
    }

    /// <summary>BR-GRA-011: only an untouched Draft marksheet may be deleted — marks are audited from first entry.</summary>
    public class MarksheetInUseException : InvalidOperationException
    {
        public MarksheetInUseException(int marksheetId, MarksheetDeleteBlocker blocker, MarksheetStatus status)
            : base($"Marksheet {marksheetId} cannot be deleted: {(blocker == MarksheetDeleteBlocker.NotDraft ? $"it is {status}" : "marks have already been entered")} (BR-GRA-011).")
        {
            Blocker = blocker;
            Status = status;
        }

        public MarksheetDeleteBlocker Blocker { get; }

        /// <summary>Where the marksheet stands, so the refusal can name it in the reader's language.</summary>
        public MarksheetStatus Status { get; }
    }
}
