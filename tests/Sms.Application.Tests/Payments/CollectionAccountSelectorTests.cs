using System.Collections.Generic;
using System.Linq;
using Sms.Application.Common.Exceptions;
using Sms.Application.Payments;
using Sms.Domain.Payments;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Payments
{
    /// <summary>
    /// BR-PAY-002 — a receipt says where the money went, and this decides which
    /// destinations a method may name.
    /// </summary>
    public class CollectionAccountSelectorTests
    {
        private static CollectionAccount Account(
            int id, CollectionAccountKind kind, string code = "A", bool active = true, bool isDefault = false, int order = 0)
            => new()
            {
                Id = id,
                Code = code,
                NameAr = "حساب",
                NameEn = "Account",
                Kind = kind,
                IsActive = active,
                IsDefault = isDefault,
                DisplayOrder = order,
            };

        [Theory]
        [InlineData(PaymentMethod.Cash, CollectionAccountKind.CashBox)]
        [InlineData(PaymentMethod.BankTransfer, CollectionAccountKind.Bank)]
        [InlineData(PaymentMethod.Card, CollectionAccountKind.Bank)]
        [InlineData(PaymentMethod.Cheque, CollectionAccountKind.Bank)]
        [BusinessRule("BR-PAY-002")]
        public void Only_cash_stays_in_the_building(PaymentMethod method, CollectionAccountKind expected)
        {
            Assert.Equal(expected, CollectionAccountSelector.KindFor(method));
        }

        [Fact]
        [BusinessRule("BR-PAY-002")]
        public void The_picker_offers_only_the_kind_the_method_collects_into()
        {
            var accounts = new[]
            {
                Account(1, CollectionAccountKind.Bank, "BANK-01"),
                Account(2, CollectionAccountKind.CashBox, "SAFE-01"),
            };

            Assert.Equal(new[] { 2 }, CollectionAccountSelector.Eligible(accounts, PaymentMethod.Cash).Select(a => a.Id));
            Assert.Equal(new[] { 1 }, CollectionAccountSelector.Eligible(accounts, PaymentMethod.BankTransfer).Select(a => a.Id));
        }

        /// <summary>A retired account is kept so old receipts read back, not so new money can be put in it.</summary>
        [Fact]
        [BusinessRule("BR-PAY-002")]
        public void A_retired_account_leaves_the_picker()
        {
            var accounts = new[] { Account(1, CollectionAccountKind.Bank, "BANK-01", active: false) };

            Assert.Empty(CollectionAccountSelector.Eligible(accounts, PaymentMethod.BankTransfer));
        }

        [Fact]
        public void The_default_comes_first_then_display_order_then_code()
        {
            var accounts = new[]
            {
                Account(1, CollectionAccountKind.Bank, "B", order: 2),
                Account(2, CollectionAccountKind.Bank, "A", order: 2),
                Account(3, CollectionAccountKind.Bank, "C", order: 1),
                Account(4, CollectionAccountKind.Bank, "D", isDefault: true, order: 9),
            };

            Assert.Equal(new[] { 4, 3, 2, 1 }, CollectionAccountSelector.Eligible(accounts, PaymentMethod.BankTransfer).Select(a => a.Id));
            Assert.Equal(4, CollectionAccountSelector.Preselected(accounts, PaymentMethod.BankTransfer)!.Id);
        }

        [Fact]
        public void Nothing_is_preselected_when_the_school_has_no_account_of_that_kind()
        {
            var accounts = new[] { Account(1, CollectionAccountKind.Bank) };

            Assert.Null(CollectionAccountSelector.Preselected(accounts, PaymentMethod.Cash));
        }

        /// <summary>
        /// The conditional rule, in both directions: a school on its first
        /// morning has defined nothing and must still be able to take money,
        /// and the moment it has defined an account, leaving the field blank is
        /// an omission rather than an absence.
        /// </summary>
        [Fact]
        [BusinessRule("BR-PAY-002")]
        public void A_blank_destination_is_allowed_only_while_the_school_has_defined_none()
        {
            CollectionAccountSelector.Validate(PaymentMethod.BankTransfer, chosen: null, anyEligible: false);

            var refusal = Assert.Throws<CollectionAccountRequiredException>(
                () => CollectionAccountSelector.Validate(PaymentMethod.BankTransfer, chosen: null, anyEligible: true));
            Assert.Equal(CollectionAccountKind.Bank, refusal.Kind);
            Assert.Equal(PaymentMethod.BankTransfer, refusal.Method);
        }

        [Fact]
        [BusinessRule("BR-PAY-002")]
        public void Cash_cannot_be_recorded_into_a_bank_account()
        {
            var bank = Account(1, CollectionAccountKind.Bank);

            var refusal = Assert.Throws<CollectionAccountMethodMismatchException>(
                () => CollectionAccountSelector.Validate(PaymentMethod.Cash, bank, anyEligible: true));
            Assert.Equal(CollectionAccountKind.CashBox, refusal.Required);
            Assert.Equal(CollectionAccountKind.Bank, refusal.Actual);
        }

        [Fact]
        [BusinessRule("BR-PAY-002")]
        public void A_transfer_cannot_be_recorded_into_a_cash_box()
        {
            var safe = Account(1, CollectionAccountKind.CashBox);

            Assert.Throws<CollectionAccountMethodMismatchException>(
                () => CollectionAccountSelector.Validate(PaymentMethod.BankTransfer, safe, anyEligible: true));
        }

        [Fact]
        [BusinessRule("BR-PAY-002")]
        public void No_new_money_is_collected_into_a_retired_account()
        {
            var retired = Account(1, CollectionAccountKind.Bank, "BANK-09", active: false);

            var refusal = Assert.Throws<CollectionAccountInactiveException>(
                () => CollectionAccountSelector.Validate(PaymentMethod.BankTransfer, retired, anyEligible: true));
            Assert.Equal("BANK-09", refusal.Code);
        }

        /// <summary>
        /// The kind is checked before the retirement is: a cash receipt pointed
        /// at a retired bank account is wrong in the way that matters more, and
        /// telling the cashier "that account is closed" would send them looking
        /// for another bank account they must not use either.
        /// </summary>
        [Fact]
        public void A_wrong_kind_is_reported_ahead_of_a_retirement()
        {
            var retiredBank = Account(1, CollectionAccountKind.Bank, active: false);

            Assert.Throws<CollectionAccountMethodMismatchException>(
                () => CollectionAccountSelector.Validate(PaymentMethod.Cash, retiredBank, anyEligible: true));
        }

        [Fact]
        public void An_empty_catalogue_offers_nothing_and_refuses_nothing()
        {
            var none = new List<CollectionAccount>();

            Assert.Empty(CollectionAccountSelector.Eligible(none, PaymentMethod.Cash));
            CollectionAccountSelector.Validate(PaymentMethod.Cash, chosen: null, anyEligible: false);
        }
    }
}
