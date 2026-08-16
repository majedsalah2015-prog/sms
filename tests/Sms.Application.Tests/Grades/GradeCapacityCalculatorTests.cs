using Sms.Application.Grades;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Grades
{
    public class GradeCapacityCalculatorTests
    {
        [Fact]
        [BusinessRule("BR-GRD-006")]
        public void Planned_seats_is_sections_times_section_size()
        {
            Assert.Equal(75, GradeCapacityCalculator.PlannedSeats(targetSections: 3, targetSectionSize: 25));
        }
    }
}
