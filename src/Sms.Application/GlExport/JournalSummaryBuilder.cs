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

        /// <summary>
        /// The difference between what a till session counted and what its receipts
        /// say it should hold (BR-PAY-001, gap G-5). An expense account that takes
        /// credits too: a drawer can come up over as easily as short, and netting
        /// the two into one figure is the point — a month of small overs and shorts
        /// that cancel is a different story from a month with one large short.
        /// </summary>
        public const string CashOverShort = "CashOverShort";

        /// <summary>
        /// Receivables given up as uncollectible (BR-INS-010, gap G-6). Not the
        /// same account as <see cref="Discounts"/> and not a revenue reversal: a
        /// discount is a price the school chose, a write-off is a price it charged
        /// and did not collect, and a reader who cannot tell them apart cannot tell
        /// a generous year from a badly collected one.
        /// </summary>
        public const string BadDebt = "BadDebt";

        /// <summary>
        /// Corrections made directly to a wallet balance (BR-CAF-009, gap G-3).
        /// No money moves and the liability changes anyway, so something has to
        /// take the other side — and it has to be an account somebody reviews,
        /// because every entry in it is a balance that was adjusted by hand.
        /// </summary>
        public const string WalletAdjustments = "WalletAdjustments";

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
    ///   Receipt        Dr Cash[method] / Cr AdvancesReceived  (the whole amount)
    ///   Allocation     Dr AdvancesReceived / Cr Receivables   (dated when it was made)
    ///   Refund paid    Dr AdvancesReceived / Cr Cash[method]
    ///   Till variance  Dr Cash:Cash / Cr CashOverShort   (over; reversed when short)
    ///   Write-off      Dr BadDebt (gross) / Cr Receivables
    ///   Wallet adj.    Dr WalletAdjustments / Cr WalletLiability   (credit; reversed when debited)
    ///   Cafeteria sale Dr WalletLiability|Cash:Cash / Cr CafeteriaRevenue (net), Cr VatOutput (vat)
    ///   Late void      the original entry, reversed, in the period of the void
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

        /// <summary>
        /// A posted receipt: Dr Cash[method] / Cr AdvancesReceived, for the whole
        /// amount.
        /// <para>
        /// Every receipt lands on advances first and an
        /// <see cref="AllocationDoc"/> moves it to receivables, even when both
        /// happen the same minute — the two net to the direct entry in that case,
        /// so nothing is lost. The reason is the case where they do not: an
        /// allocation made in October against a September receipt used to be
        /// counted inside September's receipt line, which meant regenerating
        /// September produced different numbers from the batch already posted for
        /// it, and October never saw the movement at all (gap G-10).
        /// </para>
        /// </summary>
        public sealed record ReceiptDoc(string PaymentMethod, decimal Amount);

        /// <summary>One receipt-to-charge allocation, dated when it was made: Dr AdvancesReceived / Cr Receivables.</summary>
        public sealed record AllocationDoc(decimal Amount);

        public sealed record RefundDoc(string PaymentMethod, decimal Amount);

        /// <summary>E-605: wallet top-up receipt (Dr Cash[method] / Cr WalletLiability) — a wallet refund is a negative-amount top-up in journal terms (Dr WalletLiability / Cr Cash).</summary>
        public sealed record WalletTopUpDoc(string PaymentMethod, decimal Amount);

        /// <summary>
        /// E-605 BR-CAF-007: a cafeteria sale. Wallet-tendered (Dr WalletLiability)
        /// or cash (Dr Cash:Cash), against revenue net of the tax inside it and
        /// <c>VatOutput</c> for the rest (gap G-2). Meal-plan redemptions are not
        /// journaled — revenue was recognised on the plan charge.
        /// </summary>
        public sealed record CafeteriaSaleDoc(bool IsWalletTender, decimal Amount, decimal VatAmount = 0m);

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

        /// <summary>
        /// A closed till session's variance: counted minus system, so positive is
        /// over and negative is short. Posted against cash on hand because a till
        /// count is a count of the drawer — the session records one variance rather
        /// than one per tender, so there is nowhere else honest to put it.
        /// </summary>
        public sealed record TillVarianceDoc(decimal Variance);

        /// <summary>A write-off credit note: Dr BadDebt / Cr Receivables, at gross. The VAT is not backed out — the supply happened and the tax on it is still owed.</summary>
        public sealed record WriteOffDoc(decimal Amount);

        /// <summary>A wallet correction, signed as the ledger stores it: positive credits the family's wallet (the school owes more), negative takes value back.</summary>
        public sealed record WalletAdjustmentDoc(decimal SignedAmount);

        /// <summary>
        /// A document voided in this period whose original reached the ledger in
        /// an earlier one (gap G-4).
        /// <para>
        /// The void makes it vanish from every forward-looking query, which is
        /// right — but the batch that carried it is posted and immutable, so
        /// without an entry here the receivable or the revenue it created stays on
        /// the ledger for ever. Reversed in the period of the void, never by
        /// editing the period of the original: a posted entry is not something to
        /// go back and change, and the pair has to stay visible for the correction
        /// to be auditable at all.
        /// </para>
        /// <para>
        /// A document posted and voided inside the same period is not here. It
        /// never reached a batch, so there is nothing to reverse — and reversing
        /// it would invent a loss out of an event that cost nothing.
        /// </para>
        /// </summary>
        public sealed record VoidedChargeDoc(int FeeCategoryId, string? GlExportCode, decimal NetAmount, decimal VatAmount, decimal GrossAmount);

        /// <summary>A cafeteria sale voided after its period was exported — the reverse of <see cref="CafeteriaSaleDoc"/>.</summary>
        public sealed record VoidedCafeteriaSaleDoc(bool IsWalletTender, decimal Amount, decimal VatAmount = 0m);

        /// <summary>A wallet-tendered store sale voided after its period was exported — the reverse of <see cref="StoreWalletSaleDoc"/>.</summary>
        public sealed record VoidedStoreWalletSaleDoc(decimal Amount);

        public sealed record JournalLine(string AccountKey, string Description, decimal Debit, decimal Credit, int SourceDocumentCount);

        public sealed class Journal
        {
            public IReadOnlyList<JournalLine> Lines { get; init; } = Array.Empty<JournalLine>();

            public decimal TotalDebit => Lines.Sum(l => l.Debit);

            public decimal TotalCredit => Lines.Sum(l => l.Credit);

            public int SourceDocumentCount { get; init; }

            public bool IsBalanced => TotalDebit == TotalCredit;
        }

        /// <summary>
        /// Everything a period holds, each kind defaulting to nothing. Named
        /// rather than positional because the list only grows: it reached a dozen
        /// collections while the ledger gaps were being closed, and at that width
        /// a call site is a wall of <c>Array.Empty</c> in which one misplaced
        /// argument is a silent posting to the wrong account.
        /// </summary>
        public sealed record PeriodDocuments
        {
            public IReadOnlyCollection<ChargeDoc> Charges { get; init; } = Array.Empty<ChargeDoc>();

            public IReadOnlyCollection<CreditNoteDoc> CreditNotes { get; init; } = Array.Empty<CreditNoteDoc>();

            public IReadOnlyCollection<DiscountDoc> Discounts { get; init; } = Array.Empty<DiscountDoc>();

            public IReadOnlyCollection<ReceiptDoc> Receipts { get; init; } = Array.Empty<ReceiptDoc>();

            public IReadOnlyCollection<AllocationDoc> Allocations { get; init; } = Array.Empty<AllocationDoc>();

            public IReadOnlyCollection<RefundDoc> Refunds { get; init; } = Array.Empty<RefundDoc>();

            public IReadOnlyCollection<WalletTopUpDoc> WalletTopUps { get; init; } = Array.Empty<WalletTopUpDoc>();

            public IReadOnlyCollection<WalletAdjustmentDoc> WalletAdjustments { get; init; } = Array.Empty<WalletAdjustmentDoc>();

            public IReadOnlyCollection<CafeteriaSaleDoc> CafeteriaSales { get; init; } = Array.Empty<CafeteriaSaleDoc>();

            public IReadOnlyCollection<StoreWalletSaleDoc> StoreWalletSales { get; init; } = Array.Empty<StoreWalletSaleDoc>();

            public IReadOnlyCollection<TillVarianceDoc> TillVariances { get; init; } = Array.Empty<TillVarianceDoc>();

            public IReadOnlyCollection<WriteOffDoc> WriteOffs { get; init; } = Array.Empty<WriteOffDoc>();

            public IReadOnlyCollection<VoidedChargeDoc> VoidedCharges { get; init; } = Array.Empty<VoidedChargeDoc>();

            public IReadOnlyCollection<VoidedCafeteriaSaleDoc> VoidedCafeteriaSales { get; init; } = Array.Empty<VoidedCafeteriaSaleDoc>();

            public IReadOnlyCollection<VoidedStoreWalletSaleDoc> VoidedStoreWalletSales { get; init; } = Array.Empty<VoidedStoreWalletSaleDoc>();

            public int Count => Charges.Count + CreditNotes.Count + Discounts.Count + Receipts.Count + Allocations.Count
                + Refunds.Count + WalletTopUps.Count + WalletAdjustments.Count + CafeteriaSales.Count
                + StoreWalletSales.Count + TillVariances.Count + WriteOffs.Count
                + VoidedCharges.Count + VoidedCafeteriaSales.Count + VoidedStoreWalletSales.Count;
        }

        /// <summary>The five documents a fee cycle cannot do without — a convenience for callers that touch nothing else.</summary>
        public static Journal Build(
            IReadOnlyCollection<ChargeDoc> charges, IReadOnlyCollection<CreditNoteDoc> creditNotes, IReadOnlyCollection<DiscountDoc> discounts,
            IReadOnlyCollection<ReceiptDoc> receipts, IReadOnlyCollection<RefundDoc> refunds)
            => Build(new PeriodDocuments
            {
                Charges = charges, CreditNotes = creditNotes, Discounts = discounts, Receipts = receipts, Refunds = refunds,
            });

        public static Journal Build(PeriodDocuments documents)
        {
            var (charges, creditNotes, discounts) = (documents.Charges, documents.CreditNotes, documents.Discounts);
            var (receipts, refunds, allocations) = (documents.Receipts, documents.Refunds, documents.Allocations);
            var (walletTopUps, walletAdjustments) = (documents.WalletTopUps, documents.WalletAdjustments);
            var (cafeteriaSales, storeWalletSales) = (documents.CafeteriaSales, documents.StoreWalletSales);
            var (tillVariances, writeOffs) = (documents.TillVariances, documents.WriteOffs);
            var voidedCharges = documents.VoidedCharges;

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
                acc.Credit(GlAccountKeys.CafeteriaRevenue, "Cafeteria sales", s.Amount - s.VatAmount);
                acc.Credit(GlAccountKeys.VatOutput, "VAT on cafeteria sales", s.VatAmount);
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

            foreach (var c in voidedCharges)
            {
                acc.Debit(GlAccountKeys.Revenue(c.FeeCategoryId, c.GlExportCode), "Charges voided", c.NetAmount);
                acc.Debit(GlAccountKeys.VatOutput, "VAT on voided charges", c.VatAmount);
                acc.Credit(GlAccountKeys.Receivables, "Charges voided", c.GrossAmount);
            }

            foreach (var s in documents.VoidedCafeteriaSales)
            {
                acc.Debit(GlAccountKeys.CafeteriaRevenue, "Cafeteria sales voided", s.Amount - s.VatAmount);
                acc.Debit(GlAccountKeys.VatOutput, "VAT on voided cafeteria sales", s.VatAmount);
                acc.Credit(s.IsWalletTender ? GlAccountKeys.WalletLiability : GlAccountKeys.Cash("Cash"), "Cafeteria sales voided", s.Amount);
            }

            foreach (var s in documents.VoidedStoreWalletSales)
            {
                acc.Debit(GlAccountKeys.StoreRevenue, "Store sales voided", s.Amount);
                acc.Credit(GlAccountKeys.WalletLiability, "Store sales voided", s.Amount);
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
                acc.Credit(GlAccountKeys.AdvancesReceived, "Receipts taken", r.Amount);
            }

            foreach (var a in allocations)
            {
                acc.Debit(GlAccountKeys.AdvancesReceived, "Receipts applied to charges", a.Amount);
                acc.Credit(GlAccountKeys.Receivables, "Receipts applied to charges", a.Amount);
            }

            foreach (var f in refunds)
            {
                acc.Debit(GlAccountKeys.AdvancesReceived, "Refunds paid", f.Amount);
                acc.Credit(GlAccountKeys.Cash(f.PaymentMethod), $"Refunds paid ({f.PaymentMethod})", f.Amount);
            }

            foreach (var a in walletAdjustments)
            {
                if (a.SignedAmount > 0m)
                {
                    acc.Debit(GlAccountKeys.WalletAdjustments, "Wallet adjustments (credited)", a.SignedAmount);
                    acc.Credit(GlAccountKeys.WalletLiability, "Wallet adjustments (credited)", a.SignedAmount);
                }
                else
                {
                    acc.Debit(GlAccountKeys.WalletLiability, "Wallet adjustments (debited)", -a.SignedAmount);
                    acc.Credit(GlAccountKeys.WalletAdjustments, "Wallet adjustments (debited)", -a.SignedAmount);
                }
            }

            foreach (var w in writeOffs)
            {
                acc.Debit(GlAccountKeys.BadDebt, "Written off", w.Amount);
                acc.Credit(GlAccountKeys.Receivables, "Written off", w.Amount);
            }

            foreach (var t in tillVariances)
            {
                if (t.Variance > 0m)
                {
                    // The drawer holds more than the receipts explain: the asset is really there.
                    acc.Debit(GlAccountKeys.Cash("Cash"), "Till overage", t.Variance);
                    acc.Credit(GlAccountKeys.CashOverShort, "Till overage", t.Variance);
                }
                else
                {
                    acc.Debit(GlAccountKeys.CashOverShort, "Till shortage", -t.Variance);
                    acc.Credit(GlAccountKeys.Cash("Cash"), "Till shortage", -t.Variance);
                }
            }

            var journal = new Journal
            {
                Lines = acc.ToLines(),
                SourceDocumentCount = documents.Count,
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
