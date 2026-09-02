using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Rollover;
using Sms.Domain.Calendar;
using Sms.Domain.Common;
using Sms.Domain.Fees;
using Sms.Domain.Grades;
using Sms.Domain.Grading;
using Sms.Domain.Numbering;
using Sms.Domain.Parents;
using Sms.Domain.Rollover;
using Sms.Domain.Schools;
using Sms.Domain.Sections;
using Sms.Domain.Students;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Fees;
using Sms.Infrastructure.Grades;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Rollover;
using Sms.Infrastructure.Schools;
using Sms.Infrastructure.Sections;
using Sms.Infrastructure.Students;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// S8/E-801: WF-02 rollover end to end over Sqlite. Fixture: source year Active
    /// (G1→G2→G3, G3 graduating), target year Preparation, students with guardians/payers,
    /// year results, approved target fee structures. Rehearsal (pilot-scale + kill/resume)
    /// lives in <see cref="RolloverRehearsalTests"/>.
    /// </summary>
    public sealed class RolloverAdminTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2027, 6, 15, 8, 0, 0, DateTimeKind.Utc);
        }

        private sealed class FixedUser : ICurrentUser
        {
            public int UserId { get; set; } = 42;
        }

        private sealed class FixedTenant : ITenantContext, IWorkingYearContext
        {
            public int SchoolId => 1;
            public int AcademicYearId { get; set; }
        }

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();
        private readonly RolloverFixture _fx;

        public RolloverAdminTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();
            _fx = RolloverFixture.Seed(db, _clock.UtcNow, studentsPerGrade: 4);
            _tenant.AcademicYearId = _fx.SourceYearId;
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private RolloverAdmin CreateAdmin(AppDbContext db)
        {
            var numbers = new NumberIssuer(db, _tenant, _tenant, _clock);
            return new RolloverAdmin(db, _clock, _user, _audit, new GradeStructureAdmin(db), new StudentAdmin(db, numbers, new AuditEventWriter(db, _tenant, _tenant, _user, _clock, _audit)), new SectionAdmin(db),
                new FeeAdmin(db, numbers, _clock), new AcademicYearAdmin(db));
        }

        // ---------------------------------------------------------------- steps 1–3

        [Fact]
        [BusinessRule("BR-AYR-008")]
        public async Task Opening_a_batch_copies_grade_profiles_and_seeds_one_state_per_active_enrollment()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);

            var batch = await admin.OpenBatchAsync(_fx.SourceYearId, _fx.TargetYearId);

            Assert.Equal(RolloverBatchStatus.Open, batch.Status);
            Assert.Equal(3, await db.GradeYearProfiles.CountAsync(p => p.AcademicYearId == _fx.TargetYearId));
            Assert.Equal(12, await db.RolloverStudentStates.CountAsync(s => s.RolloverBatchId == batch.Id));

            // idempotent: same pair → same batch, no duplicate states
            var again = await admin.OpenBatchAsync(_fx.SourceYearId, _fx.TargetYearId);
            Assert.Equal(batch.Id, again.Id);
            Assert.Equal(12, await db.RolloverStudentStates.CountAsync(s => s.RolloverBatchId == batch.Id));
        }

        [Fact]
        [BusinessRule("BR-AYR-008")]
        public async Task A_batch_requires_an_active_source_and_a_preparation_target()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            await Assert.ThrowsAsync<RolloverYearStatusException>(() => admin.OpenBatchAsync(_fx.TargetYearId, _fx.SourceYearId));
        }

        [Fact]
        [BusinessRule("BR-GRD-002")]
        public async Task Opening_refuses_when_an_enrolled_grade_has_no_promotion_target()
        {
            using var db = CreateContext();
            var g2 = await db.GradeLevels.SingleAsync(g => g.Code == "G2");
            g2.PromotionTargetGradeLevelId = null;
            await db.SaveChangesAsync();

            var ex = await Assert.ThrowsAsync<PromotionPathIncompleteException>(() => CreateAdmin(db).OpenBatchAsync(_fx.SourceYearId, _fx.TargetYearId));
            Assert.Contains(g2.Id, ex.GradeLevelIdsMissingTarget);
        }

        [Fact]
        [BusinessRule("BR-GRA-006")]
        public async Task Proposals_come_from_year_results_and_graduating_grades_exit()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var batch = await admin.OpenBatchAsync(_fx.SourceYearId, _fx.TargetYearId);

            var decided = await admin.ProposePromotionsAsync(batch.Id);

            // 12 students, one per grade has no year result → 9 decided
            Assert.Equal(9, decided);
            var states = await db.RolloverStudentStates.Where(s => s.RolloverBatchId == batch.Id).ToListAsync();
            var g1 = states.Where(s => s.SourceGradeYearProfileId == _fx.SourceProfileIds["G1"]).ToList();
            Assert.Contains(g1, s => s.Decision == PromotionDecision.Promote && s.TargetGradeYearProfileId == _fx.TargetProfileId(db, "G2"));
            Assert.Contains(g1, s => s.Decision == PromotionDecision.Retain && s.TargetGradeYearProfileId == _fx.TargetProfileId(db, "G1"));
            Assert.Contains(g1, s => s.Decision == PromotionDecision.Undecided && s.TargetGradeYearProfileId == null);
            var g3 = states.Where(s => s.SourceGradeYearProfileId == _fx.SourceProfileIds["G3"]).ToList();
            Assert.Equal(2, g3.Count(s => s.Decision == PromotionDecision.Graduate && s.ReRegistration == ReRegistrationStatus.NotApplicable));
        }

        [Fact]
        [BusinessRule("BR-AYR-008")]
        public async Task Manual_decisions_need_a_reason_and_survive_re_proposal()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var batch = await admin.OpenBatchAsync(_fx.SourceYearId, _fx.TargetYearId);
            await admin.ProposePromotionsAsync(batch.Id);
            var undecided = await db.RolloverStudentStates.FirstAsync(s => s.RolloverBatchId == batch.Id && s.Decision == PromotionDecision.Undecided);

            await Assert.ThrowsAsync<ArgumentException>(() => admin.DecideAsync(batch.Id, undecided.StudentId, PromotionDecision.Promote, " "));
            await Assert.ThrowsAsync<InvalidPromotionDecisionException>(() => admin.DecideAsync(batch.Id, undecided.StudentId, PromotionDecision.Graduate, "not a graduating grade"));

            await admin.DecideAsync(batch.Id, undecided.StudentId, PromotionDecision.Promote, "Registrar review: makeup exam passed");
            await admin.ProposePromotionsAsync(batch.Id);   // re-run must not overwrite the manual decision

            var state = await db.RolloverStudentStates.SingleAsync(s => s.Id == undecided.Id);
            Assert.Equal(PromotionDecision.Promote, state.Decision);
            Assert.Equal(PromotionDecisionSource.Manual, state.DecisionSource);
            Assert.NotNull(state.TargetGradeYearProfileId);
        }

        [Fact]
        [BusinessRule("BR-AYR-008")]
        public async Task Approval_is_refused_while_anyone_is_undecided()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var batch = await admin.OpenBatchAsync(_fx.SourceYearId, _fx.TargetYearId);
            await admin.ProposePromotionsAsync(batch.Id);

            await Assert.ThrowsAsync<PromotionsUndecidedException>(() => admin.ApprovePromotionsAsync(batch.Id));

            await DecideStragglersAsync(admin, db, batch.Id);
            await admin.ApprovePromotionsAsync(batch.Id);
            var reloaded = await db.RolloverBatches.SingleAsync(b => b.Id == batch.Id);
            Assert.Equal(RolloverBatchStatus.PromotionsApproved, reloaded.Status);
            Assert.Equal(42, reloaded.PromotionsApprovedByUserId);
        }

        // ---------------------------------------------------------------- steps 4–5

        [Fact]
        [BusinessRule("BR-AYR-003")]
        public async Task Confirming_re_registration_posts_the_fee_into_the_preparation_year_and_is_idempotent()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var batch = await admin.OpenBatchAsync(_fx.SourceYearId, _fx.TargetYearId);
            await admin.ProposePromotionsAsync(batch.Id);
            await _fx.AddTargetYearStructureAsync(db);   // approved re-registration line per target profile
            var promoted = await db.RolloverStudentStates.FirstAsync(s => s.RolloverBatchId == batch.Id && s.Decision == PromotionDecision.Promote);

            await admin.ConfirmReRegistrationAsync(batch.Id, promoted.StudentId, _fx.ReRegistrationCategoryId);
            await admin.ConfirmReRegistrationAsync(batch.Id, promoted.StudentId, _fx.ReRegistrationCategoryId);

            var state = await db.RolloverStudentStates.SingleAsync(s => s.Id == promoted.Id);
            Assert.Equal(ReRegistrationStatus.Confirmed, state.ReRegistration);
            var charge = await db.Charges.SingleAsync(c => c.Id == state.ReRegistrationChargeId);
            Assert.Equal(ChargeSourceType.ReRegistration, charge.SourceType);
            Assert.Equal(_fx.TargetYearId, charge.AcademicYearId);
            Assert.Equal(1, await db.Charges.CountAsync(c => c.StudentId == promoted.StudentId && c.SourceType == ChargeSourceType.ReRegistration));
        }

        [Fact]
        [BusinessRule("BR-GRD-006")]
        public async Task Seat_reservation_is_capped_at_planned_seats()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var batch = await admin.OpenBatchAsync(_fx.SourceYearId, _fx.TargetYearId);
            await admin.ProposePromotionsAsync(batch.Id);
            var g2Target = _fx.TargetProfileId(db, "G2");
            var profile = await db.GradeYearProfiles.SingleAsync(p => p.Id == g2Target);
            profile.TargetSections = 1;
            profile.TargetSectionSize = 1;   // one planned seat
            await db.SaveChangesAsync();
            var toG2 = await db.RolloverStudentStates.Where(s => s.RolloverBatchId == batch.Id && s.TargetGradeYearProfileId == g2Target).Take(2).ToListAsync();

            await admin.ConfirmReRegistrationAsync(batch.Id, toG2[0].StudentId);
            await Assert.ThrowsAsync<NoSeatAvailableException>(() => admin.ConfirmReRegistrationAsync(batch.Id, toG2[1].StudentId));
        }

        [Fact]
        [BusinessRule("BR-SCN-008")]
        public async Task Auto_assignment_places_confirmed_students_and_manual_assignment_enforces_capacity()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var batch = await admin.OpenBatchAsync(_fx.SourceYearId, _fx.TargetYearId);
            await admin.ProposePromotionsAsync(batch.Id);
            var g2Target = _fx.TargetProfileId(db, "G2");
            var (sectionA, sectionB) = await _fx.CreateTargetSectionsAsync(db, g2Target, capacity: 2);
            var toG2 = await db.RolloverStudentStates.Where(s => s.RolloverBatchId == batch.Id && s.TargetGradeYearProfileId == g2Target).ToListAsync();
            Assert.Equal(3, toG2.Count);   // 2 promoted out of G1 + 1 retained in G2 (fixture pattern per grade: Promote, Promote, Retain, none)
            foreach (var s in toG2)
            {
                await admin.ConfirmReRegistrationAsync(batch.Id, s.StudentId);
            }

            var unplaced = await admin.AutoAssignSectionsAsync(batch.Id, g2Target);

            Assert.Empty(unplaced);
            var assigned = await db.RolloverStudentStates.Where(s => s.RolloverBatchId == batch.Id && s.TargetGradeYearProfileId == g2Target).Select(s => s.AssignedSectionId).ToListAsync();
            Assert.All(assigned, a => Assert.NotNull(a));
            // size balance: 3 students over 2 empty sections of capacity 2 → 2 + 1 (least-filled first, ties by lower section id)
            var lower = Math.Min(sectionA, sectionB);
            var higher = Math.Max(sectionA, sectionB);
            Assert.Equal(2, assigned.Count(a => a == lower));
            Assert.Equal(1, assigned.Count(a => a == higher));

            // manual move into the full section is refused; into the one with room succeeds
            var inHigher = await db.RolloverStudentStates.FirstAsync(s => s.RolloverBatchId == batch.Id && s.AssignedSectionId == higher);
            await Assert.ThrowsAsync<SectionFullException>(() => admin.AssignSectionAsync(batch.Id, inHigher.StudentId, lower));
            var inLower = await db.RolloverStudentStates.FirstAsync(s => s.RolloverBatchId == batch.Id && s.AssignedSectionId == lower);
            await admin.AssignSectionAsync(batch.Id, inLower.StudentId, higher);
            Assert.Equal(higher, (await db.RolloverStudentStates.SingleAsync(x => x.Id == inLower.Id)).AssignedSectionId);
        }

        // ---------------------------------------------------------------- step 6

        [Fact]
        [BusinessRule("BR-AYR-004")]
        public async Task Activation_is_gated_by_the_opening_checklist()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var batch = await admin.OpenBatchAsync(_fx.SourceYearId, _fx.TargetYearId);
            await admin.ProposePromotionsAsync(batch.Id);
            await DecideStragglersAsync(admin, db, batch.Id);
            await admin.ApprovePromotionsAsync(batch.Id);

            // no sections / no timetable / no calendar yet
            var ex = await Assert.ThrowsAsync<ChecklistNotGreenException>(() => admin.ActivateAsync(batch.Id));
            var failing = ex.Items.Where(i => !i.IsSatisfied).Select(i => i.Code).ToList();
            Assert.Contains(OpeningChecklistEvaluator.Sections, failing);
            Assert.Contains(OpeningChecklistEvaluator.Timetable, failing);
            Assert.Contains(OpeningChecklistEvaluator.Calendar, failing);
            Assert.Contains(OpeningChecklistEvaluator.Fees, failing);          // no target-year structure lines yet
            Assert.DoesNotContain(OpeningChecklistEvaluator.Grades, failing);  // profiles were copied at batch open
            Assert.DoesNotContain(OpeningChecklistEvaluator.Promotions, failing);
            Assert.Equal(AcademicYearStatus.Preparation, (await db.AcademicYears.SingleAsync(y => y.Id == _fx.TargetYearId)).Status);
        }

        [Fact]
        [BusinessRule("BR-AYR-008")]
        public async Task Activation_enrolls_confirmed_students_graduates_leavers_and_activates_the_year()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var batch = await RunThroughApprovalAndStructureAsync(admin, db);

            await admin.ActivateAsync(batch.Id, closingWindowDays: 45);

            var source = await db.AcademicYears.SingleAsync(y => y.Id == _fx.SourceYearId);
            var target = await db.AcademicYears.SingleAsync(y => y.Id == _fx.TargetYearId);
            Assert.Equal(AcademicYearStatus.Active, target.Status);
            Assert.Equal(AcademicYearStatus.Closing, source.Status);
            Assert.Equal(_clock.UtcNow.Date.AddDays(45), source.ClosingEndsOn);

            var states = await db.RolloverStudentStates.Where(s => s.RolloverBatchId == batch.Id).ToListAsync();
            var enrolledStates = states.Where(s => s.TargetEnrollmentId != null).ToList();
            // 12 students − 2 graduates (G3 with results) − 1 declined = 9 enrolled ... minus the G3 retained/undecided handled by DecideStragglers
            Assert.Equal(states.Count(s => s.ReRegistration == ReRegistrationStatus.Confirmed), enrolledStates.Count);
            foreach (var s in enrolledStates)
            {
                var e = await db.Enrollments.SingleAsync(x => x.Id == s.TargetEnrollmentId);
                Assert.Equal(EnrollmentSourceType.Rollover, e.SourceType);
                Assert.Equal(_fx.TargetYearId, e.AcademicYearId);
                Assert.Equal(s.TargetGradeYearProfileId, e.GradeYearProfileId);
                Assert.True(await db.SectionMemberships.AnyAsync(m => m.EnrollmentId == e.Id && m.SectionId == s.AssignedSectionId && m.EffectiveToUtc == null));
                var old = await db.Enrollments.SingleAsync(x => x.Id == s.SourceEnrollmentId);
                Assert.NotEqual(EnrollmentStatus.Active, old.Status);
                Assert.Equal(source.EndDate, old.ExitDate);
            }

            foreach (var g in states.Where(s => s.Decision == PromotionDecision.Graduate))
            {
                Assert.Equal(StudentStatus.Graduated, (await db.Students.SingleAsync(x => x.Id == g.StudentId)).Status);
                Assert.Equal(EnrollmentStatus.Completed, (await db.Enrollments.SingleAsync(x => x.Id == g.SourceEnrollmentId)).Status);
                Assert.Null(g.TargetEnrollmentId);
            }

            var declined = states.Single(s => s.ReRegistration == ReRegistrationStatus.Declined);
            Assert.Null(declined.TargetEnrollmentId);
            Assert.NotNull(declined.ActivatedAtUtc);

            // BR-GLB-024: exactly one Active enrollment per student in the target year
            var dupes = await db.Enrollments.Where(e => e.AcademicYearId == _fx.TargetYearId && e.Status == EnrollmentStatus.Active)
                .GroupBy(e => e.StudentId).Where(g => g.Count() > 1).CountAsync();
            Assert.Equal(0, dupes);
        }

        [Fact]
        [BusinessRule("BR-AYR-008")]
        public async Task Re_running_activation_is_a_no_op_for_completed_students_and_picks_up_stragglers()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var batch = await RunThroughApprovalAndStructureAsync(admin, db, leaveOnePending: true);
            await admin.ActivateAsync(batch.Id);
            var before = await db.Enrollments.CountAsync(e => e.AcademicYearId == _fx.TargetYearId);
            var pending = await db.RolloverStudentStates.SingleAsync(s => s.RolloverBatchId == batch.Id && s.ReRegistration == ReRegistrationStatus.Pending);
            Assert.Null(pending.ActivatedAtUtc);

            await admin.ActivateAsync(batch.Id);   // nothing new
            Assert.Equal(before, await db.Enrollments.CountAsync(e => e.AcademicYearId == _fx.TargetYearId));

            // straggler confirms after activation → next run enrolls exactly them
            await admin.ConfirmReRegistrationAsync(batch.Id, pending.StudentId);
            await admin.AutoAssignSectionsAsync(batch.Id, pending.TargetGradeYearProfileId!.Value);
            await admin.ActivateAsync(batch.Id);
            Assert.Equal(before + 1, await db.Enrollments.CountAsync(e => e.AcademicYearId == _fx.TargetYearId));
            Assert.NotNull((await db.RolloverStudentStates.SingleAsync(s => s.Id == pending.Id)).TargetEnrollmentId);
        }

        // ---------------------------------------------------------------- step 7

        [Fact]
        [BusinessRule("BR-AYR-009")]
        public async Task Carry_forward_moves_source_year_receivables_into_opening_balances_and_reconciles()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var batch = await RunThroughApprovalAndStructureAsync(admin, db);
            await admin.ActivateAsync(batch.Id);
            var fees = new FeeAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock), _clock);
            var owing = _fx.StudentIds[0];
            var settled = _fx.StudentIds[1];
            var positionBefore = await fees.ComputeStudentPositionAsync(owing);   // 1150 (fixture charge, unpaid; this student declined so no re-registration fee)
            Assert.Equal(1150m, positionBefore);

            var total = await admin.PostCarryForwardAsync(batch.Id, _fx.OpeningBalanceCategoryId);

            Assert.Equal(1150m, total);
            var opening = await db.Charges.SingleAsync(c => c.StudentId == owing && c.SourceType == ChargeSourceType.OpeningBalance);
            Assert.Equal(_fx.TargetYearId, opening.AcademicYearId);
            Assert.Equal(_fx.SourceYearId, opening.SourceAcademicYearId);
            Assert.Equal(1150m, opening.GrossAmount);
            Assert.Equal(0m, opening.VatAmount);
            var cfNote = await db.CreditNotes.SingleAsync(n => n.IsCarryForward);
            Assert.Equal(1150m, cfNote.Amount);
            // BR-GLB-064: overall position unchanged — the money moved years, it wasn't forgiven
            Assert.Equal(1150m, await fees.ComputeStudentPositionAsync(owing));
            Assert.False(await db.Charges.AnyAsync(c => c.StudentId == settled && c.SourceType == ChargeSourceType.OpeningBalance));

            // idempotent
            Assert.Equal(1150m, await admin.PostCarryForwardAsync(batch.Id, _fx.OpeningBalanceCategoryId));
            Assert.Equal(1, await db.Charges.CountAsync(c => c.SourceType == ChargeSourceType.OpeningBalance));
            Assert.Equal(1150m, (await db.RolloverBatches.SingleAsync(b => b.Id == batch.Id)).CarryForwardTotal);
        }

        [Fact]
        [BusinessRule("BR-AYR-005")]
        public async Task Closing_requires_the_closing_checklist_then_closes_the_source_year()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var batch = await RunThroughApprovalAndStructureAsync(admin, db);
            await admin.ActivateAsync(batch.Id);

            var ex = await Assert.ThrowsAsync<ChecklistNotGreenException>(() => admin.CloseSourceYearAsync(batch.Id));
            Assert.Contains(ex.Items, i => i.Code == ClosingChecklistEvaluator.CarryForward && !i.IsSatisfied);

            await admin.PostCarryForwardAsync(batch.Id, _fx.OpeningBalanceCategoryId);
            await admin.CloseSourceYearAsync(batch.Id);

            Assert.Equal(AcademicYearStatus.Closed, (await db.AcademicYears.SingleAsync(y => y.Id == _fx.SourceYearId)).Status);
            Assert.Equal(RolloverBatchStatus.Closed, (await db.RolloverBatches.SingleAsync(b => b.Id == batch.Id)).Status);
            var progress = await admin.GetProgressAsync(batch.Id);
            Assert.Equal(12, progress.TotalStudents);
            Assert.Equal(0, progress.Undecided);
            Assert.Equal(1150m, progress.CarryForwardTotal);
        }

        // ---------------------------------------------------------------- helpers

        private async Task DecideStragglersAsync(RolloverAdmin admin, AppDbContext db, int batchId)
        {
            var undecided = await db.RolloverStudentStates.Where(s => s.RolloverBatchId == batchId && s.Decision == PromotionDecision.Undecided).ToListAsync();
            var g3 = _fx.SourceProfileIds["G3"];
            foreach (var s in undecided)
            {
                await admin.DecideAsync(batchId, s.StudentId, s.SourceGradeYearProfileId == g3 ? PromotionDecision.Graduate : PromotionDecision.Promote, "Registrar review");
            }
        }

        /// <summary>Steps 1–5 + target-year structure (sections, calendar, grading scale, timetable deferral) so activation can run.</summary>
        private async Task<RolloverBatch> RunThroughApprovalAndStructureAsync(RolloverAdmin admin, AppDbContext db, bool leaveOnePending = false)
        {
            var batch = await admin.OpenBatchAsync(_fx.SourceYearId, _fx.TargetYearId);
            await admin.ProposePromotionsAsync(batch.Id);
            await DecideStragglersAsync(admin, db, batch.Id);
            await admin.ApprovePromotionsAsync(batch.Id);

            await _fx.AddTargetYearStructureAsync(db);   // approved re-registration lines must exist before confirmations post the fee

            var states = await db.RolloverStudentStates.Where(s => s.RolloverBatchId == batch.Id && s.Decision != PromotionDecision.Graduate).OrderBy(s => s.StudentId).ToListAsync();
            await admin.DeclineReRegistrationAsync(batch.Id, states[0].StudentId);
            foreach (var s in states.Skip(1).Take(leaveOnePending ? states.Count - 2 : states.Count - 1))
            {
                await admin.ConfirmReRegistrationAsync(batch.Id, s.StudentId, _fx.ReRegistrationCategoryId);
            }

            foreach (var code in new[] { "G1", "G2", "G3" })
            {
                var pid = _fx.TargetProfileId(db, code);
                await _fx.CreateTargetSectionsAsync(db, pid, capacity: 30);
                Assert.Empty(await admin.AutoAssignSectionsAsync(batch.Id, pid));
            }

            await admin.DeferTimetableAsync(batch.Id, "Timetable finalized after activation (BR-AYR-004 explicit deferral)");
            return batch;
        }
    }

    /// <summary>Shared seed for the rollover tests (behavioural + rehearsal). Everything is per-school tenant 1.</summary>
    internal sealed class RolloverFixture
    {
        public int SourceYearId { get; private set; }
        public int TargetYearId { get; private set; }
        public int StageId { get; private set; }
        public Dictionary<string, int> GradeIds { get; } = new();
        public Dictionary<string, int> SourceProfileIds { get; } = new();
        public List<int> StudentIds { get; } = new();
        public int TuitionCategoryId { get; private set; }
        public int ReRegistrationCategoryId { get; private set; }
        public int OpeningBalanceCategoryId { get; private set; }

        public int TargetProfileId(AppDbContext db, string gradeCode)
            => db.GradeYearProfiles.Single(p => p.AcademicYearId == TargetYearId && p.GradeLevelId == GradeIds[gradeCode]).Id;

        /// <summary>
        /// Per grade: student[0] Promote, [1] Promote, [2] Retain, [3] no year result (Undecided); with 4/grade. For larger
        /// cohorts (rehearsal) the pattern repeats mod 4. Student[0] of the whole set owes 1150 in the source year; student[1]
        /// has a fully credited charge (settled).
        /// </summary>
        public static RolloverFixture Seed(AppDbContext db, DateTime now, int studentsPerGrade, bool chargeEveryStudent = false, int sourceSectionSize = int.MaxValue)
        {
            var fx = new RolloverFixture();
            foreach (var (code, entity, format) in new[] { ("INV", "Charge", "INV-{SEQ:6}"), ("CRN", "CreditNote", "CRN-{SEQ:6}") })
            {
                db.NumberingSeries.Add(new NumberingSeries { Code = code, EntityName = entity, FormatTemplate = format, ResetPolicy = ResetPolicy.Never, GapPolicy = GapPolicy.Normal, EffectiveFromUtc = now.AddYears(-2), IsActive = true });
            }

            var source = new AcademicYear { LabelAr = "٢٠٢٦-٢٠٢٧", LabelEn = "2026-2027", HijriLabel = "1448", StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2027, 6, 30), Status = AcademicYearStatus.Active };
            var target = new AcademicYear { LabelAr = "٢٠٢٧-٢٠٢٨", LabelEn = "2027-2028", HijriLabel = "1449", StartDate = new DateTime(2027, 9, 1), EndDate = new DateTime(2028, 6, 30), Status = AcademicYearStatus.Preparation };
            db.AcademicYears.AddRange(source, target);
            var stage = new Stage { Name = new LocalizedName("ابتدائي", "Elementary"), SequenceOrder = 1, DefaultGenderPolicy = GenderPolicy.Mixed };
            db.Stages.Add(stage);
            db.SaveChanges();
            fx.SourceYearId = source.Id;
            fx.TargetYearId = target.Id;
            fx.StageId = stage.Id;

            var g3 = new GradeLevel { StageId = stage.Id, Code = "G3", Name = new LocalizedName("الثالث", "Grade 3"), SequenceOrder = 3, IsGraduating = true };
            db.GradeLevels.Add(g3);
            db.SaveChanges();
            var g2 = new GradeLevel { StageId = stage.Id, Code = "G2", Name = new LocalizedName("الثاني", "Grade 2"), SequenceOrder = 2, PromotionTargetGradeLevelId = g3.Id };
            db.GradeLevels.Add(g2);
            db.SaveChanges();
            var g1 = new GradeLevel { StageId = stage.Id, Code = "G1", Name = new LocalizedName("الأول", "Grade 1"), SequenceOrder = 1, PromotionTargetGradeLevelId = g2.Id };
            db.GradeLevels.Add(g1);
            db.SaveChanges();
            fx.GradeIds["G1"] = g1.Id;
            fx.GradeIds["G2"] = g2.Id;
            fx.GradeIds["G3"] = g3.Id;

            foreach (var (code, grade) in new[] { ("G1", g1), ("G2", g2), ("G3", g3) })
            {
                // planned seats (sections × size) must cover the largest possible inflow (~1.5 × a grade's cohort) — copied into the target year at batch open
                var plannedSections = Math.Max(2, (int)Math.Ceiling(studentsPerGrade * 1.5 / 30));
                var profile = new GradeYearProfile { GradeLevelId = grade.Id, AcademicYearId = source.Id, GenderPolicy = GenderPolicy.Mixed, TargetSections = plannedSections, TargetSectionSize = 30 };
                db.GradeYearProfiles.Add(profile);
                db.SaveChanges();
                fx.SourceProfileIds[code] = profile.Id;
                Section section = null!;
                for (var i = 0; i < studentsPerGrade; i++)
                {
                    if (i % sourceSectionSize == 0)
                    {
                        // one section per `sourceSectionSize` students (default: one big section per grade, as the E-801 tests expect)
                        var ordinal = i / sourceSectionSize;
                        section = new Section { AcademicYearId = source.Id, GradeYearProfileId = profile.Id, NameAr = code + "-" + ordinal, NameEn = code + "-" + (char)('A' + ordinal % 26) + (ordinal >= 26 ? ordinal.ToString() : string.Empty), Capacity = Math.Min(studentsPerGrade, sourceSectionSize) + 5, GenderPolicy = GenderPolicy.Mixed };
                        db.Sections.Add(section);
                        db.SaveChanges();
                    }

                    var n = fx.StudentIds.Count + 1;
                    var student = new Student
                    {
                        StudentNo = $"STU-{n:D6}",
                        FirstNameAr = "طالب", FatherNameAr = "أب", GrandfatherNameAr = "جد", FamilyNameAr = "عائلة",
                        FirstNameEn = "Student" + n, FatherNameEn = "Father", GrandfatherNameEn = "Grandfather", FamilyNameEn = "Family",
                        Gender = i % 2 == 0 ? Gender.Male : Gender.Female, DateOfBirth = new DateTime(2018, 1, 1), NationalityLookupId = 1,
                    };
                    db.Students.Add(student);
                    var parent = new Parent { ParentFileNo = $"PAR-{n:D6}", NameAr = "ولي أمر", NameEn = "Guardian" + n, PrimaryMobile = "0500000000" };
                    db.Parents.Add(parent);
                    db.SaveChanges();
                    db.Payers.Add(new Payer { Type = PayerType.Parent, ParentId = parent.Id });
                    db.StudentGuardianLinks.Add(new StudentGuardianLink { StudentId = student.Id, ParentId = parent.Id, RelationshipLookupId = 1, IsPrimaryContact = true, IsFinanciallyResponsible = true, EffectiveFromUtc = new DateTime(2026, 9, 1) });
                    var enrollment = new Enrollment { AcademicYearId = source.Id, StudentId = student.Id, GradeYearProfileId = profile.Id, EnrollmentDate = new DateTime(2026, 9, 1), SourceType = EnrollmentSourceType.Admission };
                    db.Enrollments.Add(enrollment);
                    db.SaveChanges();
                    db.SectionMemberships.Add(new SectionMembership { AcademicYearId = source.Id, SectionId = section.Id, EnrollmentId = enrollment.Id, EffectiveFromUtc = new DateTime(2026, 9, 1) });

                    var outcome = (i % 4) switch { 0 => PromotionOutcome.Promote, 1 => PromotionOutcome.Promote, 2 => PromotionOutcome.Retain, _ => (PromotionOutcome?)null };
                    if (outcome != null)
                    {
                        db.YearResults.Add(new YearResult { AcademicYearId = source.Id, EnrollmentId = enrollment.Id, Gpa = 3.5m, FailedSubjectCount = 0, PromotionOutcome = outcome.Value, ComputedAtUtc = now });
                    }

                    db.SaveChanges();
                    fx.StudentIds.Add(student.Id);
                    db.ChangeTracker.Clear();   // rehearsal scale: keep the seed's tracker flat
                }
            }

            // Fees: tuition (15% VAT), re-registration, opening-balance categories; approved target-year structure lines for the two billable ones.
            var tuition = new FeeCategory { NameAr = "رسوم دراسية", NameEn = "Tuition", VatRate = 0.15m, IsMandatory = true, IsRefundable = false, IsServiceLinked = false, IsActive = true };
            var rereg = new FeeCategory { NameAr = "رسوم إعادة تسجيل", NameEn = "Re-registration", VatRate = 0.15m, IsMandatory = true, IsRefundable = false, IsServiceLinked = false, IsActive = true };
            var opening = new FeeCategory { NameAr = "رصيد افتتاحي", NameEn = "Opening balance", VatRate = null, IsMandatory = false, IsRefundable = false, IsServiceLinked = false, IsActive = true };
            db.FeeCategories.AddRange(tuition, rereg, opening);
            db.SaveChanges();
            fx.TuitionCategoryId = tuition.Id;
            fx.ReRegistrationCategoryId = rereg.Id;
            fx.OpeningBalanceCategoryId = opening.Id;

            // Source-year receivables: student[0] owes a full unpaid tuition charge (1000 + 15% VAT); student[1] has one fully credited.
            var payer0 = db.Payers.OrderBy(p => p.Id).First();
            var payer1 = db.Payers.OrderBy(p => p.Id).Skip(1).First();
            var c0 = new Charge { AcademicYearId = source.Id, StudentId = fx.StudentIds[0], PayerId = payer0.Id, FeeCategoryId = tuition.Id, SourceType = ChargeSourceType.Registration, ChargeNo = "INV-SEED-1", NetAmount = 1000m, VatRateSnapshot = 0.15m, VatAmount = 150m, GrossAmount = 1150m, Status = ChargeStatus.Posted, PostedAtUtc = now.AddMonths(-8), InvoiceUuid = Guid.NewGuid() };
            var c1 = new Charge { AcademicYearId = source.Id, StudentId = fx.StudentIds[1], PayerId = payer1.Id, FeeCategoryId = tuition.Id, SourceType = ChargeSourceType.Registration, ChargeNo = "INV-SEED-2", NetAmount = 1000m, VatRateSnapshot = 0.15m, VatAmount = 150m, GrossAmount = 1150m, Status = ChargeStatus.Posted, PostedAtUtc = now.AddMonths(-8), InvoiceUuid = Guid.NewGuid() };
            db.Charges.AddRange(c0, c1);
            db.SaveChanges();
            db.CreditNotes.Add(new CreditNote { ChargeId = c1.Id, CreditNoteNo = "CRN-SEED-1", Amount = 1150m, Reason = "Waived", IssuedAtUtc = now.AddMonths(-2) });
            db.SaveChanges();

            if (chargeEveryStudent)
            {
                // Rehearsal scale: every remaining student owes one unpaid tuition charge so the carry-forward runs at cohort scale.
                var payersByParent = db.Payers.ToDictionary(p => p.ParentId!.Value, p => p.Id);
                var parentByStudent = db.StudentGuardianLinks.ToDictionary(l => l.StudentId, l => l.ParentId);
                var seq = 3;
                foreach (var studentId in fx.StudentIds.Skip(2))
                {
                    db.Charges.Add(new Charge
                    {
                        AcademicYearId = source.Id, StudentId = studentId, PayerId = payersByParent[parentByStudent[studentId]], FeeCategoryId = tuition.Id,
                        SourceType = ChargeSourceType.Registration, ChargeNo = $"INV-SEED-{seq++}", NetAmount = 1000m, VatRateSnapshot = 0.15m, VatAmount = 150m, GrossAmount = 1150m,
                        Status = ChargeStatus.Posted, PostedAtUtc = now.AddMonths(-8), InvoiceUuid = Guid.NewGuid(),
                    });
                }

                db.SaveChanges();
            }

            return fx;
        }

        /// <summary>Approved target-year fee structure lines for every copied profile + a calendar day + a grading scale (opening checklist inputs).</summary>
        public async Task AddTargetYearStructureAsync(AppDbContext db)
        {
            foreach (var profile in await db.GradeYearProfiles.Where(p => p.AcademicYearId == TargetYearId).ToListAsync())
            {
                foreach (var (cat, amount) in new[] { (TuitionCategoryId, 12000m), (ReRegistrationCategoryId, 500m) })
                {
                    if (!await db.FeeStructureLines.AnyAsync(l => l.GradeYearProfileId == profile.Id && l.FeeCategoryId == cat))
                    {
                        db.FeeStructureLines.Add(new FeeStructureLine { AcademicYearId = TargetYearId, GradeYearProfileId = profile.Id, FeeCategoryId = cat, Amount = amount, Status = FeeStructureLineStatus.Approved });
                    }
                }
            }

            db.CalendarDays.Add(new CalendarDay { AcademicYearId = TargetYearId, Date = new DateTime(2027, 9, 23), DayType = DayType.Holiday, Source = CalendarDaySource.Manual });
            db.GradingScales.Add(new GradingScale { AcademicYearId = TargetYearId, StageId = StageId, NameAr = "سلم", NameEn = "Percentage scale" });
            await db.SaveChangesAsync();
        }

        /// <summary>Two Mixed sections in the target year for one profile.</summary>
        public async Task<(int A, int B)> CreateTargetSectionsAsync(AppDbContext db, int targetProfileId, int capacity, string suffix = "")
        {
            var a = new Section { AcademicYearId = TargetYearId, GradeYearProfileId = targetProfileId, NameAr = "أ" + suffix, NameEn = "A" + suffix, Capacity = capacity, GenderPolicy = GenderPolicy.Mixed };
            var b = new Section { AcademicYearId = TargetYearId, GradeYearProfileId = targetProfileId, NameAr = "ب" + suffix, NameEn = "B" + suffix, Capacity = capacity, GenderPolicy = GenderPolicy.Mixed };
            db.Sections.AddRange(a, b);
            await db.SaveChangesAsync();
            return (a.Id, b.Id);
        }
    }
}
