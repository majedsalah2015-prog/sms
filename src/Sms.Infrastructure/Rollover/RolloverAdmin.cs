using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Fees;
using Sms.Application.Grades;
using Sms.Application.Rollover;
using Sms.Application.Schools;
using Sms.Application.Sections;
using Sms.Application.Students;
using Sms.Domain.Fees;
using Sms.Domain.Grading;
using Sms.Domain.Rollover;
using Sms.Domain.Schools;
using Sms.Domain.Sections;
using Sms.Domain.Students;
using Sms.Domain.Timetable;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Rollover
{
    /// <summary>
    /// S8/E-801 — doc/Modules/03 §4 year-end rollover (WF-02 family), BR-AYR-008/009.
    /// Composes the owning modules' own admin services (Grades, Students, Sections,
    /// Fees, Academic Years) exactly like E-201's <c>AdmissionAdmin.RegisterAsync</c>:
    /// each self-saving call joins the outer per-student transaction via
    /// <c>SmsDbContext</c>'s ambient-transaction detection. Every step is
    /// re-runnable; per-student idempotency markers live on <see cref="RolloverStudentState"/>.
    /// </summary>
    public class RolloverAdmin : IRolloverAdmin
    {
        private readonly AppDbContext _db;
        private readonly IClock _clock;
        private readonly ICurrentUser _currentUser;
        private readonly IGradeStructureAdmin _grades;
        private readonly IStudentAdmin _students;
        private readonly ISectionAdmin _sections;
        private readonly IFeeAdmin _fees;
        private readonly IAcademicYearAdmin _years;
        private readonly IAuditContext _audit;

        public RolloverAdmin(
            AppDbContext db, IClock clock, ICurrentUser currentUser, IAuditContext audit, IGradeStructureAdmin grades, IStudentAdmin students,
            ISectionAdmin sections, IFeeAdmin fees, IAcademicYearAdmin years)
        {
            _db = db;
            _clock = clock;
            _currentUser = currentUser;
            _audit = audit;
            _grades = grades;
            _students = students;
            _sections = sections;
            _fees = fees;
            _years = years;
        }

        // ------------------------------------------------------------------ steps 1–2

        public async Task<RolloverBatch> OpenBatchAsync(int sourceAcademicYearId, int targetAcademicYearId, CancellationToken cancellationToken = default)
        {
            var source = await _db.AcademicYears.SingleAsync(y => y.Id == sourceAcademicYearId, cancellationToken);
            var target = await _db.AcademicYears.SingleAsync(y => y.Id == targetAcademicYearId, cancellationToken);

            var batch = await _db.RolloverBatches.SingleOrDefaultAsync(
                b => b.SourceAcademicYearId == sourceAcademicYearId && b.TargetAcademicYearId == targetAcademicYearId, cancellationToken);
            if (batch == null)
            {
                if (source.Status != AcademicYearStatus.Active)
                {
                    throw new RolloverYearStatusException(sourceAcademicYearId, source.Status, AcademicYearStatus.Active);
                }

                if (target.Status != AcademicYearStatus.Preparation)
                {
                    throw new RolloverYearStatusException(targetAcademicYearId, target.Status, AcademicYearStatus.Preparation);
                }

                batch = new RolloverBatch { SourceAcademicYearId = sourceAcademicYearId, TargetAcademicYearId = targetAcademicYearId };
                _db.RolloverBatches.Add(batch);
                await _db.SaveChangesAsync(cancellationToken);
            }

            if (batch.Status == RolloverBatchStatus.Open || batch.Status == RolloverBatchStatus.PromotionsApproved)
            {
                await CopyGradeProfilesAsync(sourceAcademicYearId, targetAcademicYearId, cancellationToken);
                await ValidatePromotionPathAsync(sourceAcademicYearId, cancellationToken);
                await SeedStudentStatesAsync(batch, cancellationToken);
            }

            return batch;
        }

        /// <summary>GradeYearProfile's own contract: "Rollover copies the active year's profiles into the Preparation year" — only missing ones, never overwriting structure the school already built.</summary>
        private async Task CopyGradeProfilesAsync(int sourceYearId, int targetYearId, CancellationToken ct)
        {
            var existing = await _db.GradeYearProfiles.Where(p => p.AcademicYearId == targetYearId).Select(p => p.GradeLevelId).ToListAsync(ct);
            var sourceProfiles = await _db.GradeYearProfiles.Where(p => p.AcademicYearId == sourceYearId && !existing.Contains(p.GradeLevelId)).ToListAsync(ct);
            foreach (var p in sourceProfiles)
            {
                await _grades.DefineGradeYearProfileAsync(
                    p.GradeLevelId, targetYearId, p.GenderPolicy, p.TargetSections, p.TargetSectionSize,
                    p.CurriculumLookupValueId, p.MinAgeAtCutoff, p.MaxAgeAtCutoff, p.AgeCutoffDate, ct);
            }
        }

        /// <summary>First real wiring of E-103's PromotionPathValidator (BR-GRD-002/009): only grades that actually have enrolled students must resolve.</summary>
        private async Task ValidatePromotionPathAsync(int sourceYearId, CancellationToken ct)
        {
            var grades = await _db.GradeLevels.Select(g => new GradeSnapshot(g.Id, g.PromotionTargetGradeLevelId, g.IsGraduating)).ToListAsync(ct);
            if (PromotionPathValidator.HasCycle(grades))
            {
                throw new PromotionPathIncompleteException(Array.Empty<int>(), hasCycle: true);
            }

            var enrolledGradeIds = await (
                from e in _db.Enrollments
                join p in _db.GradeYearProfiles on e.GradeYearProfileId equals p.Id
                where e.AcademicYearId == sourceYearId && e.Status == EnrollmentStatus.Active
                select p.GradeLevelId).Distinct().ToListAsync(ct);
            var missing = PromotionPathValidator.FindGradesMissingPromotionTarget(grades).Where(enrolledGradeIds.Contains).ToList();
            if (missing.Count > 0)
            {
                throw new PromotionPathIncompleteException(missing, hasCycle: false);
            }
        }

        private async Task SeedStudentStatesAsync(RolloverBatch batch, CancellationToken ct)
        {
            var known = await _db.RolloverStudentStates.Where(s => s.RolloverBatchId == batch.Id).Select(s => s.StudentId).ToListAsync(ct);
            var enrollments = await _db.Enrollments
                .Where(e => e.AcademicYearId == batch.SourceAcademicYearId && e.Status == EnrollmentStatus.Active && !known.Contains(e.StudentId))
                .OrderBy(e => e.StudentId)
                .ToListAsync(ct);
            foreach (var e in enrollments)
            {
                _db.RolloverStudentStates.Add(new RolloverStudentState
                {
                    RolloverBatchId = batch.Id,
                    StudentId = e.StudentId,
                    SourceEnrollmentId = e.Id,
                    SourceGradeYearProfileId = e.GradeYearProfileId,
                });
            }

            if (enrollments.Count > 0)
            {
                await _db.SaveChangesAsync(ct);
            }
        }

        // ------------------------------------------------------------------ step 3

        public async Task<int> ProposePromotionsAsync(int batchId, CancellationToken cancellationToken = default)
        {
            var batch = await LoadBatchAsync(batchId, cancellationToken);
            RequireStatus(batch, RolloverBatchStatus.Open, RolloverBatchStatus.PromotionsApproved);

            var states = await _db.RolloverStudentStates
                .Where(s => s.RolloverBatchId == batchId && s.DecisionSource != PromotionDecisionSource.Manual)
                .ToListAsync(cancellationToken);
            var enrollmentIds = states.Select(s => s.SourceEnrollmentId).ToList();
            var outcomes = await _db.YearResults
                .Where(r => r.AcademicYearId == batch.SourceAcademicYearId && enrollmentIds.Contains(r.EnrollmentId))
                .ToDictionaryAsync(r => r.EnrollmentId, r => r.PromotionOutcome, cancellationToken);
            var gradeByProfile = await GradeByProfileAsync(cancellationToken);
            var targetProfiles = await TargetProfilesByGradeAsync(batch.TargetAcademicYearId, cancellationToken);

            var decided = 0;
            foreach (var state in states)
            {
                var grade = gradeByProfile[state.SourceGradeYearProfileId];
                var proposal = PromotionDecisionMapper.Propose(outcomes.TryGetValue(state.SourceEnrollmentId, out var o) ? o : (PromotionOutcome?)null, grade.IsGraduating);
                state.ProposedDecision = proposal;
                state.Decision = proposal;
                state.DecisionSource = proposal == PromotionDecision.Undecided ? PromotionDecisionSource.None : PromotionDecisionSource.Auto;
                ApplyDecisionSideEffects(state, grade, targetProfiles, batch.TargetAcademicYearId);
                if (proposal != PromotionDecision.Undecided)
                {
                    decided++;
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            return decided;
        }

        public async Task DecideAsync(int batchId, int studentId, PromotionDecision decision, string reason, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("A manual promotion decision requires a reason.", nameof(reason));
            }

            var batch = await LoadBatchAsync(batchId, cancellationToken);
            RequireStatus(batch, RolloverBatchStatus.Open, RolloverBatchStatus.PromotionsApproved);
            var state = await LoadStateAsync(batchId, studentId, cancellationToken);
            var grade = (await GradeByProfileAsync(cancellationToken))[state.SourceGradeYearProfileId];

            if (decision == PromotionDecision.Undecided)
            {
                throw new InvalidPromotionDecisionException(studentId, decision, PromotionDecisionFault.MustDecide);
            }

            if (decision == PromotionDecision.Graduate && !grade.IsGraduating)
            {
                throw new InvalidPromotionDecisionException(studentId, decision, PromotionDecisionFault.GradeDoesNotGraduate);
            }

            if ((decision == PromotionDecision.Promote || decision == PromotionDecision.Conditional) && grade.PromotionTargetGradeLevelId == null)
            {
                throw new InvalidPromotionDecisionException(studentId, decision, PromotionDecisionFault.NoPromotionTarget);
            }

            state.Decision = decision;
            state.DecisionSource = PromotionDecisionSource.Manual;
            state.DecisionReason = reason;
            ApplyDecisionSideEffects(state, grade, await TargetProfilesByGradeAsync(batch.TargetAcademicYearId, cancellationToken), batch.TargetAcademicYearId);
            await _db.SaveChangesAsync(cancellationToken);
        }

        /// <summary>Resolves the target grade-year profile for the decision; a changed target drops any planned section; graduates don't re-register.</summary>
        private static void ApplyDecisionSideEffects(RolloverStudentState state, GradeInfo grade, IReadOnlyDictionary<int, int> targetProfilesByGrade, int targetYearId)
        {
            var targetGradeId = PromotionDecisionMapper.ResolveTargetGradeLevelId(state.Decision, grade.GradeLevelId, grade.PromotionTargetGradeLevelId);
            int? targetProfileId = null;
            if (targetGradeId != null)
            {
                if (!targetProfilesByGrade.TryGetValue(targetGradeId.Value, out var pid))
                {
                    throw new TargetGradeProfileMissingException(targetGradeId.Value, targetYearId);
                }

                targetProfileId = pid;
            }

            if (state.TargetGradeYearProfileId != targetProfileId)
            {
                state.AssignedSectionId = null;
            }

            state.TargetGradeYearProfileId = targetProfileId;

            if (state.Decision == PromotionDecision.Graduate && state.ReRegistration == ReRegistrationStatus.Pending)
            {
                state.ReRegistration = ReRegistrationStatus.NotApplicable;
            }
            else if (state.Decision != PromotionDecision.Graduate && state.ReRegistration == ReRegistrationStatus.NotApplicable)
            {
                state.ReRegistration = ReRegistrationStatus.Pending;
            }
        }

        public async Task ApprovePromotionsAsync(int batchId, CancellationToken cancellationToken = default)
        {
            var batch = await LoadBatchAsync(batchId, cancellationToken);
            RequireStatus(batch, RolloverBatchStatus.Open);
            var undecided = await _db.RolloverStudentStates
                .CountAsync(s => s.RolloverBatchId == batchId && s.Decision == PromotionDecision.Undecided && s.ReRegistration != ReRegistrationStatus.Declined, cancellationToken);
            if (undecided > 0)
            {
                throw new PromotionsUndecidedException(undecided);
            }

            batch.Status = RolloverBatchStatus.PromotionsApproved;
            batch.PromotionsApprovedAtUtc = _clock.UtcNow;
            batch.PromotionsApprovedByUserId = _currentUser.UserId;
            await _db.SaveChangesAsync(cancellationToken);
        }

        // ------------------------------------------------------------------ step 4

        public async Task ConfirmReRegistrationAsync(int batchId, int studentId, int? reRegistrationFeeCategoryId = null, CancellationToken cancellationToken = default)
        {
            var batch = await LoadBatchAsync(batchId, cancellationToken);
            RequireStatus(batch, RolloverBatchStatus.Open, RolloverBatchStatus.PromotionsApproved, RolloverBatchStatus.Activated);
            var state = await LoadStateAsync(batchId, studentId, cancellationToken);
            if (state.ReRegistration == ReRegistrationStatus.Confirmed)
            {
                return;   // idempotent
            }

            if (state.Decision == PromotionDecision.Graduate)
            {
                throw new InvalidPromotionDecisionException(studentId, state.Decision, PromotionDecisionFault.GraduatesDoNotReRegister);
            }

            if (state.TargetGradeYearProfileId != null)
            {
                await EnsureSeatAsync(batch, state.TargetGradeYearProfileId.Value, cancellationToken);
            }

            using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            state.ReRegistration = ReRegistrationStatus.Confirmed;
            state.ReRegistrationDecidedAtUtc = _clock.UtcNow;

            // BR-FEE-003 re-registration charge, posted into the Preparation year — BR-AYR-003's one explicit exception.
            if (reRegistrationFeeCategoryId != null && state.TargetGradeYearProfileId != null && state.ReRegistrationChargeId == null)
            {
                var payerId = await ResolvePayerAsync(studentId, cancellationToken) ?? throw new NoPayerForStudentException(studentId);
                var charge = await _fees.PostChargeAsync(studentId, payerId, state.TargetGradeYearProfileId.Value, reRegistrationFeeCategoryId.Value, ChargeSourceType.ReRegistration, cancellationToken);
                state.ReRegistrationChargeId = charge.Id;
            }

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }

        public async Task DeclineReRegistrationAsync(int batchId, int studentId, CancellationToken cancellationToken = default)
        {
            var batch = await LoadBatchAsync(batchId, cancellationToken);
            RequireStatus(batch, RolloverBatchStatus.Open, RolloverBatchStatus.PromotionsApproved, RolloverBatchStatus.Activated);
            var state = await LoadStateAsync(batchId, studentId, cancellationToken);
            if (state.TargetEnrollmentId != null)
            {
                throw new RolloverBatchStatusException(batchId, batch.Status, RolloverStepBlocker.AlreadyEnrolledInTargetYear);
            }

            state.ReRegistration = ReRegistrationStatus.Declined;
            state.ReRegistrationDecidedAtUtc = _clock.UtcNow;
            state.AssignedSectionId = null;
            await _db.SaveChangesAsync(cancellationToken);
        }

        /// <summary>BR-GRD-006 planned seats = sections × size; confirmed states + already-Active target-year enrollments (e.g. new admissions) count against it.</summary>
        private async Task EnsureSeatAsync(RolloverBatch batch, int targetProfileId, CancellationToken ct)
        {
            var profile = await _db.GradeYearProfiles.SingleAsync(p => p.Id == targetProfileId, ct);
            var planned = GradeCapacityCalculator.PlannedSeats(profile.TargetSections, profile.TargetSectionSize);
            var confirmed = await _db.RolloverStudentStates.CountAsync(
                s => s.RolloverBatchId == batch.Id && s.TargetGradeYearProfileId == targetProfileId && s.ReRegistration == ReRegistrationStatus.Confirmed && s.TargetEnrollmentId == null, ct);
            var enrolled = await _db.Enrollments.CountAsync(e => e.GradeYearProfileId == targetProfileId && e.Status == EnrollmentStatus.Active, ct);
            if (confirmed + enrolled >= planned)
            {
                throw new NoSeatAvailableException(targetProfileId);
            }
        }

        public async Task DeferTimetableAsync(int batchId, string reason, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("An explicit timetable deferral requires a reason (BR-AYR-004).", nameof(reason));
            }

            var batch = await LoadBatchAsync(batchId, cancellationToken);
            RequireStatus(batch, RolloverBatchStatus.Open, RolloverBatchStatus.PromotionsApproved);
            batch.TimetableDeferredReason = reason;
            await _db.SaveChangesAsync(cancellationToken);
        }

        // ------------------------------------------------------------------ step 5

        public async Task<IReadOnlyList<int>> AutoAssignSectionsAsync(int batchId, int targetGradeYearProfileId, CancellationToken cancellationToken = default)
        {
            var batch = await LoadBatchAsync(batchId, cancellationToken);
            RequireStatus(batch, RolloverBatchStatus.Open, RolloverBatchStatus.PromotionsApproved, RolloverBatchStatus.Activated);

            var pending = await _db.RolloverStudentStates
                .Where(s => s.RolloverBatchId == batchId && s.TargetGradeYearProfileId == targetGradeYearProfileId
                    && s.ReRegistration == ReRegistrationStatus.Confirmed && s.AssignedSectionId == null && s.TargetEnrollmentId == null)
                .ToListAsync(cancellationToken);
            if (pending.Count == 0)
            {
                return Array.Empty<int>();
            }

            var studentIds = pending.Select(s => s.StudentId).ToList();
            var genders = await _db.Students.Where(s => studentIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Gender, cancellationToken);
            var sections = await LoadDistributionSectionsAsync(batchId, targetGradeYearProfileId, cancellationToken);

            var result = SectionDistributor.Distribute(
                pending.Select(s => new DistributionCandidate(s.StudentId, genders[s.StudentId])), sections);
            foreach (var state in pending)
            {
                if (result.Assignments.TryGetValue(state.StudentId, out var sectionId))
                {
                    state.AssignedSectionId = sectionId;
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            return result.UnplacedStudentIds;
        }

        public async Task AssignSectionAsync(int batchId, int studentId, int sectionId, CancellationToken cancellationToken = default)
        {
            var batch = await LoadBatchAsync(batchId, cancellationToken);
            RequireStatus(batch, RolloverBatchStatus.Open, RolloverBatchStatus.PromotionsApproved, RolloverBatchStatus.Activated);
            var state = await LoadStateAsync(batchId, studentId, cancellationToken);
            if (state.TargetGradeYearProfileId == null)
            {
                throw new PromotionNotDecidedException(studentId);
            }

            if (state.TargetEnrollmentId != null)
            {
                throw new RolloverBatchStatusException(batchId, batch.Status, RolloverStepBlocker.AlreadyEnrolled);
            }

            var section = await _db.Sections.SingleAsync(s => s.Id == sectionId, cancellationToken);
            if (section.GradeYearProfileId != state.TargetGradeYearProfileId.Value)
            {
                throw new SectionGradeMismatchException(sectionId, state.TargetGradeYearProfileId.Value);
            }

            var gender = await _db.Students.Where(s => s.Id == studentId).Select(s => s.Gender).SingleAsync(cancellationToken);
            if (!SectionDistributor.IsGenderCompatible(section.GenderPolicy, gender))
            {
                throw new SectionGenderMismatchException(sectionId, studentId);
            }

            var fill = (await LoadDistributionSectionsAsync(batchId, section.GradeYearProfileId, cancellationToken)).Single(s => s.SectionId == sectionId);
            var alreadyHere = state.AssignedSectionId == sectionId;
            if (!alreadyHere && !SectionCapacityGuard.CanAssign(fill.CurrentCount, fill.Capacity))
            {
                throw new SectionFullException(sectionId);
            }

            state.AssignedSectionId = sectionId;
            await _db.SaveChangesAsync(cancellationToken);
        }

        /// <summary>Fill = current memberships (already-enrolled target-year students) + this batch's planned-but-not-yet-materialized assignments.</summary>
        private async Task<List<DistributionSection>> LoadDistributionSectionsAsync(int batchId, int targetProfileId, CancellationToken ct)
        {
            var sections = await _db.Sections
                .Where(s => s.GradeYearProfileId == targetProfileId && s.Status == SectionStatus.Active)
                .Select(s => new { s.Id, s.Capacity, s.GenderPolicy }).ToListAsync(ct);
            var sectionIds = sections.Select(s => s.Id).ToList();
            var memberships = (await _db.SectionMemberships.Where(m => sectionIds.Contains(m.SectionId) && m.EffectiveToUtc == null)
                .Select(m => m.SectionId).ToListAsync(ct)).GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
            var planned = (await _db.RolloverStudentStates
                .Where(s => s.RolloverBatchId == batchId && s.AssignedSectionId != null && sectionIds.Contains(s.AssignedSectionId.Value) && s.TargetEnrollmentId == null)
                .Select(s => s.AssignedSectionId!.Value).ToListAsync(ct)).GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

            return sections.Select(s => new DistributionSection(
                s.Id, s.Capacity,
                (memberships.TryGetValue(s.Id, out var m) ? m : 0) + (planned.TryGetValue(s.Id, out var p) ? p : 0),
                s.GenderPolicy)).ToList();
        }

        // ------------------------------------------------------------------ step 6

        public async Task<IReadOnlyList<ChecklistItem>> GetOpeningChecklistAsync(int batchId, CancellationToken cancellationToken = default)
        {
            var batch = await LoadBatchAsync(batchId, cancellationToken);
            return OpeningChecklistEvaluator.Evaluate(await OpeningFactsAsync(batch, cancellationToken));
        }

        private async Task<OpeningChecklistFacts> OpeningFactsAsync(RolloverBatch batch, CancellationToken ct)
        {
            var t = batch.TargetAcademicYearId;
            return new OpeningChecklistFacts
            {
                CalendarDayCount = await _db.CalendarDays.CountAsync(d => d.AcademicYearId == t, ct),
                GradeYearProfileCount = await _db.GradeYearProfiles.CountAsync(p => p.AcademicYearId == t, ct),
                SectionCount = await _db.Sections.CountAsync(s => s.AcademicYearId == t && s.Status == SectionStatus.Active, ct),
                FeeStructureLineCount = await _db.FeeStructureLines.CountAsync(l => l.AcademicYearId == t, ct),
                UnapprovedFeeStructureLineCount = await _db.FeeStructureLines.CountAsync(l => l.AcademicYearId == t && l.Status != FeeStructureLineStatus.Approved, ct),
                GradingScaleCount = await _db.GradingScales.CountAsync(g => g.AcademicYearId == t, ct),
                TimetablePublished = await _db.TimetableVersions.AnyAsync(v => v.AcademicYearId == t && v.Status == TimetableVersionStatus.Published, ct),
                TimetableExplicitlyDeferred = batch.TimetableDeferredReason != null,
                UndecidedPromotionCount = await _db.RolloverStudentStates.CountAsync(
                    s => s.RolloverBatchId == batch.Id && s.Decision == PromotionDecision.Undecided && s.ReRegistration != ReRegistrationStatus.Declined, ct),
                ConfirmedWithoutSectionCount = await _db.RolloverStudentStates.CountAsync(
                    s => s.RolloverBatchId == batch.Id && s.ReRegistration == ReRegistrationStatus.Confirmed && s.TargetGradeYearProfileId != null
                        && s.AssignedSectionId == null && s.TargetEnrollmentId == null, ct),
            };
        }

        public async Task ActivateAsync(int batchId, int closingWindowDays = 60, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
        {
            var batch = await LoadBatchAsync(batchId, cancellationToken);
            RequireStatus(batch, RolloverBatchStatus.PromotionsApproved, RolloverBatchStatus.Activated);
            var firstRun = batch.Status == RolloverBatchStatus.PromotionsApproved;
            if (firstRun)
            {
                var items = OpeningChecklistEvaluator.Evaluate(await OpeningFactsAsync(batch, cancellationToken));
                if (!OpeningChecklistEvaluator.IsGreen(items))
                {
                    throw new ChecklistNotGreenException("Opening", items);
                }
            }

            var source = await _db.AcademicYears.SingleAsync(y => y.Id == batch.SourceAcademicYearId, cancellationToken);
            var target = await _db.AcademicYears.SingleAsync(y => y.Id == batch.TargetAcademicYearId, cancellationToken);

            // Only ids here — each student is loaded, processed and committed on its own so a kill between students loses nothing.
            var pendingIds = await _db.RolloverStudentStates
                .Where(s => s.RolloverBatchId == batchId && s.ActivatedAtUtc == null)
                .OrderBy(s => s.StudentId).Select(s => s.Id).ToListAsync(cancellationToken);

            var processed = 0;
            foreach (var stateId in pendingIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var done = await ActivateOneAsync(stateId, source, target, cancellationToken);
                // Pilot-scale batch: drop the committed student's graph so the tracker doesn't grow (and every later
                // SaveChanges rescan it) across a 1–2k cohort. Everything below re-loads what it needs.
                _db.ChangeTracker.Clear();
                if (done)
                {
                    processed++;
                    progress?.Report(processed);
                }
            }

            if (firstRun)
            {
                // BR-AYR-004: activation moves the prior Active year to Closing; BR-AYR-005: closing window default 60 days, configurable.
                await _years.ActivateAsync(target.Id, cancellationToken);
                var sourceReloaded = await _db.AcademicYears.SingleAsync(y => y.Id == batch.SourceAcademicYearId, cancellationToken);
                var batchReloaded = await LoadBatchAsync(batchId, cancellationToken);
                sourceReloaded.ClosingEndsOn = _clock.UtcNow.Date.AddDays(closingWindowDays);
                batchReloaded.Status = RolloverBatchStatus.Activated;
                batchReloaded.ActivatedAtUtc = _clock.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        /// <summary>One student = one committed unit. Returns false when the student isn't ready (pending re-registration / no section) and is left for a later run.</summary>
        private async Task<bool> ActivateOneAsync(int stateId, AcademicYear source, AcademicYear target, CancellationToken ct)
        {
            var state = await _db.RolloverStudentStates.SingleAsync(s => s.Id == stateId, ct);
            var sourceEnrollment = await _db.Enrollments.SingleAsync(e => e.Id == state.SourceEnrollmentId, ct);
            using var tx = await _db.Database.BeginTransactionAsync(ct);

            if (state.Decision == PromotionDecision.Graduate)
            {
                var student = await _db.Students.SingleAsync(s => s.Id == state.StudentId, ct);
                if (student.Status == StudentStatus.Enrolled || student.Status == StudentStatus.Suspended)
                {
                    // Student.Status is T1 reason-required (BR-STU-003); the rollover IS the reason (BR-AYR §4 step 3 "exit to Graduate").
                    _audit.Reason ??= $"Year-end rollover: graduating-grade exit (BR-AYR-008, batch {state.RolloverBatchId})";
                    await _students.ChangeStatusAsync(student.Id, StudentStatus.Graduated, ct);
                }

                CloseSourceEnrollment(sourceEnrollment, EnrollmentStatus.Completed, source);
            }
            else if (state.ReRegistration == ReRegistrationStatus.Declined)
            {
                // Not returning: the source enrollment stays for WF-03 withdrawal to close with clearance — this pass only records the outcome.
            }
            else if (state.ReRegistration == ReRegistrationStatus.Confirmed && state.TargetGradeYearProfileId != null && state.AssignedSectionId != null)
            {
                // Idempotency backstop beyond the state marker: an Active target-year enrollment (direct admission, or a partially
                // committed earlier attempt) is adopted rather than duplicated (BR-GLB-024).
                var existing = await _db.Enrollments.SingleOrDefaultAsync(
                    e => e.StudentId == state.StudentId && e.AcademicYearId == target.Id && e.Status == EnrollmentStatus.Active, ct);
                var enrollment = existing ?? await _students.EnrollAsync(state.StudentId, state.TargetGradeYearProfileId.Value, target.StartDate, EnrollmentSourceType.Rollover, ct);
                var hasMembership = await _db.SectionMemberships.AnyAsync(m => m.EnrollmentId == enrollment.Id && m.EffectiveToUtc == null, ct);
                if (!hasMembership)
                {
                    await _sections.AssignMembershipAsync(state.AssignedSectionId.Value, enrollment.Id, target.StartDate, ct);
                }

                state.TargetEnrollmentId = enrollment.Id;
                CloseSourceEnrollment(sourceEnrollment, state.Decision == PromotionDecision.Retain ? EnrollmentStatus.Completed : EnrollmentStatus.Promoted, source);
            }
            else
            {
                await tx.RollbackAsync(ct);
                return false;   // straggler — a later ActivateAsync run picks them up once confirmed + assigned
            }

            state.ActivatedAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return true;
        }

        private static void CloseSourceEnrollment(Enrollment enrollment, EnrollmentStatus status, AcademicYear source)
        {
            if (enrollment.Status == EnrollmentStatus.Active)
            {
                enrollment.Status = status;
                enrollment.ExitDate = source.EndDate;
            }
        }

        // ------------------------------------------------------------------ step 7

        public async Task<decimal> PostCarryForwardAsync(int batchId, int openingBalanceFeeCategoryId, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
        {
            var batch = await LoadBatchAsync(batchId, cancellationToken);
            RequireStatus(batch, RolloverBatchStatus.Activated);
            var sourceYearId = batch.SourceAcademicYearId;

            var studentIds = await _db.Charges
                .Where(c => c.AcademicYearId == sourceYearId && c.Status == ChargeStatus.Posted)
                .Select(c => c.StudentId).Distinct().OrderBy(id => id).ToListAsync(cancellationToken);
            var alreadyCarried = await _db.Charges
                .Where(c => c.SourceType == ChargeSourceType.OpeningBalance && c.SourceAcademicYearId == sourceYearId)
                .Select(c => c.StudentId).Distinct().ToListAsync(cancellationToken);
            var targetYearId = batch.TargetAcademicYearId;
            var alreadyCarriedSet = new HashSet<int>(alreadyCarried);

            var processed = 0;
            foreach (var studentId in studentIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (alreadyCarriedSet.Contains(studentId))
                {
                    continue;   // idempotent per student — a killed run resumes here
                }

                var total = await CarryForwardOneAsync(studentId, batchId, sourceYearId, targetYearId, openingBalanceFeeCategoryId, cancellationToken);
                _db.ChangeTracker.Clear();   // same tracker-growth discipline as ActivateAsync
                if (total > 0m)
                {
                    processed++;
                    progress?.Report(processed);
                }
            }

            var (opening, transferred) = await CarryForwardTotalsAsync(sourceYearId, cancellationToken);
            if (!CarryForwardCalculator.Reconciles(transferred, opening))
            {
                throw new CarryForwardReconciliationException(transferred, opening);
            }

            var batchReloaded = await LoadBatchAsync(batchId, cancellationToken);
            batchReloaded.CarryForwardPostedAtUtc = _clock.UtcNow;
            batchReloaded.CarryForwardTotal = opening;
            await _db.SaveChangesAsync(cancellationToken);
            return opening;
        }

        private async Task<decimal> CarryForwardOneAsync(int studentId, int batchId, int sourceYearId, int targetYearId, int feeCategoryId, CancellationToken ct)
        {
            var charges = await _db.Charges
                .Where(c => c.AcademicYearId == sourceYearId && c.StudentId == studentId && c.Status == ChargeStatus.Posted)
                .Select(c => new { c.Id, c.PayerId, c.GrossAmount }).ToListAsync(ct);
            var chargeIds = charges.Select(c => c.Id).ToList();
            // Sqlite can't Sum() decimals in SQL — materialize, then aggregate in memory (see build conventions).
            var credited = (await _db.CreditNotes.Where(n => chargeIds.Contains(n.ChargeId)).Select(n => new { n.ChargeId, n.Amount }).ToListAsync(ct))
                .GroupBy(x => x.ChargeId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
            var discounted = (await _db.DiscountDocuments.Where(d => chargeIds.Contains(d.ChargeId)).Select(d => new { d.ChargeId, d.Amount }).ToListAsync(ct))
                .GroupBy(x => x.ChargeId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
            var allocated = (await _db.PaymentAllocations.Where(a => chargeIds.Contains(a.ChargeId)).Select(a => new { a.ChargeId, a.AllocatedAmount }).ToListAsync(ct))
                .GroupBy(x => x.ChargeId).ToDictionary(g => g.Key, g => g.Sum(x => x.AllocatedAmount));

            var plan = CarryForwardCalculator.PlanForStudent(charges.Select(c => new ChargeRemainder(
                c.Id, c.PayerId, c.GrossAmount,
                credited.TryGetValue(c.Id, out var cr) ? cr : 0m,
                discounted.TryGetValue(c.Id, out var di) ? di : 0m,
                allocated.TryGetValue(c.Id, out var al) ? al : 0m)));
            if (plan.Count == 0)
            {
                return 0m;
            }

            using var tx = await _db.Database.BeginTransactionAsync(ct);
            var studentTotal = 0m;
            foreach (var (payerId, (total, lines)) in plan.OrderBy(p => p.Key))
            {
                await _fees.PostOpeningBalanceAsync(studentId, payerId, targetYearId, sourceYearId, feeCategoryId, total, ct);
                await _db.SaveChangesAsync(ct);   // one charge per save keeps BR-FEE-005's hash chain linear
                foreach (var line in lines)
                {
                    await _fees.IssueCarryForwardCreditNoteAsync(line.ChargeId, line.Remaining, ct);
                }

                studentTotal += total;
            }

            var state = await _db.RolloverStudentStates.SingleOrDefaultAsync(s => s.RolloverBatchId == batchId && s.StudentId == studentId, ct);
            if (state != null)
            {
                state.CarryForwardAmount = studentTotal;   // students without a state (e.g. withdrawn mid-year with a balance) still carry forward
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return studentTotal;
        }

        private async Task<(decimal OpeningBalances, decimal Transferred)> CarryForwardTotalsAsync(int sourceYearId, CancellationToken ct)
        {
            var opening = (await _db.Charges
                .Where(c => c.SourceType == ChargeSourceType.OpeningBalance && c.SourceAcademicYearId == sourceYearId && c.Status == ChargeStatus.Posted)
                .Select(c => c.GrossAmount).ToListAsync(ct)).Sum();
            var transferred = (await (
                from n in _db.CreditNotes
                join c in _db.Charges on n.ChargeId equals c.Id
                where n.IsCarryForward && c.AcademicYearId == sourceYearId
                select n.Amount).ToListAsync(ct)).Sum();
            return (opening, transferred);
        }

        public async Task<IReadOnlyList<ChecklistItem>> GetClosingChecklistAsync(int batchId, CancellationToken cancellationToken = default)
        {
            var batch = await LoadBatchAsync(batchId, cancellationToken);
            return ClosingChecklistEvaluator.Evaluate(await ClosingFactsAsync(batch, cancellationToken));
        }

        private async Task<ClosingChecklistFacts> ClosingFactsAsync(RolloverBatch batch, CancellationToken ct)
        {
            var s = batch.SourceAcademicYearId;
            var (opening, transferred) = await CarryForwardTotalsAsync(s, ct);
            return new ClosingChecklistFacts
            {
                // Module 17 has no "Voided" marksheet state (E-302/E-402) — "approved or explicitly voided" reduces to Published here.
                UnresolvedMarksheetCount = await _db.Marksheets.CountAsync(m => m.AcademicYearId == s && m.Status != MarksheetStatus.Published, ct),
                OpenWorkflowInstanceCount = await _db.WorkflowInstances.CountAsync(w => w.AcademicYearId == s && !w.IsClosed, ct),
                CarryForwardPosted = batch.CarryForwardPostedAtUtc != null,
                CarryForwardReconciled = batch.CarryForwardPostedAtUtc != null && CarryForwardCalculator.Reconciles(transferred, opening),
            };
        }

        public async Task CloseSourceYearAsync(int batchId, CancellationToken cancellationToken = default)
        {
            var batch = await LoadBatchAsync(batchId, cancellationToken);
            RequireStatus(batch, RolloverBatchStatus.Activated);
            var items = ClosingChecklistEvaluator.Evaluate(await ClosingFactsAsync(batch, cancellationToken));
            if (!ClosingChecklistEvaluator.IsGreen(items))
            {
                throw new ChecklistNotGreenException("Closing", items);
            }

            await _years.CloseAsync(batch.SourceAcademicYearId, cancellationToken);
            batch.Status = RolloverBatchStatus.Closed;
            batch.ClosedAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        // ------------------------------------------------------------------ progress + helpers

        public async Task<RolloverProgress> GetProgressAsync(int batchId, CancellationToken cancellationToken = default)
        {
            var batch = await LoadBatchAsync(batchId, cancellationToken);
            var states = await _db.RolloverStudentStates.Where(s => s.RolloverBatchId == batchId).ToListAsync(cancellationToken);
            return new RolloverProgress
            {
                TotalStudents = states.Count,
                Decided = states.Count(s => s.Decision != PromotionDecision.Undecided),
                Undecided = states.Count(s => s.Decision == PromotionDecision.Undecided),
                ProposedGraduates = states.Count(s => s.Decision == PromotionDecision.Graduate),
                ManualOverrides = states.Count(s => s.DecisionSource == PromotionDecisionSource.Manual),
                Confirmed = states.Count(s => s.ReRegistration == ReRegistrationStatus.Confirmed),
                Declined = states.Count(s => s.ReRegistration == ReRegistrationStatus.Declined),
                PendingReRegistration = states.Count(s => s.ReRegistration == ReRegistrationStatus.Pending),
                Assigned = states.Count(s => s.AssignedSectionId != null),
                ConfirmedUnassigned = states.Count(s => s.ReRegistration == ReRegistrationStatus.Confirmed && s.AssignedSectionId == null && s.TargetEnrollmentId == null),
                Enrolled = states.Count(s => s.TargetEnrollmentId != null),
                Processed = states.Count(s => s.ActivatedAtUtc != null),
                CarriedForward = states.Count(s => s.CarryForwardAmount > 0m),
                CarryForwardTotal = batch.CarryForwardTotal ?? 0m,
            };
        }

        private async Task<RolloverBatch> LoadBatchAsync(int batchId, CancellationToken ct)
            => await _db.RolloverBatches.SingleAsync(b => b.Id == batchId, ct);

        private async Task<RolloverStudentState> LoadStateAsync(int batchId, int studentId, CancellationToken ct)
            => await _db.RolloverStudentStates.SingleAsync(s => s.RolloverBatchId == batchId && s.StudentId == studentId, ct);

        private static void RequireStatus(RolloverBatch batch, params RolloverBatchStatus[] allowed)
        {
            if (!allowed.Contains(batch.Status))
            {
                throw new RolloverBatchStatusException(batch.Id, batch.Status, RolloverStepBlocker.BatchStage, allowed);
            }
        }

        private sealed class GradeInfo
        {
            public int GradeLevelId { get; set; }

            public int? PromotionTargetGradeLevelId { get; set; }

            public bool IsGraduating { get; set; }
        }

        private async Task<Dictionary<int, GradeInfo>> GradeByProfileAsync(CancellationToken ct)
        {
            return await (
                from p in _db.GradeYearProfiles
                join g in _db.GradeLevels on p.GradeLevelId equals g.Id
                select new { p.Id, Info = new GradeInfo { GradeLevelId = g.Id, PromotionTargetGradeLevelId = g.PromotionTargetGradeLevelId, IsGraduating = g.IsGraduating } })
                .ToDictionaryAsync(x => x.Id, x => x.Info, ct);
        }

        private async Task<Dictionary<int, int>> TargetProfilesByGradeAsync(int targetYearId, CancellationToken ct)
            => await _db.GradeYearProfiles.Where(p => p.AcademicYearId == targetYearId).ToDictionaryAsync(p => p.GradeLevelId, p => p.Id, ct);

        /// <summary>Same resolution as LibraryAdmin/StoreAdmin: the financially-responsible guardian's Payer (BR-FEE-004).</summary>
        private async Task<int?> ResolvePayerAsync(int studentId, CancellationToken ct)
        {
            var parentIds = await _db.StudentGuardianLinks
                .Where(l => l.StudentId == studentId && l.IsFinanciallyResponsible && l.EffectiveToUtc == null)
                .Select(l => l.ParentId).ToListAsync(ct);
            return await _db.Payers.Where(p => p.ParentId != null && parentIds.Contains(p.ParentId.Value))
                .OrderBy(p => p.Id).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        }
    }
}
