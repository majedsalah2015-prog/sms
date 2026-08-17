using System;
using Sms.Application.Installments;
using Sms.Domain.Installments;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Installments
{
    public class InstallmentStatusDeriverTests
    {
        private static readonly DateTime Due = new(2027, 1, 10);

        [Theory]
        [InlineData("2027-01-01", 0, InstallmentStatus.Scheduled)]
        [InlineData("2027-01-10", 0, InstallmentStatus.Due)]
        [InlineData("2027-01-13", 0, InstallmentStatus.Due)]
        [InlineData("2027-01-14", 0, InstallmentStatus.Overdue)]
        [InlineData("2027-01-14", 1000, InstallmentStatus.Paid)]
        [InlineData("2027-01-14", 300, InstallmentStatus.PartiallyPaid)]
        [InlineData("2027-01-01", 300, InstallmentStatus.Scheduled)]
        [BusinessRule("BR-INS-007")]
        public void Status_derives_from_paid_amount_and_dates(string today, decimal paid, InstallmentStatus expected)
        {
            var status = InstallmentStatusDeriver.Derive(1000m, paid, Due, graceDays: 3, DateTime.Parse(today), isSuperseded: false, isWrittenOff: false);

            Assert.Equal(expected, status);
        }

        [Fact]
        [BusinessRule("BR-INS-007")]
        public void Terminal_flags_win_over_everything()
        {
            Assert.Equal(InstallmentStatus.Rescheduled, InstallmentStatusDeriver.Derive(1000m, 1000m, Due, 0, Due, isSuperseded: true, isWrittenOff: false));
            Assert.Equal(InstallmentStatus.WrittenOff, InstallmentStatusDeriver.Derive(1000m, 0m, Due, 0, Due, isSuperseded: false, isWrittenOff: true));
        }

        [Fact]
        [BusinessRule("BR-INS-008")]
        public void Truly_overdue_needs_grace_elapsed_and_money_outstanding()
        {
            Assert.False(InstallmentStatusDeriver.IsTrulyOverdue(1000m, 0m, Due, 3, new DateTime(2027, 1, 13), false, false));
            Assert.True(InstallmentStatusDeriver.IsTrulyOverdue(1000m, 0m, Due, 3, new DateTime(2027, 1, 14), false, false));
            Assert.True(InstallmentStatusDeriver.IsTrulyOverdue(1000m, 500m, Due, 3, new DateTime(2027, 1, 14), false, false));
            Assert.False(InstallmentStatusDeriver.IsTrulyOverdue(1000m, 1000m, Due, 3, new DateTime(2027, 2, 14), false, false));
        }
    }
}
