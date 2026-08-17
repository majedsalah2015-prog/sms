using System;
using Sms.Application.Discipline;
using Sms.Domain.Discipline;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Discipline
{
    public class DisciplineEnginesTests
    {
        [Fact]
        [BusinessRule("BR-DCP-005")]
        public void The_ladder_proposes_by_repetition_and_caps_at_the_highest_defined_step()
        {
            var ladder = new[]
            {
                new RepetitionEscalationEvaluator.Step(2, 1, 10), new RepetitionEscalationEvaluator.Step(2, 2, 20), new RepetitionEscalationEvaluator.Step(2, 3, 30),
            };

            Assert.Equal(10, RepetitionEscalationEvaluator.Propose(2, priorSameSeverityCount: 0, ladder));
            Assert.Equal(20, RepetitionEscalationEvaluator.Propose(2, 1, ladder));
            Assert.Equal(30, RepetitionEscalationEvaluator.Propose(2, 7, ladder));
            Assert.Null(RepetitionEscalationEvaluator.Propose(3, 0, ladder));
        }

        [Fact]
        [BusinessRule("BR-DCP-005")]
        public void Deviation_below_needs_a_reason_and_above_or_suspension_class_needs_the_principal()
        {
            Assert.Equal(new DecisionPolicy.Check(false, false), DecisionPolicy.Evaluate(proposedRank: 2, decidedRank: 2, false, false));
            Assert.Equal(new DecisionPolicy.Check(true, false), DecisionPolicy.Evaluate(2, 1, false, false));
            Assert.Equal(new DecisionPolicy.Check(false, true), DecisionPolicy.Evaluate(2, 3, false, false));
            Assert.Equal(new DecisionPolicy.Check(false, true), DecisionPolicy.Evaluate(2, 2, decidedIsSuspensionClass: true, false));
            Assert.Equal(new DecisionPolicy.Check(false, true), DecisionPolicy.Evaluate(null, 1, false, caseRequiresPrincipal: true));
        }

        [Fact]
        [BusinessRule("BR-DCP-003")]
        public void Statements_are_mandatory_from_severity_three()
        {
            Assert.True(DueProcessPolicy.StatementsSatisfied(2, Array.Empty<StatementKind>()));
            Assert.False(DueProcessPolicy.StatementsSatisfied(3, new[] { StatementKind.Witness }));
            Assert.True(DueProcessPolicy.StatementsSatisfied(3, new[] { StatementKind.Parent }));
        }

        [Fact]
        [BusinessRule("BR-DCP-006")]
        public void Appeals_need_severity_two_within_window_once_and_an_independent_reviewer()
        {
            var decided = new DateTime(2026, 10, 1);
            Assert.True(AppealPolicy.CanFile(2, decided, decided.AddDays(7), 7, alreadyAppealed: false));
            Assert.False(AppealPolicy.CanFile(2, decided, decided.AddDays(8), 7, false));
            Assert.False(AppealPolicy.CanFile(1, decided, decided, 7, false));
            Assert.False(AppealPolicy.CanFile(3, decided, decided, 7, alreadyAppealed: true));
            Assert.False(AppealPolicy.IsIndependentReviewer(5, deciderUserId: 5));
            Assert.True(AppealPolicy.IsIndependentReviewer(6, 5));
        }

        [Fact]
        [BusinessRule("BR-DCP-007")]
        public void Points_aggregate_per_source_and_flags_and_grade_derive_from_thresholds()
        {
            var totals = PointsAggregator.Aggregate(new[] { (PointSource.Violation, -5), (PointSource.Violation, -20), (PointSource.Merit, 30) });

            Assert.Equal(25, totals.ViolationPoints);
            Assert.Equal(30, totals.MeritPoints);
            Assert.Equal(5, totals.Net);
            Assert.Equal(new PointsAggregator.Flags(true, true), PointsAggregator.Evaluate(totals, welfareReviewThreshold: 20, honorListThreshold: 30));
            var bands = new[] { (-100, "D"), (0, "C"), (10, "B"), (25, "A") };
            Assert.Equal("C", PointsAggregator.BehaviorGrade(5, bands));
            Assert.Equal("A", PointsAggregator.BehaviorGrade(40, bands));
            Assert.Equal("D", PointsAggregator.BehaviorGrade(-3, bands));
        }

        [Fact]
        [BusinessRule("BR-DCP-008")]
        public void Portal_visibility_filters_by_level_and_hides_the_narrative_below_full()
        {
            Assert.True(PortalVisibilityPolicy.IsVisible(PortalVisibilityLevel.Full, hasDecision: false, isSummons: false));
            Assert.False(PortalVisibilityPolicy.IsVisible(PortalVisibilityLevel.DecisionsOnly, false, false));
            Assert.True(PortalVisibilityPolicy.IsVisible(PortalVisibilityLevel.DecisionsOnly, true, false));
            Assert.False(PortalVisibilityPolicy.IsVisible(PortalVisibilityLevel.SummonsOnly, true, false));
            Assert.Null(PortalVisibilityPolicy.Project(PortalVisibilityLevel.DecisionsOnly, "INC-1", DateTime.UtcNow, 2, "narrative", "3.2", "Detention").Narrative);
            Assert.Equal("narrative", PortalVisibilityPolicy.Project(PortalVisibilityLevel.Full, "INC-1", DateTime.UtcNow, 2, "narrative", null, null).Narrative);
        }

        [Fact]
        [BusinessRule("BR-DCP-004")]
        public void Suspension_cap_only_binds_when_configured()
        {
            Assert.True(SuspensionLimitPolicy.IsWithinCap(10, null));
            Assert.True(SuspensionLimitPolicy.IsWithinCap(3, 3));
            Assert.False(SuspensionLimitPolicy.IsWithinCap(4, 3));
        }
    }
}
