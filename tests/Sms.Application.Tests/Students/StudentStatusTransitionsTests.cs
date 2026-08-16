using Sms.Application.Students;
using Sms.Domain.Students;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Students
{
    public class StudentStatusTransitionsTests
    {
        [Theory]
        [InlineData(StudentStatus.Enrolled, StudentStatus.Suspended)]
        [InlineData(StudentStatus.Suspended, StudentStatus.Enrolled)]
        [InlineData(StudentStatus.Enrolled, StudentStatus.Withdrawn)]
        [InlineData(StudentStatus.Enrolled, StudentStatus.Graduated)]
        [InlineData(StudentStatus.Enrolled, StudentStatus.Transferred)]
        [InlineData(StudentStatus.Graduated, StudentStatus.Alumni)]
        [InlineData(StudentStatus.Withdrawn, StudentStatus.Enrolled)]
        [BusinessRule("BR-STU-002")]
        public void Legal_moves_are_allowed(StudentStatus from, StudentStatus to)
        {
            Assert.True(StudentStatusTransitions.CanTransition(from, to));
        }

        [Theory]
        [InlineData(StudentStatus.Alumni, StudentStatus.Enrolled)]
        [InlineData(StudentStatus.Transferred, StudentStatus.Enrolled)]
        [InlineData(StudentStatus.Graduated, StudentStatus.Enrolled)]
        [InlineData(StudentStatus.Withdrawn, StudentStatus.Graduated)]
        [BusinessRule("BR-STU-002")]
        public void Illegal_moves_are_rejected(StudentStatus from, StudentStatus to)
        {
            Assert.False(StudentStatusTransitions.CanTransition(from, to));
        }

        [Fact]
        [BusinessRule("BR-STU-007")]
        public void Readmission_moves_a_withdrawn_student_back_to_enrolled()
        {
            Assert.True(StudentStatusTransitions.CanTransition(StudentStatus.Withdrawn, StudentStatus.Enrolled));
        }
    }
}
