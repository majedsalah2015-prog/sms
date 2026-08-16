using Sms.Application.Admissions;
using Sms.Domain.Admissions;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Admissions
{
    public class ApplicationStatusTransitionsTests
    {
        [Theory]
        [InlineData(ApplicationStatus.Draft, ApplicationStatus.Submitted)]
        [InlineData(ApplicationStatus.Submitted, ApplicationStatus.UnderReview)]
        [InlineData(ApplicationStatus.UnderReview, ApplicationStatus.Recommended)]
        [InlineData(ApplicationStatus.UnderReview, ApplicationStatus.Rejected)]
        [InlineData(ApplicationStatus.Recommended, ApplicationStatus.Approved)]
        [InlineData(ApplicationStatus.Recommended, ApplicationStatus.Rejected)]
        [InlineData(ApplicationStatus.Recommended, ApplicationStatus.Waitlisted)]
        [InlineData(ApplicationStatus.Waitlisted, ApplicationStatus.Approved)]
        [InlineData(ApplicationStatus.Waitlisted, ApplicationStatus.Rejected)]
        [InlineData(ApplicationStatus.Waitlisted, ApplicationStatus.Lapsed)]
        [InlineData(ApplicationStatus.Approved, ApplicationStatus.Registered)]
        [InlineData(ApplicationStatus.Approved, ApplicationStatus.Lapsed)]
        [BusinessRule("BR-ADM-005")]
        public void Legal_moves_are_allowed(ApplicationStatus from, ApplicationStatus to)
        {
            Assert.True(ApplicationStatusTransitions.CanTransition(from, to));
        }

        [Theory]
        [InlineData(ApplicationStatus.Draft, ApplicationStatus.Approved)]
        [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Approved)]
        [InlineData(ApplicationStatus.Rejected, ApplicationStatus.Submitted)]
        [InlineData(ApplicationStatus.Registered, ApplicationStatus.Draft)]
        [InlineData(ApplicationStatus.Lapsed, ApplicationStatus.Approved)]
        [InlineData(ApplicationStatus.UnderReview, ApplicationStatus.Approved)]
        [BusinessRule("BR-ADM-005")]
        public void Illegal_moves_are_rejected(ApplicationStatus from, ApplicationStatus to)
        {
            Assert.False(ApplicationStatusTransitions.CanTransition(from, to));
        }
    }
}
