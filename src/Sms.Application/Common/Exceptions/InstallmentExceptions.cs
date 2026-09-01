using System;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>What is wrong with the split table — one case per way BR-INS-001 can be broken.</summary>
    public enum TemplateSplitFault
    {
        /// <summary>The percentages do not add up to a whole plan.</summary>
        SplitsDoNotSumTo100 = 1,

        /// <summary>A split says how much but not when.</summary>
        SplitHasNoDueDateRule = 2,
    }

    /// <summary>
    /// BR-INS-001 / doc §9: splits must sum to 100% and every split needs a due-date rule.
    /// <para>
    /// The fault is carried as a value rather than as an English clause, because the sentence
    /// shown to a collections officer is composed at the Web boundary in their own language, and
    /// a boundary that has to recognise "splits must sum to 100%" by its text is one rename away
    /// from silently going back to English.
    /// </para>
    /// </summary>
    public class InvalidTemplateSplitException : InvalidOperationException
    {
        public InvalidTemplateSplitException(TemplateSplitFault fault)
            : base($"Invalid installment template splits: {Describe(fault)} (BR-INS-001).")
        {
            Fault = fault;
        }

        public TemplateSplitFault Fault { get; }

        private static string Describe(TemplateSplitFault fault) => fault switch
        {
            TemplateSplitFault.SplitsDoNotSumTo100 => "splits must sum to 100%",
            TemplateSplitFault.SplitHasNoDueDateRule => "every split needs a due date or an offset from year start",
            _ => fault.ToString(),
        };
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
            Remainder = remainder;
            Proposed = proposed;
        }

        /// <summary>What is actually left unpaid — the figure the proposal has to match.</summary>
        public decimal Remainder { get; }

        /// <summary>What the proposed instalments add up to.</summary>
        public decimal Proposed { get; }
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
            PromisedDate = promisedDate;
        }

        /// <summary>The date the officer typed, so the refusal can show it back to them.</summary>
        public DateTime PromisedDate { get; }
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

    /// <summary>
    /// doc/Modules/20 §8.5: the collection window runs backwards.
    /// <para>
    /// Refused rather than tolerated because of what tolerating it looks like. An
    /// inverted range matches no installment, so the arrears roll comes back empty
    /// — and an empty arrears screen does not read as "your dates are the wrong way
    /// round", it reads as "nobody owes the school anything". That is the one wrong
    /// answer this module must never give quietly.
    /// </para>
    /// <para>
    /// The dates travel as values, not inside an English clause, so the Web boundary
    /// can say the whole sentence in the reader's language (see
    /// <c>Sms.Web/Models/UserMessage.Finance.cs</c>).
    /// </para>
    /// </summary>
    public class InvalidCollectionWindowException : InvalidOperationException
    {
        public InvalidCollectionWindowException(DateTime from, DateTime to)
            : base($"Collection window starts {from:yyyy-MM-dd} and ends {to:yyyy-MM-dd}, which is before it starts.")
        {
            From = from;
            To = to;
        }

        public DateTime From { get; }

        public DateTime To { get; }
    }
}
