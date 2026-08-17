using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Discipline;
using Sms.Application.Notifications;
using Sms.Application.Numbering;
using Sms.Domain.Discipline;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Discipline
{
    /// <summary>Standalone — saves itself. The active code for the working year is the newest active BehaviorCode of that year.</summary>
    public class DisciplineAdmin : IDisciplineAdmin
    {
        public const string IncidentSeriesCode = "INC";
        public const string IncidentEventCode = "DisciplineIncidentRecorded";
        public const string DecisionEventCode = "DisciplineDecision";

        private readonly AppDbContext _db;
        private readonly INumberIssuer _numberIssuer;
        private readonly IClock _clock;
        private readonly IAuditContext _audit;
        private readonly IWorkingYearContext _workingYear;
        private readonly INotificationPublisher _notifications;

        public DisciplineAdmin(AppDbContext db, INumberIssuer numberIssuer, IClock clock, IAuditContext audit, IWorkingYearContext workingYear, INotificationPublisher notifications)
        {
            _db = db;
            _numberIssuer = numberIssuer;
            _clock = clock;
            _audit = audit;
            _workingYear = workingYear;
            _notifications = notifications;
        }

        // ------------------------------------------------------------------ code

        public async Task<BehaviorCode> DefineBehaviorCodeAsync(
            string nameAr, string nameEn, IReadOnlyList<ViolationTypeInput> violations, IReadOnlyList<MeritTypeInput> merits,
            IReadOnlyList<ConsequenceTypeInput> consequences, IReadOnlyList<LadderStepInput> ladder,
            int? maxSuspensionDays = null, int appealWindowDays = 7, CancellationToken cancellationToken = default)
        {
            var yearId = _workingYear.AcademicYearId;
            var previous = await _db.BehaviorCodes.Where(c => c.AcademicYearId == yearId).OrderByDescending(c => c.Version).FirstOrDefaultAsync(cancellationToken);
            var code = new BehaviorCode
            {
                AcademicYearId = yearId, NameAr = nameAr, NameEn = nameEn, Version = (previous?.Version ?? 0) + 1,
                MaxSuspensionDays = maxSuspensionDays, AppealWindowDays = appealWindowDays,
            };
            foreach (var v in violations)
            {
                code.ViolationTypes.Add(new ViolationType { ArticleRef = v.ArticleRef, NameAr = v.NameAr, NameEn = v.NameEn, Severity = v.Severity, Points = v.Points });
            }

            foreach (var m in merits)
            {
                code.MeritTypes.Add(new MeritType { NameAr = m.NameAr, NameEn = m.NameEn, Points = m.Points, MaxPointsPerAward = m.MaxPointsPerAward });
            }

            foreach (var c in consequences)
            {
                code.ConsequenceTypes.Add(new ConsequenceType { Kind = c.Kind, NameAr = c.NameAr, NameEn = c.NameEn, SeverityRank = c.SeverityRank, IsSuspensionClass = c.IsSuspensionClass });
            }

            _db.BehaviorCodes.Add(code);
            await _db.SaveChangesAsync(cancellationToken);   // consequence ids needed for ladder rows

            foreach (var step in ladder)
            {
                _db.LadderSteps.Add(new LadderStep { BehaviorCodeId = code.Id, Severity = step.Severity, RepetitionCount = step.RepetitionCount, ConsequenceTypeId = code.ConsequenceTypes[step.ConsequenceIndex].Id });
            }

            if (previous != null)
            {
                previous.IsActive = false;
            }

            await _db.SaveChangesAsync(cancellationToken);
            return code;
        }

        public async Task PublishBehaviorCodeAsync(int behaviorCodeId, CancellationToken cancellationToken = default)
        {
            var code = await _db.BehaviorCodes.SingleAsync(c => c.Id == behaviorCodeId, cancellationToken);
            code.IsPublished = true;
            await _db.SaveChangesAsync(cancellationToken);
        }

        // ------------------------------------------------------------------ incidents + merits

        public async Task<Incident> RecordIncidentAsync(int studentId, int violationTypeId, int reporterUserId, string narrative, DateTime occurredAtUtc, int? termId = null, int? evidenceAttachmentId = null, bool notifyParent = true, CancellationToken cancellationToken = default)
        {
            var violation = await _db.ViolationTypes.SingleAsync(v => v.Id == violationTypeId, cancellationToken);
            var incident = new Incident
            {
                AcademicYearId = _workingYear.AcademicYearId, IncidentNo = await _numberIssuer.IssueAsync(IncidentSeriesCode, cancellationToken), StudentId = studentId, TermId = termId,
                ReporterUserId = reporterUserId, ViolationTypeId = violationTypeId, Severity = violation.Severity, OccurredAtUtc = occurredAtUtc, Narrative = narrative,
                EvidenceAttachmentId = evidenceAttachmentId, IsTeacherResolved = violation.Severity == 1,
            };
            _db.Incidents.Add(incident);
            await _db.SaveChangesAsync(cancellationToken);

            _db.PointLedgerEntries.Add(new PointLedgerEntry { AcademicYearId = incident.AcademicYearId, StudentId = studentId, TermId = termId, Source = PointSource.Violation, SourceId = incident.Id, Points = -violation.Points, OccurredAtUtc = occurredAtUtc });

            if (violation.Severity >= 2)
            {
                // BR-DCP-005: repetition = prior same-severity incidents of this student in the year (the "period").
                var priorCount = await _db.Incidents.CountAsync(i => i.StudentId == studentId && i.AcademicYearId == incident.AcademicYearId && i.Severity == violation.Severity && i.Id != incident.Id, cancellationToken);
                var ladder = await _db.LadderSteps.Where(s => s.BehaviorCodeId == violation.BehaviorCodeId).Select(s => new RepetitionEscalationEvaluator.Step(s.Severity, s.RepetitionCount, s.ConsequenceTypeId)).ToListAsync(cancellationToken);
                var disciplineCase = new DisciplineCase
                {
                    IncidentId = incident.Id, StudentId = studentId, Severity = violation.Severity, RequiresPrincipal = violation.Severity >= 4,
                    ProposedConsequenceTypeId = RepetitionEscalationEvaluator.Propose(violation.Severity, priorCount, ladder),
                };
                _db.DisciplineCases.Add(disciplineCase);
                await _db.SaveChangesAsync(cancellationToken);
                incident.CaseId = disciplineCase.Id;
            }

            await _db.SaveChangesAsync(cancellationToken);

            if (notifyParent)
            {
                await NotifyGuardiansAsync(studentId, IncidentEventCode, new Dictionary<string, string> { ["IncidentNo"] = incident.IncidentNo, ["Severity"] = violation.Severity.ToString(CultureInfo.InvariantCulture) }, cancellationToken);
            }

            return incident;
        }

        public async Task<Merit> RecordMeritAsync(int studentId, int meritTypeId, int points, int recordedByUserId, int? termId = null, string? note = null, CancellationToken cancellationToken = default)
        {
            var type = await _db.MeritTypes.SingleAsync(m => m.Id == meritTypeId, cancellationToken);
            if (points <= 0 || points > type.MaxPointsPerAward)
            {
                throw new MeritPointsOutOfBoundsException(meritTypeId, points);
            }

            var merit = new Merit { AcademicYearId = _workingYear.AcademicYearId, StudentId = studentId, TermId = termId, MeritTypeId = meritTypeId, Points = points, RecordedByUserId = recordedByUserId, Note = note };
            _db.Merits.Add(merit);
            await _db.SaveChangesAsync(cancellationToken);
            _db.PointLedgerEntries.Add(new PointLedgerEntry { AcademicYearId = merit.AcademicYearId, StudentId = studentId, TermId = termId, Source = PointSource.Merit, SourceId = merit.Id, Points = points, OccurredAtUtc = _clock.UtcNow });
            await _db.SaveChangesAsync(cancellationToken);
            return merit;
        }

        // ------------------------------------------------------------------ case workflow

        private async Task<DisciplineCase> LoadCaseForAsync(int caseId, CaseStatus target, CancellationToken cancellationToken)
        {
            var disciplineCase = await _db.DisciplineCases.SingleAsync(c => c.Id == caseId, cancellationToken);
            if (!CaseStatusTransitions.CanTransition(disciplineCase.Status, target))
            {
                throw new InvalidCaseStatusTransitionException(disciplineCase.Status, target);
            }

            return disciplineCase;
        }

        public async Task StartInvestigationAsync(int caseId, int officerUserId, CancellationToken cancellationToken = default)
        {
            var disciplineCase = await LoadCaseForAsync(caseId, CaseStatus.UnderInvestigation, cancellationToken);
            disciplineCase.Status = CaseStatus.UnderInvestigation;
            disciplineCase.OfficerUserId = officerUserId;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task AddStatementAsync(int caseId, StatementKind kind, string text, int? attachmentId = null, CancellationToken cancellationToken = default)
        {
            _db.CaseStatements.Add(new CaseStatement { DisciplineCaseId = caseId, Kind = kind, Text = text, RecordedAtUtc = _clock.UtcNow, AttachmentId = attachmentId });
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task DecideAsync(int caseId, int consequenceTypeId, string articleRef, int decidedByUserId, string? deviationReason = null, int? principalUserId = null, CancellationToken cancellationToken = default)
        {
            var disciplineCase = await LoadCaseForAsync(caseId, CaseStatus.Decided, cancellationToken);
            if (string.IsNullOrWhiteSpace(articleRef))
            {
                throw new DecisionArticleRequiredException(caseId);
            }

            var statements = await _db.CaseStatements.Where(s => s.DisciplineCaseId == caseId).Select(s => s.Kind).ToListAsync(cancellationToken);
            if (!DueProcessPolicy.StatementsSatisfied(disciplineCase.Severity, statements))
            {
                throw new StatementsRequiredException(caseId);
            }

            var decided = await _db.ConsequenceTypes.SingleAsync(c => c.Id == consequenceTypeId, cancellationToken);
            var proposedRank = disciplineCase.ProposedConsequenceTypeId.HasValue
                ? (await _db.ConsequenceTypes.SingleAsync(c => c.Id == disciplineCase.ProposedConsequenceTypeId.Value, cancellationToken)).SeverityRank
                : (int?)null;
            var check = DecisionPolicy.Evaluate(proposedRank, decided.SeverityRank, decided.IsSuspensionClass, disciplineCase.RequiresPrincipal);
            if (check.NeedsReason && string.IsNullOrWhiteSpace(deviationReason))
            {
                throw new DecisionDeviationReasonRequiredException(caseId);
            }

            if (check.NeedsPrincipal && !principalUserId.HasValue)
            {
                throw new PrincipalApprovalRequiredException(caseId);
            }

            if (check.NeedsReason)
            {
                _audit.Reason = deviationReason;
                disciplineCase.DeviationReason = deviationReason;
            }

            disciplineCase.Status = CaseStatus.Decided;
            disciplineCase.DecidedConsequenceTypeId = consequenceTypeId;
            disciplineCase.DecisionArticleRef = articleRef;
            disciplineCase.DecidedByUserId = decidedByUserId;
            disciplineCase.PrincipalUserId = principalUserId;
            disciplineCase.DecidedAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            await NotifyGuardiansAsync(disciplineCase.StudentId, DecisionEventCode, new Dictionary<string, string> { ["Article"] = articleRef, ["Consequence"] = decided.NameEn }, cancellationToken);
        }

        public async Task<ActionApplied> ApplyActionAsync(int caseId, DateTime startDate, int? days = null, CancellationToken cancellationToken = default)
        {
            var disciplineCase = await LoadCaseForAsync(caseId, CaseStatus.ActionApplied, cancellationToken);
            var consequence = await _db.ConsequenceTypes.SingleAsync(c => c.Id == disciplineCase.DecidedConsequenceTypeId!.Value, cancellationToken);
            var code = await _db.BehaviorCodes.IgnoreQueryFilters().SingleAsync(c => c.Id == consequence.BehaviorCodeId, cancellationToken);
            if (consequence.IsSuspensionClass && !SuspensionLimitPolicy.IsWithinCap(days, code.MaxSuspensionDays))
            {
                throw new SuspensionExceedsPackLimitException(days ?? 0, code.MaxSuspensionDays!.Value);
            }

            var action = new ActionApplied
            {
                DisciplineCaseId = caseId, ConsequenceTypeId = consequence.Id, StartDate = startDate.Date, Days = days,
                ApprovedByPrincipalUserId = consequence.IsSuspensionClass ? disciplineCase.PrincipalUserId : null,
            };
            _db.ActionsApplied.Add(action);
            disciplineCase.Status = CaseStatus.ActionApplied;
            await _db.SaveChangesAsync(cancellationToken);

            // The appeal window opens as soon as the action is applied (BR-DCP-003 sequence).
            disciplineCase.Status = CaseStatus.AppealWindow;
            await _db.SaveChangesAsync(cancellationToken);
            return action;
        }

        public async Task CompleteActionAsync(int actionAppliedId, CancellationToken cancellationToken = default)
        {
            var action = await _db.ActionsApplied.SingleAsync(a => a.Id == actionAppliedId, cancellationToken);
            action.CompletedAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<Appeal> FileAppealAsync(int caseId, int parentId, string grounds, CancellationToken cancellationToken = default)
        {
            var disciplineCase = await _db.DisciplineCases.SingleAsync(c => c.Id == caseId, cancellationToken);
            var code = await ActiveCodeForCaseAsync(disciplineCase, cancellationToken);
            var already = await _db.Appeals.AnyAsync(a => a.DisciplineCaseId == caseId, cancellationToken);
            if (disciplineCase.DecidedAtUtc == null || !AppealPolicy.CanFile(disciplineCase.Severity, disciplineCase.DecidedAtUtc.Value, _clock.UtcNow, code.AppealWindowDays, already))
            {
                throw new AppealNotAllowedException(caseId);
            }

            var appeal = new Appeal { DisciplineCaseId = caseId, FiledByParentId = parentId, FiledAtUtc = _clock.UtcNow, Grounds = grounds };
            _db.Appeals.Add(appeal);
            await _db.SaveChangesAsync(cancellationToken);
            return appeal;
        }

        public async Task DecideAppealAsync(int appealId, int reviewerUserId, AppealOutcome outcome, string? note = null, int? modifiedConsequenceTypeId = null, CancellationToken cancellationToken = default)
        {
            var appeal = await _db.Appeals.SingleAsync(a => a.Id == appealId, cancellationToken);
            var disciplineCase = await _db.DisciplineCases.SingleAsync(c => c.Id == appeal.DisciplineCaseId, cancellationToken);
            if (!AppealPolicy.IsIndependentReviewer(reviewerUserId, disciplineCase.DecidedByUserId))
            {
                throw new AppealReviewerNotIndependentException(appealId);
            }

            appeal.ReviewerUserId = reviewerUserId;
            appeal.Outcome = outcome;
            appeal.DecidedAtUtc = _clock.UtcNow;
            appeal.DecisionNote = note;
            if (outcome == AppealOutcome.Modified && modifiedConsequenceTypeId.HasValue)
            {
                _audit.Reason = note ?? "appeal modified the decision";
                disciplineCase.DecidedConsequenceTypeId = modifiedConsequenceTypeId;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task CloseCaseAsync(int caseId, CancellationToken cancellationToken = default)
        {
            var disciplineCase = await LoadCaseForAsync(caseId, CaseStatus.Closed, cancellationToken);
            var code = await ActiveCodeForCaseAsync(disciplineCase, cancellationToken);
            var appeal = await _db.Appeals.SingleOrDefaultAsync(a => a.DisciplineCaseId == caseId, cancellationToken);
            var windowElapsed = disciplineCase.DecidedAtUtc.HasValue && _clock.UtcNow > disciplineCase.DecidedAtUtc.Value.AddDays(code.AppealWindowDays);
            var appealDecided = appeal != null && appeal.Outcome != AppealOutcome.Pending;
            if (!(windowElapsed || appealDecided))
            {
                throw new CaseNotClosableException(caseId);
            }

            disciplineCase.Status = CaseStatus.Closed;
            disciplineCase.ClosedAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        private async Task<BehaviorCode> ActiveCodeForCaseAsync(DisciplineCase disciplineCase, CancellationToken cancellationToken)
        {
            var incident = await _db.Incidents.SingleAsync(i => i.Id == disciplineCase.IncidentId, cancellationToken);
            var violation = await _db.ViolationTypes.SingleAsync(v => v.Id == incident.ViolationTypeId, cancellationToken);
            return await _db.BehaviorCodes.IgnoreQueryFilters().SingleAsync(c => c.Id == violation.BehaviorCodeId, cancellationToken);
        }

        // ------------------------------------------------------------------ points, portal, contracts

        public async Task<(PointsAggregator.Totals Totals, PointsAggregator.Flags Flags)> GetPointsAsync(int studentId, int? termId, int welfareReviewThreshold = 20, int honorListThreshold = 20, CancellationToken cancellationToken = default)
        {
            var yearId = _workingYear.AcademicYearId;
            var entries = await _db.PointLedgerEntries
                .Where(e => e.StudentId == studentId && e.AcademicYearId == yearId && (termId == null || e.TermId == termId))
                .Select(e => new { e.Source, e.Points }).ToListAsync(cancellationToken);
            var totals = PointsAggregator.Aggregate(entries.Select(e => (e.Source, e.Points)));
            return (totals, PointsAggregator.Evaluate(totals, welfareReviewThreshold, honorListThreshold));
        }

        public async Task<IReadOnlyList<PortalVisibilityPolicy.ParentIncidentView>> GetParentViewAsync(int studentId, PortalVisibilityLevel level, CancellationToken cancellationToken = default)
        {
            var incidents = await _db.Incidents.Where(i => i.StudentId == studentId).OrderByDescending(i => i.OccurredAtUtc).ToListAsync(cancellationToken);
            var caseIds = incidents.Where(i => i.CaseId.HasValue).Select(i => i.CaseId!.Value).ToList();
            var cases = await _db.DisciplineCases.Where(c => caseIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken);
            var consequenceIds = cases.Values.Where(c => c.DecidedConsequenceTypeId.HasValue).Select(c => c.DecidedConsequenceTypeId!.Value).Distinct().ToList();
            var consequences = await _db.ConsequenceTypes.Where(c => consequenceIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken);

            var views = new List<PortalVisibilityPolicy.ParentIncidentView>();
            foreach (var incident in incidents)
            {
                var disciplineCase = incident.CaseId.HasValue && cases.TryGetValue(incident.CaseId.Value, out var c) ? c : null;
                var consequence = disciplineCase?.DecidedConsequenceTypeId != null && consequences.TryGetValue(disciplineCase.DecidedConsequenceTypeId.Value, out var ct) ? ct : null;
                var hasDecision = disciplineCase?.DecidedAtUtc != null;
                var isSummons = consequence?.Kind == ConsequenceKind.ParentSummons;
                if (!PortalVisibilityPolicy.IsVisible(level, hasDecision, isSummons))
                {
                    continue;
                }

                views.Add(PortalVisibilityPolicy.Project(level, incident.IncidentNo, incident.OccurredAtUtc, incident.Severity, incident.Narrative, disciplineCase?.DecisionArticleRef, consequence?.NameEn));
            }

            return views;
        }

        public async Task<BehaviorContract> DraftBehaviorContractAsync(int studentId, string terms, int? caseId = null, DateTime? endsOn = null, CancellationToken cancellationToken = default)
        {
            var contract = new BehaviorContract { StudentId = studentId, Terms = terms, DisciplineCaseId = caseId, EndsOn = endsOn };
            _db.BehaviorContracts.Add(contract);
            await _db.SaveChangesAsync(cancellationToken);
            return contract;
        }

        public async Task SignBehaviorContractAsync(int contractId, bool parentSigned, bool studentAcknowledged, int? pledgeAttachmentId = null, CancellationToken cancellationToken = default)
        {
            var contract = await _db.BehaviorContracts.SingleAsync(c => c.Id == contractId, cancellationToken);
            if (parentSigned)
            {
                contract.ParentSignedAtUtc = _clock.UtcNow;
            }

            if (studentAcknowledged)
            {
                contract.StudentAcknowledgedAtUtc = _clock.UtcNow;
            }

            contract.PledgeAttachmentId ??= pledgeAttachmentId;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<KeepApartPair>> ActiveKeepApartPairsAsync(CancellationToken cancellationToken = default)
            => await _db.KeepApartPairs.Where(p => p.IsActive).ToListAsync(cancellationToken);

        public async Task<KeepApartPair> AddKeepApartPairAsync(int studentAId, int studentBId, string reason, CancellationToken cancellationToken = default)
        {
            var pair = new KeepApartPair { StudentAId = Math.Min(studentAId, studentBId), StudentBId = Math.Max(studentAId, studentBId), Reason = reason };
            _db.KeepApartPairs.Add(pair);
            await _db.SaveChangesAsync(cancellationToken);
            return pair;
        }

        public async Task<ParentMeeting> ScheduleParentMeetingAsync(int studentId, DateTime scheduledAtUtc, int? caseId = null, CancellationToken cancellationToken = default)
        {
            var meeting = new ParentMeeting { StudentId = studentId, ScheduledAtUtc = scheduledAtUtc, DisciplineCaseId = caseId };
            _db.ParentMeetings.Add(meeting);
            await _db.SaveChangesAsync(cancellationToken);
            return meeting;
        }

        private async Task NotifyGuardiansAsync(int studentId, string eventCode, IReadOnlyDictionary<string, string> payload, CancellationToken cancellationToken)
        {
            var parentIds = await _db.StudentGuardianLinks.Where(l => l.StudentId == studentId && l.EffectiveToUtc == null).Select(l => l.ParentId).ToListAsync(cancellationToken);
            var parents = await _db.Parents.Where(p => parentIds.Contains(p.Id) && p.UserAccountId != null).Select(p => new { p.UserAccountId, p.PreferredLanguage }).ToListAsync(cancellationToken);
            await _notifications.PublishAsync(eventCode, parents.Select(p => new NotificationRecipient(p.UserAccountId!.Value, p.PreferredLanguage)).ToList(), payload, cancellationToken);
        }
    }
}
