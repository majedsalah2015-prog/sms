using System;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-INS-001 / doc §9: splits must sum to 100% and every split needs a due-date rule.</summary>
    public class InvalidTemplateSplitException : InvalidOperationException
    {
        public InvalidTemplateSplitException(string detail)
            : base($"Invalid installment template splits: {detail} (BR-INS-001).")
        {
        }
    }

    /// <summary>BR-INS-001: only an Approved template can be assigned.</summary>
    public class PlanTemplateNotApprovedException : InvalidOperationException
    {
        public PlanTemplateNotApprovedException(int planTemplateId)
            : base($"Plan template {planTemplateId} is not approved (BR-INS-001).")
        {
        }
    }

    /// <summary>BR-INS-002: nothing posted to schedule for this student-year (and category).</summary>
    public class NoChargesToScheduleException : InvalidOperationException
    {
        public NoChargesToScheduleException(int studentId)
            : base($"Student {studentId} has no posted charges to schedule (BR-INS-002).")
        {
        }
    }

    /// <summary>BR-INS-002: one plan per student-year per category group.</summary>
    public class PlanAssignmentExistsException : InvalidOperationException
    {
        public PlanAssignmentExistsException(int studentId)
            : base($"Student {studentId} already has a plan assignment for this year and category (BR-INS-002).")
        {
        }
    }

    /// <summary>BR-INS-002 / doc §9: exception assignments require a reason.</summary>
    public class ExceptionAssignmentReasonRequiredException : InvalidOperationException
    {
        public ExceptionAssignmentReasonRequiredException()
            : base("A per-family exception assignment requires a reason (BR-INS-002).")
        {
        }
    }

    /// <summary>BR-INS-005 / doc §9: a reschedule must cover exactly the unpaid remainder — no orphan amounts.</summary>
    public class RescheduleRemainderMismatchException : InvalidOperationException
    {
        public RescheduleRemainderMismatchException(decimal remainder, decimal proposed)
            : base($"Reschedule proposal totals {proposed} but the unpaid remainder is {remainder} (BR-INS-005).")
        {
        }
    }

    /// <summary>BR-INS-005: only a Proposed case can be decided.</summary>
    public class RescheduleCaseNotPendingException : InvalidOperationException
    {
        public RescheduleCaseNotPendingException(int rescheduleCaseId)
            : base($"Reschedule case {rescheduleCaseId} is not pending (BR-INS-005).")
        {
        }
    }

    /// <summary>BR-INS-006 / doc §9: promise dates are today..today+horizon, and only against a truly overdue installment.</summary>
    public class PromiseDateOutOfRangeException : InvalidOperationException
    {
        public PromiseDateOutOfRangeException(DateTime promisedDate)
            : base($"Promise date {promisedDate:yyyy-MM-dd} is outside the allowed horizon (BR-INS-006).")
        {
        }
    }

    /// <summary>BR-INS-006: promises are recorded against overdue installments only.</summary>
    public class InstallmentNotOverdueException : InvalidOperationException
    {
        public InstallmentNotOverdueException(int installmentId)
            : base($"Installment {installmentId} is not overdue (BR-INS-006).")
        {
        }
    }

    /// <summary>BR-INS-009: the PDC must belong to the schedule's payer and be live (Lodged/Due/Deposited).</summary>
    public class PdcNotCoverableException : InvalidOperationException
    {
        public PdcNotCoverableException(int pdcId)
            : base($"PDC {pdcId} cannot cover this installment — wrong payer or not a live cheque (BR-INS-009).")
        {
        }
    }

    /// <summary>BR-INS-003/007: a paid, superseded or written-off installment never mutates.</summary>
    public class InstallmentNotOpenException : InvalidOperationException
    {
        public InstallmentNotOpenException(int installmentId)
            : base($"Installment {installmentId} is not open (paid, superseded or written off) (BR-INS-003).")
        {
        }
    }

    /// <summary>
    /// BR-INS-002: a grade-wide run schedules mandatory fees only, so a template
    /// scoped to a non-mandatory category can never schedule anything through it.
    /// Refused up front rather than reported as "no charges" once per student —
    /// thirty identical skips do not tell the officer their template was wrong.
    /// </summary>
    public class TemplateCategoryNotMandatoryException : InvalidOperationException
    {
        public TemplateCategoryNotMandatoryException(int planTemplateId)
            : base($"Plan template {planTemplateId} is scoped to a non-mandatory fee category and cannot drive a mandatory-fees-only grade assignment (BR-INS-002).")
        {
        }
    }

    /// <summary>
    /// BR-INS-001: only a draft template may be rewritten. An approved one may already
    /// have produced schedules, and a schedule is a copy of the shape taken at
    /// assignment — editing the template would leave new families on one shape and
    /// existing ones on another under a single name.
    /// </summary>
    public class PlanTemplateNotDraftException : InvalidOperationException
    {
        public PlanTemplateNotDraftException(int planTemplateId)
            : base($"Plan template {planTemplateId} is approved and can no longer be edited.")
        {
        }
    }
}
