using System;
using System.Linq;
using Sms.Application.GlExport;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.GlExport
{
    public class JournalSummaryBuilderTests
    {
        [Fact]
        [BusinessRule("BR-FEE-001")]
        public void A_charge_posts_receivables_against_revenue_and_VAT_and_balances()
        {
            var journal = JournalSummaryBuilder.Build(
                new[] { new JournalSummaryBuilder.ChargeDoc(1, "4100", 1000m, 150m, 1150m) },
                Array.Empty<JournalSummaryBuilder.CreditNoteDoc>(), Array.Empty<JournalSummaryBuilder.DiscountDoc>(),
                Array.Empty<JournalSummaryBuilder.ReceiptDoc>(), Array.Empty<JournalSummaryBuilder.RefundDoc>());

            Assert.True(journal.IsBalanced);
            Assert.Equal(1150m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.Receivables).Debit);
            Assert.Equal(1000m, journal.Lines.Single(l => l.AccountKey == "4100").Credit);
            Assert.Equal(150m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.VatOutput).Credit);
        }

        [Fact]
        [BusinessRule("BR-FEE-001")]
        public void Credit_notes_split_VAT_back_out_and_receipts_split_allocated_from_advances()
        {
            var journal = JournalSummaryBuilder.Build(
                Array.Empty<JournalSummaryBuilder.ChargeDoc>(),
                new[] { new JournalSummaryBuilder.CreditNoteDoc(1, "4100", 115m, 0.15m) },
                new[] { new JournalSummaryBuilder.DiscountDoc(115m, 0.15m) },
                new[] { new JournalSummaryBuilder.ReceiptDoc("Cash", 500m, 400m) },
                new[] { new JournalSummaryBuilder.RefundDoc("BankTransfer", 30m) });

            Assert.True(journal.IsBalanced);
            Assert.Equal(100m, journal.Lines.Single(l => l.AccountKey == "4100").Debit);

            // G-11: the discount splits its VAT back out exactly as the credit note does, so both
            // debits land on VatOutput — 15 from each. Before the fix the discount's 15 stayed in
            // VatOutput as tax on revenue that never happened.
            Assert.Equal(30m, journal.Lines.Where(l => l.AccountKey == GlAccountKeys.VatOutput).Sum(l => l.Debit));
            Assert.Equal(100m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.Discounts).Debit);
            Assert.Equal(500m, journal.Lines.Single(l => l.AccountKey == "Cash:Cash").Debit);
            Assert.Equal(100m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.AdvancesReceived && l.Credit > 0).Credit);
            Assert.Equal(30m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.AdvancesReceived && l.Debit > 0).Debit);
            Assert.Equal(30m, journal.Lines.Single(l => l.AccountKey == "Cash:BankTransfer").Credit);
            // Receivables is credited the gross of each: 115 credit note + 115 discount + 400 allocated.
            Assert.Equal(630m, journal.Lines.Where(l => l.AccountKey == GlAccountKeys.Receivables).Sum(l => l.Credit));
            Assert.Equal(4, journal.SourceDocumentCount);
        }

        [Fact]
        [BusinessRule("BR-PAY-001")]
        public void A_till_over_and_a_till_short_both_reach_the_ledger_and_net()
        {
            var journal = JournalSummaryBuilder.Build(
                Array.Empty<JournalSummaryBuilder.ChargeDoc>(), Array.Empty<JournalSummaryBuilder.CreditNoteDoc>(),
                Array.Empty<JournalSummaryBuilder.DiscountDoc>(), Array.Empty<JournalSummaryBuilder.ReceiptDoc>(),
                Array.Empty<JournalSummaryBuilder.RefundDoc>(), Array.Empty<JournalSummaryBuilder.WalletTopUpDoc>(),
                Array.Empty<JournalSummaryBuilder.CafeteriaSaleDoc>(), Array.Empty<JournalSummaryBuilder.StoreWalletSaleDoc>(),
                new[] { new JournalSummaryBuilder.TillVarianceDoc(12m), new JournalSummaryBuilder.TillVarianceDoc(-30m) },
                Array.Empty<JournalSummaryBuilder.WriteOffDoc>(), Array.Empty<JournalSummaryBuilder.WalletAdjustmentDoc>());

            Assert.True(journal.IsBalanced);

            // Kept as two lines, not one net 18. A month of small overs and shorts that cancel is a
            // different story from a month with one large short, and netting erases the difference.
            Assert.Equal(12m, journal.Lines.Single(l => l.AccountKey == "Cash:Cash" && l.Debit > 0m).Debit);
            Assert.Equal(12m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.CashOverShort && l.Credit > 0m).Credit);
            Assert.Equal(30m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.CashOverShort && l.Debit > 0m).Debit);
            Assert.Equal(30m, journal.Lines.Single(l => l.AccountKey == "Cash:Cash" && l.Credit > 0m).Credit);
            Assert.Equal(2, journal.SourceDocumentCount);
        }

        [Fact]
        [BusinessRule("BR-INS-010")]
        public void A_write_off_hits_bad_debt_and_leaves_revenue_and_VAT_alone()
        {
            var journal = JournalSummaryBuilder.Build(
                Array.Empty<JournalSummaryBuilder.ChargeDoc>(), Array.Empty<JournalSummaryBuilder.CreditNoteDoc>(),
                Array.Empty<JournalSummaryBuilder.DiscountDoc>(), Array.Empty<JournalSummaryBuilder.ReceiptDoc>(),
                Array.Empty<JournalSummaryBuilder.RefundDoc>(), Array.Empty<JournalSummaryBuilder.WalletTopUpDoc>(),
                Array.Empty<JournalSummaryBuilder.CafeteriaSaleDoc>(), Array.Empty<JournalSummaryBuilder.StoreWalletSaleDoc>(),
                Array.Empty<JournalSummaryBuilder.TillVarianceDoc>(),
                new[] { new JournalSummaryBuilder.WriteOffDoc(115m) }, Array.Empty<JournalSummaryBuilder.WalletAdjustmentDoc>());

            Assert.True(journal.IsBalanced);
            Assert.Equal(115m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.BadDebt).Debit);
            Assert.Equal(115m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.Receivables).Credit);

            // The supply happened and the tax on it is still owed, so unlike a credit note this
            // touches neither revenue nor VAT — and it is not the discounts account either, because
            // a price the school chose and a price it failed to collect are different facts.
            Assert.DoesNotContain(journal.Lines, l => l.AccountKey == GlAccountKeys.VatOutput);
            Assert.DoesNotContain(journal.Lines, l => l.AccountKey == GlAccountKeys.Discounts);
        }

        [Fact]
        [BusinessRule("BR-CAF-009")]
        public void A_wallet_correction_moves_the_liability_against_an_adjustments_account()
        {
            var journal = JournalSummaryBuilder.Build(
                Array.Empty<JournalSummaryBuilder.ChargeDoc>(), Array.Empty<JournalSummaryBuilder.CreditNoteDoc>(),
                Array.Empty<JournalSummaryBuilder.DiscountDoc>(), Array.Empty<JournalSummaryBuilder.ReceiptDoc>(),
                Array.Empty<JournalSummaryBuilder.RefundDoc>(), Array.Empty<JournalSummaryBuilder.WalletTopUpDoc>(),
                Array.Empty<JournalSummaryBuilder.CafeteriaSaleDoc>(), Array.Empty<JournalSummaryBuilder.StoreWalletSaleDoc>(),
                Array.Empty<JournalSummaryBuilder.TillVarianceDoc>(), Array.Empty<JournalSummaryBuilder.WriteOffDoc>(),
                new[] { new JournalSummaryBuilder.WalletAdjustmentDoc(25m), new JournalSummaryBuilder.WalletAdjustmentDoc(-10m) });

            Assert.True(journal.IsBalanced);

            // Crediting a family's wallet costs the school something even though no cash moved.
            Assert.Equal(25m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.WalletAdjustments && l.Debit > 0m).Debit);
            Assert.Equal(25m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.WalletLiability && l.Credit > 0m).Credit);
            Assert.Equal(10m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.WalletLiability && l.Debit > 0m).Debit);
            Assert.Equal(10m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.WalletAdjustments && l.Credit > 0m).Credit);
        }

        [Fact]
        [BusinessRule("BR-FEE-001")]
        public void Unmapped_categories_fall_back_to_a_revenue_key_per_category()
        {
            Assert.Equal("Revenue:7", GlAccountKeys.Revenue(7, null));
            Assert.Equal("4100", GlAccountKeys.Revenue(7, "4100"));
        }

        [Fact]
        [BusinessRule("BR-FEE-001")]
        public void Csv_is_culture_invariant_and_quotes_text()
        {
            var csv = CsvJournalWriter.Render("GLX-0001", new DateTime(2026, 9, 1), new DateTime(2026, 9, 30), new[]
            {
                new CsvJournalWriter.Row(1, "1200", GlAccountKeys.Receivables, "Charges \"posted\"", 1150m, 0m, 3),
            });

            var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal("BatchNo,PeriodFrom,PeriodTo,Seq,AccountCode,AccountKey,Description,Debit,Credit,SourceDocs", lines[0]);
            Assert.Equal("\"GLX-0001\",2026-09-01,2026-09-30,1,\"1200\",\"Receivables\",\"Charges \"\"posted\"\"\",1150.00,0.00,3", lines[1]);
        }
    }
}
