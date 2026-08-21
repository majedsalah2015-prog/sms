using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Attendance;
using Sms.Domain.Common;
using Sms.Domain.Grading;
using Sms.Domain.Numbering;
using Sms.Domain.Payments;
using Sms.Domain.Schools;
using Sms.Domain.Subjects;
using Sms.Infrastructure.Attendance;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Fees;
using Sms.Infrastructure.Grading;
using Sms.Infrastructure.Installments;
using Sms.Infrastructure.Notifications;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Payments;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.ReadModels;
using Sms.Infrastructure.Statements;
using Sms.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// S8/E-802 — DB/04 §6 performance acceptance gates: "NF-P3 (P95 ≤ 2 s) verified against a seeded 5,000-student
    /// demo tenant for: cashier screen position load, attendance sheet save (NF-P4 ≤ 1 s), marksheet open/save,
    /// pipeline board, statement render" — plus the heavy-report path (snapshot refresh, NF-P5 ≤ 10 s). Runs on Sqlite
    /// in-memory, so the numbers are indicative (single connection, no network, no SQL Server plan cache) and the
    /// budgets are the doc's; the real gate is re-measured on SQL Server at pilot (P4 open). Cohort size defaults to
    /// 1,000 (pilot band 800–2,000) and scales via SMS_PERF_STUDENTS (5000 = the doc's demo tenant).
    /// </summary>
    public sealed class PerfGateTests : IDisposable
    {
        private static readonly int Students = int.TryParse(Environment.GetEnvironmentVariable("SMS_PERF_STUDENTS"), out var n) && n >= 30 ? n : 1000;
        private const int SectionSize = 30;

        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2027, 3, 10, 8, 0, 0, DateTimeKind.Utc);
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

        public PerfGateTests(ITestOutputHelper output)
        {
            _output = output;
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();
            var sw = Stopwatch.StartNew();
            _fx = RolloverFixture.Seed(db, _clock.UtcNow, studentsPerGrade: Students / 3, chargeEveryStudent: true, sourceSectionSize: SectionSize);
            db.NumberingSeries.Add(new NumberingSeries { Code = "RCP", EntityName = "Receipt", FormatTemplate = "RCP-{SEQ:6}", ResetPolicy = ResetPolicy.Never, GapPolicy = GapPolicy.Strict, EffectiveFromUtc = _clock.UtcNow.AddYears(-1), IsActive = true });
            db.NumberingSeries.Add(new NumberingSeries { Code = "STM", EntityName = "StatementIssue", FormatTemplate = "STM-{SEQ:6}", ResetPolicy = ResetPolicy.Never, GapPolicy = GapPolicy.Normal, EffectiveFromUtc = _clock.UtcNow.AddYears(-1), IsActive = true });
            db.SaveChanges();
            _seedTime = sw.Elapsed;
            _tenant.AcademicYearId = _fx.SourceYearId;
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
            => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options, _tenant, _user, _clock, _audit);

        [Fact]
        public async Task Db04_section6_gates_hold_on_the_seeded_tenant()
        {
            _output.WriteLine($"tenant: {_fx.StudentIds.Count} students, sections of {SectionSize}, seeded in {_seedTime.TotalSeconds:F1}s (SMS_PERF_STUDENTS to scale)");
            var gates = new List<PerfGate>();
            var rng = new Random(2027);
            var sampleStudents = Enumerable.Range(0, 40).Select(_ => _fx.StudentIds[rng.Next(_fx.StudentIds.Count)]).ToList();

            // ---- cashier: receipt capture then position load (NF-P3 ≤ 2 s)
            var receiptGate = new PerfGate("cashier receipt capture + auto-allocation", TimeSpan.FromSeconds(2));
            var positionGate = new PerfGate("cashier position load (ComputeStudentPositionAsync)", TimeSpan.FromSeconds(2));
            using (var db = CreateContext())
            {
                var numbers = new NumberIssuer(db, _tenant, _tenant, _clock);
                var payments = new PaymentAdmin(db, numbers, _clock);
                var fees = new FeeAdmin(db, numbers, _clock);
                var till = await payments.OpenTillSessionAsync(_user.UserId, "TILL-1", 0m);
                var payerOf = await db.Charges.Where(c => sampleStudents.Contains(c.StudentId)).Select(c => new { c.StudentId, c.PayerId }).Distinct().ToDictionaryAsync(x => x.StudentId, x => x.PayerId);
                await receiptGate.SampleAsync(sampleStudents.Count, i =>
                    payments.CaptureReceiptAsync(payerOf[sampleStudents[i]], PaymentMethod.Cash, 400m, till.Id).ContinueWith(_ => db.ChangeTracker.Clear()));
                await positionGate.SampleAsync(sampleStudents.Count, i => fees.ComputeStudentPositionAsync(sampleStudents[i]));
            }

            gates.Add(receiptGate);
            gates.Add(positionGate);

            // ---- attendance sheet save: one section of 30 (NF-P4 ≤ 1 s per section)
            var attendanceGate = new PerfGate("attendance sheet save (30-student section)", TimeSpan.FromSeconds(1));
            var sections = new List<int>();
            using (var db = CreateContext())
            {
                sections = await db.Sections.Where(s => s.AcademicYearId == _fx.SourceYearId).OrderBy(s => s.Id).Select(s => s.Id).ToListAsync();
                var attendance = new AttendanceAdmin(db);
                var day = new DateTime(2027, 3, 10);
                foreach (var sectionId in sections.Take(10))
                {
                    var enrollmentIds = await db.SectionMemberships.Where(m => m.SectionId == sectionId && m.EffectiveToUtc == null).Select(m => m.EnrollmentId).ToListAsync();
                    await attendanceGate.SampleAsync(async () =>
                    {
                        foreach (var (e, i) in enrollmentIds.Select((e, i) => (e, i)))
                        {
                            await attendance.CaptureAsync(e, day, i % 9 == 0 ? AttendanceStatus.AbsentUnexcused : AttendanceStatus.Present, _user.UserId);
                        }
                    });
                    db.ChangeTracker.Clear();
                }
            }

            gates.Add(attendanceGate);

            // ---- marksheet open / save (NF-P3 ≤ 2 s)
            var marksheetOpenGate = new PerfGate("marksheet open (load entries)", TimeSpan.FromSeconds(2));
            var marksheetSaveGate = new PerfGate("marksheet save (30 marks × 1 component)", TimeSpan.FromSeconds(2));
            using (var db = CreateContext())
            {
                var grading = new GradingAdmin(db, _clock, _audit);
                var semester = new Semester { AcademicYearId = _fx.SourceYearId, SequenceNumber = 1, NameAr = "١", NameEn = "S1", StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2027, 1, 31) };
                db.Semesters.Add(semester);
                await db.SaveChangesAsync();
                var term = new Term { AcademicYearId = _fx.SourceYearId, SemesterId = semester.Id, SequenceNumber = 1, NameAr = "١", NameEn = "T1", StartDate = semester.StartDate, EndDate = semester.EndDate };
                db.Terms.Add(term);
                var subject = new Subject { Code = "MATH", Name = new LocalizedName("رياضيات", "Math"), Category = "Core" };
                db.Subjects.Add(subject);
                await db.SaveChangesAsync();
                var offering = new CurriculumOffering { AcademicYearId = _fx.SourceYearId, GradeYearProfileId = _fx.SourceProfileIds["G1"], SubjectId = subject.Id, WeeklyPeriods = 5, IsAssessable = true, GpaWeight = 1m, EffectiveFromUtc = new DateTime(2026, 9, 1) };
                db.CurriculumOfferings.Add(offering);
                await db.SaveChangesAsync();
                var scale = await grading.DefineScaleAsync(_fx.StageId, "سلم", "Scale");
                await grading.AddScaleBandAsync(scale.Id, 0m, 59.99m, "F", "راسب", "Fail", isPassing: false, sortOrder: 1);
                await grading.AddScaleBandAsync(scale.Id, 60m, 100m, "P", "ناجح", "Pass", isPassing: true, sortOrder: 2);
                await grading.LockScaleAsync(scale.Id);
                var blueprint = await grading.DefineBlueprintAsync(offering.Id, term.Id, scale.Id);
                var component = await grading.AddBlueprintComponentAsync(blueprint.Id, "اختبار", "Test", 100m, 100m);
                await grading.LockBlueprintAsync(blueprint.Id);
                var g1Sections = await db.Sections.Where(s => s.GradeYearProfileId == _fx.SourceProfileIds["G1"]).OrderBy(s => s.Id).Select(s => s.Id).Take(5).ToListAsync();
                foreach (var sectionId in g1Sections)
                {
                    var sheet = await grading.CreateMarksheetAsync(blueprint.Id, sectionId);
                    db.ChangeTracker.Clear();
                    List<MarkEntry> entries = new();
                    await marksheetOpenGate.SampleAsync(async () => entries = await db.MarkEntries.AsNoTracking().Where(m => m.MarksheetId == sheet.Id).ToListAsync());
                    await marksheetSaveGate.SampleAsync(async () =>
                    {
                        foreach (var e in entries)
                        {
                            await grading.EnterMarkAsync(sheet.Id, component.Id, e.EnrollmentId, 55m + e.EnrollmentId % 45, isAbsent: false, isExempt: false);
                        }
                    });
                    db.ChangeTracker.Clear();
                }
            }

            gates.Add(marksheetOpenGate);
            gates.Add(marksheetSaveGate);

            // ---- statement render (NF-P5 standard report ≤ 10 s; interactive statement page NF-P3 ≤ 2 s — hold it to 2)
            var statementGate = new PerfGate("payer statement render (StatementService.BuildAsync)", TimeSpan.FromSeconds(2));
            using (var db = CreateContext())
            {
                var statements = new StatementService(db, new NumberIssuer(db, _tenant, _tenant, _clock), _clock);
                var payerIds = await db.Charges.Where(c => sampleStudents.Contains(c.StudentId)).Select(c => c.PayerId).Distinct().ToListAsync();
                await statementGate.SampleAsync(payerIds.Count, i => statements.BuildAsync(payerIds[i]));
            }

            gates.Add(statementGate);

            // ---- pipeline / seat board (NF-P3 ≤ 2 s) and the read-model views
            var boardGate = new PerfGate("seat utilization board (vw_SeatUtilization)", TimeSpan.FromSeconds(2));
            var positionsViewGate = new PerfGate("whole-school student positions (vw_StudentPosition)", TimeSpan.FromSeconds(10));
            using (var db = CreateContext())
            {
                var views = new ReadModelQuery(db);
                await boardGate.SampleAsync(5, _ => views.GetSeatUtilizationAsync(_fx.SourceYearId));
                await positionsViewGate.SampleAsync(3, _ => views.GetStudentPositionsAsync());
            }

            gates.Add(boardGate);
            gates.Add(positionsViewGate);

            // ---- heavy-report path: snapshot refreshes over the whole tenant (NF-P5 ≤ 10 s)
            var agedGate = new PerfGate("snap_AgedReceivables refresh (whole tenant)", TimeSpan.FromSeconds(10));
            var dailyGate = new PerfGate("snap_DailyAttendanceSummary refresh", TimeSpan.FromSeconds(10));
            using (var db = CreateContext())
            {
                var refresher = new SnapshotRefreshService(db, _clock, new InstallmentAdmin(db, _clock, _audit, _tenant, new NotificationPublisher(db), new FeeAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock), _clock)));
                await agedGate.SampleAsync(3, async _ => { await refresher.RefreshAgedReceivablesAsync(); db.ChangeTracker.Clear(); });
                await dailyGate.SampleAsync(3, async _ => { await refresher.RefreshDailyAttendanceSummaryAsync(new DateTime(2027, 3, 10)); db.ChangeTracker.Clear(); });
                // every student owes (1150, or 750 after the sampled 400 receipt) except the fixture's fully-credited student[1]
                Assert.Equal(_fx.StudentIds.Count - 1, await db.AgedReceivablesSnapshots.CountAsync());
            }

            gates.Add(agedGate);
            gates.Add(dailyGate);

            foreach (var g in gates)
            {
                _output.WriteLine(g.Summary());
            }

            var failed = gates.Where(g => !g.Passed).Select(g => g.Summary()).ToList();
            Assert.True(failed.Count == 0, "Perf gates failed:\n" + string.Join("\n", failed));
        }
    }
}
