using Sms.Application.Learning;
using Sms.Domain.Learning;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Learning
{
    /// <summary>
    /// BR-LRN-011/012 (doc/Modules/37 §4, §8.5): what must be true before a
    /// homework's marks may be handed to Module 17.
    /// </summary>
    public class HomeworkReleaseGateTests
    {
        private const int AComponent = 7;

        [Theory]
        [BusinessRule("BR-LRN-012")]
        [InlineData(HomeworkStatus.Draft)]
        [InlineData(HomeworkStatus.Issued)]
        [InlineData(HomeworkStatus.Collecting)]
        [InlineData(HomeworkStatus.Released)]
        [InlineData(HomeworkStatus.Withdrawn)]
        public void Release_is_the_step_out_of_marking_and_nowhere_else(HomeworkStatus status)
        {
            // Released is refused here as well as by the status table: releasing
            // twice would be a mark CHANGE, which BR-LRN-012 puts under Module
            // 17's change control rather than a second release from this module.
            var refusal = HomeworkReleaseGate.Check(status, maxMarks: 20m, AComponent, unscoredSubmissionCount: 0);

            Assert.Equal(HomeworkReleaseRefusal.NotBeingMarked, refusal);
        }

        [Fact]
        [BusinessRule("BR-LRN-012")]
        public void The_lifecycle_is_checked_before_anything_else()
        {
            // An Issued homework with unscored work is refused for being Issued,
            // not for the unscored count: telling a teacher to finish marking work
            // whose class is still doing it sends them to the wrong screen.
            var refusal = HomeworkReleaseGate.Check(
                HomeworkStatus.Issued, maxMarks: null, blueprintComponentId: null, unscoredSubmissionCount: 4);

            Assert.Equal(HomeworkReleaseRefusal.NotBeingMarked, refusal);
        }

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void Ungraded_practice_has_nothing_to_release()
        {
            // BR-LRN-004: ungraded practice never reaches Module 17. For it,
            // marking with feedback IS the finished state.
            var refusal = HomeworkReleaseGate.Check(
                HomeworkStatus.Marking, maxMarks: null, blueprintComponentId: null, unscoredSubmissionCount: 0);

            Assert.Equal(HomeworkReleaseRefusal.UngradedPractice, refusal);
        }

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void Zero_max_marks_is_ungraded_practice_too()
        {
            // IsGraded is "> 0", not "not null" - a homework out of zero has no
            // scale for Module 17 to receive.
            var refusal = HomeworkReleaseGate.Check(
                HomeworkStatus.Marking, maxMarks: 0m, AComponent, unscoredSubmissionCount: 0);

            Assert.Equal(HomeworkReleaseRefusal.UngradedPractice, refusal);
        }

        [Fact]
        [BusinessRule("BR-LRN-012")]
        public void Graded_work_naming_no_component_has_nowhere_to_land()
        {
            // The issue gate already refuses this, so reaching it means the
            // component was cleared after issue.
            var refusal = HomeworkReleaseGate.Check(
                HomeworkStatus.Marking, maxMarks: 20m, blueprintComponentId: null, unscoredSubmissionCount: 0);

            Assert.Equal(HomeworkReleaseRefusal.NoBlueprintComponent, refusal);
        }

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void One_unscored_hand_in_blocks_the_whole_release()
        {
            // Releasing half a class publishes a mark of nothing for the other
            // half, and Module 17 cannot tell "scored zero" from "not looked at".
            var refusal = HomeworkReleaseGate.Check(
                HomeworkStatus.Marking, maxMarks: 20m, AComponent, unscoredSubmissionCount: 1);

            Assert.Equal(HomeworkReleaseRefusal.SubmissionsUnscored, refusal);
        }

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void A_fully_marked_graded_homework_releases()
        {
            var refusal = HomeworkReleaseGate.Check(
                HomeworkStatus.Marking, maxMarks: 20m, AComponent, unscoredSubmissionCount: 0);

            Assert.Equal(HomeworkReleaseRefusal.None, refusal);
        }

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void A_homework_nobody_handed_in_still_releases()
        {
            // Deliberately not a refusal. The alternative strands the row in
            // Marking for ever with no action that can move it, and "nobody handed
            // anything in" is a real outcome a teacher must be able to close.
            var refusal = HomeworkReleaseGate.Check(
                HomeworkStatus.Marking, maxMarks: 20m, AComponent, unscoredSubmissionCount: 0);

            Assert.Equal(HomeworkReleaseRefusal.None, refusal);
        }
    }
}
