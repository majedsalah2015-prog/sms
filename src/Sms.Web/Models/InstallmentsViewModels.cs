using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Domain.Fees;
using Sms.Domain.Grades;
using Sms.Domain.Installments;
using Sms.Domain.Parents;
using Sms.Domain.Payments;
using Sms.Domain.Schools;
using Sms.Domain.Students;
using Sms.Application.Common.Guards;
using Sms.Application.Installments;

namespace Sms.Web.Models
{
    // ---------------------------------------------------------------- Installment Plans (doc/Modules/20 §8, engine from E-501, screens this pass)

    /// <summary>Bilingual labels for the installment enums (mirrors FinanceLabels' shape/placement rationale).</summary>
    public static class InstallmentLabels
    {
        public static string Status(InstallmentStatus s, bool ar) => s switch
        {
            InstallmentStatus.Scheduled => ar ? "مجدوَل" : "Scheduled",
            InstallmentStatus.Due => ar ? "مستحق" : "Due",
            InstallmentStatus.Overdue => ar ? "متأخر" : "Overdue",
            InstallmentStatus.Paid => ar ? "مسدَّد" : "Paid",
            InstallmentStatus.PartiallyPaid => ar ? "مسدَّد جزئياً" : "Partially paid",
            InstallmentStatus.Rescheduled => ar ? "أُعيدت جدولته" : "Rescheduled",
            InstallmentStatus.WrittenOff => ar ? "مشطوب" : "Written off",
            _ => s.ToString(),
        };

        public static string StatusBadge(InstallmentStatus s) => s switch
        {
            InstallmentStatus.Paid => "text-bg-success",
            InstallmentStatus.Overdue => "text-bg-danger",
            InstallmentStatus.Due => "text-bg-warning",
            InstallmentStatus.PartiallyPaid => "text-bg-info",
            InstallmentStatus.Scheduled => "text-bg-light border",
            _ => "text-bg-secondary",
        };

        /// <summary>doc §8.2 grade-wide run — what happened to one student, or would.</summary>
        public static string GradeOutcome(GradeAssignmentOutcome o, bool ar) => o switch
        {
            GradeAssignmentOutcome.Ready => ar ? "سيُسنَد" : "Will be scheduled",
            GradeAssignmentOutcome.Assigned => ar ? "أُسنِد" : "Scheduled",
            GradeAssignmentOutcome.AlreadyPlanned => ar ? "له خطة بالفعل" : "Already has a plan",
            GradeAssignmentOutcome.NoMandatoryCharges => ar ? "لا رسوم إلزامية مرحّلة" : "No posted mandatory charges",
            GradeAssignmentOutcome.PayerSplit => ar ? "رسومه على أكثر من دافع" : "Billed to more than one payer",
            _ => o.ToString(),
        };

        /// <summary>Why a student was skipped — the sentence that tells the officer what to do about it.</summary>
        public static string GradeOutcomeHint(GradeAssignmentOutcome o, bool ar) => o switch
        {
            GradeAssignmentOutcome.AlreadyPlanned => ar
                ? "خطته القائمة لا تُمَس — افتحها وأعد جدولتها إن لزم."
                : "The existing plan is left untouched — open it and reschedule if it needs changing.",
            GradeAssignmentOutcome.NoMandatoryCharges => ar
                ? "رحّل رسومه الإلزامية من شاشة الرسوم أولاً، أو أن إشعارات دائنة أسقطتها بالكامل."
                : "Post their mandatory fees from the Fees screen first — or credit notes have taken them all off.",
            GradeAssignmentOutcome.PayerSplit => ar
                ? "الجدول موجَّه لدافع واحد، ورسومه الإلزامية موزَّعة على أكثر من دافع — أسنده من لوحة الطالب الواحد."
                : "A schedule is addressed to one payer and this student's mandatory charges span several — assign them from the single-student panel.",
            _ => string.Empty,
        };

        public static string GradeOutcomeBadge(GradeAssignmentOutcome o) => o switch
        {
            GradeAssignmentOutcome.Assigned => "text-bg-success",
            GradeAssignmentOutcome.Ready => "text-bg-primary",
            GradeAssignmentOutcome.AlreadyPlanned => "text-bg-light border",
            _ => "text-bg-warning",
        };

