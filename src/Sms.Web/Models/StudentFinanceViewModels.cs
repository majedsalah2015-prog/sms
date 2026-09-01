using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Application.Installments;
using Sms.Application.ReadModels;
using Sms.Application.Statements;
using Sms.Domain.Discounts;
using Sms.Domain.Fees;
using Sms.Domain.Grades;
using Sms.Domain.Installments;
using Sms.Domain.Parents;
using Sms.Domain.Sections;
using Sms.Domain.Students;

namespace Sms.Web.Models
{
    // ------------------------------------------------- Student finance (doc/Modules/19 §8.7, student side)
    //
    // The payer half of §8.7 lives in FinanceViewModels.cs. This half asks the same question of one
    // child, because that is the name that arrives at the counter: a mother gives her daughter's
    // name, not her own file number, and until now the clerk had to find the guardian first.
    //
    // Every money figure on these models is BR-FEE-008's one subtraction — gross − credit notes −
    // discounts − allocations — and BR-DIS-010 keeps the three subtrahends visible as their own
    // columns rather than folding them into a single "net" the family cannot check.

    /// <summary>doc/Modules/19 §8.7 (student side), P-LIST: the roll with its money attached, filtered by grade, section, name and guardian.</summary>
    public sealed class StudentFinanceListViewModel : FinancePageViewModel
    {
        /// <summary>
        /// One student's year on one line. <paramref name="PayerId"/> is the payer of the guardian
        /// the school actually holds responsible (BR-FEE-004/BR-PAR-005) and is what the Pay button
        /// aims at — null when nobody has been made responsible, which disables the button rather
        /// than sending a cashier to a screen that cannot take money.
        /// </summary>
        public sealed record Row(
            Student Student,
            string? GradeName,
            string? SectionName,
            Parent? Guardian,
            bool GuardianIsResponsible,
            int? PayerId,
            decimal Gross,
            decimal Discounts,
            decimal CreditNotes,
            decimal Paid,
            bool HasDiscount,
            ScheduledPositionSplitter.ScheduledPosition? Position = null)
        {
            /// <summary>BR-DIS-010: what is actually owed, with the reductions still shown separately beside it.</summary>
            public decimal Net => Gross - Discounts - CreditNotes;

            public decimal Remaining => Net - Paid;

            public bool IsSettled => Remaining <= 0m;

            /// <summary>
            /// What the counter may actually ask for today (BR-INS-007 dates over BR-FEE-008's
            /// balance). Equal to <see cref="Remaining"/> when no schedule stands over it, which
            /// is what makes this safe to read as the collection figure for every row.
            /// </summary>
            public decimal DueNow => Position?.DueNow ?? Remaining;

            /// <summary>True when a plan is holding part of this balance back from today's claim.</summary>
            public bool HasDeferred => Position?.DefersAnything ?? false;
        }

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        public string? Q { get; set; }

        public int? GradeId { get; set; }

        public int? SectionId { get; set; }

        public string? Guardian { get; set; }

        /// <summary>Narrows the roll to the students who still owe something — the collection worklist.</summary>
        public bool DueOnly { get; set; }

        public IReadOnlyList<GradeLevel> Grades { get; set; } = Array.Empty<GradeLevel>();

        /// <summary>Sections of the chosen grade only; a school's full section list is meaningless as a flat picker.</summary>
        public IReadOnlyList<Section> Sections { get; set; } = Array.Empty<Section>();

        /// <summary>Enrolled students matching the filters, before the display cap.</summary>
        public int MatchCount { get; set; }

        /// <summary>True when <see cref="MatchCount"/> exceeded the cap and the grid is showing a prefix of it.</summary>
        public bool IsTruncated { get; set; }

        /// <summary>BR-SEC-010: the buttons a user may not use are not rendered disabled, they are not rendered.</summary>
        public bool CanOpenCashier { get; set; }

        public bool CanPrintStatement { get; set; }

        public decimal TotalRemaining => Rows.Sum(r => r.Remaining);

        /// <summary>The listed students' collectible total — what a chase today could actually recover.</summary>
        public decimal TotalDueNow => Rows.Sum(r => r.DueNow);

        /// <summary>True when any listed student is on a plan that defers part of their balance — the column is pointless otherwise.</summary>
        public bool AnyDeferred => Rows.Any(r => r.HasDeferred);

