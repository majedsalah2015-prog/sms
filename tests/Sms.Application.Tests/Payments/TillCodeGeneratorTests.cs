using System;
using Sms.Application.Payments;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Payments
{
    /// <summary>
    /// BR-PAY-001 (doc/Modules/21 §8.2): a session is cashier × till × day, and the till is a
    /// physical drawer — so the console assigns the code rather than asking a cashier with a queue
    /// in front of them to invent one, and assigns the <i>same</i> one each day.
    /// </summary>
    public sealed class TillCodeGeneratorTests
    {
        private static readonly string[] None = Array.Empty<string>();

        [Fact]
        [BusinessRule("BR-PAY-001")]
        public void A_cashier_who_has_never_opened_a_session_gets_the_first_till()
        {
            Assert.Equal("TILL-1", TillCodeGenerator.Resolve(cashiersLastCode: null, None, None));
        }

        [Fact]
        [BusinessRule("BR-PAY-001")]
        public void Each_new_cashier_gets_a_drawer_nobody_has_ever_used()
        {
            Assert.Equal("TILL-3", TillCodeGenerator.Resolve(null, openCodes: new[] { "TILL-1" }, everUsedCodes: new[] { "TILL-1", "TILL-2" }));
        }

        [Fact]
        [BusinessRule("BR-PAY-001")]
        public void A_returning_cashier_keeps_the_drawer_they_had()
        {
            // The point of the whole rule: the daily-collection-by-till and cashier-variance-history
            // reports are only worth reading while a cashier's days accumulate on one code.
            Assert.Equal("TILL-2", TillCodeGenerator.Resolve("TILL-2", None, everUsedCodes: new[] { "TILL-1", "TILL-2", "TILL-3" }));
        }

        [Fact]
        [BusinessRule("BR-PAY-001")]
        public void A_code_typed_by_hand_before_this_existed_is_kept_not_replaced()
        {
            // "T1" and "Counter A" are already in the data. A cashier on one stays on it, and a
            // minted code steps around it rather than colliding with its history.
            Assert.Equal("T1", TillCodeGenerator.Resolve("T1", None, new[] { "T1", "Counter A" }));
            Assert.Equal("TILL-1", TillCodeGenerator.Resolve(null, None, new[] { "T1", "Counter A" }));
        }

        [Fact]
        [BusinessRule("BR-PAY-001")]
        public void A_cashier_whose_drawer_is_in_someone_elses_hands_gets_a_fresh_one()
        {
            // Only reachable after a hand-typed code was shared, or a handover — but the answer is a
            // drawer they can work at, not a refusal a cashier at a counter cannot act on.
            Assert.Equal("TILL-3", TillCodeGenerator.Resolve("TILL-1", openCodes: new[] { "TILL-1" }, everUsedCodes: new[] { "TILL-1", "TILL-2" }));
        }

        [Fact]
        [BusinessRule("BR-PAY-001")]
        public void Codes_are_matched_without_regard_to_case()
        {
            // On Sqlite "TILL-1" and "till-1" are two strings; they are one drawer, and minting a
            // "TILL-2" beside a hand-typed "till-2" would put two cashiers at one till.
            Assert.Equal("TILL-3", TillCodeGenerator.Resolve(null, None, new[] { "till-1", "Till-2" }));
            Assert.Equal("TILL-2", TillCodeGenerator.Resolve("TILL-1", openCodes: new[] { "till-1" }, everUsedCodes: new[] { "till-1" }));
        }

        [Fact]
        [BusinessRule("BR-PAY-001")]
        public void Blank_and_null_entries_in_the_history_are_not_drawers()
        {
            Assert.Equal("TILL-1", TillCodeGenerator.Resolve("   ", None, new[] { null, "", "  " }));
        }

        [Fact]
        [BusinessRule("BR-PAY-001")]
        public void A_minted_code_never_lands_on_a_closed_sessions_till()
        {
            // everUsed carries closed sessions too: reusing TILL-1 would graft a new cashier's day
            // onto another's variance history.
            Assert.Equal("TILL-2", TillCodeGenerator.Next(new[] { "TILL-1" }));
            Assert.Equal("TILL-4", TillCodeGenerator.Next(new[] { "TILL-1", "TILL-2", "TILL-3" }));
        }

        [Fact]
        [BusinessRule("BR-PAY-001")]
        public void The_next_code_fills_a_gap_left_by_a_retired_till()
        {
            Assert.Equal("TILL-2", TillCodeGenerator.Next(new[] { "TILL-1", "TILL-3" }));
        }

        [Fact]
        [BusinessRule("BR-PAY-001")]
        public void A_minted_code_fits_the_columns_twenty_characters()
        {
            Assert.True(TillCodeGenerator.Next(new[] { "TILL-1" }).Length <= 20);
        }
    }
}
