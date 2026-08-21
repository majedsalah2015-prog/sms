using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Installments;
using Sms.Domain.Calendar;
using Sms.Domain.Common;
using Sms.Domain.Fees;
using Sms.Domain.Grades;
using Sms.Domain.Installments;
using Sms.Domain.Numbering;
using Sms.Domain.Parents;
using Sms.Domain.Payments;
using Sms.Domain.Schools;
using Sms.Domain.Students;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Fees;
using Sms.Infrastructure.Installments;
using Sms.Infrastructure.Notifications;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Payments;
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// S5/E-501 (Installment Plans + PDC lifecycle, doc/Modules/20,
    /// BR-INS-001..010) over a real Sqlite-backed AppDbContext. Charges
    /// come from E-303's FeeAdmin and money from PaymentAdmin, so the
    /// derived statuses are exercised against real allocations.
    /// </summary>
    public sealed class InstallmentAdminTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2026, 9, 15, 8, 0, 0, DateTimeKind.Utc);
        }

        private sealed class FixedUser : ICurrentUser
        {
            public int UserId { get; set; }
        }

        private sealed class FixedTenant : ITenantContext, IWorkingYearContext
        {
            public int SchoolId => 1;

            public int AcademicYearId { get; set; }
        }

        private static readonly HashSet<DayOfWeek> KsaWeekend = new() { DayOfWeek.Friday, DayOfWeek.Saturday };

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();
        private int _yearId;
        private int _studentId;
        private int _payerId;
        private int _categoryId;
        private int _transportCategoryId;

        public InstallmentAdminTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();

            foreach (var (code, template) in new[] { ("INV", "INV-{SEQ:6}"), ("RCP", "RCP-{SEQ:6}"), ("CRN", "CRN-{SEQ:5}") })
            {
                db.NumberingSeries.Add(new NumberingSeries
                {
                    Code = code, EntityName = code, FormatTemplate = template,
                    ResetPolicy = ResetPolicy.Never, GapPolicy = GapPolicy.Strict, EffectiveFromUtc = _clock.UtcNow, IsActive = true,
                });
            }

            var year = new AcademicYear
            {
                LabelAr = "Year", LabelEn = "2026-2027", HijriLabel = "Hijri",
                StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2027, 6, 30), Status = AcademicYearStatus.Active,
            };
            db.AcademicYears.Add(year);
            var stage = new Stage { Name = new LocalizedName("Stage", "Elementary"), SequenceOrder = 1, DefaultGenderPolicy = GenderPolicy.Mixed };
            db.Stages.Add(stage);
            db.SaveChanges();
            _tenant.AcademicYearId = year.Id;

            var grade = new GradeLevel { StageId = stage.Id, Code = "G3", Name = new LocalizedName("Grade", "Grade 3"), SequenceOrder = 3 };
            db.GradeLevels.Add(grade);
            db.SaveChanges();
            var profile = new GradeYearProfile { GradeLevelId = grade.Id, AcademicYearId = year.Id, GenderPolicy = GenderPolicy.Mixed, TargetSections = 1, TargetSectionSize = 25 };
            db.GradeYearProfiles.Add(profile);
            db.SaveChanges();

            var student = new Student
            {
                StudentNo = "STU-TEST-1",
                FirstNameAr = "Student", FatherNameAr = "Father", GrandfatherNameAr = "Grandfather", FamilyNameAr = "Family",
                FirstNameEn = "Student", FatherNameEn = "Father", GrandfatherNameEn = "Grandfather", FamilyNameEn = "Family",
                Gender = Gender.Male, DateOfBirth = new DateTime(2018, 1, 1), NationalityLookupId = 1,
            };
            db.Students.Add(student);
            db.SaveChanges();
            db.Enrollments.Add(new Enrollment
            {
                AcademicYearId = year.Id, StudentId = student.Id, GradeYearProfileId = profile.Id,
                EnrollmentDate = new DateTime(2026, 9, 1), SourceType = EnrollmentSourceType.Admission,
            });

            var parent = new Parent { ParentFileNo = "PAR-000001", NameAr = "Guardian", NameEn = "Guardian", PrimaryMobile = "0500000000" };
            db.Parents.Add(parent);
            db.SaveChanges();
            var payer = new Payer { Type = PayerType.Parent, ParentId = parent.Id };
            db.Payers.Add(payer);

            var tuition = new FeeCategory { NameAr = "Tuition", NameEn = "Tuition", IsMandatory = true, IsRefundable = true };
            var transport = new FeeCategory { NameAr = "Transport", NameEn = "Transport", IsMandatory = false, IsRefundable = true, IsServiceLinked = true };
            db.FeeCategories.AddRange(tuition, transport);
            db.SaveChanges();

            _yearId = year.Id;
            _studentId = student.Id;
            _payerId = payer.Id;
            _categoryId = tuition.Id;
            _transportCategoryId = transport.Id;
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private InstallmentAdmin CreateAdmin(AppDbContext db) => new(db, _clock, _audit, _tenant, new NotificationPublisher(db), CreateFeeAdmin(db));

        private PaymentAdmin CreatePaymentAdmin(AppDbContext db) => new(db, new NumberIssuer(db, _tenant, _tenant, _clock), _clock);

        private FeeAdmin CreateFeeAdmin(AppDbContext db) => new(db, new NumberIssuer(db, _tenant, _tenant, _clock), _clock);

        private Task<Charge> PostCharge(AppDbContext db, decimal amount, int? categoryId = null)
            => CreateFeeAdmin(db).PostManualChargeAsync(_studentId, _payerId, categoryId ?? _categoryId, amount);

        private static IReadOnlyList<TemplateSplit> Quarterly() => new[]
        {
            new TemplateSplit(25m, new DateTime(2026, 9, 20)), new TemplateSplit(25m, new DateTime(2026, 11, 22)),
            new TemplateSplit(25m, new DateTime(2027, 1, 24)), new TemplateSplit(25m, new DateTime(2027, 3, 21)),
        };

        private async Task<PlanTemplate> ApprovedTemplateAsync(InstallmentAdmin admin, IReadOnlyList<TemplateSplit>? splits = null, int graceDays = 3, int? categoryId = null)
        {
            var template = await admin.DefineTemplateAsync(_yearId, "Plan", "Quarterly", splits ?? Quarterly(), feeCategoryId: categoryId, graceDays: graceDays);
            await admin.ApproveTemplateAsync(template.Id);
            return template;
        }

        private async Task<PlanAssignment> StandardScheduleAsync(AppDbContext db, InstallmentAdmin admin, decimal charge = 1000m)
        {
            await PostCharge(db, charge);
            var template = await ApprovedTemplateAsync(admin);
            return await admin.AssignPlanAsync(_studentId, _payerId, template.Id, KsaWeekend);
        }

        // --- BR-INS-001 templates -----------------------------------------------------------

        [Fact]
        [BusinessRule("BR-INS-001")]
        public async Task Template_splits_must_sum_to_hundred_and_carry_a_due_date_rule()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);

            await Assert.ThrowsAsync<InvalidTemplateSplitException>(() => admin.DefineTemplateAsync(
                _yearId, "Bad", "Bad", new[] { new TemplateSplit(60m, new DateTime(2026, 10, 1)), new TemplateSplit(30m, new DateTime(2027, 1, 1)) }));
            await Assert.ThrowsAsync<InvalidTemplateSplitException>(() => admin.DefineTemplateAsync(
                _yearId, "Bad", "Bad", new[] { new TemplateSplit(100m) }));
        }

        [Fact]
        [BusinessRule("BR-INS-001")]
        public async Task An_unapproved_template_cannot_be_assigned()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            await PostCharge(db, 1000m);
            var template = await admin.DefineTemplateAsync(_yearId, "Plan", "Plan", Quarterly());

            await Assert.ThrowsAsync<PlanTemplateNotApprovedException>(() => admin.AssignPlanAsync(_studentId, _payerId, template.Id, KsaWeekend));
        }

        // --- BR-INS-002 assignment + schedule generation -----------------------------------

        [Fact]
        [BusinessRule("BR-INS-002")]
        public async Task Assignment_generates_dated_amounts_summing_exactly_to_the_net_charges()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            await PostCharge(db, 1000m);
            await PostCharge(db, 500m);
            var template = await ApprovedTemplateAsync(admin);

            var assignment = await admin.AssignPlanAsync(_studentId, _payerId, template.Id, KsaWeekend);

            var schedule = await admin.GetScheduleAsync(assignment.Id);
            Assert.Equal(4, schedule.Count);
            Assert.All(schedule, i => Assert.Equal(375m, i.Amount));
            Assert.Equal(1500m, schedule.Sum(i => i.Amount));
            var lines = db.InstallmentChargeLines.ToList();
            Assert.Equal(1500m, lines.Sum(l => l.Amount));
            Assert.Equal(2, lines.Select(l => l.ChargeId).Distinct().Count());
        }

        [Fact]
        [BusinessRule("BR-INS-002")]
        public async Task Rounding_differences_land_in_the_last_installment()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            await PostCharge(db, 1000m);
            var template = await ApprovedTemplateAsync(admin, new[]
            {
                new TemplateSplit(33.33m, new DateTime(2026, 10, 4)), new TemplateSplit(33.33m, new DateTime(2027, 1, 3)), new TemplateSplit(33.34m, new DateTime(2027, 4, 4)),
            });

            var assignment = await admin.AssignPlanAsync(_studentId, _payerId, template.Id, KsaWeekend);

            var schedule = await admin.GetScheduleAsync(assignment.Id);
            Assert.Equal(new[] { 333.30m, 333.30m, 333.40m }, schedule.Select(i => i.Amount));
        }

        [Fact]
        [BusinessRule("BR-INS-002")]
        public async Task A_category_scoped_template_only_schedules_that_category()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            await PostCharge(db, 1000m);
            await PostCharge(db, 300m, _transportCategoryId);
            var template = await ApprovedTemplateAsync(admin, categoryId: _transportCategoryId);

            var assignment = await admin.AssignPlanAsync(_studentId, _payerId, template.Id, KsaWeekend);

            Assert.Equal(300m, (await admin.GetScheduleAsync(assignment.Id)).Sum(i => i.Amount));
        }

        [Fact]
        [BusinessRule("BR-INS-002")]
        public async Task One_plan_per_student_year_per_category_and_exceptions_need_a_reason()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            await PostCharge(db, 1000m);
            var template = await ApprovedTemplateAsync(admin);
            await admin.AssignPlanAsync(_studentId, _payerId, template.Id, KsaWeekend);

            await Assert.ThrowsAsync<PlanAssignmentExistsException>(() => admin.AssignPlanAsync(_studentId, _payerId, template.Id, KsaWeekend));
            await Assert.ThrowsAsync<ExceptionAssignmentReasonRequiredException>(() => admin.AssignPlanAsync(99, _payerId, template.Id, KsaWeekend, isException: true));
        }

        [Fact]
        [BusinessRule("BR-INS-002")]
        public async Task Assigning_with_nothing_posted_is_rejected()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var template = await ApprovedTemplateAsync(admin);

            await Assert.ThrowsAsync<NoChargesToScheduleException>(() => admin.AssignPlanAsync(_studentId, _payerId, template.Id, KsaWeekend));
        }

        // --- BR-INS-004 due-date calendar ---------------------------------------------------

        [Fact]
        [BusinessRule("BR-INS-004")]
        public async Task Due_dates_on_weekends_or_holidays_shift_to_the_next_working_day()
        {
            using var db = CreateContext();
            db.CalendarDays.Add(new CalendarDay { AcademicYearId = _yearId, Date = new DateTime(2027, 2, 22), DayType = DayType.Holiday, Source = CalendarDaySource.Manual });
            await db.SaveChangesAsync();
            var admin = CreateAdmin(db);
            await PostCharge(db, 1000m);
            var template = await ApprovedTemplateAsync(admin, new[]
            {
                new TemplateSplit(50m, new DateTime(2027, 1, 1)),   // Friday -> Sunday 2027-01-03
                new TemplateSplit(50m, new DateTime(2027, 2, 22)),  // Founding Day holiday (Monday) -> Tuesday 2027-02-23
            });

            var assignment = await admin.AssignPlanAsync(_studentId, _payerId, template.Id, KsaWeekend);

            var schedule = await admin.GetScheduleAsync(assignment.Id);
            Assert.Equal(new DateTime(2027, 1, 3), schedule[0].DueDate);
            Assert.Equal(new DateTime(2027, 2, 23), schedule[1].DueDate);
        }

        [Fact]
        [BusinessRule("BR-INS-001")]
        public async Task An_offset_split_is_dated_from_the_year_start()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            await PostCharge(db, 1000m);
            var template = await ApprovedTemplateAsync(admin, new[] { new TemplateSplit(100m, OffsetDaysFromYearStart: 30) });

            var assignment = await admin.AssignPlanAsync(_studentId, _payerId, template.Id, KsaWeekend);

            Assert.Equal(new DateTime(2026, 10, 1), (await admin.GetScheduleAsync(assignment.Id)).Single().DueDate);
        }

        // --- BR-INS-007 derived status -----------------------------------------------------

        [Fact]
        [BusinessRule("BR-INS-007")]
        public async Task Status_derives_from_Module_21_allocations_and_dates()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var assignment = await StandardScheduleAsync(db, admin);   // 4 x 250, due 9/20, 11/22, 1/24, 3/21, grace 3
            await CreatePaymentAdmin(db).CaptureReceiptAsync(_payerId, PaymentMethod.Cash, 400m);

            _clock.UtcNow = new DateTime(2026, 11, 30, 8, 0, 0, DateTimeKind.Utc);
            var schedule = await admin.GetScheduleAsync(assignment.Id);

            Assert.Equal(InstallmentStatus.Paid, schedule[0].Status);
            Assert.Equal(250m, schedule[0].Paid);
            Assert.Equal(InstallmentStatus.PartiallyPaid, schedule[1].Status);
            Assert.Equal(150m, schedule[1].Paid);
            Assert.Equal(InstallmentStatus.Scheduled, schedule[2].Status);
        }

        [Fact]
        [BusinessRule("BR-INS-007")]
        public async Task Due_becomes_Overdue_only_after_grace_elapses()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var assignment = await StandardScheduleAsync(db, admin);

            _clock.UtcNow = new DateTime(2026, 9, 23, 8, 0, 0, DateTimeKind.Utc);
            Assert.Equal(InstallmentStatus.Due, (await admin.GetScheduleAsync(assignment.Id))[0].Status);

            _clock.UtcNow = new DateTime(2026, 9, 24, 8, 0, 0, DateTimeKind.Utc);
            Assert.Equal(InstallmentStatus.Overdue, (await admin.GetScheduleAsync(assignment.Id))[0].Status);
        }

        // --- BR-INS-003 controlled recomputation ------------------------------------------

        [Fact]
        [BusinessRule("BR-INS-003")]
        public async Task An_appended_charge_spreads_over_the_open_installments_and_is_logged()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var assignment = await StandardScheduleAsync(db, admin);
            await CreatePaymentAdmin(db).CaptureReceiptAsync(_payerId, PaymentMethod.Cash, 250m);   // first installment paid
            var transport = await PostCharge(db, 300m, _transportCategoryId);

            await admin.AppendChargeAsync(assignment.Id, transport.Id);

            var schedule = await admin.GetScheduleAsync(assignment.Id);
            Assert.Equal(new[] { 250m, 350m, 350m, 350m }, schedule.Select(i => i.Amount));
            Assert.Equal(InstallmentStatus.Paid, schedule[0].Status);
            var revision = db.ScheduleRevisions.Single(r => r.Cause == ScheduleRevisionCause.ChargeAppended);
            Assert.Contains("\"Amount\":250", revision.BeforeJson);
            Assert.Contains("\"Amount\":350", revision.AfterJson);
        }

        [Fact]
        [BusinessRule("BR-INS-003")]
        public async Task A_reduction_hits_future_installments_first_and_never_touches_paid_money()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var assignment = await StandardScheduleAsync(db, admin);
            await CreatePaymentAdmin(db).CaptureReceiptAsync(_payerId, PaymentMethod.Cash, 250m);
            _clock.UtcNow = new DateTime(2026, 12, 1, 8, 0, 0, DateTimeKind.Utc);   // installments 1,2 past; 3,4 future

            await admin.ReduceScheduleAsync(assignment.Id, 300m, "sibling discount");

            var schedule = await admin.GetScheduleAsync(assignment.Id);
            Assert.Equal(new[] { 250m, 250m, 200m, 0m }, schedule.Select(i => i.Amount));
            Assert.Equal(700m, db.InstallmentChargeLines.ToList().Sum(l => l.Amount));
            Assert.Single(db.ScheduleRevisions.Where(r => r.Cause == ScheduleRevisionCause.Reduced));
        }

        [Fact]
        [BusinessRule("BR-INS-003")]
        public async Task Reducing_beyond_the_unpaid_remainder_is_rejected()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var assignment = await StandardScheduleAsync(db, admin);
            await CreatePaymentAdmin(db).CaptureReceiptAsync(_payerId, PaymentMethod.Cash, 900m);

            await Assert.ThrowsAsync<InvalidOperationException>(() => admin.ReduceScheduleAsync(assignment.Id, 200m, "too much"));
        }

        // --- BR-INS-005 rescheduling --------------------------------------------------------

        [Fact]
        [BusinessRule("BR-INS-005")]
        public async Task A_proposal_must_cover_exactly_the_unpaid_remainder()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var assignment = await StandardScheduleAsync(db, admin);
            await CreatePaymentAdmin(db).CaptureReceiptAsync(_payerId, PaymentMethod.Cash, 400m);   // remainder 600

            await Assert.ThrowsAsync<RescheduleRemainderMismatchException>(() => admin.ProposeRescheduleAsync(
                assignment.Id, 1, "hardship", new[] { new ProposedInstallment(new DateTime(2027, 5, 2), 500m) }, KsaWeekend));
        }

        [Fact]
        [BusinessRule("BR-INS-005")]
        public async Task Approval_supersedes_the_unpaid_installments_and_materializes_the_new_split()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var assignment = await StandardScheduleAsync(db, admin);
            await CreatePaymentAdmin(db).CaptureReceiptAsync(_payerId, PaymentMethod.Cash, 400m);   // #1 paid, #2 has 150 of 250
            _clock.UtcNow = new DateTime(2026, 12, 1, 8, 0, 0, DateTimeKind.Utc);
            var proposal = new[]
            {
                new ProposedInstallment(new DateTime(2027, 2, 7), 300m), new ProposedInstallment(new DateTime(2027, 4, 4), 300m),
            };
            var rescheduleCase = await admin.ProposeRescheduleAsync(assignment.Id, 1, "family hardship", proposal, KsaWeekend);
            Assert.False(rescheduleCase.RequiresPrincipal);

            await admin.DecideRescheduleAsync(rescheduleCase.Id, approve: true, "FM approved");

            var schedule = await admin.GetScheduleAsync(assignment.Id);
            Assert.Equal(6, schedule.Count);
            Assert.Equal(InstallmentStatus.Paid, schedule.Single(i => i.SequenceNumber == 1).Status);
            var trimmed = schedule.Single(i => i.SequenceNumber == 2);
            Assert.Equal(150m, trimmed.Amount);
            Assert.Equal(InstallmentStatus.Paid, trimmed.Status);
            Assert.All(schedule.Where(i => i.SequenceNumber is 3 or 4), i => Assert.Equal(InstallmentStatus.Rescheduled, i.Status));
            Assert.Equal(new[] { 300m, 300m }, schedule.Where(i => i.SequenceNumber >= 5).Select(i => i.Amount));
            Assert.Equal(1000m, db.InstallmentChargeLines.ToList().Sum(l => l.Amount));
            Assert.Equal(1, db.PlanAssignments.Single().RescheduleCount);
            Assert.Equal(RescheduleCaseStatus.Approved, db.RescheduleCases.Single().Status);
            Assert.Single(db.ScheduleRevisions.Where(r => r.Cause == ScheduleRevisionCause.Rescheduled));
        }

        [Fact]
        [BusinessRule("BR-INS-005")]
        public async Task Crossing_year_end_flags_the_case_for_Principal_and_a_decided_case_is_final()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var assignment = await StandardScheduleAsync(db, admin);
            var rescheduleCase = await admin.ProposeRescheduleAsync(
                assignment.Id, 1, "extend", new[] { new ProposedInstallment(new DateTime(2027, 8, 1), 1000m) }, KsaWeekend);

            Assert.True(rescheduleCase.RequiresPrincipal);
            await admin.DecideRescheduleAsync(rescheduleCase.Id, approve: false, "rejected");
            Assert.Equal(RescheduleCaseStatus.Rejected, db.RescheduleCases.Single().Status);
            await Assert.ThrowsAsync<RescheduleCaseNotPendingException>(() => admin.DecideRescheduleAsync(rescheduleCase.Id, approve: true));
        }

        // --- BR-INS-006 promises ---------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-INS-006")]
        public async Task Promises_are_only_recorded_against_overdue_installments_within_the_horizon()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var assignment = await StandardScheduleAsync(db, admin);
            var first = (await admin.GetScheduleAsync(assignment.Id))[0];

            await Assert.ThrowsAsync<InstallmentNotOverdueException>(() => admin.RecordPromiseAsync(first.InstallmentId, 1, new DateTime(2026, 9, 20), 250m));

            _clock.UtcNow = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
            await Assert.ThrowsAsync<PromiseDateOutOfRangeException>(() => admin.RecordPromiseAsync(first.InstallmentId, 1, new DateTime(2026, 12, 1), 250m, horizonDays: 30));
            var promise = await admin.RecordPromiseAsync(first.InstallmentId, 1, new DateTime(2026, 10, 10), 250m);
            Assert.Equal(PromiseStatus.Open, promise.Status);
        }

        [Fact]
        [BusinessRule("BR-INS-006")]
        public async Task A_promise_whose_date_passes_unpaid_breaks_and_escalates_the_ladder()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var assignment = await StandardScheduleAsync(db, admin);
            var first = (await admin.GetScheduleAsync(assignment.Id))[0];
            _clock.UtcNow = new DateTime(2026, 9, 25, 8, 0, 0, DateTimeKind.Utc);   // due 9/20 + grace 3 -> overdue
            await admin.RunDunningAsync();   // fires Overdue3
            await admin.RecordPromiseAsync(first.InstallmentId, 1, new DateTime(2026, 9, 28), 250m);

            _clock.UtcNow = new DateTime(2026, 9, 30, 8, 0, 0, DateTimeKind.Utc);   // promise date passed, still +10 (< +14)
            var fired = await admin.RunDunningAsync();

            Assert.Equal(PromiseStatus.Broken, db.PromisesToPay.Single().Status);
            var escalated = Assert.Single(fired);
            Assert.Equal(DunningStep.Overdue14, escalated.Step);
            Assert.True(escalated.TriggeredByBrokenPromise);
        }

        [Fact]
        [BusinessRule("BR-INS-006")]
        public async Task A_promise_paid_in_time_is_kept()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var assignment = await StandardScheduleAsync(db, admin);
            var first = (await admin.GetScheduleAsync(assignment.Id))[0];
            _clock.UtcNow = new DateTime(2026, 9, 25, 8, 0, 0, DateTimeKind.Utc);
            await admin.RecordPromiseAsync(first.InstallmentId, 1, new DateTime(2026, 9, 28), 250m);
            await CreatePaymentAdmin(db).CaptureReceiptAsync(_payerId, PaymentMethod.Cash, 250m);
            _clock.UtcNow = new DateTime(2026, 9, 30, 8, 0, 0, DateTimeKind.Utc);

            Assert.Equal(0, await admin.EvaluatePromisesAsync());
            Assert.Equal(PromiseStatus.Kept, db.PromisesToPay.Single().Status);
        }

        // --- BR-INS-008 dunning ladder --------------------------------------------------------

        [Fact]
        [BusinessRule("BR-INS-008")]
        public async Task The_ladder_fires_one_step_per_installment_per_run_and_is_idempotent()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var assignment = await StandardScheduleAsync(db, admin);

            _clock.UtcNow = new DateTime(2026, 9, 13, 8, 0, 0, DateTimeKind.Utc);   // D-7 for the 9/20 installment
            var first = await admin.RunDunningAsync();
            var again = await admin.RunDunningAsync();

            Assert.Equal(DunningStep.ReminderD7, Assert.Single(first).Step);
            Assert.Empty(again);

            _clock.UtcNow = new DateTime(2026, 10, 10, 8, 0, 0, DateTimeKind.Utc);   // +20 -> Overdue14 (skips the +3 backlog)
            var overdue = await admin.RunDunningAsync();
            Assert.Equal(DunningStep.Overdue14, Assert.Single(overdue).Step);
            Assert.Equal(2, db.DunningEvents.Count(e => e.InstallmentId == assignment.Installments.First().Id));
        }

        [Fact]
        [BusinessRule("BR-INS-008")]
        public async Task Paid_installments_never_receive_dunning()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            await StandardScheduleAsync(db, admin);
            await CreatePaymentAdmin(db).CaptureReceiptAsync(_payerId, PaymentMethod.Cash, 1000m);
            _clock.UtcNow = new DateTime(2027, 6, 1, 8, 0, 0, DateTimeKind.Utc);

            Assert.Empty(await admin.RunDunningAsync());
        }

        // --- BR-INS-009 PDC coverage ------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-INS-009")]
        public async Task PDC_coverage_suppresses_dunning_and_a_bounce_lifts_it()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var payments = CreatePaymentAdmin(db);
            var assignment = await StandardScheduleAsync(db, admin);
            var first = (await admin.GetScheduleAsync(assignment.Id))[0];
            var pdc = await payments.LodgePdcAsync(_payerId, "Bank", "CHQ-1", new DateTime(2026, 10, 15), 250m);
            await admin.MarkPdcCoveredAsync(first.InstallmentId, pdc.Id);

            _clock.UtcNow = new DateTime(2026, 10, 10, 8, 0, 0, DateTimeKind.Utc);
            Assert.Empty(await admin.RunDunningAsync());
            Assert.True((await admin.GetScheduleAsync(assignment.Id))[0].IsPdcCovered);
            Assert.NotEqual(InstallmentStatus.Paid, (await admin.GetScheduleAsync(assignment.Id))[0].Status);   // covered != paid

            await payments.ChangePdcStatusAsync(pdc.Id, PdcStatus.Due, _clock.UtcNow);
            await payments.ChangePdcStatusAsync(pdc.Id, PdcStatus.Deposited, _clock.UtcNow);
            await payments.ChangePdcStatusAsync(pdc.Id, PdcStatus.Bounced, _clock.UtcNow);

            Assert.False((await admin.GetScheduleAsync(assignment.Id))[0].IsPdcCovered);
            Assert.Equal(DunningStep.Overdue14, Assert.Single(await admin.RunDunningAsync()).Step);
        }

        [Fact]
        [BusinessRule("BR-INS-009")]
        public async Task PDC_clearance_pays_the_installment_through_Module_21()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var payments = CreatePaymentAdmin(db);
            var assignment = await StandardScheduleAsync(db, admin);
            var pdc = await payments.LodgePdcAsync(_payerId, "Bank", "CHQ-1", new DateTime(2026, 10, 15), 250m);
            await payments.ChangePdcStatusAsync(pdc.Id, PdcStatus.Due, _clock.UtcNow);
            await payments.ChangePdcStatusAsync(pdc.Id, PdcStatus.Deposited, _clock.UtcNow);

            await payments.ChangePdcStatusAsync(pdc.Id, PdcStatus.Cleared, _clock.UtcNow);

            Assert.Equal(InstallmentStatus.Paid, (await admin.GetScheduleAsync(assignment.Id))[0].Status);
        }

        [Fact]
        [BusinessRule("BR-INS-009")]
        public async Task A_cheque_of_another_payer_or_a_dead_cheque_cannot_cover()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var payments = CreatePaymentAdmin(db);
            var assignment = await StandardScheduleAsync(db, admin);
            var first = (await admin.GetScheduleAsync(assignment.Id))[0];
            var otherPayer = new Payer { Type = PayerType.Parent };
            db.Payers.Add(otherPayer);
            await db.SaveChangesAsync();
            var foreign = await payments.LodgePdcAsync(otherPayer.Id, "Bank", "CHQ-X", new DateTime(2026, 10, 15), 250m);
            var bounced = await payments.LodgePdcAsync(_payerId, "Bank", "CHQ-2", new DateTime(2026, 10, 15), 250m);
            await payments.ChangePdcStatusAsync(bounced.Id, PdcStatus.Due, _clock.UtcNow);
            await payments.ChangePdcStatusAsync(bounced.Id, PdcStatus.Deposited, _clock.UtcNow);
            await payments.ChangePdcStatusAsync(bounced.Id, PdcStatus.Bounced, _clock.UtcNow);

            await Assert.ThrowsAsync<PdcNotCoverableException>(() => admin.MarkPdcCoveredAsync(first.InstallmentId, foreign.Id));
            await Assert.ThrowsAsync<PdcNotCoverableException>(() => admin.MarkPdcCoveredAsync(first.InstallmentId, bounced.Id));
        }

        // --- WF-06 write-off + BR-INS-010 audit ------------------------------------------------

        [Fact]
        [BusinessRule("BR-INS-010")]
        public async Task Write_off_is_reason_required_and_T1_audited()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var assignment = await StandardScheduleAsync(db, admin);
            var last = (await admin.GetScheduleAsync(assignment.Id))[3];

            await admin.WriteOffAsync(last.InstallmentId, "uncollectable - family left the country");

            Assert.Equal(InstallmentStatus.WrittenOff, (await admin.GetScheduleAsync(assignment.Id))[3].Status);
            var audit = db.AuditEntries.Single(e => e.EntityType == nameof(Installment) && e.FieldName == nameof(Installment.IsWrittenOff));
            Assert.Equal("uncollectable - family left the country", audit.Reason);
        }

        [Fact]
        [BusinessRule("BR-INS-010")]
        public async Task A_write_off_relieves_the_charge_rather_than_only_flagging_the_installment()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var assignment = await StandardScheduleAsync(db, admin);
            var last = (await admin.GetScheduleAsync(assignment.Id))[3];

            var before = await CreateFeeAdmin(db).ComputeStudentPositionAsync(_studentId);
            Assert.True(before > 0m);

            await admin.WriteOffAsync(last.InstallmentId, "uncollectable");

            // Gap G-6: the flag alone left the receivable standing for ever. What the school gave up
            // has to leave the balance sheet, and it leaves it as a credit note marked as a write-off.
            var note = db.CreditNotes.Single(n => n.IsWriteOff);
            Assert.Equal(last.Amount, note.Amount);
            Assert.Equal("uncollectable", note.Reason);
            Assert.Equal(before - last.Amount, await CreateFeeAdmin(db).ComputeStudentPositionAsync(_studentId));
        }

        [Fact]
        [BusinessRule("BR-INS-010")]
        public async Task A_write_off_gives_up_only_what_is_still_unpaid()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var assignment = await StandardScheduleAsync(db, admin);
            var first = (await admin.GetScheduleAsync(assignment.Id))[0];

            // Half of the first installment is already in the drawer. Writing it off gives up the
            // other half — treating the whole scheduled amount as a loss would credit the family
            // money they had already handed over.
            var half = Math.Round(first.Amount / 2m, 2, MidpointRounding.AwayFromZero);
            await CreatePaymentAdmin(db).CaptureReceiptAsync(_payerId, PaymentMethod.Cash, half);

            await admin.WriteOffAsync(first.InstallmentId, "settled short");

            var note = db.CreditNotes.Single(n => n.IsWriteOff);
            Assert.Equal(first.Amount - half, note.Amount);
        }

        [Fact]
        [BusinessRule("BR-INS-010")]
        public async Task Schedule_amount_changes_are_field_level_audited()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var assignment = await StandardScheduleAsync(db, admin);

            await admin.ReduceScheduleAsync(assignment.Id, 100m, "credit note");

            var audit = db.AuditEntries.Where(e => e.EntityType == nameof(Installment) && e.FieldName == nameof(Installment.Amount)).ToList();
            Assert.NotEmpty(audit);
            Assert.All(audit, e => Assert.Equal("credit note", e.Reason));
        }
    }
}
