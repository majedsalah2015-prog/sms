using System;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-BAK-004: the pre-operation snapshot failed, so the initiating operation (purge/import commit/rollover) must abort.</summary>
    public class SnapshotFailedException : InvalidOperationException
    {
        public SnapshotFailedException(string triggerOperation)
            : base($"Pre-operation snapshot failed for '{triggerOperation}' — the operation was blocked (BR-BAK-004).")
        {
        }
    }

    /// <summary>BR-BAK-005: the restore-case chain only allows the documented next step.</summary>
    public class InvalidRestoreCaseTransitionException : InvalidOperationException
    {
        public InvalidRestoreCaseTransitionException(int restoreCaseId, string from, string to)
            : base($"Restore case {restoreCaseId} cannot move from {from} to {to} (BR-BAK-005).")
        {
        }
    }
}
