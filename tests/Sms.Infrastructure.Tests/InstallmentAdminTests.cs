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
        private int _gradeId;
        private int _profileId;

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
            _gradeId = grade.Id;
            _profileId = profile.Id;
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private InstallmentAdmin CreateAdmin(AppDbContext db) => new(db, _clock, _audit, _tenant, new NotificationPublisher(db, new TestAddressBook()), CreateFeeAdmin(db));

        private PaymentAdmin CreatePaymentAdmin(AppDbContext db) => new(db, new NumberIssuer(db, _tenant, _tenant, _clock), _clock);

        private FeeAdmin CreateFeeAdmin(AppDbContext db) => new(db, new NumberIssuer(db, _tenant, _tenant, _clock), _clock);

        private Task<Charge> PostCharge(AppDbContext db, decimal amount, int? categoryId = null)
            => CreateFeeAdmin(db).PostManualChargeAsync(_studentId, _payerId, categoryId ?? _categoryId, amount);

        private Task<Charge> PostChargeFor(AppDbContext db, int studentId, int payerId, decimal amount, int? categoryId = null)
            => CreateFeeAdmin(db).PostManualChargeAsync(studentId, payerId, categoryId ?? _categoryId, amount);

        /// <summary>
        /// Another child of the grade, with their own family and payer. A grade-wide run against a
        /// cohort of one proves nothing about the loop it is.
        /// </summary>
        private (int StudentId, int PayerId) EnrollStudent(string studentNo, int? profileId = null)
        {
            using var db = CreateContext();
            var student = new Student
            {
                StudentNo = studentNo,
                FirstNameAr = studentNo, FatherNameAr = "Father", GrandfatherNameAr = "Grandfather", FamilyNameAr = "Family",
                FirstNameEn = studentNo, FatherNameEn = "Father", GrandfatherNameEn = "Grandfather", FamilyNameEn = "Family",
                Gender = Gender.Male, DateOfBirth = new DateTime(2018, 1, 1), NationalityLookupId = 1,
            };
            db.Students.Add(student);
            var parent = new Parent { ParentFileNo = $"PAR-{studentNo}", NameAr = studentNo, NameEn = studentNo, PrimaryMobile = "0500000001" };
            db.Parents.Add(parent);
            db.SaveChanges();

            db.Payers.Add(new Payer { Type = PayerType.Parent, ParentId = parent.Id });
            db.Enrollments.Add(new Enrollment
            {
                AcademicYearId = _yearId, StudentId = student.Id, GradeYearProfileId = profileId ?? _profileId,
                EnrollmentDate = new DateTime(2026, 9, 1), SourceType = EnrollmentSourceType.Admission,
            });
            db.SaveChanges();

            var payerId = db.Payers.Single(p => p.ParentId == parent.Id).Id;
            return (student.Id, payerId);
        }

        /// <summary>A second grade running the same year, to prove a grade-wide run stops at its own grade.</summary>
        private (int GradeId, int ProfileId) AddGrade(string code, int sequence)
        {
            using var db = CreateContext();
            var stageId = db.Stages.First().Id;
            var grade = new GradeLevel { StageId = stageId, Code = code, Name = new LocalizedName(code, code), SequenceOrder = sequence };
            db.GradeLevels.Add(grade);
            db.SaveChanges();
            var profile = new GradeYearProfile
            {
                GradeLevelId = grade.Id, AcademicYearId = _yearId, GenderPolicy = GenderPolicy.Mixed, TargetSections = 1, TargetSectionSize = 25,
            };
            db.GradeYearProfiles.Add(profile);
            db.SaveChanges();
            return (grade.Id, profile.Id);
        }

        private static GradeAssignmentLine LineFor(GradeAssignmentRun run, int studentId)
            => run.Lines.Single(l => l.StudentId == studentId);

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

        // --- BR-INS-003 "plan change (§BR-INS-005)" --------------------------------------------

        /// <summary>Two halves in the second half of the year — a shape a quarterly plan can be moved onto without extending past year-end.</summary>
        private static IReadOnlyList<TemplateSplit> TwoHalves() => new[]
        {
            new TemplateSplit(50m, new DateTime(2027, 1, 10)), new TemplateSplit(50m, new DateTime(2027, 3, 14)),
        };

        [Fact]
        [BusinessRule("BR-INS-003")]
        public async Task Changing_the_template_redates_the_unpaid_remainder_and_leaves_what_was_collected()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var assignment = await StandardScheduleAsync(db, admin);                                // 4 × 250
            await CreatePaymentAdmin(db).CaptureReceiptAsync(_payerId, PaymentMethod.Cash, 400m);   // #1 paid, #2 has 150 of 250
            var target = await ApprovedTemplateAsync(admin, TwoHalves());

            await admin.ChangePlanTemplateAsync(assignment.Id, target.Id, "family asked for two payments", KsaWeekend);

            var schedule = await admin.GetScheduleAsync(assignment.Id);
            Assert.Equal(6, schedule.Count);
            Assert.Equal(InstallmentStatus.Paid, schedule.Single(i => i.SequenceNumber == 1).Status);
            var trimmed = schedule.Single(i => i.SequenceNumber == 2);
            Assert.Equal(150m, trimmed.Amount);                                                     // trimmed to what was received
            Assert.Equal(InstallmentStatus.Paid, trimmed.Status);
            Assert.All(schedule.Where(i => i.SequenceNumber is 3 or 4), i => Assert.Equal(InstallmentStatus.Rescheduled, i.Status));

            // The 600 still owed, on the new template's two dates — not the original 1000.
            var replacement = schedule.Where(i => i.SequenceNumber >= 5).OrderBy(i => i.SequenceNumber).ToList();
            Assert.Equal(new[] { 300m, 300m }, replacement.Select(i => i.Amount));
            Assert.Equal(new[] { new DateTime(2027, 1, 10), new DateTime(2027, 3, 14) }, replacement.Select(i => i.DueDate));

            // Same money, different dates: the charge lines still add up to what was billed.
            Assert.Equal(1000m, db.InstallmentChargeLines.ToList().Sum(l => l.Amount));

            var reloaded = db.PlanAssignments.Single();
            Assert.Equal(target.Id, reloaded.PlanTemplateId);
            Assert.Equal(1, reloaded.RescheduleCount);
            var revision = db.ScheduleRevisions.Single(r => r.Cause == ScheduleRevisionCause.Rescheduled);
            Assert.Equal("family asked for two payments", revision.Reason);
            Assert.Contains("250", revision.BeforeJson);
        }

        [Fact]
        [BusinessRule("BR-INS-002")]
        public async Task A_template_scoped_to_another_fee_category_cannot_carry_the_plan()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            await PostCharge(db, 1000m, _categoryId);
            var tuitionPlan = await ApprovedTemplateAsync(admin, categoryId: _categoryId);
            var assignment = await admin.AssignPlanAsync(_studentId, _payerId, tuitionPlan.Id, KsaWeekend);
            var transportPlan = await ApprovedTemplateAsync(admin, TwoHalves(), categoryId: _transportCategoryId);

            await Assert.ThrowsAsync<PlanTemplateScopeMismatchException>(
                () => admin.ChangePlanTemplateAsync(assignment.Id, transportPlan.Id, "wrong group", KsaWeekend));

            // A template that names no category applies to any group, so that one is allowed.
            var anyCategory = await ApprovedTemplateAsync(admin, TwoHalves());
            await admin.ChangePlanTemplateAsync(assignment.Id, anyCategory.Id, "onto the general plan", KsaWeekend);
            Assert.Equal(anyCategory.Id, db.PlanAssignments.Single().PlanTemplateId);

            // The plan still covers the group it was assigned over — BR-INS-002's uniqueness reads this column.
            Assert.Equal(_categoryId, db.PlanAssignments.Single().FeeCategoryId);
        }

        [Fact]
        [BusinessRule("BR-INS-005")]
        public async Task A_template_running_past_year_end_is_sent_to_the_reschedule_chain()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var assignment = await StandardScheduleAsync(db, admin);
            var overrunning = await ApprovedTemplateAsync(admin, new[]
            {
                new TemplateSplit(50m, new DateTime(2027, 3, 14)), new TemplateSplit(50m, new DateTime(2027, 8, 1)),
            });

            var refusal = await Assert.ThrowsAsync<RescheduleNeedsPrincipalException>(
                () => admin.ChangePlanTemplateAsync(assignment.Id, overrunning.Id, "stretch it out", KsaWeekend));
            Assert.Equal(new DateTime(2027, 8, 1), refusal.ProposedLastDueDate);

            // Refused before anything was written: the schedule the family has is untouched.
            Assert.Equal(4, db.Installments.Count());
            Assert.All(db.Installments.ToList(), i => Assert.False(i.IsSuperseded));
        }

        [Fact]
        [BusinessRule("BR-INS-005")]
        public async Task A_fully_collected_schedule_has_no_remainder_to_move()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var assignment = await StandardScheduleAsync(db, admin);
            await CreatePaymentAdmin(db).CaptureReceiptAsync(_payerId, PaymentMethod.Cash, 1000m);
            var target = await ApprovedTemplateAsync(admin, TwoHalves());

            await Assert.ThrowsAsync<ScheduleFullyCollectedException>(
                () => admin.ChangePlanTemplateAsync(assignment.Id, target.Id, "too late", KsaWeekend));
        }

        [Fact]
        [BusinessRule("BR-INS-003")]
        public async Task A_plan_change_needs_a_different_approved_template_and_a_reason()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var assignment = await StandardScheduleAsync(db, admin);
            var target = await ApprovedTemplateAsync(admin, TwoHalves());
            var draft = await admin.DefineTemplateAsync(_yearId, "Draft", "Draft", TwoHalves());

            await Assert.ThrowsAsync<ExceptionAssignmentReasonRequiredException>(
                () => admin.ChangePlanTemplateAsync(assignment.Id, target.Id, "   ", KsaWeekend));
            await Assert.ThrowsAsync<PlanTemplateNotApprovedException>(
                () => admin.ChangePlanTemplateAsync(assignment.Id, draft.Id, "onto a draft", KsaWeekend));
            await Assert.ThrowsAsync<PlanTemplateUnchangedException>(
                () => admin.ChangePlanTemplateAsync(assignment.Id, db.PlanAssignments.Single().PlanTemplateId, "no change", KsaWeekend));

            Assert.Equal(0, db.PlanAssignments.Single().RescheduleCount);
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

        // --- BR-INS-002 / doc §8.2 grade-wide defaults, mandatory fees only ---------------------

        [Fact]
        [BusinessRule("BR-INS-002")]
        public async Task A_grade_wide_run_schedules_every_enrolled_student_over_mandatory_fees_only()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var second = EnrollStudent("STU-TEST-2");
            await PostCharge(db, 1000m);                                   // tuition — mandatory
            await PostCharge(db, 300m, _transportCategoryId);              // transport — optional, must not be scheduled
            await PostChargeFor(db, second.StudentId, second.PayerId, 800m);
            var template = await ApprovedTemplateAsync(admin);

            var run = await admin.AssignPlanToGradeAsync(_gradeId, template.Id, KsaWeekend);

            Assert.Equal(2, run.Count(GradeAssignmentOutcome.Assigned));
            var first = LineFor(run, _studentId);
            Assert.Equal(GradeAssignmentOutcome.Assigned, first.Outcome);
            Assert.Equal(1000m, first.MandatoryTotal);
            Assert.Equal(_payerId, first.PayerId);

            // The optional transport charge is left off the schedule, not folded into it.
            var schedule = await admin.GetScheduleAsync(first.PlanAssignmentId!.Value);
            Assert.Equal(1000m, schedule.Sum(i => i.Amount));
            Assert.All(schedule, i => Assert.Equal(250m, i.Amount));
            Assert.Equal(800m, (await admin.GetScheduleAsync(LineFor(run, second.StudentId).PlanAssignmentId!.Value)).Sum(i => i.Amount));

            // BR-INS-002 makes the per-family exception the deliberate gesture; a grade default is its opposite.
            Assert.All(db.PlanAssignments.ToList(), a => Assert.False(a.IsException));
        }

        [Fact]
        [BusinessRule("BR-INS-002")]
        public async Task A_grade_wide_run_leaves_a_student_who_already_has_a_plan_untouched()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var second = EnrollStudent("STU-TEST-2");
            await PostCharge(db, 1000m);
            await PostChargeFor(db, second.StudentId, second.PayerId, 800m);
            var template = await ApprovedTemplateAsync(admin);
            var existing = await admin.AssignPlanAsync(_studentId, _payerId, template.Id, KsaWeekend);

            var run = await admin.AssignPlanToGradeAsync(_gradeId, template.Id, KsaWeekend);

            var line = LineFor(run, _studentId);
            Assert.Equal(GradeAssignmentOutcome.AlreadyPlanned, line.Outcome);
            Assert.Equal(existing.Id, line.PlanAssignmentId);
            Assert.Equal(GradeAssignmentOutcome.Assigned, LineFor(run, second.StudentId).Outcome);
            Assert.Single(db.PlanAssignments.Where(a => a.StudentId == _studentId).ToList());
        }

        [Fact]
        [BusinessRule("BR-INS-002")]
        public async Task A_student_with_no_mandatory_charges_is_reported_rather_than_dropped()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var second = EnrollStudent("STU-TEST-2");
            await PostCharge(db, 1000m);
            await PostChargeFor(db, second.StudentId, second.PayerId, 300m, _transportCategoryId);
            var template = await ApprovedTemplateAsync(admin);

            var run = await admin.AssignPlanToGradeAsync(_gradeId, template.Id, KsaWeekend);

            var line = LineFor(run, second.StudentId);
            Assert.Equal(GradeAssignmentOutcome.NoMandatoryCharges, line.Outcome);
            Assert.Null(line.PlanAssignmentId);
            Assert.Empty(db.PlanAssignments.Where(a => a.StudentId == second.StudentId).ToList());

            // One student's missing fees never stops the rest of the grade.
            Assert.Equal(GradeAssignmentOutcome.Assigned, LineFor(run, _studentId).Outcome);
        }

        [Fact]
        [BusinessRule("BR-INS-002")]
        public async Task A_grade_wide_run_stops_at_its_own_grade()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var other = AddGrade("G4", 4);
            var elsewhere = EnrollStudent("STU-TEST-3", other.ProfileId);
            await PostCharge(db, 1000m);
            await PostChargeFor(db, elsewhere.StudentId, elsewhere.PayerId, 900m);
            var template = await ApprovedTemplateAsync(admin);

            var run = await admin.AssignPlanToGradeAsync(_gradeId, template.Id, KsaWeekend);

            Assert.Equal(new[] { _studentId }, run.Lines.Select(l => l.StudentId));
            Assert.Empty(db.PlanAssignments.Where(a => a.StudentId == elsewhere.StudentId).ToList());
        }

        [Fact]
        [BusinessRule("BR-INS-002")]
        public async Task Mandatory_charges_split_across_two_payers_are_left_for_the_single_student_console()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var otherFamily = EnrollStudent("STU-TEST-2");
            await PostCharge(db, 1000m);
            // BR-FEE-004 sponsor billing does exactly this: one child, two payers. A schedule is
            // addressed to one of them, so picking either here would leave the other unscheduled.
            await PostChargeFor(db, _studentId, otherFamily.PayerId, 400m);
            var template = await ApprovedTemplateAsync(admin);

            var run = await admin.AssignPlanToGradeAsync(_gradeId, template.Id, KsaWeekend);

            var line = LineFor(run, _studentId);
            Assert.Equal(GradeAssignmentOutcome.PayerSplit, line.Outcome);
            Assert.Empty(db.PlanAssignments.Where(a => a.StudentId == _studentId).ToList());
        }

        [Fact]
        [BusinessRule("BR-INS-002")]
        public async Task A_mandatory_category_retired_mid_year_still_reaches_the_schedule()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            await PostCharge(db, 1000m);

            // The category is soft-active master data; the charge it already posted is not. Reading
            // the mandatory list through the query filter would drop this charge silently, and the
            // grade would be scheduled short by a whole fee with nothing on screen to say so.
            var category = db.FeeCategories.Single(c => c.Id == _categoryId);
            category.IsActive = false;
            db.SaveChanges();

            var template = await ApprovedTemplateAsync(admin);
            var run = await admin.AssignPlanToGradeAsync(_gradeId, template.Id, KsaWeekend);

            var line = LineFor(run, _studentId);
            Assert.Equal(GradeAssignmentOutcome.Assigned, line.Outcome);
            Assert.Equal(1000m, line.MandatoryTotal);
            Assert.Equal(1000m, (await admin.GetScheduleAsync(line.PlanAssignmentId!.Value)).Sum(i => i.Amount));
        }

        [Fact]
        [BusinessRule("BR-INS-002")]
        public async Task The_preview_writes_nothing_and_says_what_the_run_will_do()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var second = EnrollStudent("STU-TEST-2");
            await PostCharge(db, 1000m);
            await PostChargeFor(db, second.StudentId, second.PayerId, 300m, _transportCategoryId);
            var template = await ApprovedTemplateAsync(admin);

            var preview = await admin.PreviewGradeAssignmentAsync(_gradeId, template.Id);

            Assert.Equal(1, preview.Count(GradeAssignmentOutcome.Ready));
            Assert.Equal(1000m, LineFor(preview, _studentId).MandatoryTotal);
            Assert.Equal(GradeAssignmentOutcome.NoMandatoryCharges, LineFor(preview, second.StudentId).Outcome);
            Assert.Empty(db.PlanAssignments.ToList());
            Assert.Empty(db.Installments.ToList());

            var run = await admin.AssignPlanToGradeAsync(_gradeId, template.Id, KsaWeekend);

            // Same evaluation behind both, so the preview cannot promise what the run will not do.
            Assert.Equal(preview.Lines.Select(l => l.StudentId), run.Lines.Select(l => l.StudentId));
            Assert.Equal(preview.Lines.Select(l => l.MandatoryTotal), run.Lines.Select(l => l.MandatoryTotal));
            Assert.Equal(1, run.Count(GradeAssignmentOutcome.Assigned));
        }

        [Fact]
        [BusinessRule("BR-INS-002")]
        public async Task A_template_scoped_to_an_optional_category_is_refused_before_the_run_starts()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            await PostCharge(db, 300m, _transportCategoryId);
            var template = await ApprovedTemplateAsync(admin, categoryId: _transportCategoryId);

            // Refused once, up front — not reported as "no mandatory charges" thirty times over.
            await Assert.ThrowsAsync<TemplateCategoryNotMandatoryException>(
                () => admin.PreviewGradeAssignmentAsync(_gradeId, template.Id));
            await Assert.ThrowsAsync<TemplateCategoryNotMandatoryException>(
                () => admin.AssignPlanToGradeAsync(_gradeId, template.Id, KsaWeekend));
            Assert.Empty(db.PlanAssignments.ToList());
        }

        [Fact]
        [BusinessRule("BR-INS-001")]
        public async Task A_grade_wide_run_refuses_an_unapproved_template()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            await PostCharge(db, 1000m);
            var template = await admin.DefineTemplateAsync(_yearId, "Plan", "Plan", Quarterly());

            await Assert.ThrowsAsync<PlanTemplateNotApprovedException>(
                () => admin.AssignPlanToGradeAsync(_gradeId, template.Id, KsaWeekend));
            Assert.Empty(db.PlanAssignments.ToList());
        }

        [Fact]
        [BusinessRule("BR-INS-002")]
        public async Task A_grade_wide_run_ignores_a_withdrawn_enrolment()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var gone = EnrollStudent("STU-TEST-2");
            await PostCharge(db, 1000m);
            await PostChargeFor(db, gone.StudentId, gone.PayerId, 800m);
            var enrolment = db.Enrollments.Single(e => e.StudentId == gone.StudentId);
            enrolment.Status = EnrollmentStatus.Withdrawn;
            db.SaveChanges();
            var template = await ApprovedTemplateAsync(admin);

            var run = await admin.AssignPlanToGradeAsync(_gradeId, template.Id, KsaWeekend);

            Assert.Equal(new[] { _studentId }, run.Lines.Select(l => l.StudentId));
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
