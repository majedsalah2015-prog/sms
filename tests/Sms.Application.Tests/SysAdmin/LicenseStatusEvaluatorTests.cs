using System;
using Sms.Application.SysAdmin;
using Sms.Domain.SysAdmin;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.SysAdmin
{
    public class LicenseStatusEvaluatorTests
    {
        private static readonly DateTime ExpiresAtUtc = new(2027, 6, 30, 0, 0, 0, DateTimeKind.Utc);

        [Fact]
        [BusinessRule("BR-SYS-006")]
        public void Active_before_expiry()
        {
            var status = LicenseStatusEvaluator.ComputeStatus(ExpiresAtUtc.AddDays(-1), ExpiresAtUtc, graceDays: 30);

            Assert.Equal(LicenseStatus.Active, status);
        }

        [Fact]
        [BusinessRule("BR-SYS-006")]
        public void Grace_after_expiry_within_the_grace_window()
        {
            var status = LicenseStatusEvaluator.ComputeStatus(ExpiresAtUtc.AddDays(15), ExpiresAtUtc, graceDays: 30);

            Assert.Equal(LicenseStatus.Grace, status);
        }

        [Fact]
        [BusinessRule("BR-SYS-006")]
        public void ReadOnly_once_the_grace_window_elapses()
        {
            var status = LicenseStatusEvaluator.ComputeStatus(ExpiresAtUtc.AddDays(31), ExpiresAtUtc, graceDays: 30);

            Assert.Equal(LicenseStatus.ReadOnly, status);
        }
    }
}
