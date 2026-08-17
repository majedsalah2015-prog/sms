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
                new[] { new JournalSummaryBuilder.DiscountDoc(50m) },
                new[] { new JournalSummaryBuilder.ReceiptDoc("Cash", 500m, 400m) },
                new[] { new JournalSummaryBuilder.RefundDoc("BankTransfer", 30m) });

            Assert.True(journal.IsBalanced);
            Assert.Equal(100m, journal.Lines.Single(l => l.AccountKey == "4100").Debit);
            Assert.Equal(15m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.VatOutput).Debit);
            Assert.Equal(50m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.Discounts).Debit);
            Assert.Equal(500m, journal.Lines.Single(l => l.AccountKey == "Cash:Cash").Debit);
            Assert.Equal(100m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.AdvancesReceived && l.Credit > 0).Credit);
            Assert.Equal(30m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.AdvancesReceived && l.Debit > 0).Debit);
            Assert.Equal(30m, journal.Lines.Single(l => l.AccountKey == "Cash:BankTransfer").Credit);
            Assert.Equal(565m, journal.Lines.Where(l => l.AccountKey == GlAccountKeys.Receivables).Sum(l => l.Credit));
            Assert.Equal(4, journal.SourceDocumentCount);
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