        public static string TemplateStatus(PlanTemplateStatus s, bool ar) => s switch
        {
            PlanTemplateStatus.Draft => ar ? "مسودة" : "Draft",
            PlanTemplateStatus.Approved => ar ? "معتمد" : "Approved",
            _ => s.ToString(),
        };

        public static string RescheduleStatus(RescheduleCaseStatus s, bool ar) => s switch
        {
            RescheduleCaseStatus.Proposed => ar ? "مقترح" : "Proposed",
            RescheduleCaseStatus.Approved => ar ? "معتمد" : "Approved",
            RescheduleCaseStatus.Rejected => ar ? "مرفوض" : "Rejected",
            _ => s.ToString(),
        };

        public static string PromiseStatus(PromiseStatus s, bool ar) => s switch
        {
            Domain.Installments.PromiseStatus.Open => ar ? "قائم" : "Open",
            Domain.Installments.PromiseStatus.Kept => ar ? "أُوفي به" : "Kept",
            Domain.Installments.PromiseStatus.Broken => ar ? "أُخلف" : "Broken",
            _ => s.ToString(),
        };

        public static string DunningStep(DunningStep s, bool ar) => s switch
        {
            Domain.Installments.DunningStep.ReminderD7 => ar ? "تذكير: قبل 7 أيام" : "Reminder D-7",
            Domain.Installments.DunningStep.ReminderD1 => ar ? "تذكير: قبل يوم" : "Reminder D-1",
            Domain.Installments.DunningStep.Overdue3 => ar ? "متأخر +3" : "Overdue +3",
            Domain.Installments.DunningStep.Overdue14 => ar ? "متأخر +14" : "Overdue +14",
            Domain.Installments.DunningStep.Overdue30 => ar ? "متأخر +30" : "Overdue +30",
            Domain.Installments.DunningStep.PortalBanner => ar ? "لافتة البوابة" : "Portal banner",
            Domain.Installments.DunningStep.StatementLetter => ar ? "خطاب كشف حساب" : "Statement letter",
            Domain.Installments.DunningStep.Escalation => ar ? "تصعيد للإدارة" : "Escalation",
            _ => s.ToString(),
        };
    }

    /// <summary>Year picker shared by the Templates/Assign screens (mirrors FinancePageViewModel).</summary>
    public abstract class InstallmentsPageViewModel
    {
        public IReadOnlyList<AcademicYear> Years { get; set; } = Array.Empty<AcademicYear>();

        public AcademicYear? Year { get; set; }
    }

    // ---- §8.1 Template designer ----

    public sealed class TemplatesViewModel : InstallmentsPageViewModel
    {
        public sealed record Row(PlanTemplate Template, FeeCategory? Category, int AssignmentCount, UsageReport Usage)
        {
            /// <summary>Draft only: an approved template may already have produced schedules, and a schedule is a copy of the shape taken at assignment.</summary>
            public bool CanEdit => Template.Status == PlanTemplateStatus.Draft;

            /// <summary>Nothing may have been assigned from it. Asked before the button is drawn, so a delete that cannot work is never offered.</summary>
            public bool CanDelete => !Usage.IsInUse;
        }

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        public IReadOnlyList<FeeCategory> Categories { get; set; } = Array.Empty<FeeCategory>();
    }

    // ---- §8.1 drill-down: one template in full ----

    public sealed class TemplateDetailViewModel
    {
        public PlanTemplate Template { get; set; } = null!;

        public AcademicYear? Year { get; set; }

        public FeeCategory? Category { get; set; }

        /// <summary>
        /// One split, with the date its rule resolves to for this year. An offset
        /// rule is meaningless on its own — "+120 days" says nothing until it is
        /// counted from the year's start — so the screen resolves it rather than
        /// leaving the reader to.
        /// </summary>
        public sealed record SplitRow(TemplateInstallment Split, DateTime? ResolvedDueDate);

        public IReadOnlyList<SplitRow> Splits { get; set; } = Array.Empty<SplitRow>();

        public sealed record UsageRow(PlanAssignment Assignment, Student Student, PayerCard? Payer, int InstallmentCount);

        public IReadOnlyList<UsageRow> Usage { get; set; } = Array.Empty<UsageRow>();

        /// <summary>What a plan of this shape looks like against a round number, so the percentages read as money.</summary>
        public decimal PreviewBase { get; set; } = 10000m;
    }

