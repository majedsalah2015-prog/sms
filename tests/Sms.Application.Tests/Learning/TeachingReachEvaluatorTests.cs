using System;
using System.Collections.Generic;
using Sms.Application.Learning;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Learning
{
    /// <summary>BR-LRN-002 — who may put content in front of which students.</summary>
    public class TeachingReachEvaluatorTests
    {
        private static readonly PlacementReach[] TeachesMathIn3A = { new(curriculumOfferingId: 10, sectionId: 100) };

        [Fact]
        [BusinessRule("BR-LRN-002")]
        public void A_teacher_reaches_an_offering_they_hold_a_placement_on()
        {
            Assert.True(TeachingReachEvaluator.CanAuthorContent(
                TeachesMathIn3A, Array.Empty<int>(), hasSchoolWideReach: false, curriculumOfferingId: 10));
        }

        [Fact]
        [BusinessRule("BR-LRN-002")]
        public void A_teacher_does_not_reach_an_offering_they_do_not_teach()
        {
            Assert.False(TeachingReachEvaluator.CanAuthorContent(
                TeachesMathIn3A, Array.Empty<int>(), hasSchoolWideReach: false, curriculumOfferingId: 11));
        }

        [Fact]
        [BusinessRule("BR-LRN-002")]
        public void A_head_of_department_reaches_every_offering_in_the_department()
        {
            Assert.True(TeachingReachEvaluator.CanAuthorContent(
                Array.Empty<PlacementReach>(), new[] { 11, 12 }, hasSchoolWideReach: false, curriculumOfferingId: 12));
        }

        [Fact]
        [BusinessRule("BR-LRN-002")]
        public void An_empty_placement_list_grants_nothing()
        {
            // The rule that must not invert: no placements means NO reach. An
            // absent list read as "unrestricted" is exactly the bug BR-LRN-002's
            // "no all-sections path below Vice-Principal" exists to prevent.
            Assert.False(TeachingReachEvaluator.CanAuthorContent(
                Array.Empty<PlacementReach>(), Array.Empty<int>(), hasSchoolWideReach: false, curriculumOfferingId: 10));
        }

        [Fact]
        [BusinessRule("BR-LRN-002")]
        public void A_null_placement_list_grants_nothing()
        {
            Assert.False(TeachingReachEvaluator.CanAuthorContent(
                null, null, hasSchoolWideReach: false, curriculumOfferingId: 10));
        }

        [Fact]
        [BusinessRule("BR-LRN-002")]
        public void School_wide_reach_is_passed_in_explicitly_never_inferred()
        {
            Assert.True(TeachingReachEvaluator.CanAuthorContent(
                Array.Empty<PlacementReach>(), Array.Empty<int>(), hasSchoolWideReach: true, curriculumOfferingId: 10));
        }

        [Fact]
        [BusinessRule("BR-LRN-002")]
        public void Issuing_to_a_section_needs_the_pair_not_just_the_offering()
        {
            // Teaching Math in 3-A does not license setting work for 3-B.
            Assert.True(TeachingReachEvaluator.CanIssueToSection(
                TeachesMathIn3A, Array.Empty<int>(), false, curriculumOfferingId: 10, sectionId: 100));

            Assert.False(TeachingReachEvaluator.CanIssueToSection(
                TeachesMathIn3A, Array.Empty<int>(), false, curriculumOfferingId: 10, sectionId: 101));
        }

        [Fact]
        [BusinessRule("BR-LRN-002")]
        public void A_head_of_department_may_issue_to_any_section_of_their_offerings()
        {
            Assert.True(TeachingReachEvaluator.CanIssueToSection(
                Array.Empty<PlacementReach>(), new[] { 10 }, false, curriculumOfferingId: 10, sectionId: 101));
        }

        [Fact]
        [BusinessRule("BR-LRN-002")]
        public void Reach_over_one_offering_does_not_leak_to_another_section_pair()
        {
            var placements = new List<PlacementReach>
            {
                new(curriculumOfferingId: 10, sectionId: 100),
                new(curriculumOfferingId: 20, sectionId: 200),
            };

            Assert.False(TeachingReachEvaluator.CanIssueToSection(
                placements, Array.Empty<int>(), false, curriculumOfferingId: 10, sectionId: 200));
        }
    }
}
