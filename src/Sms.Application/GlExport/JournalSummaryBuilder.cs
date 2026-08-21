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

        /// <summary>S6/E-605 BR-CAF-001/007: wallet balances are payer money held — a liability.</summary>
        public const string WalletLiability = "WalletLiability";

        public const string CafeteriaRevenue = "CafeteriaRevenue";

        /// <summary>Store sales tendered from the wallet — the cafeteria's counterpart for Module 28 (gap G-1).</summary>
        public const string StoreRevenue = "StoreRevenue";

        public static string Cash(string paymentMethod) => $"Cash:{paymentMethod}";

        public static string Revenue(int feeCategoryId, string? glExportCode) => string.IsNullOrWhiteSpace(glExportCode) ? $"Revenue:{feeCategoryId}" : glExportCode!;
    }

    /// <summary>
    /// Pure journal-summary composition (E-503, O3 assumption): every
    /// Module 19/21/22 document of the period folds into balanced summary
    /// lines. Postings:
    ///   Charge         Dr Receivables (gross)   / Cr Revenue[category] (net), Cr VatOutput (vat)
    ///   Credit note    Dr Revenue[category] (net part), Dr VatOutput (vat part) / Cr Receivables (gross)
    ///   Discount doc   Dr Discounts (net part), Dr VatOutput (vat part) / Cr Receivables (gross)
    ///   Store sale     Dr WalletLiability / Cr StoreRevenue   (wallet-tendered only)
    ///   Receipt        Dr Cash[method] (amount) / Cr Receivables (allocated), Cr AdvancesReceived (unallocated)
    ///   Refund paid    Dr AdvancesReceived / Cr Cash[method]
    /// Balanced by construction; the builder asserts it anyway.
    /// </summary>
    public static class JournalSummaryBuilder
    {
        public sealed record ChargeDoc(int FeeCategoryId, string? GlExportCode, decimal NetAmount, decimal VatAmount, decimal GrossAmount);

        public sealed record CreditNoteDoc(int FeeCategoryId, string? GlExportCode, decimal Amount, decimal VatRate);

        /// <summary>
        /// A discount document. <paramref name="Amount"/> is gross — VAT-inclusive,
        /// as <c>DiscountAmountCalculator</c> produces it — and
        /// <paramref name="VatRate"/> is the rate its charge froze at posting, so
        /// the two halves can be separated again.
        /// <para>
        /// Carrying the rate is the fix for gap G-11. A discount reduces a
        /// receivable that included VAT, so the VAT credited when the charge was
        /// posted has to come back with it. Posting the whole gross figure to
        /// Discounts left <c>VatOutput</c> holding tax on revenue that never
        /// happened — and credit notes, which do exactly the same thing to a
        /// charge, split it correctly. The asymmetry was undocumented, which is how
        /// it survived.
        /// </para>
        /// </summary>
        public sealed record DiscountDoc(decimal Amount, decimal VatRate);

        public sealed record ReceiptDoc(string PaymentMethod, decimal Amount, decimal AllocatedAmount);

        public sealed record RefundDoc(string PaymentMethod, decimal Amount);

        /// <summary>E-605: wallet top-up receipt (Dr Cash[method] / Cr WalletLiability) — a wallet refund is a negative-amount top-up in journal terms (Dr WalletLiability / Cr Cash).</summary>
        public sealed record WalletTopUpDoc(string PaymentMethod, decimal Amount);

        /// <summary>E-605 BR-CAF-007: a cafeteria sale — wallet-tendered (Dr WalletLiability / Cr CafeteriaRevenue) or cash (Dr Cash:Cash / Cr CafeteriaRevenue). Meal-plan redemptions are not journaled (revenue was recognized on the plan charge).</summary>
        public sealed record CafeteriaSaleDoc(bool IsWalletTender, decimal Amount);

        /// <summary>
        /// A wallet-tendered store sale (Dr WalletLiability / Cr StoreRevenue).
        /// <para>
        /// Only the wallet ones. A store sale paid in cash, by card, or put on the
        /// family account already reaches the ledger as a Charge and a Receipt —
        /// those are real documents and journal themselves. A wallet sale creates
        /// neither: it debits the wallet and stops, so before this it moved money
        /// nowhere the ledger could see. The wallet liability grew and never came
        /// down, and store revenue was never recognised at all.
        /// </para>
        /// </summary>
        public sealed record StoreWalletSaleDoc(decimal Amount);

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
            => Build(charges, creditNotes, discounts, receipts, refunds, Array.Empty<WalletTopUpDoc>(), Array.Empty<CafeteriaSaleDoc>(), Array.Empty<StoreWalletSaleDoc>());

        public static Journal Build(
            IReadOnlyCollection<ChargeDoc> charges, IReadOnlyCollection<CreditNoteDoc> creditNotes, IReadOnlyCollection<DiscountDoc> discounts,
            IReadOnlyCollection<ReceiptDoc> receipts, IReadOnlyCollection<RefundDoc> refunds,
            IReadOnlyCollection<WalletTopUpDoc> walletTopUps, IReadOnlyCollection<CafeteriaSaleDoc> cafeteriaSales)
            => Build(charges, creditNotes, discounts, receipts, refunds, walletTopUps, cafeteriaSales, Array.Empty<StoreWalletSaleDoc>());

        public static Journal Build(
            IReadOnlyCollection<ChargeDoc> charges, IReadOnlyCollection<CreditNoteDoc> creditNotes, IReadOnlyCollection<DiscountDoc> discounts,
            IReadOnlyCollection<ReceiptDoc> receipts, IReadOnlyCollection<RefundDoc> refunds,
            IReadOnlyCollection<WalletTopUpDoc> walletTopUps, IReadOnlyCollection<CafeteriaSaleDoc> cafeteriaSales,
            IReadOnlyCollection<StoreWalletSaleDoc> storeWalletSales)
        {
            var acc = new Accumulator();

            foreach (var w in walletTopUps)
            {
                if (w.Amount >= 0m)
                {
                    acc.Debit(GlAccountKeys.Cash(w.PaymentMethod), $"Wallet top-ups ({w.PaymentMethod})", w.Amount);
                    acc.Credit(GlAccountKeys.WalletLiability, "Wallet top-ups", w.Amount);
                }
                else
                {
                    acc.Debit(GlAccountKeys.WalletLiability, "Wallet refunds", -w.Amount);
                    acc.Credit(GlAccountKeys.Cash(w.PaymentMethod), $"Wallet refunds ({w.PaymentMethod})", -w.Amount);
                }
            }

            foreach (var s in cafeteriaSales)
            {
                acc.Debit(s.IsWalletTender ? GlAccountKeys.WalletLiability : GlAccountKeys.Cash("Cash"), s.IsWalletTender ? "Cafeteria sales (wallet)" : "Cafeteria sales (cash)", s.Amount);
                acc.Credit(GlAccountKeys.CafeteriaRevenue, "Cafeteria sales", s.Amount);
            }

            foreach (var s in storeWalletSales)
            {
                acc.Debit(GlAccountKeys.WalletLiability, "Store sales (wallet)", s.Amount);
                acc.Credit(GlAccountKeys.StoreRevenue, "Store sales", s.Amount);
            }

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
                // Same split, same rounding, same direction as a credit note above — because a discount
                // does the same thing to the same charge, and two answers to one question is the defect.
                var net = Math.Round(d.Amount / (1m + d.VatRate), 2, MidpointRounding.AwayFromZero);
                var vat = d.Amount - net;
                acc.Debit(GlAccountKeys.Discounts, "Discounts granted", net);
                acc.Debit(GlAccountKeys.VatOutput, "VAT on discounts", vat);
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
                SourceDocumentCount = charges.Count + creditNotes.Count + discounts.Count + receipts.Count + refunds.Count + walletTopUps.Count + cafeteriaSales.Count + storeWalletSales.Count,
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
