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
        public void Credit_notes_and_discounts_both_split_their_VAT_back_out()
        {
            var journal = JournalSummaryBuilder.Build(
                Array.Empty<JournalSummaryBuilder.ChargeDoc>(),
                new[] { new JournalSummaryBuilder.CreditNoteDoc(1, "4100", 115m, 0.15m) },
                new[] { new JournalSummaryBuilder.DiscountDoc(115m, 0.15m) },
                Array.Empty<JournalSummaryBuilder.ReceiptDoc>(),
                new[] { new JournalSummaryBuilder.RefundDoc("BankTransfer", 30m) });

            Assert.True(journal.IsBalanced);
            Assert.Equal(100m, journal.Lines.Single(l => l.AccountKey == "4100").Debit);

            // G-11: the discount splits its VAT back out exactly as the credit note does, so both
            // debits land on VatOutput — 15 from each. Before the fix the discount's 15 stayed in
            // VatOutput as tax on revenue that never happened.
            Assert.Equal(30m, journal.Lines.Where(l => l.AccountKey == GlAccountKeys.VatOutput).Sum(l => l.Debit));
            Assert.Equal(100m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.Discounts).Debit);
            Assert.Equal(30m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.AdvancesReceived).Debit);
            Assert.Equal(30m, journal.Lines.Single(l => l.AccountKey == "Cash:BankTransfer").Credit);
            Assert.Equal(230m, journal.Lines.Where(l => l.AccountKey == GlAccountKeys.Receivables).Sum(l => l.Credit));
            Assert.Equal(3, journal.SourceDocumentCount);
        }

        [Fact]
        [BusinessRule("BR-PAY-003")]
        public void A_receipt_lands_on_advances_and_its_allocation_is_what_settles_the_receivable()
        {
            var journal = JournalSummaryBuilder.Build(new JournalSummaryBuilder.PeriodDocuments
            {
                Receipts = new[] { new JournalSummaryBuilder.ReceiptDoc("Cash", 500m) },
                Allocations = new[] { new JournalSummaryBuilder.AllocationDoc(400m) },
            });

            Assert.True(journal.IsBalanced);
            Assert.Equal(500m, journal.Lines.Single(l => l.AccountKey == "Cash:Cash").Debit);

            // Taken in full to advances, then 400 of it moved on to receivables. Net over the two
            // lines is the 100 the family is still in credit for — the same answer the old one-step
            // entry gave, reached in a way that survives the allocation happening a month later.
            Assert.Equal(500m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.AdvancesReceived && l.Credit > 0m).Credit);
            Assert.Equal(400m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.AdvancesReceived && l.Debit > 0m).Debit);
            Assert.Equal(400m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.Receivables).Credit);
        }

        [Fact]
        [BusinessRule("BR-PAY-003")]
        public void An_allocation_alone_clears_an_advance_raised_in_an_earlier_period()
        {
            // October's batch, against a receipt September already took to advances. Before G-10 this
            // period showed nothing at all, and September's posted batch quietly disagreed with what
            // regenerating it would now produce.
            var journal = JournalSummaryBuilder.Build(new JournalSummaryBuilder.PeriodDocuments
            {
                Allocations = new[] { new JournalSummaryBuilder.AllocationDoc(100m) },
            });

            Assert.True(journal.IsBalanced);
            Assert.Equal(100m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.AdvancesReceived).Debit);
            Assert.Equal(100m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.Receivables).Credit);
            Assert.DoesNotContain(journal.Lines, l => l.AccountKey.StartsWith("Cash:"));
        }

        [Fact]
        [BusinessRule("BR-PAY-001")]
        public void A_till_over_and_a_till_short_both_reach_the_ledger_and_net()
        {
            var journal = JournalSummaryBuilder.Build(new JournalSummaryBuilder.PeriodDocuments
            {
                TillVariances = new[] { new JournalSummaryBuilder.TillVarianceDoc(12m), new JournalSummaryBuilder.TillVarianceDoc(-30m) },
            });

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
            var journal = JournalSummaryBuilder.Build(new JournalSummaryBuilder.PeriodDocuments
            {
                WriteOffs = new[] { new JournalSummaryBuilder.WriteOffDoc(115m) },
            });

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
            var journal = JournalSummaryBuilder.Build(new JournalSummaryBuilder.PeriodDocuments
            {
                WalletAdjustments = new[] { new JournalSummaryBuilder.WalletAdjustmentDoc(25m), new JournalSummaryBuilder.WalletAdjustmentDoc(-10m) },
            });

            Assert.True(journal.IsBalanced);

            // Crediting a family's wallet costs the school something even though no cash moved.
            Assert.Equal(25m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.WalletAdjustments && l.Debit > 0m).Debit);
            Assert.Equal(25m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.WalletLiability && l.Credit > 0m).Credit);
            Assert.Equal(10m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.WalletLiability && l.Debit > 0m).Debit);
            Assert.Equal(10m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.WalletAdjustments && l.Credit > 0m).Credit);
        }

        [Fact]
        [BusinessRule("BR-GLB-062")]
        public void A_charge_voided_after_its_period_shipped_is_reversed_in_the_period_of_the_void()
        {
            var journal = JournalSummaryBuilder.Build(new JournalSummaryBuilder.PeriodDocuments
            {
                VoidedCharges = new[] { new JournalSummaryBuilder.VoidedChargeDoc(1, "4100", 1000m, 150m, 1150m) },
            });

            Assert.True(journal.IsBalanced);

            // Exactly the charge entry, the other way round. Not a credit note — nothing was
            // corrected about the amount; the document should never have existed.
            Assert.Equal(1000m, journal.Lines.Single(l => l.AccountKey == "4100").Debit);
            Assert.Equal(150m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.VatOutput).Debit);
            Assert.Equal(1150m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.Receivables).Credit);
        }

        [Fact]
        [BusinessRule("BR-GLB-062")]
        public void A_charge_posted_and_voided_in_one_period_nets_to_nothing()
        {
            // Both sides in the same batch: the service does not put such a charge in either stream,
            // and if it did the journal would still come out flat. This pins the arithmetic that
            // makes the service's scoping safe rather than merely conventional.
            var journal = JournalSummaryBuilder.Build(new JournalSummaryBuilder.PeriodDocuments
            {
                Charges = new[] { new JournalSummaryBuilder.ChargeDoc(1, "4100", 1000m, 150m, 1150m) },
                VoidedCharges = new[] { new JournalSummaryBuilder.VoidedChargeDoc(1, "4100", 1000m, 150m, 1150m) },
            });

            Assert.True(journal.IsBalanced);
            Assert.Equal(
                journal.Lines.Where(l => l.AccountKey == GlAccountKeys.Receivables).Sum(l => l.Debit),
                journal.Lines.Where(l => l.AccountKey == GlAccountKeys.Receivables).Sum(l => l.Credit));
            Assert.Equal(
                journal.Lines.Where(l => l.AccountKey == "4100").Sum(l => l.Credit),
                journal.Lines.Where(l => l.AccountKey == "4100").Sum(l => l.Debit));
        }

        [Fact]
        [BusinessRule("BR-CAF-007")]
        public void A_late_voided_sale_gives_the_wallet_its_money_back_in_the_ledger_too()
        {
            var journal = JournalSummaryBuilder.Build(new JournalSummaryBuilder.PeriodDocuments
            {
                VoidedCafeteriaSales = new[] { new JournalSummaryBuilder.VoidedCafeteriaSaleDoc(true, 30m) },
                VoidedStoreWalletSales = new[] { new JournalSummaryBuilder.VoidedStoreWalletSaleDoc(45m) },
            });

            Assert.True(journal.IsBalanced);
            Assert.Equal(30m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.CafeteriaRevenue).Debit);
            Assert.Equal(45m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.StoreRevenue).Debit);

            // The wallet was credited back when the sale was voided, so the liability has to come
            // back with it — otherwise the school holds money the ledger says it does not.
            Assert.Equal(75m, journal.Lines.Where(l => l.AccountKey == GlAccountKeys.WalletLiability).Sum(l => l.Credit));
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
