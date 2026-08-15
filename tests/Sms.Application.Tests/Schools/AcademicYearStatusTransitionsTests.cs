using Sms.Application.Schools;
using Sms.Domain.Schools;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Schools
{
    public class AcademicYearStatusTransitionsTests
    {
        [Theory]
        [InlineData(AcademicYearStatus.Preparation, AcademicYearStatus.Active)]
        [InlineData(AcademicYearStatus.Active, AcademicYearStatus.Closing)]
        [InlineData(AcademicYearStatus.Closing, AcademicYearStatus.Closed)]
        [InlineData(AcademicYearStatus.Closed, AcademicYearStatus.Archived)]
        [BusinessRule("BR-AYR-002")]
        public void Legal_moves_are_allowed(AcademicYearStatus from, AcademicYearStatus to)
        {
            Assert.True(AcademicYearStatusTransitions.CanTransition(from, to));
        }

        [Theory]
        [InlineData(AcademicYearStatus.Preparation, AcademicYearStatus.Closing)]
        [InlineData(AcademicYearStatus.Preparation, AcademicYearStatus.Closed)]
        [InlineData(AcademicYearStatus.Active, AcademicYearStatus.Archived)]
        [InlineData(AcademicYearStatus.Archived, AcademicYearStatus.Active)]
        [InlineData(AcademicYearStatus.Closed, AcademicYearStatus.Preparation)]
        [BusinessRule("BR-AYR-002")]
        public void Illegal_moves_are_rejected(AcademicYearStatus from, AcademicYearStatus to)
        {
            Assert.False(AcademicYearStatusTransitions.CanTransition(from, to));
        }

        [Fact]
        [BusinessRule("BR-AYR-006")]
        public void Archived_is_terminal()
        {
            foreach (AcademicYearStatus target in System.Enum.GetValues(typeof(AcademicYearStatus)))
            {
                Assert.False(AcademicYearStatusTransitions.CanTransition(AcademicYearStatus.Archived, target));
            }
        }
    }
}
