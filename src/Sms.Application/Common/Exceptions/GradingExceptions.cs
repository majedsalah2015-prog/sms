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
        }
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
        }
    }
}
