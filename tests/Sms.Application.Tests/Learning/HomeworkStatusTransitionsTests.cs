using System;
using System.Linq;
using Sms.Application.Learning;
using Sms.Domain.Learning;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Learning
{
    /// <summary>
    /// doc/Modules/37 §4 homework lifecycle, BR-LRN-003/012/016.
    /// </summary>
    public class HomeworkStatusTransitionsTests
    {
        [Theory]
        [InlineData(HomeworkStatus.Draft, HomeworkStatus.Issued)]
        [InlineData(HomeworkStatus.Draft, HomeworkStatus.Withdrawn)]
        [InlineData(HomeworkStatus.Issued, HomeworkStatus.Collecting)]
        [InlineData(HomeworkStatus.Issued, HomeworkStatus.Marking)]
        [InlineData(HomeworkStatus.Issued, HomeworkStatus.Withdrawn)]
        [InlineData(HomeworkStatus.Collecting, HomeworkStatus.Marking)]
        [InlineData(HomeworkStatus.Collecting, HomeworkStatus.Withdrawn)]
        [InlineData(HomeworkStatus.Marking, HomeworkStatus.Released)]
        [InlineData(HomeworkStatus.Marking, HomeworkStatus.Withdrawn)]
        public void The_documented_lifecycle_edges_are_allowed(HomeworkStatus from, HomeworkStatus to)
        {
            Assert.True(HomeworkStatusTransitions.CanTransition(from, to));
        }

        [Fact]
        [BusinessRule("BR-LRN-003")]
        public void There_is_no_un_issue()
        {
            // BR-LRN-003 makes issue the event families see. Work a class wrote
            // down on Sunday does not become a draft again on Monday - it is
            // withdrawn with a reason they are told.
            Assert.False(HomeworkStatusTransitions.CanTransition(HomeworkStatus.Issued, HomeworkStatus.Draft));
            Assert.False(HomeworkStatusTransitions.CanTransition(HomeworkStatus.Collecting, HomeworkStatus.Draft));
            Assert.False(HomeworkStatusTransitions.CanTransition(HomeworkStatus.Marking, HomeworkStatus.Draft));
        }

        [Fact]
        [BusinessRule("BR-LRN-012")]
        public void Nothing_moves_out_of_released_because_the_mark_belongs_to_module_17()
        {
            var everyStatus = Enum.GetValues(typeof(HomeworkStatus)).Cast<HomeworkStatus>();

            Assert.All(everyStatus, to =>
                Assert.False(HomeworkStatusTransitions.CanTransition(HomeworkStatus.Released, to)));
        }

        [Fact]
        [BusinessRule("BR-LRN-016")]
        public void Withdrawn_is_terminal()
        {
            var everyStatus = Enum.GetValues(typeof(HomeworkStatus)).Cast<HomeworkStatus>();

            Assert.All(everyStatus, to =>
                Assert.False(HomeworkStatusTransitions.CanTransition(HomeworkStatus.Withdrawn, to)));
        }

        [Fact]
        public void A_status_never_transitions_to_itself()
        {
            var everyStatus = Enum.GetValues(typeof(HomeworkStatus)).Cast<HomeworkStatus>();

            Assert.All(everyStatus, s =>
                Assert.False(HomeworkStatusTransitions.CanTransition(s, s)));
        }

        [Fact]
        [BusinessRule("BR-LRN-003")]
        public void A_draft_is_invisible_in_the_portal_and_a_withdrawal_stops_being_visible()
        {
            // BR-GLB-031 / BR-SEC-012: the portal shows finished work only.
            Assert.False(HomeworkStatusTransitions.IsVisibleToPortal(HomeworkStatus.Draft));
            Assert.False(HomeworkStatusTransitions.IsVisibleToPortal(HomeworkStatus.Withdrawn));

            Assert.True(HomeworkStatusTransitions.IsVisibleToPortal(HomeworkStatus.Issued));
            Assert.True(HomeworkStatusTransitions.IsVisibleToPortal(HomeworkStatus.Collecting));
            Assert.True(HomeworkStatusTransitions.IsVisibleToPortal(HomeworkStatus.Marking));
            Assert.True(HomeworkStatusTransitions.IsVisibleToPortal(HomeworkStatus.Released));
        }

        [Fact]
        [BusinessRule("BR-LRN-005")]
        public void Work_is_still_accepted_while_it_is_issued_or_collecting()
        {
            // BR-LRN-005: lateness is flagged and penalised, never a closed door,
            // so acceptance turns on status rather than on the clock.
            Assert.True(HomeworkStatusTransitions.AcceptsSubmissions(HomeworkStatus.Issued));
            Assert.True(HomeworkStatusTransitions.AcceptsSubmissions(HomeworkStatus.Collecting));

            Assert.False(HomeworkStatusTransitions.AcceptsSubmissions(HomeworkStatus.Draft));
            Assert.False(HomeworkStatusTransitions.AcceptsSubmissions(HomeworkStatus.Marking));
            Assert.False(HomeworkStatusTransitions.AcceptsSubmissions(HomeworkStatus.Released));
            Assert.False(HomeworkStatusTransitions.AcceptsSubmissions(HomeworkStatus.Withdrawn));
        }
    }
}
