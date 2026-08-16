using System.Collections.Generic;
using Sms.Application.Portal;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Portal
{
    public class PortalAccessEvaluatorTests
    {
        [Fact]
        [BusinessRule("BR-SEC-011")]
        public void A_student_viewing_their_own_record_is_allowed()
        {
            Assert.True(PortalAccessEvaluator.CanView(studentId: 5, studentOwnUserAccountId: 42, requestingUserAccountId: 42, guardianVisibleStudentIds: new List<int>()));
        }

        [Fact]
        [BusinessRule("BR-SEC-011")]
        public void A_parent_viewing_a_visible_child_is_allowed()
        {
            Assert.True(PortalAccessEvaluator.CanView(studentId: 5, studentOwnUserAccountId: null, requestingUserAccountId: 42, guardianVisibleStudentIds: new List<int> { 5 }));
        }

        [Fact]
        [BusinessRule("BR-SEC-011")]
        public void An_unrelated_account_is_denied()
        {
            Assert.False(PortalAccessEvaluator.CanView(studentId: 5, studentOwnUserAccountId: 99, requestingUserAccountId: 42, guardianVisibleStudentIds: new List<int> { 6 }));
        }
    }
}
