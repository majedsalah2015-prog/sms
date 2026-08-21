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
        public sealed record Row(PlanTemplate Template, FeeCategory? Category, int AssignmentCount);

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        public IReadOnlyList<FeeCategory> Categories { get; set; } = Array.Empty<FeeCategory>();
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
