using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Domain.Discipline;

namespace Sms.Application.Discipline
{
    /// <summary>Pure BR-DCP-003 WF-11 spine.</summary>
    public static class CaseStatusTransitions
    {
        public static bool CanTransition(CaseStatus from, CaseStatus to) => (from, to) switch
        {
            (CaseStatus.Reported, CaseStatus.UnderInvestigation) => true,
            (CaseStatus.Reported, CaseStatus.Decided) => true,
            (CaseStatus.UnderInvestigation, CaseStatus.Decided) => true,
            (CaseStatus.Decided, CaseStatus.ActionApplied) => true,
            (CaseStatus.ActionApplied, CaseStatus.AppealWindow) => true,
            (CaseStatus.AppealWindow, CaseStatus.Closed) => true,
            _ => false,
        };
    }

    /// <summary>Pure BR-DCP-005: repetition escalation — the Nth same-severity incident in the period proposes the ladder step for that repetition (capped at the highest defined step). Advisory only.</summary>
    public static class RepetitionEscalationEvaluator
    {
        public sealed record Step(int Severity, int RepetitionCount, int ConsequenceTypeId);

        public static int? Propose(int severity, int priorSameSeverityCount, IReadOnlyCollection<Step> ladder)
        {
            var repetition = priorSameSeverityCount + 1;
            return ladder.Where(s => s.Severity == severity && s.RepetitionCount <= repetition)
                .OrderByDescending(s => s.RepetitionCount)
                .Select(s => (int?)s.ConsequenceTypeId)
                .FirstOrDefault();
        }
    }

    /// <summary>Pure BR-DCP-005/004: deviating below the proposal needs a reason; going above the proposal (or any suspension-class action, or a severity-4 case) needs the Principal.</summary>
    public static class DecisionPolicy
    {
        public sealed record Check(bool NeedsReason, bool NeedsPrincipal);

        public static Check Evaluate(int? proposedRank, int decidedRank, bool decidedIsSuspensionClass, bool caseRequiresPrincipal)
        {
            var below = proposedRank.HasValue && decidedRank < proposedRank.Value;
            var above = proposedRank.HasValue && decidedRank > proposedRank.Value;
            return new Check(NeedsReason: below, NeedsPrincipal: above || decidedIsSuspensionClass || caseRequiresPrincipal);
        }
    }

    /// <summary>Pure BR-DCP-003 due process: student/parent statement mandatory for severity ≥ 3.</summary>
    public static class DueProcessPolicy
    {
        public static bool StatementsSatisfied(int severity, IReadOnlyCollection<StatementKind> statements)
            => severity < 3 || statements.Contains(StatementKind.Student) || statements.Contains(StatementKind.Parent);
    }

    /// <summary>Pure BR-DCP-004: suspension days ≤ pack cap when one is configured.</summary>
    public static class SuspensionLimitPolicy
    {
        public static bool IsWithinCap(int? days, int? maxSuspensionDays) => !maxSuspensionDays.HasValue || (days ?? 0) <= maxSuspensionDays.Value;
    }

    /// <summary>Pure BR-DCP-006: appeal for severity ≥ 2, within the window, once, reviewed by someone other than the decider.</summary>
    public static class AppealPolicy
    {
        public static bool CanFile(int severity, DateTime decidedAtUtc, DateTime nowUtc, int windowDays, bool alreadyAppealed)
            => severity >= 2 && !alreadyAppealed && nowUtc <= decidedAtUtc.AddDays(windowDays);

        public static bool IsIndependentReviewer(int reviewerUserId, int? deciderUserId) => deciderUserId != reviewerUserId;
    }

    /// <summary>Pure BR-DCP-007: per student-term aggregation with configurable thresholds → flags; behavior grade by points band for Module 17.</summary>
    public static class PointsAggregator
    {
        public sealed record Totals(int ViolationPoints, int MeritPoints)
        {
            public int Net => MeritPoints - ViolationPoints;
        }

        public sealed record Flags(bool WelfareReview, bool HonorList);

        public static Totals Aggregate(IEnumerable<(PointSource Source, int Points)> entries)
        {
            var list = entries.ToList();
            return new Totals(
                list.Where(e => e.Source == PointSource.Violation).Sum(e => Math.Abs(e.Points)),
                list.Where(e => e.Source == PointSource.Merit).Sum(e => e.Points));
        }

        public static Flags Evaluate(Totals totals, int welfareReviewThreshold, int honorListThreshold)
            => new(totals.ViolationPoints >= welfareReviewThreshold, totals.MeritPoints >= honorListThreshold);

        /// <summary>Bands ordered by ascending minimum net points; the highest band whose minimum ≤ net applies.</summary>
        public static string BehaviorGrade(int netPoints, IReadOnlyList<(int MinNet, string Grade)> bands)
            => bands.Where(b => b.MinNet <= netPoints).OrderByDescending(b => b.MinNet).Select(b => b.Grade).FirstOrDefault() ?? bands.OrderBy(b => b.MinNet).First().Grade;
    }

    /// <summary>Pure BR-DCP-008/010: what a parent may see per policy level; the reporter is never shown.</summary>
    public static class PortalVisibilityPolicy
    {
        public sealed record ParentIncidentView(string IncidentNo, DateTime OccurredAtUtc, int Severity, string? Narrative, string? DecisionArticleRef, string? ConsequenceName);

        public static bool IsVisible(PortalVisibilityLevel level, bool hasDecision, bool isSummons)
            => level switch
            {
                PortalVisibilityLevel.Full => true,
                PortalVisibilityLevel.DecisionsOnly => hasDecision,
                PortalVisibilityLevel.SummonsOnly => isSummons,
                _ => false,
            };

        public static ParentIncidentView Project(PortalVisibilityLevel level, string incidentNo, DateTime occurredAtUtc, int severity, string narrative, string? decisionArticleRef, string? consequenceName)
            => new(incidentNo, occurredAtUtc, severity, level == PortalVisibilityLevel.Full ? narrative : null, decisionArticleRef, consequenceName);
    }
}
