using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Discipline;

namespace Sms.Application.Discipline
{
    public sealed record ViolationTypeInput(string ArticleRef, string NameAr, string NameEn, int Severity, int Points);

    public sealed record MeritTypeInput(string NameAr, string NameEn, int Points, int MaxPointsPerAward);

    public sealed record ConsequenceTypeInput(ConsequenceKind Kind, string NameAr, string NameEn, int SeverityRank, bool IsSuspensionClass);

    /// <summary>Ladder step: severity × repetition → the consequence by its input index in <see cref="ConsequenceTypeInput"/> list.</summary>
    public sealed record LadderStepInput(int Severity, int RepetitionCount, int ConsequenceIndex);

    /// <summary>
    /// doc/Modules/25 §8 Code editor / Incident desk / Case workbench /
    /// Appeals / Points & flags / Contracts screens backing (screens
    /// deferred, operations are core). Every WF-11 chain step is a status
    /// gate on the case (status-only substitution) — the due-process,
    /// principal and pack-cap checks are real.
    /// </summary>
    public interface IDisciplineAdmin
    {
        Task<BehaviorCode> DefineBehaviorCodeAsync(
            string nameAr, string nameEn, IReadOnlyList<ViolationTypeInput> violations, IReadOnlyList<MeritTypeInput> merits,
            IReadOnlyList<ConsequenceTypeInput> consequences, IReadOnlyList<LadderStepInput> ladder,
            int? maxSuspensionDays = null, int appealWindowDays = 7, CancellationToken cancellationToken = default);

        Task PublishBehaviorCodeAsync(int behaviorCodeId, CancellationToken cancellationToken = default);

        /// <summary>BR-DCP-002: numbered incident; severity 1 resolves teacher-level (no case) and notifies parents when <paramref name="notifyParent"/>; severity ≥ 2 opens a Case (Reported) with the ladder's advisory proposal (BR-DCP-005). Points post to the ledger either way.</summary>
        Task<Incident> RecordIncidentAsync(int studentId, int violationTypeId, int reporterUserId, string narrative, DateTime occurredAtUtc, int? termId = null, int? evidenceAttachmentId = null, bool notifyParent = true, CancellationToken cancellationToken = default);

        /// <summary>BR-DCP-002: merit points within the type's bounds (<see cref="Common.Exceptions.MeritPointsOutOfBoundsException"/>).</summary>
        Task<Merit> RecordMeritAsync(int studentId, int meritTypeId, int points, int recordedByUserId, int? termId = null, string? note = null, CancellationToken cancellationToken = default);

        Task StartInvestigationAsync(int caseId, int officerUserId, CancellationToken cancellationToken = default);

        Task AddStatementAsync(int caseId, StatementKind kind, string text, int? attachmentId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-DCP-003/004/005: article ref mandatory; statements mandatory for severity ≥ 3; below-proposal needs <paramref name="deviationReason"/>;
        /// above-proposal, suspension-class or severity-4 needs <paramref name="principalUserId"/>. Notifies parents (DisciplineDecision).
        /// </summary>
        Task DecideAsync(int caseId, int consequenceTypeId, string articleRef, int decidedByUserId, string? deviationReason = null, int? principalUserId = null, CancellationToken cancellationToken = default);

        /// <summary>BR-DCP-004: applies the decided consequence; suspension days ≤ pack cap (<see cref="Common.Exceptions.SuspensionExceedsPackLimitException"/>). Case → ActionApplied → AppealWindow.</summary>
        Task<ActionApplied> ApplyActionAsync(int caseId, DateTime startDate, int? days = null, CancellationToken cancellationToken = default);

        Task CompleteActionAsync(int actionAppliedId, CancellationToken cancellationToken = default);

        /// <summary>BR-DCP-006: severity ≥ 2, within window, once (<see cref="Common.Exceptions.AppealNotAllowedException"/>).</summary>
        Task<Appeal> FileAppealAsync(int caseId, int parentId, string grounds, CancellationToken cancellationToken = default);

        /// <summary>BR-DCP-006: reviewer must not be the original decider (<see cref="Common.Exceptions.AppealReviewerNotIndependentException"/>); Modified re-points the case at <paramref name="modifiedConsequenceTypeId"/>.</summary>
        Task DecideAppealAsync(int appealId, int reviewerUserId, AppealOutcome outcome, string? note = null, int? modifiedConsequenceTypeId = null, CancellationToken cancellationToken = default);

        /// <summary>Closes once the appeal window has elapsed or the appeal is decided (<see cref="Common.Exceptions.CaseNotClosableException"/>).</summary>
        Task CloseCaseAsync(int caseId, CancellationToken cancellationToken = default);

        /// <summary>BR-DCP-007: aggregated points + flags for a student-term (null term = whole year).</summary>
        Task<(PointsAggregator.Totals Totals, PointsAggregator.Flags Flags)> GetPointsAsync(int studentId, int? termId, int welfareReviewThreshold = 20, int honorListThreshold = 20, CancellationToken cancellationToken = default);

        /// <summary>BR-DCP-008/010: the parent-facing projection — filtered by policy level, reporter never included.</summary>
        Task<IReadOnlyList<PortalVisibilityPolicy.ParentIncidentView>> GetParentViewAsync(int studentId, PortalVisibilityLevel level, CancellationToken cancellationToken = default);

        Task<BehaviorContract> DraftBehaviorContractAsync(int studentId, string terms, int? caseId = null, DateTime? endsOn = null, CancellationToken cancellationToken = default);

        /// <summary>doc §9: contract needs signatures — parent e-ack/pledge doc and student acknowledgement.</summary>
        Task SignBehaviorContractAsync(int contractId, bool parentSigned, bool studentAcknowledged, int? pledgeAttachmentId = null, CancellationToken cancellationToken = default);

        /// <summary>BR-DCP-009: active (fully signed, unexpired) contracts and keep-apart pairs for Sections balancing.</summary>
        Task<IReadOnlyList<KeepApartPair>> ActiveKeepApartPairsAsync(CancellationToken cancellationToken = default);

        Task<KeepApartPair> AddKeepApartPairAsync(int studentAId, int studentBId, string reason, CancellationToken cancellationToken = default);

        Task<ParentMeeting> ScheduleParentMeetingAsync(int studentId, DateTime scheduledAtUtc, int? caseId = null, CancellationToken cancellationToken = default);
    }
}