    // ---- §8.2 Assignment console ----

    public sealed class AssignViewModel : InstallmentsPageViewModel
    {
        public sealed record StudentOption(Student Student, GradeLevel? Grade);

        public sealed record PayerOption(Parent Parent, bool IsFinanciallyResponsible, Payer? Payer);

        public sealed record ExistingRow(PlanAssignment Assignment, PlanTemplate Template);

        public IReadOnlyList<StudentOption> Students { get; set; } = Array.Empty<StudentOption>();

        public Student? Selected { get; set; }

        public IReadOnlyList<PayerOption> Payers { get; set; } = Array.Empty<PayerOption>();

        public IReadOnlyList<PlanTemplate> ApprovedTemplates { get; set; } = Array.Empty<PlanTemplate>();

        public IReadOnlyList<ExistingRow> Existing { get; set; } = Array.Empty<ExistingRow>();

        // ---- filter state: a school runs thousands of enrolments, and one flat picker of them is not a screen ----

        /// <summary>Free text over student number and name (both languages).</summary>
        public string? Q { get; set; }

        /// <summary>Free text over the paying family — parent name, file number, mobile.</summary>
        public string? PayerQ { get; set; }

        public int? GradeId { get; set; }

        /// <summary>Grades that actually run in the selected year, so the filter never offers an empty answer.</summary>
        public IReadOnlyList<GradeLevel> Grades { get; set; } = Array.Empty<GradeLevel>();

        /// <summary>
        /// One family the payer filter matched. The guardian is the identity, not the
        /// <see cref="Payments.Payer"/> row — that row is only created when money first moves, so a
        /// filter keyed on it would be blind to every family that has not paid yet, which on a fresh
        /// year is all of them.
        /// </summary>
        public sealed record FamilyMatch(Parent Parent, Payer? Payer, int ChildCount)
        {
            public string Label(bool ar) => $"{FinanceLabels.ParentName(Parent, ar)} · {Parent.ParentFileNo}";
        }

        /// <summary>Families the payer filter matched — shown so the officer can see what narrowed the list.</summary>
        public IReadOnlyList<FamilyMatch> PayerMatches { get; set; } = Array.Empty<FamilyMatch>();

        /// <summary>
        /// The payer the filter points at, preselected in the assign form. Choosing a student
        /// after searching for a family and then billing a different guardian is precisely the
        /// mistake the assignment console exists to prevent.
        /// </summary>
        public int? PreferredPayerId { get; set; }

        /// <summary>How many enrolled students match the filter, before the picker cap.</summary>
        public int MatchCount { get; set; }

        /// <summary>True when the picker is showing less than the filter matched — stated, never silent.</summary>
        public bool Truncated => MatchCount > Students.Count;

        public bool HasFilter => !string.IsNullOrWhiteSpace(Q) || !string.IsNullOrWhiteSpace(PayerQ) || GradeId != null;

        // ---- doc §8.2 "defaults per grade": one template across a whole grade, mandatory fees only ----

        /// <summary>
        /// One student in the grade-wide preview, named for the screen. The payer is carried
        /// because the schedule is addressed to them — every dunning message and every statement
        /// goes to that person, and a grade-wide run is precisely where nobody checks each one.
        /// </summary>
        public sealed record GradeRunRow(Student Student, GradeAssignmentLine Line, Parent? Payer);

        /// <summary>The grade the bulk card is aimed at — its own picker, so narrowing the list above does not silently retarget a 30-schedule run.</summary>
        public int? BulkGradeId { get; set; }

        public int? BulkTemplateId { get; set; }

        /// <summary>True once a grade and an approved template have both been chosen and the preview was computed.</summary>
        public bool HasBulkPreview { get; set; }

        /// <summary>A refusal the preview itself raised — an unapproved or non-mandatory template — already translated.</summary>
        public string? BulkError { get; set; }

        /// <summary>
        /// The engine assigns in the working year, whatever year the filter above shows. A
        /// grade-wide run is thirty schedules, so the card refuses to appear while those two
        /// disagree rather than writing them into a year the officer is not looking at.
        /// </summary>
        public bool BulkAvailable { get; set; }

        public IReadOnlyList<GradeRunRow> BulkRows { get; set; } = Array.Empty<GradeRunRow>();

        public int BulkCount(GradeAssignmentOutcome outcome) => BulkRows.Count(r => r.Line.Outcome == outcome);

