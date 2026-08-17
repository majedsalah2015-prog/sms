using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Sms.Application.GlExport
{
    /// <summary>The fixed journal keys of the O3 mapping table. Revenue keys are per FeeCategory (its GlExportCode).</summary>
    public static class GlAccountKeys
    {
        public const string Receivables = "Receivables";
        public const string VatOutput = "VatOutput";
        public const string Discounts = "Discounts";
        public const string AdvancesReceived = "AdvancesReceived";

        public static string Cash(string paymentMethod) => $"Cash:{paymentMethod}";

        public static string Revenue(int feeCategoryId, string? glExportCode) => string.IsNullOrWhiteSpace(glExportCode) ? $"Revenue:{feeCategoryId}" : glExportCode!;
    }

    /// <summary>
    /// Pure journal-summary composition (E-503, O3 assumption): every
    /// Module 19/21/22 document of the period folds into balanced summary
    /// lines. Postings:
    ///   Charge         Dr Receivables (gross)   / Cr Revenue[category] (net), Cr VatOutput (vat)
    ///   Credit note    Dr Revenue[category] (net part), Dr VatOutput (vat part) / Cr Receivables (gross)
    ///   Discount doc   Dr Discounts (contra revenue, gross) / Cr Receivables
    ///   Receipt        Dr Cash[method] (amount) / Cr Receivables (allocated), Cr AdvancesReceived (unallocated)
    ///   Refund paid    Dr AdvancesReceived / Cr Cash[method]
    /// Balanced by construction; the builder asserts it anyway.
    /// </summary>
    public static class JournalSummaryBuilder
    {
        public sealed record ChargeDoc(int FeeCategoryId, string? GlExportCode, decimal NetAmount, decimal VatAmount, decimal GrossAmount);

        public sealed record CreditNoteDoc(int FeeCategoryId, string? GlExportCode, decimal Amount, decimal VatRate);

        public sealed record DiscountDoc(decimal Amount);

        public sealed record ReceiptDoc(string PaymentMethod, decimal Amount, decimal AllocatedAmount);

        public sealed record RefundDoc(string PaymentMethod, decimal Amount);

        public sealed record JournalLine(string AccountKey, string Description, decimal Debit, decimal Credit, int SourceDocumentCount);

        public sealed class Journal
        {
            public IReadOnlyList<JournalLine> Lines { get; init; } = Array.Empty<JournalLine>();

            public decimal TotalDebit => Lines.Sum(l => l.Debit);

            public decimal TotalCredit => Lines.Sum(l => l.Credit);

            public int SourceDocumentCount { get; init; }

            public bool IsBalanced => TotalDebit == TotalCredit;
        }

        public static Journal Build(
            IReadOnlyCollection<ChargeDoc> charges, IReadOnlyCollection<CreditNoteDoc> creditNotes, IReadOnlyCollection<DiscountDoc> discounts,
            IReadOnlyCollection<ReceiptDoc> receipts, IReadOnlyCollection<RefundDoc> refunds)
        {
            var acc = new Accumulator();

            foreach (var c in charges)
            {
                acc.Debit(GlAccountKeys.Receivables, "Charges posted", c.GrossAmount);
                acc.Credit(GlAccountKeys.Revenue(c.FeeCategoryId, c.GlExportCode), "Fee revenue", c.NetAmount);
                acc.Credit(GlAccountKeys.VatOutput, "VAT on charges", c.VatAmount);
            }

            foreach (var n in creditNotes)
            {
                var net = Math.Round(n.Amount / (1m + n.VatRate), 2, MidpointRounding.AwayFromZero);
                var vat = n.Amount - net;
                acc.Debit(GlAccountKeys.Revenue(n.FeeCategoryId, n.GlExportCode), "Credit notes", net);
                acc.Debit(GlAccountKeys.VatOutput, "VAT on credit notes", vat);
                acc.Credit(GlAccountKeys.Receivables, "Credit notes", n.Amount);
            }

            foreach (var d in discounts)
            {
                acc.Debit(GlAccountKeys.Discounts, "Discounts granted", d.Amount);
                acc.Credit(GlAccountKeys.Receivables, "Discounts granted", d.Amount);
            }

            foreach (var r in receipts)
            {
                acc.Debit(GlAccountKeys.Cash(r.PaymentMethod), $"Receipts ({r.PaymentMethod})", r.Amount);
                acc.Credit(GlAccountKeys.Receivables, "Receipts allocated", r.AllocatedAmount);
                acc.Credit(GlAccountKeys.AdvancesReceived, "Receipts unallocated (advances)", r.Amount - r.AllocatedAmount);
            }

            foreach (var f in refunds)
            {
                acc.Debit(GlAccountKeys.AdvancesReceived, "Refunds paid", f.Amount);
                acc.Credit(GlAccountKeys.Cash(f.PaymentMethod), $"Refunds paid ({f.PaymentMethod})", f.Amount);
            }

            var journal = new Journal
            {
                Lines = acc.ToLines(),
                SourceDocumentCount = charges.Count + creditNotes.Count + discounts.Count + receipts.Count + refunds.Count,
            };
            if (!journal.IsBalanced)
            {
                throw new InvalidOperationException($"Journal does not balance: Dr {journal.TotalDebit} vs Cr {journal.TotalCredit}.");
            }

            return journal;
        }

        private sealed class Accumulator
        {
            private readonly Dictionary<(string Key, string Description, bool IsDebit), (decimal Amount, int Count)> _cells = new();

            public void Debit(string key, string description, decimal amount) => Add(key, description, true, amount);

            public void Credit(string key, string description, decimal amount) => Add(key, description, false, amount);

            private void Add(string key, string description, bool isDebit, decimal amount)
            {
                if (amount == 0m)
                {
                    return;
                }

                var cellKey = (key, description, isDebit);
                var current = _cells.TryGetValue(cellKey, out var c) ? c : (0m, 0);
                _cells[cellKey] = (current.Item1 + amount, current.Item2 + 1);
            }

            public IReadOnlyList<JournalLine> ToLines() => _cells
                .OrderBy(kv => kv.Key.Key, StringComparer.Ordinal).ThenByDescending(kv => kv.Key.IsDebit).ThenBy(kv => kv.Key.Description, StringComparer.Ordinal)
                .Select(kv => new JournalLine(kv.Key.Key, kv.Key.Description, kv.Key.IsDebit ? kv.Value.Amount : 0m, kv.Key.IsDebit ? 0m : kv.Value.Amount, kv.Value.Count))
                .ToList();
        }
    }

    /// <summary>Pure CSV rendering of a batch — culture-invariant, CRLF, quoted text — the O3 "generic CSV" deliverable.</summary>
    public static class CsvJournalWriter
    {
        public sealed record Row(int Sequence, string AccountCode, string AccountKey, string Description, decimal Debit, decimal Credit, int SourceDocumentCount);

        public static string Render(string batchNo, DateTime periodFromUtc, DateTime periodToUtc, IEnumerable<Row> rows)
        {
            var sb = new StringBuilder();
            sb.Append("BatchNo,PeriodFrom,PeriodTo,Seq,AccountCode,AccountKey,Description,Debit,Credit,SourceDocs\r\n");
            foreach (var r in rows)
            {
                sb.Append(Quote(batchNo)).Append(',')
                  .Append(periodFromUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',')
                  .Append(periodToUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',')
                  .Append(r.Sequence.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(Quote(r.AccountCode)).Append(',')
                  .Append(Quote(r.AccountKey)).Append(',')
                  .Append(Quote(r.Description)).Append(',')
                  .Append(r.Debit.ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
                  .Append(r.Credit.ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
                  .Append(r.SourceDocumentCount.ToString(CultureInfo.InvariantCulture))
                  .Append("\r\n");
            }

            return sb.ToString();
        }

        private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
