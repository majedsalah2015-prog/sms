using Sms.Application.Messaging;
using Sms.Domain.Messaging;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Messaging
{
    public class AnnouncementApprovalGateTests
    {
        [Theory]
        [InlineData(AudienceScope.Grade, true)]
        [InlineData(AudienceScope.Stage, true)]
        [InlineData(AudienceScope.SchoolWide, true)]
        [InlineData(AudienceScope.Section, false)]
        [BusinessRule("BR-MSG-001")]
        public void RequiresApproval_matches_the_doc_scope_split(AudienceScope scope, bool expected)
        {
            Assert.Equal(expected, AnnouncementApprovalGate.RequiresApproval(scope));
        }
    }
}
