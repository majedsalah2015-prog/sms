using Sms.Application.Classrooms;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Classrooms
{
    public class RoomCapacityValidatorTests
    {
        [Fact]
        [BusinessRule("BR-ROM-002")]
        public void Exam_capacity_equal_to_standard_is_valid()
        {
            Assert.True(RoomCapacityValidator.IsValidCapacity(standardCapacity: 30, examCapacity: 30));
        }

        [Fact]
        [BusinessRule("BR-ROM-002")]
        public void Exam_capacity_below_standard_is_valid()
        {
            Assert.True(RoomCapacityValidator.IsValidCapacity(standardCapacity: 30, examCapacity: 20));
        }

        [Fact]
        [BusinessRule("BR-ROM-002")]
        public void Exam_capacity_above_standard_is_invalid()
        {
            Assert.False(RoomCapacityValidator.IsValidCapacity(standardCapacity: 30, examCapacity: 31));
        }
    }
}