        /// <summary>What the whole grade would be put on a schedule for — stated before the run, not after it.</summary>
        public decimal BulkReadyTotal => BulkRows
            .Where(r => r.Line.Outcome == GradeAssignmentOutcome.Ready)
            .Sum(r => r.Line.MandatoryTotal);
    }

    // ---- §8.3 Family schedule view (the collection officer's main screen) ----

    public sealed class FamilyScheduleViewModel
    {
        public sealed record Row(InstallmentView View, PlanAssignment Assignment, PlanTemplate Template, Student Student, bool HasOpenPromise, Pdc? CoveringPdc);

        public IReadOnlyList<PayerCard> Matches { get; set; } = Array.Empty<PayerCard>();

        public string? Q { get; set; }

        public PayerCard? Selected { get; set; }

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        public IReadOnlyList<Pdc> LivePdcs { get; set; } = Array.Empty<Pdc>();

        public decimal TotalOutstanding => Rows
            .Where(r => r.View.Status is InstallmentStatus.Due or InstallmentStatus.Overdue or InstallmentStatus.PartiallyPaid)
            .Sum(r => r.View.Amount - r.View.Paid);
    }

    // ---- §8.3 drill-down: a single plan assignment's full schedule ----

    public sealed class ScheduleDetailViewModel
    {
        public PlanAssignment Assignment { get; set; } = null!;

        public PlanTemplate Template { get; set; } = null!;

        public Student Student { get; set; } = null!;

        public PayerCard? Payer { get; set; }

        public sealed record Row(InstallmentView View, PromiseToPay? OpenPromise, Pdc? CoveringPdc);

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        public IReadOnlyList<Pdc> LivePdcs { get; set; } = Array.Empty<Pdc>();

        public IReadOnlyList<RescheduleCase> RescheduleCases { get; set; } = Array.Empty<RescheduleCase>();

        public decimal UnpaidRemainder => Rows
            .Where(r => r.View.Status is not (InstallmentStatus.Paid or InstallmentStatus.Rescheduled or InstallmentStatus.WrittenOff))
            .Sum(r => r.View.Amount - r.View.Paid);
    }

    // ---- §8.4 Reschedule wizard ----

    public sealed class RescheduleWizardViewModel
    {
        public PlanAssignment Assignment { get; set; } = null!;

        public PlanTemplate Template { get; set; } = null!;

        public Student Student { get; set; } = null!;

        public PayerCard? Payer { get; set; }

        public IReadOnlyList<InstallmentView> Unpaid { get; set; } = Array.Empty<InstallmentView>();

        public decimal Remainder => Unpaid.Sum(i => i.Amount - i.Paid);

        public int MaxExtensionMonths { get; set; } = 3;
    }

    public sealed class RescheduleCasesViewModel
    {
        public sealed record Row(RescheduleCase Case, PlanAssignment Assignment, Student Student, PayerCard? Payer);

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        public RescheduleCaseStatus? Filter { get; set; }
    }

    // ---- §8.5 Dunning console ----

    public sealed class DunningConsoleViewModel
    {
        public sealed record EventRow(DunningEvent Event, Installment Installment, PlanAssignment Assignment, Student Student, PayerCard? Payer);

        public IReadOnlyList<EventRow> Recent { get; set; } = Array.Empty<EventRow>();

        public sealed record BrokenPromiseRow(PromiseToPay Promise, Installment Installment, Student Student, PayerCard? Payer);

        public IReadOnlyList<BrokenPromiseRow> BrokenPromises { get; set; } = Array.Empty<BrokenPromiseRow>();

        public sealed record EscalationRow(Installment Installment, InstallmentView View, Student Student, PayerCard? Payer);

        public IReadOnlyList<EscalationRow> Escalations { get; set; } = Array.Empty<EscalationRow>();

        public int? LastRunFiredCount { get; set; }
    }

    // ---- shared per-row action forms (Family + Schedule screens) ----

    public sealed class InstallmentActionsViewModel
    {
        public int InstallmentId { get; set; }

        public InstallmentStatus Status { get; set; }

        public bool IsPdcCovered { get; set; }

        public IReadOnlyList<Pdc> LivePdcs { get; set; } = Array.Empty<Pdc>();

        public string ReturnUrl { get; set; } = "";
    }
}
