using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Application.ReadModels;
using Sms.Domain.Admissions;
using Sms.Domain.Attendance;
using Sms.Domain.Cafeteria;
using Sms.Domain.Common;
using Sms.Domain.Employees;
using Sms.Domain.Fees;
using Sms.Domain.Payments;
using Sms.Domain.Subjects;
using Sms.Domain.Teachers;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Installments;
using Sms.Infrastructure.Jobs;
using Sms.Infrastructure.Notifications;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.ReadModels;
using Sms.TestSupport;
using Xunit;
using AdmissionApplication = Sms.Domain.Admissions.Application;

namespace Sms.Infrastructure.Tests
{
    /// <summary>S8/E-802 — DB/04 §4 read models ("views") and snapshot tables over the E-801 fixture (12 students, 3 grades).</summary>
    public sealed class ReadModelTests : IDisposable
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

        public ReadModelTests()
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

        private SnapshotRefreshService CreateRefresher(AppDbContext db)
            => new(db, _clock, new InstallmentAdmin(db, _clock, _audit, _tenant, new NotificationPublisher(db)));

        // ---------------------------------------------------------------- views

        [Fact]
        [BusinessRule("BR-FEE-008")]
        public async Task Student_position_view_nets_charges_credits_discounts_and_allocations_per_student_and_payer()
        {
            using var db = CreateContext();
            // student[0]: 1150 unpaid; student[1]: 1150 fully credited (fixture). Add a partial allocation to student[0].
            var owingCharge = await db.Charges.SingleAsync(c => c.StudentId == _fx.StudentIds[0]);
            var receipt = new Receipt { PayerId = owingCharge.PayerId, ReceiptNo = "RCP-1", Method = PaymentMethod.Cash, Amount = 150m, IssuedAtUtc = _clock.UtcNow };
            db.Receipts.Add(receipt);
            await db.SaveChangesAsync();
            db.PaymentAllocations.Add(new PaymentAllocation { ReceiptId = receipt.Id, ChargeId = owingCharge.Id, AllocatedAmount = 150m });
            await db.SaveChangesAsync();

            var all = await new ReadModelQuery(db).GetStudentPositionsAsync();
            var one = await new ReadModelQuery(db).GetStudentPositionsAsync(_fx.StudentIds[0]);

            Assert.Equal(2, all.Count);
            var owing = all.Single(r => r.StudentId == _fx.StudentIds[0]);
            Assert.Equal(1150m, owing.Charges);
            Assert.Equal(150m, owing.Allocated);
            Assert.Equal(1000m, owing.Position);
            Assert.Equal(0m, all.Single(r => r.StudentId == _fx.StudentIds[1]).Position);
            Assert.Single(one);
            Assert.Equal(owing, one[0]);
        }

        [Fact]
        [BusinessRule("BR-ATD-009")]
        public async Task Attendance_rates_view_uses_the_canonical_percentage_per_enrollment()
        {
            using var db = CreateContext();
            var section = await db.Sections.FirstAsync(s => s.AcademicYearId == _fx.SourceYearId);
            var enrollments = await db.SectionMemberships.Where(m => m.SectionId == section.Id).Select(m => m.EnrollmentId).ToListAsync();
            var day1 = new DateTime(2027, 5, 2);
            foreach (var (e, i) in enrollments.Select((e, i) => (e, i)))
            {
                db.AttendanceDays.Add(new AttendanceDay { AcademicYearId = _fx.SourceYearId, EnrollmentId = e, SectionId = section.Id, Date = day1, Status = i == 0 ? AttendanceStatus.AbsentUnexcused : AttendanceStatus.Present, CapturedByUserId = 42 });
                db.AttendanceDays.Add(new AttendanceDay { AcademicYearId = _fx.SourceYearId, EnrollmentId = e, SectionId = section.Id, Date = day1.AddDays(1), Status = i == 1 ? AttendanceStatus.Exempted : AttendanceStatus.Present, CapturedByUserId = 42 });
            }

            await db.SaveChangesAsync();

            var rows = await new ReadModelQuery(db).GetAttendanceRatesAsync(section.Id, day1, day1.AddDays(1));

            Assert.Equal(enrollments.Count, rows.Count);
            Assert.Equal(50m, rows.Single(r => r.EnrollmentId == enrollments[0]).AttendancePercent);     // 1 absent of 2
            Assert.Equal(100m, rows.Single(r => r.EnrollmentId == enrollments[1]).AttendancePercent);    // 1 exempted → denominator 1
            Assert.Equal(100m, rows.Single(r => r.EnrollmentId == enrollments[2]).AttendancePercent);
        }

