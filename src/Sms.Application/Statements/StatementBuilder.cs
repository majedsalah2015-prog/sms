using System;
using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Statements
{
    public enum StatementLineKind : short
    {
        Charge = 1,
        CreditNote = 2,
        Discount = 3,
        Payment = 4,
        Refund = 5,
    }

    /// <summary>One document on the payer's statement: debits raise what is owed, credits lower it.</summary>
    public sealed record StatementLine(DateTime DateUtc, StatementLineKind Kind, string DocumentNo, string Description, decimal Debit, decimal Credit)
    {
        public decimal RunningBalance { get; init; }
    }

    /// <summary>BR-DIS-010: gross charges, discounts and net are always shown as separate figures — never netted invisibly.</summary>
    public sealed class PayerStatement
    {
        public IReadOnlyList<StatementLine> Lines { get; init; } = Array.Empty<StatementLine>();

        public decimal GrossCharges { get; init; }

        public decimal Discounts { get; init; }

        public decimal CreditNotes { get; init; }

        public decimal Payments { get; init; }

        public decimal Refunds { get; init; }

        /// <summary>Gross − discounts − credit notes (what the payer actually owes before payments).</summary>
        public decimal NetCharges => GrossCharges - Discounts - CreditNotes;

        /// <summary>Net charges − payments + refunds = closing balance (positive = owes).</summary>
        public decimal ClosingBalance => NetCharges - Payments + Refunds;
    }

    /// <summary>Pure statement-of-account assembly (doc/Modules/19 §8.7 + BR-DIS-010): sorts by date, computes running balance and the separated totals.</summary>
    public static class StatementBuilder
    {
        public static PayerStatement Build(IEnumerable<StatementLine> lines, DateTime? asOfUtc = null)
        {
            var ordered = lines
                .Where(l => asOfUtc == null || l.DateUtc <= asOfUtc.Value)
                .OrderBy(l => l.DateUtc).ThenBy(l => l.Kind).ThenBy(l => l.DocumentNo)
                .ToList();

            var running = 0m;
            var withBalance = new List<StatementLine>(ordered.Count);
            foreach (var line in ordered)
            {
                running += line.Debit - line.Credit;
                withBalance.Add(line with { RunningBalance = running });
            }

            return new PayerStatement
            {
                Lines = withBalance,
                GrossCharges = ordered.Where(l => l.Kind == StatementLineKind.Charge).Sum(l => l.Debit),
                Discounts = ordered.Where(l => l.Kind == StatementLineKind.Discount).Sum(l => l.Credit),
                CreditNotes = ordered.Where(l => l.Kind == StatementLineKind.CreditNote).Sum(l => l.Credit),
                Payments = ordered.Where(l => l.Kind == StatementLineKind.Payment).Sum(l => l.Credit),
                Refunds = ordered.Where(l => l.Kind == StatementLineKind.Refund).Sum(l => l.Debit),
            };
        }
    }
}
