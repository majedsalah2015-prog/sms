using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Domain.Discounts;
using Sms.Domain.Fees;
using Sms.Domain.Installments;

namespace Sms.Web.Models
{
    /// <summary>
    /// Why the basket cannot be committed right now. An enum rather than a message so the
    /// sentence is written once at the view boundary in both languages, and so the screen can
    /// still render everything else it knows — a blocked panel explains itself instead of
    /// disappearing, which is the difference between "not yet" and "not for you".
    /// </summary>
    public enum FeeFileBlocker
    {
        None = 0,

        /// <summary>
        /// The year picker is on a year that is not the working year. The engines behind the
        /// basket read the working year and nothing else (BR-AYR-002), so committing here would
        /// silently write into a different year from the one on screen.
        /// </summary>
        NotWorkingYear = 1,

        /// <summary>No live enrollment in the working year, so there is no grade whose price list applies.</summary>
        NotEnrolled = 2,

        /// <summary>No guardian on file to bill — a charge is addressed to a payer (BR-FEE-004).</summary>
        NoGuardian = 3,
    }

    /// <summary>
    /// doc/Modules/19 §8.7, §8.4 and doc/Modules/20 §8.2 / 22 §8.3 gathered onto the student's
    /// own file: tick the items, choose the installment template, choose the discount, see the
    /// gross the ticks add up to, and approve the three together (owner request, 2026-08-31).
    /// <para>
    /// Nothing here is written until the button is pressed — the basket lives in the form. That
    /// is what lets a line be edited or taken off freely beforehand, and it is why the panel
    /// shows a gross rather than a net: the discount and the split are computed by the engines
    /// that own those rules, and a second opinion rendered in JavaScript would be a number the
    /// system does not stand behind.
    /// </para>
    /// </summary>
    public sealed class StudentFeeFilePanel
    {
        /// <summary>One priced item of the grade's approved structure that the student has not been billed for.</summary>
        public sealed record ItemOption(FeeCategory Category, decimal Amount);

        public FeeFileBlocker Blocker { get; set; }

        /// <summary>The working year's label, shown when <see cref="FeeFileBlocker.NotWorkingYear"/> sends the clerk back to it.</summary>
        public int WorkingYearId { get; set; }

        /// <summary>Fees / Charges / Post — without it the panel is not offered at all (BR-SEC-010).</summary>
        public bool CanCommit { get; set; }

        /// <summary>Installments / Assignment / Create. Absent, the template picker is hidden rather than refused after the fact.</summary>
        public bool CanAssignPlan { get; set; }

        /// <summary>Discounts / Grants / Submit + Approve. This screen approves a grant outright, so it demands both.</summary>
        public bool CanGrantDiscount { get; set; }

        /// <summary>Fees / Charges / Deactivate — the edit and remove buttons on a billed item.</summary>
        public bool CanAdjustItems { get; set; }

        public IReadOnlyList<ItemOption> Items { get; set; } = Array.Empty<ItemOption>();

        /// <summary>Every category, for the off-list item — a service the grade's price list does not carry (BR-FEE-003).</summary>
        public IReadOnlyList<FeeCategory> AllCategories { get; set; } = Array.Empty<FeeCategory>();

        /// <summary>Approved templates of the working year. A draft template cannot be assigned (BR-INS-001).</summary>
        public IReadOnlyList<PlanTemplate> Templates { get; set; } = Array.Empty<PlanTemplate>();

        public IReadOnlyList<DiscountType> DiscountTypes { get; set; } = Array.Empty<DiscountType>();

        /// <summary>Guardians who can be billed, the financially responsible one first (BR-PAR-005).</summary>
        public IReadOnlyList<StudentFinanceDetailViewModel.GuardianRow> Payers { get; set; } = Array.Empty<StudentFinanceDetailViewModel.GuardianRow>();

        /// <summary>A student already carrying a plan for the year cannot take a second one — the picker says so rather than offering it (BR-INS-002).</summary>
        public bool HasPlan { get; set; }

        public bool HasDiscount { get; set; }

        public int? DefaultParentId => Payers.FirstOrDefault(p => p.IsFinanciallyResponsible)?.Parent.Id ?? Payers.FirstOrDefault()?.Parent.Id;

        /// <summary>Nothing left to offer: every priced item billed, a plan in place and a discount standing.</summary>
        public bool IsExhausted => Items.Count == 0 && HasPlan && HasDiscount;

        /// <summary>The panel is drawn, but only as an explanation, when something blocks it or the user may not commit.</summary>
        public bool IsActionable => Blocker == FeeFileBlocker.None && CanCommit;
    }
}
