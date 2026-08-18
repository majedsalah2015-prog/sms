using System;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-SYS-003: commit/rollback only apply to a batch actually sitting in DryRun/Committed as appropriate.</summary>
    public class ImportNotDryRunException : InvalidOperationException
    {
        public ImportNotDryRunException(int importBatchId)
            : base($"Import batch {importBatchId} is not in DryRun (BR-SYS-003).")
        {
        }
    }

    /// <summary>BR-SYS-003: rollback is only allowed while no dependent transactions exist against the batch — approximated as "no later committed batch exists for the same template".</summary>
    public class ImportRollbackWindowClosedException : InvalidOperationException
    {
        public ImportRollbackWindowClosedException(int importBatchId)
            : base($"Import batch {importBatchId} can no longer be rolled back — a later batch has since committed against the same template (BR-SYS-003).")
        {
        }
    }

    /// <summary>BR-SYS-005/BR-AUM-005: horizon not yet reached, an active legal hold, or (for audit data) a frozen-maintenance state.</summary>
    public class PurgeNotEligibleException : InvalidOperationException
    {
        public PurgeNotEligibleException(int purgeExecutionId)
            : base($"Purge execution {purgeExecutionId} is not eligible to run (BR-SYS-005).")
        {
        }
    }

    /// <summary>BR-SYS-005: dual confirmation requires a second, distinct approver.</summary>
    public class SelfApprovalNotAllowedException : InvalidOperationException
    {
        public SelfApprovalNotAllowedException(int userId)
            : base($"User {userId} requested this operation and cannot also be its second approver (BR-SYS-005 dual confirmation).")
        {
        }
    }

    /// <summary>BR-SYS-007: a non-emergency maintenance banner needs the configured minimum lead time before its window starts.</summary>
    public class InsufficientMaintenanceLeadTimeException : InvalidOperationException
    {
        public InsufficientMaintenanceLeadTimeException()
            : base("Maintenance window does not meet the minimum lead time (BR-SYS-007).")
        {
        }
    }
}
