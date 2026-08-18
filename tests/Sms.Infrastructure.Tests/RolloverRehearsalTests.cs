using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Fees;
using Sms.Domain.Rollover;
using Sms.Domain.Schools;
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
using Xunit.Abstractions;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// S8/E-801 exit gate (Implementation/05 §"Gates at stage exits", S8): "rollover completes on
    /// pilot-scale data within its maintenance window, resumable after kill." Pilot = one KSA school
    /// of 800–2,000 students (Implementation/07). This rehearsal runs the whole WF-02 on a 1,020-student
    /// cohort over Sqlite in-memory, kills the activation pass and the carry-forward pass mid-way via
    /// cancellation, resumes both, and proves nothing was lost or duplicated.
    /// </summary>
    public sealed class RolloverRehearsalTests : IDisposable
    {
        private const int StudentsPerGrade = 340;   // × 3 grades = 1,020 students, inside the pilot band

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

        private readonly ITestOutputHelper _output;
        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();
        private readonly RolloverFixture _fx;
        private readonly TimeSpan _seedTime;

        public RolloverRehearsalTests(ITestOutputHelper output)
        {
            _output = output;
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();
            var sw = Stopwatch.StartNew();
            _fx = RolloverFixture.Seed(db, _clock.UtcNow, StudentsPerGrade, chargeEveryStudent: true);
            _seedTime = sw.Elapsed;
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
            return new RolloverAdmin(db, _clock, _user, _audit, new GradeStructureAdmin(db), new StudentAdmin(db, numbers), new SectionAdmin(db),
                new FeeAdmin(db, numbers, _clock), new AcademicYearAdmin(db));
        }

        [Fact]
        [BusinessRule("BR-AYR-008")]
        public async Task Full_rollover_on_a_pilot_scale_cohort_completes_and_survives_a_kill_in_every_long_pass()
        {
            var total = Stopwatch.StartNew();
            _output.WriteLine($"seed: {_fx.StudentIds.Count} students in {_seedTime.TotalSeconds:F1}s");

            // ---- steps 1–3
            var sw = Stopwatch.StartNew();
            int batchId;
            using (var db = CreateContext())
            {
                var admin = CreateAdmin(db);
                var batch = await admin.OpenBatchAsync(_fx.SourceYearId, _fx.TargetYearId);
                batchId = batch.Id;
                Assert.Equal(_fx.StudentIds.Count, await db.RolloverStudentStates.CountAsync(s => s.RolloverBatchId == batchId));
                var proposed = await admin.ProposePromotionsAsync(batchId);
                Assert.Equal(_fx.StudentIds.Count * 3 / 4, proposed);   // fixture: 3 of every 4 have a year result

                var undecided = await db.RolloverStudentStates.Where(s => s.RolloverBatchId == batchId && s.Decision == PromotionDecision.Undecided).ToListAsync();
                var g3 = _fx.SourceProfileIds["G3"];
                foreach (var s in undecided)
                {
                    await admin.DecideAsync(batchId, s.StudentId, s.SourceGradeYearProfileId == g3 ? PromotionDecision.Graduate : PromotionDecision.Promote, "Registrar review (rehearsal)");
                    db.ChangeTracker.Clear();   // each decision is its own request in production (fresh scoped context)
                }

                await admin.ApprovePromotionsAsync(batchId);
            }

            _output.WriteLine($"steps 1-3 (open, propose, decide {_fx.StudentIds.Count / 4}, approve): {sw.Elapsed.TotalSeconds:F1}s");

            // ---- steps 4–5
            sw.Restart();
            int expectedEnrolled;
            using (var db = CreateContext())
            {
                var admin = CreateAdmin(db);
                await _fx.AddTargetYearStructureAsync(db);
                var returning = await db.RolloverStudentStates.Where(s => s.RolloverBatchId == batchId && s.Decision != PromotionDecision.Graduate).OrderBy(s => s.StudentId).ToListAsync();
                // ~5% decline (every 20th), the rest confirm with the re-registration fee
                var declined = 0;
                foreach (var (s, i) in returning.Select((s, i) => (s, i)))
                {
                    if (i % 20 == 0)
                    {
                        await admin.DeclineReRegistrationAsync(batchId, s.StudentId);
                        declined++;
                    }
                    else
                    {
                        await admin.ConfirmReRegistrationAsync(batchId, s.StudentId, _fx.ReRegistrationCategoryId);
                    }

                    db.ChangeTracker.Clear();   // each confirmation is its own request in production (fresh scoped context)
                }

                expectedEnrolled = returning.Count - declined;
                foreach (var code in new[] { "G1", "G2", "G3" })
                {
                    var pid = _fx.TargetProfileId(db, code);
                    // enough seats: 3 profiles × ~450 max each → 16 sections of 30
                    for (var i = 0; i < 8; i++)
                    {
                        await _fx.CreateTargetSectionsAsync(db, pid, capacity: 30, suffix: i.ToString());
                    }

                    Assert.Empty(await admin.AutoAssignSectionsAsync(batchId, pid));
                }

                await admin.DeferTimetableAsync(batchId, "rehearsal");
                var progress = await admin.GetProgressAsync(batchId);
                Assert.Equal(0, progress.ConfirmedUnassigned);
                _output.WriteLine($"steps 4-5 (confirm {expectedEnrolled} + fee, decline {declined}, auto-assign): {sw.Elapsed.TotalSeconds:F1}s");
            }

            // ---- step 6: kill after ~200 students, then resume
            sw.Restart();
            const int killAfter = 200;
            using (var db = CreateContext())
            {
                var admin = CreateAdmin(db);
                using var cts = new CancellationTokenSource();
                var seen = 0;
                var sync = new SyncProgress(n => { seen = n; if (n >= killAfter) { cts.Cancel(); } });
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => admin.ActivateAsync(batchId, 60, sync, cts.Token));
                Assert.Equal(killAfter, seen);
            }

            using (var db = CreateContext())
            {
                // exactly the committed prefix survived the kill — no partial student, no year flip
                Assert.Equal(AcademicYearStatus.Preparation, (await db.AcademicYears.SingleAsync(y => y.Id == _fx.TargetYearId)).Status);
                var processed = await db.RolloverStudentStates.CountAsync(s => s.RolloverBatchId == batchId && s.ActivatedAtUtc != null);
                Assert.Equal(killAfter, processed);
                var enrolledSoFar = await db.Enrollments.CountAsync(e => e.AcademicYearId == _fx.TargetYearId);
                Assert.Equal(await db.RolloverStudentStates.CountAsync(s => s.RolloverBatchId == batchId && s.TargetEnrollmentId != null), enrolledSoFar);
                _output.WriteLine($"step 6 first run killed after {killAfter} students in {sw.Elapsed.TotalSeconds:F1}s ({enrolledSoFar} enrollments committed)");
            }

            sw.Restart();
            using (var db = CreateContext())
            {
                await CreateAdmin(db).ActivateAsync(batchId);   // resume
            }

            using (var db = CreateContext())
            {
                Assert.Equal(AcademicYearStatus.Active, (await db.AcademicYears.SingleAsync(y => y.Id == _fx.TargetYearId)).Status);
                Assert.Equal(AcademicYearStatus.Closing, (await db.AcademicYears.SingleAsync(y => y.Id == _fx.SourceYearId)).Status);
                Assert.Equal(expectedEnrolled, await db.Enrollments.CountAsync(e => e.AcademicYearId == _fx.TargetYearId && e.SourceType == EnrollmentSourceType.Rollover));
                var duplicates = await db.Enrollments.Where(e => e.AcademicYearId == _fx.TargetYearId && e.Status == EnrollmentStatus.Active)
                    .GroupBy(e => e.StudentId).CountAsync(g => g.Count() > 1);
                Assert.Equal(0, duplicates);
                var withoutSection = await db.Enrollments.Where(e => e.AcademicYearId == _fx.TargetYearId)
                    .CountAsync(e => !db.SectionMemberships.Any(m => m.EnrollmentId == e.Id && m.EffectiveToUtc == null));
                Assert.Equal(0, withoutSection);
                var graduates = await db.RolloverStudentStates.CountAsync(s => s.RolloverBatchId == batchId && s.Decision == PromotionDecision.Graduate);
                Assert.Equal(graduates, await db.Students.CountAsync(s => s.Status == StudentStatus.Graduated));
                Assert.Equal(0, await db.Enrollments.CountAsync(e => e.AcademicYearId == _fx.SourceYearId && e.Status == EnrollmentStatus.Active
                    && !db.RolloverStudentStates.Any(s => s.RolloverBatchId == batchId && s.SourceEnrollmentId == e.Id && s.ReRegistration == ReRegistrationStatus.Declined)));
                _output.WriteLine($"step 6 resume ({expectedEnrolled - killAfter + graduates} remaining) + year activation: {sw.Elapsed.TotalSeconds:F1}s");
            }

            // ---- step 7: kill the carry-forward after ~150 students, then resume; hard reconciliation
            sw.Restart();
            const int killCarryAfter = 150;
            using (var db = CreateContext())
            {
                var admin = CreateAdmin(db);
                using var cts = new CancellationTokenSource();
                var sync = new SyncProgress(n => { if (n >= killCarryAfter) { cts.Cancel(); } });
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => admin.PostCarryForwardAsync(batchId, _fx.OpeningBalanceCategoryId, sync, cts.Token));
            }

            using (var db = CreateContext())
            {
                Assert.Equal(killCarryAfter, await db.Charges.CountAsync(c => c.SourceType == ChargeSourceType.OpeningBalance));
                Assert.Null((await db.RolloverBatches.SingleAsync(b => b.Id == batchId)).CarryForwardPostedAtUtc);
            }

            decimal carried;
            using (var db = CreateContext())
            {
                carried = await CreateAdmin(db).PostCarryForwardAsync(batchId, _fx.OpeningBalanceCategoryId);
            }

            using (var db = CreateContext())
            {
                var owing = _fx.StudentIds.Count - 1;   // student[1] is fully credited in the fixture
                Assert.Equal(owing, await db.Charges.CountAsync(c => c.SourceType == ChargeSourceType.OpeningBalance));
                Assert.Equal(owing * 1150m, carried);
                var notes = (await db.CreditNotes.Where(n => n.IsCarryForward).Select(n => n.Amount).ToListAsync()).Sum();
                Assert.Equal(carried, notes);
                _output.WriteLine($"step 7 carry-forward killed at {killCarryAfter}, resumed to {owing} students, {carried:N0} carried: {sw.Elapsed.TotalSeconds:F1}s");
            }

            // ---- close
            using (var db = CreateContext())
            {
                var admin = CreateAdmin(db);
                await admin.CloseSourceYearAsync(batchId);
                Assert.Equal(AcademicYearStatus.Closed, (await db.AcademicYears.SingleAsync(y => y.Id == _fx.SourceYearId)).Status);
                var progress = await admin.GetProgressAsync(batchId);
                Assert.Equal(_fx.StudentIds.Count, progress.TotalStudents);
                Assert.Equal(expectedEnrolled, progress.Enrolled);
                Assert.Equal(carried, progress.CarryForwardTotal);
            }

            _output.WriteLine($"TOTAL rollover wall-clock (excl. seed): {total.Elapsed.TotalSeconds:F1}s for {_fx.StudentIds.Count} students");
            // Maintenance-window sanity bound for the Sqlite rehearsal; the real gate is re-measured on SQL Server at pilot (P4 open).
            Assert.True(total.Elapsed < TimeSpan.FromMinutes(10), $"rollover took {total.Elapsed}");
        }

        /// <summary>Synchronous IProgress — the BCL Progress&lt;T&gt; marshals asynchronously, which would make the kill point non-deterministic.</summary>
        private sealed class SyncProgress : IProgress<int>
        {
            private readonly Action<int> _onReport;

            public SyncProgress(Action<int> onReport) => _onReport = onReport;

            public void Report(int value) => _onReport(value);
        }
    }
}
