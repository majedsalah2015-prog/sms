using Sms.Application.Examinations;
using Sms.Domain.Examinations;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Examinations
{
    public class ExamRoundStatusTransitionsTests
    {
        [Theory]
        [InlineData(ExamRoundStatus.Draft, ExamRoundStatus.Validated)]
        [InlineData(ExamRoundStatus.Validated, ExamRoundStatus.Published)]
        [InlineData(ExamRoundStatus.Validated, ExamRoundStatus.Draft)]
        [BusinessRule("BR-EXM-003")]
        public void Legal_moves_are_allowed(ExamRoundStatus from, ExamRoundStatus to)
        {
            Assert.True(ExamRoundStatusTransitions.CanTransition(from, to));
        }

        [Theory]
        [InlineData(ExamRoundStatus.Draft, ExamRoundStatus.Published)]
        [InlineData(ExamRoundStatus.Published, ExamRoundStatus.Draft)]
        [BusinessRule("BR-EXM-003")]
        public void Illegal_moves_are_rejected(ExamRoundStatus from, ExamRoundStatus to)
        {
            Assert.False(ExamRoundStatusTransitions.CanTransition(from, to));
        }
    }
}