        public decimal TotalNet => Rows.Sum(r => r.Net);

        public decimal TotalDiscounts => Rows.Sum(r => r.Discounts);
    }

    /// <summary>
    /// doc/Modules/19 §8.7 (student side), P-DETAIL: what the fee is made of for this child —
    /// the price list its grade was billed from, the invoices that came out of it, how those
    /// invoices are spread over installments (Module 20), and every discount standing against
    /// them (Module 22).
    /// </summary>
    public sealed class StudentFinanceDetailViewModel : FinancePageViewModel
    {
        /// <summary>
        /// One line of the grade's approved price list beside what the student was actually billed
        /// for it. The two disagreeing is the point of the row: a priced category with no charge is
        /// revenue leakage (doc/Modules/19 §10 "expected vs posted"), and a charge with no line is a
        /// manual or service charge, which the source column names.
        /// </summary>
        public sealed record StructureRow(FeeStructureLine Line, FeeCategory Category, decimal Charged)
        {
            public decimal Expected => Line.Amount;

            public bool IsCharged => Charged > 0m;

            public decimal Variance => Charged - Expected;
        }

        /// <summary>A discount as the family sees it: which type, on what basis, approved by whom, and what it has actually taken off so far.</summary>
        public sealed record DiscountRow(DiscountGrant Grant, DiscountType? Type, decimal Applied, int DocumentCount);

        /// <summary>
        /// One installment of one plan, with the share of it that the allocations have already
        /// covered and the status <see cref="Sms.Application.Installments.InstallmentStatusDeriver"/>
        /// derives from that — status is not stored, and re-deriving it in a view would be a second
        /// opinion the dunning console does not share.
        /// </summary>
        public sealed record InstallmentRow(Installment Installment, decimal Covered, InstallmentStatus Status)
        {
            public decimal Outstanding => Math.Max(0m, Installment.Amount - Covered);
        }

        public sealed record PlanRow(PlanAssignment Assignment, PlanTemplate? Template, FeeCategory? Category, IReadOnlyList<InstallmentRow> Installments)
        {
            public decimal Total => Installments.Sum(i => i.Installment.Amount);
        }

        /// <summary>A guardian of this student and whether the school bills them (BR-PAR-005 assigns responsibility per child, so siblings can differ).</summary>
        public sealed record GuardianRow(Parent Parent, string RelationshipAr, string RelationshipEn, bool IsFinanciallyResponsible, int? PayerId)
        {
            public string Relationship(bool ar) => ar ? RelationshipAr : RelationshipEn;
        }

        public Student Student { get; set; } = null!;

        public string? GradeName { get; set; }

        public string? SectionName { get; set; }

        public IReadOnlyList<GuardianRow> Guardians { get; set; } = Array.Empty<GuardianRow>();

        /// <summary>The payer the Pay button aims at — the responsible guardian's, or null when none is set.</summary>
        public int? PayerId => Guardians.FirstOrDefault(g => g.IsFinanciallyResponsible)?.PayerId;

        public IReadOnlyList<StructureRow> Structure { get; set; } = Array.Empty<StructureRow>();

        /// <summary>Posted charges of the chosen year, each already carrying its own subtractions.</summary>
        public IReadOnlyList<OpenChargeRow> Charges { get; set; } = Array.Empty<OpenChargeRow>();

        public IReadOnlyList<DiscountRow> Discounts { get; set; } = Array.Empty<DiscountRow>();

        public IReadOnlyList<PlanRow> Plans { get; set; } = Array.Empty<PlanRow>();

        /// <summary>True when the student's grade-year profile has no approved price list — the reason an empty breakdown is empty.</summary>
        public bool HasNoStructure { get; set; }

        /// <summary>True when the student is not enrolled in the chosen year at all.</summary>
        public bool NotEnrolled { get; set; }

        public bool CanOpenCashier { get; set; }

        public bool CanPrintStatement { get; set; }

        /// <summary>
        /// The basket that turns this screen from a report into the place the fee file is
        /// actually built (owner request, 2026-08-31). Null when the reader may not post
        /// charges at all — the panel is then not rendered rather than rendered disabled.
        /// </summary>
        public StudentFeeFilePanel? FeeFile { get; set; }

        public decimal ExpectedTotal => Structure.Sum(r => r.Expected);

        public decimal Gross => Charges.Sum(r => r.Charge.GrossAmount);

