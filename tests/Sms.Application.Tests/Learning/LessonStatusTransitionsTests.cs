using Sms.Application.Learning;
using Sms.Domain.Learning;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Learning
{
    /// <summary>doc/Modules/37 §4 content spine — BR-LRN-003 publication gate, BR-LRN-016 no-delete.</summary>
    public class LessonStatusTransitionsTests
    {
        [Fact]
        [BusinessRule("BR-LRN-003")]
        public void A_draft_may_be_published()
        {
            Assert.True(LessonStatusTransitions.CanTransition(LessonStatus.Draft, LessonStatus.Published));
        }

        [Fact]
        [BusinessRule("BR-LRN-016")]
        public void Published_content_leaves_the_portal_by_being_retired()
        {
            Assert.True(LessonStatusTransitions.CanTransition(LessonStatus.Published, LessonStatus.Retired));
        }

        [Fact]
        [BusinessRule("BR-LRN-016")]
        public void A_draft_may_be_retired_without_ever_being_published()
        {
            Assert.True(LessonStatusTransitions.CanTransition(LessonStatus.Draft, LessonStatus.Retired));
        }

        [Fact]
        [BusinessRule("BR-LRN-003")]
        public void There_is_no_un_publish()
        {
            // The refusal that matters: un-publishing would let a lesson a student
            // read on Sunday vanish on Monday with no reason recorded. Retirement
            // is the only exit, and it states why.
            Assert.False(LessonStatusTransitions.CanTransition(LessonStatus.Published, LessonStatus.Draft));
        }

        [Theory]
        [BusinessRule("BR-LRN-016")]
        [InlineData(LessonStatus.Draft)]
        [InlineData(LessonStatus.Published)]
        public void Retired_is_terminal(LessonStatus to)
        {
            Assert.False(LessonStatusTransitions.CanTransition(LessonStatus.Retired, to));
        }

        [Fact]
        [BusinessRule("BR-LRN-003")]
        public void Only_published_content_is_visible_to_the_portal()
        {
            Assert.True(LessonStatusTransitions.IsVisibleToPortal(LessonStatus.Published));
            Assert.False(LessonStatusTransitions.IsVisibleToPortal(LessonStatus.Draft));
            Assert.False(LessonStatusTransitions.IsVisibleToPortal(LessonStatus.Retired));
        }
    }
}
