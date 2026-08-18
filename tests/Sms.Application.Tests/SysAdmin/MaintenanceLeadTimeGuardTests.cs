using System;
using Sms.Application.SysAdmin;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.SysAdmin
{
    public class MaintenanceLeadTimeGuardTests
    {
        private static readonly DateTime Now = new(2027, 3, 1, 8, 0, 0, DateTimeKind.Utc);

        [Fact]
        [BusinessRule("BR-SYS-007")]
        public void Sufficient_lead_time_is_allowed()
        {
            Assert.True(MaintenanceLeadTimeGuard.HasSufficientLeadTime(Now, Now.AddDays(3), TimeSpan.FromDays(2), isEmergency: false));
        }

        [Fact]
        [BusinessRule("BR-SYS-007")]
        public void Insufficient_lead_time_is_rejected_unless_emergency()
        {
            Assert.False(MaintenanceLeadTimeGuard.HasSufficientLeadTime(Now, Now.AddHours(1), TimeSpan.FromDays(2), isEmergency: false));
            Assert.True(MaintenanceLeadTimeGuard.HasSufficientLeadTime(Now, Now.AddHours(1), TimeSpan.FromDays(2), isEmergency: true));
        }
    }
}
