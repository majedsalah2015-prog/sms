using Sms.Application.Activities;
using Sms.Domain.Activities;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Activities
{
    public class ProgramEnrollmentStatusTransitionsTests
    {
        [Theory]
        [InlineData(ProgramEnrollmentStatus.Requested, ProgramEnrollmentStatus.Waitlisted)]
        [InlineData(ProgramEnrollmentStatus.Requested, ProgramEnrollmentStatus.ConsentPending)]
        [InlineData(ProgramEnrollmentStatus.Requested, ProgramEnrollmentStatus.Active)]
        [InlineData(ProgramEnrollmentStatus.ConsentPending, ProgramEnrollmentStatus.Active)]
        [InlineData(ProgramEnrollmentStatus.ConsentPending, ProgramEnrollmentStatus.Waitlisted)]
        [InlineData(ProgramEnrollmentStatus.Waitlisted, ProgramEnrollmentStatus.Active)]
        [InlineData(ProgramEnrollmentStatus.Active, ProgramEnrollmentStatus.Withdrawn)]
        [BusinessRule("BR-ACT-002")]
        public void Legal_moves_are_allowed(ProgramEnrollmentStatus from, ProgramEnrollmentStatus to)
        {
            Assert.True(ProgramEnrollmentStatusTransitions.CanTransition(from, to));
        }

        [Theory]
        [InlineData(ProgramEnrollmentStatus.Withdrawn, ProgramEnrollmentStatus.Active)]
        [InlineData(ProgramEnrollmentStatus.Active, ProgramEnrollmentStatus.Waitlisted)]
        [BusinessRule("BR-ACT-002")]
        public void Illegal_moves_are_rejected(ProgramEnrollmentStatus from, ProgramEnrollmentStatus to)
        {
            Assert.False(ProgramEnrollmentStatusTransitions.CanTransition(from, to));
        }
    }
}
