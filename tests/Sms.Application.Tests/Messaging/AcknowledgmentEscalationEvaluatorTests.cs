using System;
using Sms.Application.Messaging;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Messaging
{
    public class AcknowledgmentEscalationEvaluatorTests
    {
        [Fact]
        [BusinessRule("BR-MSG-004")]
        public void Not_yet_overdue_before_the_escalation_window()
        {
            var issued = new DateTime(2027, 1, 1);
            var now = issued.AddDays(4);

            Assert.False(AcknowledgmentEscalationEvaluator.IsOverdue(issued, now, escalationDays: 5));
        }

        [Fact]
        [BusinessRule("BR-MSG-004")]
        public void Overdue_once_the_window_elapses()
        {
            var issued = new DateTime(2027, 1, 1);
            var now = issued.AddDays(5);

            Assert.True(AcknowledgmentEscalationEvaluator.IsOverdue(issued, now, escalationDays: 5));
        }
    }
}