        [Fact]
        [BusinessRule("BR-TCH-004")]
        public async Task Teacher_load_view_sums_current_assignments_against_the_max()
        {
            using var db = CreateContext();
            var employee = new Employee { EmployeeNo = "EMP-1", FirstNameAr = "م", FatherNameAr = "أ", GrandfatherNameAr = "ج", FamilyNameAr = "ع", FirstNameEn = "T", FatherNameEn = "F", GrandfatherNameEn = "G", FamilyNameEn = "L", Gender = Gender.Male, DateOfBirth = new DateTime(1990, 1, 1), NationalityLookupId = 1 };
            db.Employees.Add(employee);
            var subject = new Subject { Code = "MATH", Name = new LocalizedName("رياضيات", "Math"), Category = "Core" };
            db.Subjects.Add(subject);
            await db.SaveChangesAsync();
            var profile = new TeacherProfile { EmployeeId = employee.Id, MaxWeeklyPeriods = 10 };
            db.TeacherProfiles.Add(profile);
            var g1 = _fx.SourceProfileIds["G1"];
            var offering = new CurriculumOffering { AcademicYearId = _fx.SourceYearId, GradeYearProfileId = g1, SubjectId = subject.Id, WeeklyPeriods = 6, IsAssessable = true, GpaWeight = 1m, EffectiveFromUtc = new DateTime(2026, 9, 1) };
            db.CurriculumOfferings.Add(offering);
            await db.SaveChangesAsync();
            var sections = await db.Sections.Where(s => s.AcademicYearId == _fx.SourceYearId).Take(2).ToListAsync();
            db.TeacherAssignments.Add(new TeacherAssignment { AcademicYearId = _fx.SourceYearId, TeacherProfileId = profile.Id, CurriculumOfferingId = offering.Id, SectionId = sections[0].Id, EffectiveFromUtc = new DateTime(2026, 9, 1) });
            db.TeacherAssignments.Add(new TeacherAssignment { AcademicYearId = _fx.SourceYearId, TeacherProfileId = profile.Id, CurriculumOfferingId = offering.Id, SectionId = sections[1].Id, EffectiveFromUtc = new DateTime(2026, 9, 1) });
            db.TeacherAssignments.Add(new TeacherAssignment { AcademicYearId = _fx.SourceYearId, TeacherProfileId = profile.Id, CurriculumOfferingId = offering.Id, SectionId = sections[1].Id, Role = TeacherRole.CoTeacher, EffectiveFromUtc = new DateTime(2026, 9, 1), EffectiveToUtc = new DateTime(2027, 1, 1) });   // ended — not counted
            await db.SaveChangesAsync();

            var rows = await new ReadModelQuery(db).GetTeacherLoadsAsync(_fx.SourceYearId);

            var row = Assert.Single(rows);
            Assert.Equal(12, row.CurrentWeeklyPeriods);
            Assert.Equal(10, row.MaxWeeklyPeriods);
            Assert.True(row.IsOverloaded);
        }

        [Fact]
        [BusinessRule("BR-GRD-006")]
        public async Task Seat_utilization_view_reports_planned_capacity_enrolled_and_pipeline_per_grade()
        {
            using var db = CreateContext();
            var g1 = _fx.SourceProfileIds["G1"];
            var campaign = new AdmissionCampaign { AcademicYearId = _fx.SourceYearId, GradeYearProfileId = g1, OpenDate = new DateTime(2027, 1, 1), CloseDate = new DateTime(2027, 8, 1) };
            db.AdmissionCampaigns.Add(campaign);
            await db.SaveChangesAsync();
            foreach (var status in new[] { ApplicationStatus.Submitted, ApplicationStatus.Approved, ApplicationStatus.Rejected, ApplicationStatus.Registered })
            {
                db.Applications.Add(new AdmissionApplication
                {
                    AcademicYearId = _fx.SourceYearId, CampaignId = campaign.Id, ApplicationNo = "APP-" + (int)status, Status = status,
                    FirstNameAr = "أ", FatherNameAr = "ب", GrandfatherNameAr = "ج", FamilyNameAr = "د", FirstNameEn = "A", FatherNameEn = "B", GrandfatherNameEn = "C", FamilyNameEn = "D",
                    Gender = Gender.Male, DateOfBirth = new DateTime(2020, 1, 1), NationalityLookupId = 1,
                });
            }

            await db.SaveChangesAsync();

            var rows = await new ReadModelQuery(db).GetSeatUtilizationAsync(_fx.SourceYearId);

            Assert.Equal(3, rows.Count);
            var row = rows.Single(r => r.GradeYearProfileId == g1);
            Assert.Equal(60, row.PlannedSeats);           // 2 × 30 (fixture)
            Assert.Equal(9, row.SectionCapacity);         // one section, capacity studentsPerGrade + 5
            Assert.Equal(4, row.Enrolled);
            Assert.Equal(2, row.PipelineApplications);    // Submitted + Approved; Rejected/Registered are terminal
            Assert.Equal(56, row.FreeSeats);
        }

