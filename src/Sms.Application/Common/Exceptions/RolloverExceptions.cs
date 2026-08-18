using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Application.Rollover;
using Sms.Domain.Rollover;
using Sms.Domain.Schools;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-AYR-008: a rollover runs from the Active year into the Preparation year only.</summary>
    public class RolloverYearStatusException : InvalidOperationException
    {
        public RolloverYearStatusException(int academicYearId, AcademicYearStatus actual, AcademicYearStatus expected)
            : base($"Academic year {academicYearId} is {actual}; the rollover requires it to be {expected} (BR-AYR-008).")
        {
        }
    }

    /// <summary>A step was invoked while the batch is in a status that doesn't allow it.</summary>
    public class RolloverBatchStatusException : InvalidOperationException
    {
        public RolloverBatchStatusException(int batchId, RolloverBatchStatus actual, string requirement)
            : base($"Rollover batch {batchId} is {actual}; this step requires {requirement}.")
        {
        }
    }

    /// <summary>BR-GRD-002/009: a grade with enrolled students has no promotion target and isn't graduating (or the path cycles).</summary>
    public class PromotionPathIncompleteException : InvalidOperationException
    {
        public PromotionPathIncompleteException(IReadOnlyList<int> gradeLevelIdsMissingTarget, bool hasCycle)
            : base(hasCycle
                ? "The grade promotion path contains a cycle (BR-GRD-002)."
                : $"Grade level(s) [{string.Join(", ", gradeLevelIdsMissingTarget)}] have enrolled students but no promotion target and are not graduating (BR-GRD-002).")
        {
            GradeLevelIdsMissingTarget = gradeLevelIdsMissingTarget;
            HasCycle = hasCycle;
        }

        public IReadOnlyList<int> GradeLevelIdsMissingTarget { get; }

        public bool HasCycle { get; }
    }

    /// <summary>The target year has no grade-year profile for the grade a decision points at.</summary>
    public class TargetGradeProfileMissingException : InvalidOperationException
    {
        public TargetGradeProfileMissingException(int gradeLevelId, int targetAcademicYearId)
            : base($"Grade level {gradeLevelId} has no grade-year profile in target year {targetAcademicYearId}; define it (or re-open the batch to copy profiles) before deciding.")
        {
        }
    }

    /// <summary>A manual decision that the student's grade doesn't allow (e.g. Graduate on a non-graduating grade, or Undecided).</summary>
    public class InvalidPromotionDecisionException : InvalidOperationException
    {
        public InvalidPromotionDecisionException(int studentId, PromotionDecision decision, string why)
            : base($"Decision '{decision}' is not valid for student {studentId}: {why}.")
        {
        }
    }

    /// <summary>Step 3 approval (P3) refused while any student is still Undecided (doc §9).</summary>
    public class PromotionsUndecidedException : InvalidOperationException
    {
        public PromotionsUndecidedException(int undecidedCount)
            : base($"{undecidedCount} student(s) still have no promotion decision; the batch can't be approved (BR-AYR-008 step 3).")
        {
        }
    }

    /// <summary>Step 4: seat reservation against the target grade's planned capacity failed.</summary>
    public class NoSeatAvailableException : InvalidOperationException
    {
        public NoSeatAvailableException(int gradeYearProfileId)
            : base($"No re-registration seat left for grade-year profile {gradeYearProfileId} (planned seats = sections × size, BR-GRD-006).")
        {
        }
    }

    /// <summary>Step 4/5: the operation needs a decided target grade first (e.g. section assignment while Undecided).</summary>
    public class PromotionNotDecidedException : InvalidOperationException
    {
        public PromotionNotDecidedException(int studentId)
            : base($"Student {studentId} has no promotion decision / target grade yet.")
        {
        }
    }

    /// <summary>Step 5: the chosen section belongs to a different grade-year profile than the student's decision.</summary>
    public class SectionGradeMismatchException : InvalidOperationException
    {
        public SectionGradeMismatchException(int sectionId, int expectedGradeYearProfileId)
            : base($"Section {sectionId} does not belong to grade-year profile {expectedGradeYearProfileId}.")
        {
        }
    }

    /// <summary>Step 5: the section's gender policy (BR-GRD-004 semantics) doesn't admit the student.</summary>
    public class SectionGenderMismatchException : InvalidOperationException
    {
        public SectionGenderMismatchException(int sectionId, int studentId)
            : base($"Section {sectionId}'s gender policy does not admit student {studentId} (BR-GRD-004).")
        {
        }
    }

    /// <summary>The student has no financially-responsible guardian with a payer — nothing to bill the fee / opening balance to.</summary>
    public class NoPayerForStudentException : InvalidOperationException
    {
        public NoPayerForStudentException(int studentId)
            : base($"Student {studentId} has no payer (no financially-responsible guardian with a Payer row, BR-FEE-004).")
        {
        }
    }

    /// <summary>BR-AYR-004 / BR-AYR-005: a year status change refused because its checklist isn't green.</summary>
    public class ChecklistNotGreenException : InvalidOperationException
    {
        public ChecklistNotGreenException(string checklistName, IReadOnlyList<ChecklistItem> items)
            : base($"{checklistName} checklist not green: " + string.Join("; ", items.Where(i => !i.IsSatisfied).Select(i => $"{i.Code} ({i.Detail})")))
        {
            Items = items;
        }

        public IReadOnlyList<ChecklistItem> Items { get; }
    }

    /// <summary>doc/Modules/03 §9 hard check: closing receivables ≠ opening balances posted.</summary>
    public class CarryForwardReconciliationException : InvalidOperationException
    {
        public CarryForwardReconciliationException(decimal closingReceivables, decimal openingBalances)
            : base($"Carry-forward does not reconcile: closing receivables {closingReceivables} ≠ opening balances posted {openingBalances} (BR-AYR-009).")
        {
        }
    }
}