        public decimal Discounted => Charges.Sum(r => r.Discounted);

        public decimal Credited => Charges.Sum(r => r.Credited);

        public decimal Allocated => Charges.Sum(r => r.Allocated);

        public decimal Remaining => Charges.Sum(r => r.Remaining);

        /// <summary>
        /// <see cref="Remaining"/> cut along the schedule's dates
        /// (<see cref="ScheduledPositionSplitter"/>): what the counter may ask for today against
        /// what the plan has moved into the future. Without a plan the whole balance is due now,
        /// which is the same statement this screen made before plans were reflected at all.
        /// </summary>
        public ScheduledPositionSplitter.ScheduledPosition Position { get; set; } = new(0m, 0m, 0m, 0m);

        /// <summary>Charges of this year that no installment line claims — payable on demand rather than on a schedule.</summary>
        public decimal UnscheduledTotal => Position.Unscheduled;

        /// <summary>
        /// One entry per plan whose template this reader may change, matched to its schedule by
        /// <see cref="PlanTemplateChangePanel.PlanAssignmentId"/>. Empty when nobody may do it
        /// here — no plan, or a reader without both halves of the right (BR-SEC-010).
        /// </summary>
        public IReadOnlyList<PlanTemplateChangePanel> PlanChanges { get; set; } = Array.Empty<PlanTemplateChangePanel>();

        /// <summary>The change panel for one plan row, or null when that plan is not changeable here.</summary>
        public PlanTemplateChangePanel? PlanChangeFor(int planAssignmentId)
            => PlanChanges.FirstOrDefault(p => p.PlanAssignmentId == planAssignmentId);
    }

    /// <summary>
    /// doc/Modules/20 §8.2 exception assignment reached from the child's own file: swap the
    /// template a live plan is on and let BR-INS-003's controlled recomputation re-date the unpaid
    /// remainder (owner request, 2026-09-01).
    /// <para>
    /// Separate from <see cref="StudentFeeFilePanel"/> on purpose. That panel is the basket, and
    /// it exists only for someone who may post charges; this is a different act, held by a
    /// different pair of permissions, and a Finance Manager who never posts charges must still be
    /// able to perform it.
    /// </para>
    /// </summary>
    public sealed class PlanTemplateChangePanel
    {
        /// <summary>One template this plan could move to, named the way the picker names it.</summary>
        public sealed record Option(PlanTemplate Template, int InstallmentCount);

        /// <summary>The plan being reshaped. One per fee-category group, so the panel is per plan row.</summary>
        public int PlanAssignmentId { get; set; }

        /// <summary>The template it is on now — shown so the choice is a change from something stated.</summary>
        public PlanTemplate? Current { get; set; }

        /// <summary>
        /// Approved templates of the year that could carry this plan: the same fee-category group
        /// or none at all, minus the one it is already on (BR-INS-001, BR-INS-002).
        /// </summary>
        public IReadOnlyList<Option> Options { get; set; } = Array.Empty<Option>();

        /// <summary>What is still owed on the schedule — the only amount a change re-dates (BR-INS-005).</summary>
        public decimal Remainder { get; set; }

        /// <summary>Nothing left unpaid: the panel explains that instead of offering a picker it would refuse.</summary>
        public bool IsFullyCollected => Remainder <= 0m;
    }

    /// <summary>doc/Modules/19 §8.7 + BR-DIS-010, P-STMT: the printable statement of one child's account.</summary>
    public sealed class StudentStatementViewModel
    {
        public Student Student { get; set; } = null!;

        public string? GradeName { get; set; }

        public string? SectionName { get; set; }

        public IReadOnlyList<Parent> Guardians { get; set; } = Array.Empty<Parent>();

        public PayerStatement Statement { get; set; } = new();

        public DateTime? AsOf { get; set; }

        public DateTime PrintedAtUtc { get; set; }

        public IReadOnlyList<OpenChargeRow> OpenCharges { get; set; } = Array.Empty<OpenChargeRow>();

        public Dictionary<AgingBucket, decimal> Aging { get; set; } = new();

        public string SchoolNameAr { get; set; } = "";

        public string SchoolNameEn { get; set; } = "";

        public string? SchoolAddress { get; set; }

        public decimal OpenTotal => OpenCharges.Sum(r => r.Remaining);
    }
}