        [Fact]
        [BusinessRule("BR-CAF-007")]
        public async Task Wallet_balance_view_is_ledger_derived()
        {
            using var db = CreateContext();
            var wallet = new Wallet { HolderKind = WalletHolderKind.Student, HolderId = _fx.StudentIds[0] };
            db.Wallets.Add(wallet);
            await db.SaveChangesAsync();
            db.WalletLedgerEntries.AddRange(
                new WalletLedgerEntry { WalletId = wallet.Id, Kind = WalletLedgerKind.TopUp, Amount = 100m, AtUtc = _clock.UtcNow },
                new WalletLedgerEntry { WalletId = wallet.Id, Kind = WalletLedgerKind.Sale, Amount = -35.5m, AtUtc = _clock.UtcNow });
            await db.SaveChangesAsync();

            var rows = await new ReadModelQuery(db).GetWalletBalancesAsync();

            var row = Assert.Single(rows);
            Assert.Equal(64.5m, row.Balance);
            Assert.Equal(_fx.StudentIds[0], row.HolderId);
        }

        // ---------------------------------------------------------------- snapshots

        [Fact]
        [BusinessRule("BR-DSH-002")]
        public async Task Aged_receivables_snapshot_buckets_open_remainders_and_stamps_as_of()
        {
            using var db = CreateContext();
            // fixture: student[0] owes 1150 posted 8 months ago (→ Over90); add a fresh charge for student[2] (→ Current)
            var payer = await db.Payers.OrderBy(p => p.Id).Skip(2).FirstAsync();
            db.Charges.Add(new Charge { AcademicYearId = _fx.SourceYearId, StudentId = _fx.StudentIds[2], PayerId = payer.Id, FeeCategoryId = _fx.TuitionCategoryId, SourceType = ChargeSourceType.Manual, ChargeNo = "INV-NEW", NetAmount = 200m, VatRateSnapshot = 0m, VatAmount = 0m, GrossAmount = 200m, PostedAtUtc = _clock.UtcNow.AddDays(-3), InvoiceUuid = Guid.NewGuid() });
            await db.SaveChangesAsync();

            var written = await CreateRefresher(db).RefreshAgedReceivablesAsync();

            Assert.Equal(2, written);
            var rows = await db.AgedReceivablesSnapshots.OrderBy(r => r.StudentId).ToListAsync();
            Assert.All(rows, r => Assert.Equal(_clock.UtcNow, r.AsOfUtc));
            var old = rows.Single(r => r.StudentId == _fx.StudentIds[0]);
            Assert.Equal(1150m, old.Over90);
            Assert.Equal(1150m, old.Total);
            Assert.Equal(_fx.SourceProfileIds["G1"], old.GradeYearProfileId);
            var fresh = rows.Single(r => r.StudentId == _fx.StudentIds[2]);
            Assert.Equal(200m, fresh.Current);
            Assert.DoesNotContain(rows, r => r.StudentId == _fx.StudentIds[1]);   // fully credited → no receivable

            // a re-run replaces, never accumulates
            _clock.UtcNow = _clock.UtcNow.AddDays(1);
            Assert.Equal(2, await CreateRefresher(db).RefreshAgedReceivablesAsync());
            Assert.Equal(2, await db.AgedReceivablesSnapshots.CountAsync());
            Assert.All(await db.AgedReceivablesSnapshots.ToListAsync(), r => Assert.Equal(_clock.UtcNow, r.AsOfUtc));
        }

        [Fact]
        [BusinessRule("BR-ATD-009")]
        public async Task Daily_attendance_summary_snapshot_is_per_section_and_replaces_only_that_date()
        {
            using var db = CreateContext();
            var section = await db.Sections.FirstAsync(s => s.AcademicYearId == _fx.SourceYearId);
            var enrollments = await db.SectionMemberships.Where(m => m.SectionId == section.Id).Select(m => m.EnrollmentId).ToListAsync();
            var day = new DateTime(2027, 5, 2);
            foreach (var (e, i) in enrollments.Select((e, i) => (e, i)))
            {
                db.AttendanceDays.Add(new AttendanceDay { AcademicYearId = _fx.SourceYearId, EnrollmentId = e, SectionId = section.Id, Date = day, Status = i == 0 ? AttendanceStatus.AbsentExcused : i == 1 ? AttendanceStatus.Late : AttendanceStatus.Present, CapturedByUserId = 42 });
                db.AttendanceDays.Add(new AttendanceDay { AcademicYearId = _fx.SourceYearId, EnrollmentId = e, SectionId = section.Id, Date = day.AddDays(1), Status = AttendanceStatus.Present, CapturedByUserId = 42 });
            }

            await db.SaveChangesAsync();
            var refresher = CreateRefresher(db);

            Assert.Equal(1, await refresher.RefreshDailyAttendanceSummaryAsync(day));
            Assert.Equal(1, await refresher.RefreshDailyAttendanceSummaryAsync(day.AddDays(1)));
            Assert.Equal(1, await refresher.RefreshDailyAttendanceSummaryAsync(day));   // re-run of day 1 replaces day 1 only

            var rows = await db.DailyAttendanceSummarySnapshots.OrderBy(r => r.Date).ToListAsync();
            Assert.Equal(2, rows.Count);
            Assert.Equal(4, rows[0].ScheduledCount);
            Assert.Equal(1, rows[0].AbsentCount);
            Assert.Equal(1, rows[0].LateCount);
            Assert.Equal(75m, rows[0].PresentPercent);
            Assert.Equal(_fx.StageId, rows[0].StageId);
            Assert.Equal(100m, rows[1].PresentPercent);
        }

