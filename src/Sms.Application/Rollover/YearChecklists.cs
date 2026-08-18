using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Rollover
{
    /// <summary>One named checklist item and whether it's satisfied (doc/Modules/03 §8.3 checklist consoles).</summary>
    public sealed class ChecklistItem
    {
        public ChecklistItem(string code, bool isSatisfied, string detail)
        {
            Code = code;
            IsSatisfied = isSatisfied;
            Detail = detail;
        }

        public string Code { get; }

        public bool IsSatisfied { get; }

        /// <summary>Human-readable evidence ("3 mark sheets not approved"), for the console and the exception message.</summary>
        public string Detail { get; }
    }

    /// <summary>Raw facts about the target (Preparation) year gathered by the caller — the evaluator stays pure.</summary>
    public sealed class OpeningChecklistFacts
    {
        public int CalendarDayCount { get; set; }

        public int GradeYearProfileCount { get; set; }

        public int SectionCount { get; set; }

        public int FeeStructureLineCount { get; set; }

        public int UnapprovedFeeStructureLineCount { get; set; }

        public int GradingScaleCount { get; set; }

        public bool TimetablePublished { get; set; }

        public bool TimetableExplicitlyDeferred { get; set; }

        public int UndecidedPromotionCount { get; set; }

        public int ConfirmedWithoutSectionCount { get; set; }
    }

    /// <summary>
    /// BR-AYR-004: "Activation requires the opening checklist green: calendar
    /// defined, grades/sections created, fee structures approved, grading scales
    /// confirmed, timetable published or explicitly deferred (permission)" —
    /// plus doc §9's "promotion decision mandatory for every enrolled student
    /// before activation" and §11's "unassigned students count must be zero
    /// after rollover".
    /// </summary>
    public static class OpeningChecklistEvaluator
    {
        public const string Calendar = "CALENDAR_DEFINED";
        public const string Grades = "GRADES_DEFINED";
        public const string Sections = "SECTIONS_CREATED";
        public const string Fees = "FEE_STRUCTURES_APPROVED";
        public const string GradingScales = "GRADING_SCALES_CONFIRMED";
        public const string Timetable = "TIMETABLE_PUBLISHED_OR_DEFERRED";
        public const string Promotions = "PROMOTIONS_DECIDED";
        public const string Assignments = "CONFIRMED_STUDENTS_ASSIGNED";

        public static IReadOnlyList<ChecklistItem> Evaluate(OpeningChecklistFacts f)
        {
            return new[]
            {
                new ChecklistItem(Calendar, f.CalendarDayCount > 0, $"{f.CalendarDayCount} calendar day(s) defined"),
                new ChecklistItem(Grades, f.GradeYearProfileCount > 0, $"{f.GradeYearProfileCount} grade-year profile(s)"),
                new ChecklistItem(Sections, f.SectionCount > 0, $"{f.SectionCount} section(s)"),
                new ChecklistItem(Fees, f.FeeStructureLineCount > 0 && f.UnapprovedFeeStructureLineCount == 0,
                    $"{f.FeeStructureLineCount} fee structure line(s), {f.UnapprovedFeeStructureLineCount} not approved"),
                new ChecklistItem(GradingScales, f.GradingScaleCount > 0, $"{f.GradingScaleCount} grading scale(s)"),
                new ChecklistItem(Timetable, f.TimetablePublished || f.TimetableExplicitlyDeferred,
                    f.TimetablePublished ? "published" : f.TimetableExplicitlyDeferred ? "explicitly deferred" : "neither published nor deferred"),
                new ChecklistItem(Promotions, f.UndecidedPromotionCount == 0, $"{f.UndecidedPromotionCount} student(s) undecided"),
                new ChecklistItem(Assignments, f.ConfirmedWithoutSectionCount == 0, $"{f.ConfirmedWithoutSectionCount} confirmed student(s) without a section"),
            };
        }

        public static bool IsGreen(IEnumerable<ChecklistItem> items) => items.All(i => i.IsSatisfied);
    }

    /// <summary>Raw facts about the source (Closing) year for BR-AYR-005's closing checklist.</summary>
    public sealed class ClosingChecklistFacts
    {
        public int UnresolvedMarksheetCount { get; set; }

        public int OpenWorkflowInstanceCount { get; set; }

        public bool CarryForwardPosted { get; set; }

        public bool CarryForwardReconciled { get; set; }
    }

    /// <summary>
    /// BR-AYR-005: "Closing → Closed requires the closing checklist: all mark
    /// sheets approved or explicitly voided, attendance complete, receivable
    /// balances carried forward, pending workflows resolved." Attendance
    /// completeness has no computable definition in Module 14 yet (no
    /// "expected days" model per section) — deliberately not an item here
    /// rather than a fake always-green one.
    /// </summary>
    public static class ClosingChecklistEvaluator
    {
        public const string Marksheets = "MARKSHEETS_RESOLVED";
        public const string Workflows = "WORKFLOWS_RESOLVED";
        public const string CarryForward = "CARRY_FORWARD_POSTED";
        public const string Reconciled = "CARRY_FORWARD_RECONCILED";

        public static IReadOnlyList<ChecklistItem> Evaluate(ClosingChecklistFacts f)
        {
            return new[]
            {
                new ChecklistItem(Marksheets, f.UnresolvedMarksheetCount == 0, $"{f.UnresolvedMarksheetCount} mark sheet(s) not published"),
                new ChecklistItem(Workflows, f.OpenWorkflowInstanceCount == 0, $"{f.OpenWorkflowInstanceCount} workflow instance(s) still open"),
                new ChecklistItem(CarryForward, f.CarryForwardPosted, f.CarryForwardPosted ? "posted" : "not posted"),
                new ChecklistItem(Reconciled, f.CarryForwardReconciled, f.CarryForwardReconciled ? "reconciled" : "closing receivables ≠ opening balances"),
            };
        }

        public static bool IsGreen(IEnumerable<ChecklistItem> items) => items.All(i => i.IsSatisfied);
    }
}