        [Fact]
        [BusinessRule("BR-INS-007")]
        public async Task Collection_calendar_snapshot_aggregates_derived_installment_status_per_due_date()
        {
            using var db = CreateContext();
            var charge = await db.Charges.SingleAsync(c => c.StudentId == _fx.StudentIds[0]);   // 1150 unpaid
            var template = new Domain.Installments.PlanTemplate { AcademicYearId = _fx.SourceYearId, NameAr = "خطة", NameEn = "Plan", GraceDays = 0, Status = Domain.Installments.PlanTemplateStatus.Approved, IsActive = true };
            db.PlanTemplates.Add(template);
            await db.SaveChangesAsync();
            var assignment = new Domain.Installments.PlanAssignment { AcademicYearId = _fx.SourceYearId, StudentId = charge.StudentId, PayerId = charge.PayerId, PlanTemplateId = template.Id };
            db.PlanAssignments.Add(assignment);
            await db.SaveChangesAsync();
            var past = _clock.UtcNow.Date.AddDays(-20);
            var future = _clock.UtcNow.Date.AddDays(40);
            var i1 = new Domain.Installments.Installment { PlanAssignmentId = assignment.Id, SequenceNumber = 1, DueDate = past, Amount = 500m };
            var i2 = new Domain.Installments.Installment { PlanAssignmentId = assignment.Id, SequenceNumber = 2, DueDate = future, Amount = 650m };
            db.Installments.AddRange(i1, i2);
            await db.SaveChangesAsync();
            db.InstallmentChargeLines.AddRange(
                new Domain.Installments.InstallmentChargeLine { InstallmentId = i1.Id, ChargeId = charge.Id, Amount = 500m },
                new Domain.Installments.InstallmentChargeLine { InstallmentId = i2.Id, ChargeId = charge.Id, Amount = 650m });
            await db.SaveChangesAsync();

            var written = await CreateRefresher(db).RefreshCollectionCalendarAsync();

            Assert.Equal(2, written);
            var rows = await db.CollectionCalendarSnapshots.OrderBy(r => r.DueDate).ToListAsync();
            Assert.Equal(past, rows[0].DueDate);
            Assert.Equal(500m, rows[0].ScheduledAmount);
            Assert.Equal(500m, rows[0].OutstandingAmount);
            Assert.Equal(1, rows[0].OverdueCount);
            Assert.Equal(future, rows[1].DueDate);
            Assert.Equal(0, rows[1].OverdueCount);
            Assert.All(rows, r => Assert.Equal(_clock.UtcNow, r.AsOfUtc));
        }

        [Fact]
        public async Task Snapshot_job_handlers_run_through_the_job_runner_by_code()
        {
            using var db = CreateContext();
            var jobs = new JobDefinitionAdmin(db);
            foreach (var code in new[] { SnapshotJobCodes.AgedReceivables, SnapshotJobCodes.DailyAttendanceSummary, SnapshotJobCodes.CollectionCalendar })
            {
                await jobs.DefineJobAsync(code, code, code, "0 2 * * *", isEnabled: true);
            }

            var refresher = CreateRefresher(db);
            var runner = new JobRunner(db, _clock, new AuditEventWriter(db, _tenant, _tenant, _user, _clock, _audit), new Application.Jobs.IJobHandler[]
            {
                new AgedReceivablesSnapshotJobHandler(refresher), new DailyAttendanceSummarySnapshotJobHandler(refresher, _clock), new CollectionCalendarSnapshotJobHandler(refresher),
            });

            foreach (var code in new[] { SnapshotJobCodes.AgedReceivables, SnapshotJobCodes.DailyAttendanceSummary, SnapshotJobCodes.CollectionCalendar })
            {
                var run = await runner.RunAsync(code, Domain.Jobs.JobTriggerType.Manual);
                Assert.Equal(Domain.Jobs.JobStatus.Succeeded, run.Status);
            }

            Assert.Equal(1, await db.AgedReceivablesSnapshots.CountAsync());   // student[0]'s 1150
        }
    }
}
