using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Installments;
using Sms.Application.ReadModels;
using Sms.Domain.Common;
using Sms.Domain.Fees;
using Sms.Domain.Grades;
using Sms.Domain.Installments;
using Sms.Domain.Notifications;
using Sms.Domain.Numbering;
using Sms.Domain.Parents;
using Sms.Domain.Payments;
using Sms.Domain.Schools;
using Sms.Domain.Security;
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
    /// doc/Modules/20 §8.5's collection follow-up over a real Sqlite-backed
    /// AppDbContext — the roll, and the notices a human issues from it.
    /// <para>
    /// Charges come from E-303's <c>FeeAdmin</c> and money from
    /// <c>PaymentAdmin</c>, so what the roll calls outstanding is measured
    /// against real allocations rather than against numbers this file made up.
    /// </para>
    /// </summary>
    public sealed class CollectionFollowUpTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2026, 12, 1, 8, 0, 0, DateTimeKind.Utc);
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
        private int _parentId;
        private int _categoryId;
        private int _profileId;
        private int _gradeId;

        public CollectionFollowUpTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();

            foreach (var (code, template) in new[] { ("INV", "INV-{SEQ:6}"), ("RCP", "RCP-{SEQ:6}"), ("DUN", "DUN-{SEQ:5}") })
            {
                db.NumberingSeries.Add(new NumberingSeries
                {
                    Code = code, EntityName = code, FormatTemplate = template,
                    ResetPolicy = ResetPolicy.Never, GapPolicy = GapPolicy.Normal, EffectiveFromUtc = _clock.UtcNow, IsActive = true,
                });
            }

            var year = new AcademicYear
            {
                LabelAr = "العام", LabelEn = "2026-2027", HijriLabel = "Hijri",
                StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2027, 6, 30), Status = AcademicYearStatus.Active,
            };
            db.AcademicYears.Add(year);
            var stage = new Stage { Name = new LocalizedName("مرحلة", "Elementary"), SequenceOrder = 1, DefaultGenderPolicy = GenderPolicy.Mixed };
            db.Stages.Add(stage);
            db.SaveChanges();
            _tenant.AcademicYearId = year.Id;

            var grade = new GradeLevel { StageId = stage.Id, Code = "G3", Name = new LocalizedName("الثالث", "Grade 3"), SequenceOrder = 3 };
            db.GradeLevels.Add(grade);
            db.SaveChanges();
            var profile = new GradeYearProfile
            {
                GradeLevelId = grade.Id, AcademicYearId = year.Id, GenderPolicy = GenderPolicy.Mixed, TargetSections = 1, TargetSectionSize = 25,
            };
            db.GradeYearProfiles.Add(profile);

            var tuition = new FeeCategory { NameAr = "رسوم", NameEn = "Tuition", IsMandatory = true, IsRefundable = true };
            db.FeeCategories.Add(tuition);
            db.SaveChanges();

            _yearId = year.Id;
            _gradeId = grade.Id;
            _profileId = profile.Id;
            _categoryId = tuition.Id;

            var (studentId, payerId, parentId) = Enrol(db, "STU-0001", "سارة", "Sara", withPortalAccount: true);
            _studentId = studentId;
            _payerId = payerId;
            _parentId = parentId;
        }

        public void Dispose() => _connection.Dispose();

        // ------------------------------------------------------------------ fixture

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private CollectionFollowUp CreateFollowUp(AppDbContext db) => new(
            db, _clock, _tenant, new NumberIssuer(db, _tenant, _tenant, _clock), new NotificationPublisher(db, new TestAddressBook()));

        private InstallmentAdmin CreateAdmin(AppDbContext db) => new(
            db, _clock, _audit, _tenant, new NotificationPublisher(db, new TestAddressBook()), CreateFeeAdmin(db));

        private FeeAdmin CreateFeeAdmin(AppDbContext db) => new(db, new NumberIssuer(db, _tenant, _tenant, _clock), _clock);

        private PaymentAdmin CreatePaymentAdmin(AppDbContext db) => new(db, new NumberIssuer(db, _tenant, _tenant, _clock), _clock);

        /// <summary>
        /// A child, their family, and the guardian the school bills. The financial
        /// link is what BR-PAR-005 makes the notice's addressee, so a fixture
        /// without one would prove nothing about who gets written to.
        /// </summary>
        private (int StudentId, int PayerId, int ParentId) Enrol(
            AppDbContext db, string studentNo, string firstAr, string firstEn, bool withPortalAccount, int? profileId = null)
        {
            var student = new Student
            {
                StudentNo = studentNo,
                FirstNameAr = firstAr, FatherNameAr = "الأب", GrandfatherNameAr = "الجد", FamilyNameAr = "العائلة",
                FirstNameEn = firstEn, FatherNameEn = "Father", GrandfatherNameEn = "Grandfather", FamilyNameEn = "Family",
                Gender = Gender.Female, DateOfBirth = new DateTime(2017, 1, 1), NationalityLookupId = 1,
            };
            db.Students.Add(student);

            var parent = new Parent
            {
                ParentFileNo = $"PAR-{studentNo}", NameAr = $"ولي {firstAr}", NameEn = $"Guardian of {firstEn}",
                PrimaryMobile = "0500000000", PreferredLanguage = "ar",
            };
            db.Parents.Add(parent);
            db.SaveChanges();

            if (withPortalAccount)
            {
                var account = new UserAccount { UserName = $"portal-{studentNo}", AccountType = AccountType.Parent };
                db.UserAccounts.Add(account);
                db.SaveChanges();
                parent.UserAccountId = account.Id;
            }

            db.Payers.Add(new Payer { Type = PayerType.Parent, ParentId = parent.Id });
            db.Enrollments.Add(new Enrollment
            {
                AcademicYearId = _yearId, StudentId = student.Id, GradeYearProfileId = profileId ?? _profileId,
                EnrollmentDate = new DateTime(2026, 9, 1), SourceType = EnrollmentSourceType.Admission,
            });
            db.StudentGuardianLinks.Add(new StudentGuardianLink
            {
                StudentId = student.Id, ParentId = parent.Id, RelationshipLookupId = 1,
                IsPrimaryContact = true, IsFinanciallyResponsible = true,
                EffectiveFromUtc = new DateTime(2026, 9, 1),
            });
            db.SaveChanges();

            return (student.Id, db.Payers.Single(p => p.ParentId == parent.Id).Id, parent.Id);
        }

        /// <summary>Three equal installments due in October, November and December.</summary>
        private static IReadOnlyList<TemplateSplit> Termly() => new[]
        {
            new TemplateSplit(40m, new DateTime(2026, 10, 1)),
            new TemplateSplit(30m, new DateTime(2026, 11, 1)),
            new TemplateSplit(30m, new DateTime(2026, 12, 1)),
        };

        private async Task<PlanAssignment> ScheduleAsync(AppDbContext db, decimal charge = 1000m, int? studentId = null, int? payerId = null)
        {
            var admin = CreateAdmin(db);
            await CreateFeeAdmin(db).PostManualChargeAsync(studentId ?? _studentId, payerId ?? _payerId, _categoryId, charge);
            var template = await admin.DefineTemplateAsync(_yearId, "خطة", "Termly", Termly(), graceDays: 3);
            await admin.ApproveTemplateAsync(template.Id);
            return await admin.AssignPlanAsync(studentId ?? _studentId, payerId ?? _payerId, template.Id, KsaWeekend);
        }

        /// <summary>The rule and the content the notification engine needs before it will queue anything (BR-NOT-003/008).</summary>
        private static async Task EnablePortalNoticesAsync(AppDbContext db)
        {
            var admin = new NotificationConfigAdmin(db);
            await admin.DefineTemplateAsync(
                "DunningLetterIssued", NotificationChannel.InApp, null, null,
                "رسوم مستحقة {NoticeNo}", "Fees outstanding {NoticeNo}");
            await admin.DefineSubscriptionRuleAsync(
                "DunningLetterIssued", NotificationChannel.InApp, NotificationTiming.Immediate, isEnabled: true);
        }

        // ------------------------------------------------------------------ the window

        [Fact]
        public async Task A_backwards_window_is_refused_by_both_the_roll_and_a_notice_run()
        {
            using var db = CreateContext();
            var followUp = CreateFollowUp(db);
            var window = new CollectionFilter(new DateTime(2026, 12, 31), new DateTime(2026, 12, 1));

            await Assert.ThrowsAsync<InvalidCollectionWindowException>(() => followUp.GetRollAsync(window));
            await Assert.ThrowsAsync<InvalidCollectionWindowException>(
                () => followUp.IssueNoticesAsync(new[] { _studentId }, CollectionNoticeChannel.Paper, window));
        }

        // ------------------------------------------------------------------ the roll

        [Fact]
        public async Task The_window_selects_installments_by_their_due_date()
        {
            using var db = CreateContext();
            await ScheduleAsync(db);
            var followUp = CreateFollowUp(db);

            // October only: 40% of 1,000.
            var october = await followUp.GetRollAsync(new CollectionFilter(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31)));
            Assert.Equal(400m, october.Rows.Single().Position.Outstanding);
            Assert.Equal(1, october.Rows.Single().Position.ItemCount);

            // Everything up to the end of November: October plus November.
            var toNovember = await followUp.GetRollAsync(new CollectionFilter(null, new DateTime(2026, 11, 30)));
            Assert.Equal(700m, toNovember.Rows.Single().Position.Outstanding);
            Assert.Equal(2, toNovember.Rows.Single().Position.ItemCount);

            // The whole plan.
            var all = await followUp.GetRollAsync(new CollectionFilter());
            Assert.Equal(1000m, all.Rows.Single().Position.Outstanding);
            Assert.Equal(1000m, all.TotalOutstanding);
        }

        [Fact]
        public async Task A_settled_installment_leaves_the_roll_and_a_fully_paid_family_leaves_it_entirely()
        {
            using var db = CreateContext();
            var assignment = await ScheduleAsync(db);
            var charge = db.Charges.Single(c => c.StudentId == _studentId);

            // BR-PAY-003 allocates oldest-first, so 400 settles October exactly.
            await CreatePaymentAdmin(db).CaptureReceiptAsync(_payerId, PaymentMethod.Cash, 400m);

            var followUp = CreateFollowUp(db);
            var october = await followUp.GetRollAsync(new CollectionFilter(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31)));
            Assert.Empty(october.Rows);

            var all = await followUp.GetRollAsync(new CollectionFilter());
            Assert.Equal(600m, all.Rows.Single().Position.Outstanding);
            Assert.Equal(new DateTime(2026, 11, 1), all.Rows.Single().Position.OldestDueDate);

            await CreatePaymentAdmin(db).CaptureReceiptAsync(_payerId, PaymentMethod.Cash, 600m);
            Assert.Empty((await CreateFollowUp(db).GetRollAsync(new CollectionFilter())).Rows);
            Assert.NotNull(assignment);
            Assert.NotNull(charge);
        }

        [Fact]
        public async Task A_family_with_no_installment_plan_still_appears_from_its_posted_charges()
        {
            // The case a roll built only from ppl.Installment would show as an empty screen: a
            // school that never adopted plans still has arrears. Charges age by posting date, which
            // is the reference RefreshAgedReceivablesAsync already uses.
            using var db = CreateContext();
            _clock.UtcNow = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
            await CreateFeeAdmin(db).PostManualChargeAsync(_studentId, _payerId, _categoryId, 750m);
            _clock.UtcNow = new DateTime(2026, 12, 1, 8, 0, 0, DateTimeKind.Utc);

            var roll = await CreateFollowUp(db).GetRollAsync(new CollectionFilter(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31)));

            var row = Assert.Single(roll.Rows);
            Assert.Equal(750m, row.Position.Outstanding);
            Assert.Equal(new DateTime(2026, 10, 5), row.Position.OldestDueDate);
            // 5 Oct to 1 Dec is 57 days, and ReceivablesAgingBucketer holds a charge "current" for its
            // first 30 — so 27 days into the ladder, which is the same arithmetic the finance dashboard does.
            Assert.Equal(AgingBucket.Days1To30, row.Bucket);
        }

        [Fact]
        public async Task The_roll_names_the_guardian_the_school_bills_and_says_who_can_be_reached_on_the_portal()
        {
            using var db = CreateContext();
            var (noPortalStudent, noPortalPayer, _) = Enrol(db, "STU-0002", "خالد", "Khalid", withPortalAccount: false);
            await ScheduleAsync(db);
            await CreateFeeAdmin(db).PostManualChargeAsync(noPortalStudent, noPortalPayer, _categoryId, 500m);

            var roll = await CreateFollowUp(db).GetRollAsync(new CollectionFilter());

            var sara = roll.Rows.Single(r => r.StudentNo == "STU-0001");
            Assert.Equal("ولي سارة", sara.GuardianNameAr);
            Assert.Equal("Guardian of Sara", sara.GuardianNameEn);
            Assert.True(sara.GuardianIsResponsible);
            Assert.True(sara.GuardianHasPortalAccount);
            Assert.Equal("Grade 3", sara.GradeNameEn);

            Assert.False(roll.Rows.Single(r => r.StudentNo == "STU-0002").GuardianHasPortalAccount);
        }

        [Fact]
        public async Task The_page_is_capped_but_the_totals_and_the_count_are_not()
        {
            using var db = CreateContext();
            await ScheduleAsync(db);
            var (second, secondPayer, _) = Enrol(db, "STU-0003", "نور", "Noor", withPortalAccount: true);
            await CreateFeeAdmin(db).PostManualChargeAsync(second, secondPayer, _categoryId, 250m);

            var roll = await CreateFollowUp(db).GetRollAsync(new CollectionFilter(), take: 1);

            Assert.Single(roll.Rows);
            Assert.Equal(2, roll.MatchCount);
            Assert.True(roll.IsTruncated);
            Assert.Equal(1250m, roll.TotalOutstanding);
        }

        // ------------------------------------------------------------------ notices

        [Fact]
        [BusinessRule("BR-INS-010")]
        public async Task A_paper_notice_is_numbered_logged_and_snapshots_what_was_owed()
        {
            using var db = CreateContext();
            await ScheduleAsync(db);
            var window = new CollectionFilter(new DateTime(2026, 10, 1), new DateTime(2026, 11, 30));

            var batch = await CreateFollowUp(db).IssueNoticesAsync(new[] { _studentId }, CollectionNoticeChannel.Paper, window);

            var issued = Assert.Single(batch.Issued);
            Assert.StartsWith("DUN-", issued.Notice.NoticeNo);
            Assert.Equal(700m, issued.Notice.AmountDue);
            Assert.Equal(CollectionNoticeChannel.Paper, issued.Notice.Channel);
            Assert.Equal(new DateTime(2026, 10, 1), issued.Notice.WindowFrom);
            Assert.Equal(new DateTime(2026, 11, 30), issued.Notice.WindowTo);
            Assert.Equal(_payerId, issued.Notice.PayerId);

            // Committed, not just returned — the log is the retained record BR-GLB-102 requires.
            using var fresh = CreateContext();
            var stored = Assert.Single(fresh.CollectionNotices);
            Assert.Equal(issued.Notice.NoticeNo, stored.NoticeNo);
            Assert.Equal(700m, stored.AmountDue);

            // And it stays as issued after the family pays, so the sheet in their hand keeps matching.
            await CreatePaymentAdmin(fresh).CaptureReceiptAsync(_payerId, PaymentMethod.Cash, 700m);
            Assert.Equal(700m, CreateContext().CollectionNotices.Single().AmountDue);
        }

        [Fact]
        [BusinessRule("BR-INS-008")]
        public async Task A_hand_issued_notice_never_advances_the_automatic_dunning_ladder()
        {
            // The trap this design exists to avoid: DunningLadderEvaluator takes the highest fired
            // step as its floor, so a manual letter recorded as a ladder step would cancel every
            // rung below it — the +3/+14/+30 notices BR-INS-008 requires would never fire.
            using var db = CreateContext();
            await ScheduleAsync(db);

            await CreateFollowUp(db).IssueNoticesAsync(new[] { _studentId }, CollectionNoticeChannel.Paper, new CollectionFilter());

            using var fresh = CreateContext();
            Assert.Empty(fresh.DunningEvents);
            Assert.Single(fresh.CollectionNotices);

            // The ladder then runs and still fires its own steps, unaware of the letter.
            var fired = await CreateAdmin(fresh).RunDunningAsync();
            Assert.NotEmpty(fired);
        }

        [Fact]
        public async Task A_portal_notice_queues_an_in_app_delivery_to_the_guardian()
        {
            using var db = CreateContext();
            await ScheduleAsync(db);
            await EnablePortalNoticesAsync(db);

            var batch = await CreateFollowUp(db).IssueNoticesAsync(
                new[] { _studentId }, CollectionNoticeChannel.Portal, new CollectionFilter());

            Assert.Single(batch.Issued);
            Assert.Equal(0, batch.SkippedNoPortalAccount);

            using var fresh = CreateContext();
            var portalUserId = fresh.Parents.Single(p => p.Id == _parentId).UserAccountId;
            var delivery = Assert.Single(fresh.Deliveries.Where(d => d.EventCode == "DunningLetterIssued"));
            Assert.Equal(NotificationChannel.InApp, delivery.Channel);
            Assert.Equal(portalUserId, delivery.RecipientUserId);
            Assert.Equal(DeliveryStatus.Queued, delivery.Status);

            // The number reached the rendered body, so the family's inbox names the notice they can quote
            // back — and it is rendered in the guardian's own language (BR-NOT-001), not the officer's.
            Assert.Contains(fresh.CollectionNotices.Single().NoticeNo, delivery.RenderedBody);
            Assert.StartsWith("رسوم مستحقة", delivery.RenderedBody);
        }

        [Fact]
        public async Task A_family_with_no_portal_sign_in_is_skipped_and_counted_rather_than_dropped()
        {
            using var db = CreateContext();
            var (offline, offlinePayer, _) = Enrol(db, "STU-0004", "ريم", "Reem", withPortalAccount: false);
            await CreateFeeAdmin(db).PostManualChargeAsync(offline, offlinePayer, _categoryId, 300m);
            await EnablePortalNoticesAsync(db);

            var batch = await CreateFollowUp(db).IssueNoticesAsync(
                new[] { offline }, CollectionNoticeChannel.Portal, new CollectionFilter());

            Assert.Empty(batch.Issued);
            Assert.Equal(1, batch.SkippedNoPortalAccount);
            Assert.Empty(CreateContext().CollectionNotices);
        }

        [Fact]
        [BusinessRule("BR-INS-009")]
        public async Task A_balance_covered_by_a_post_dated_cheque_is_shown_but_never_chased()
        {
            using var db = CreateContext();
            var assignment = await ScheduleAsync(db);
            var admin = CreateAdmin(db);
            var october = (await admin.GetScheduleAsync(assignment.Id))[0];
            var pdc = await CreatePaymentAdmin(db).LodgePdcAsync(_payerId, "بنك", "CHQ-1", new DateTime(2026, 10, 15), 400m);
            await admin.MarkPdcCoveredAsync(october.InstallmentId, pdc.Id);

            var window = new CollectionFilter(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31));
            var followUp = CreateFollowUp(db);

            var roll = await followUp.GetRollAsync(window);
            var row = Assert.Single(roll.Rows);
            Assert.Equal(400m, row.Position.Outstanding);   // still owed
            Assert.Equal(0m, row.Position.Notifiable);      // but not chaseable
            Assert.True(row.Position.HasPdcCoveredItems);
            Assert.Equal(0m, roll.TotalNotifiable);

            var batch = await followUp.IssueNoticesAsync(new[] { _studentId }, CollectionNoticeChannel.Paper, window);
            Assert.Empty(batch.Issued);
            Assert.Equal(1, batch.SkippedPdcCovered);
            Assert.Empty(CreateContext().CollectionNotices);

            // And the cheque-covered part is netted off a notice raised over a wider window.
            var wider = await CreateFollowUp(CreateContext()).IssueNoticesAsync(
                new[] { _studentId }, CollectionNoticeChannel.Paper, new CollectionFilter());
            Assert.Equal(600m, Assert.Single(wider.Issued).Notice.AmountDue);
        }

        [Fact]
        public async Task A_student_who_owes_nothing_in_the_window_is_skipped_and_counted()
        {
            using var db = CreateContext();
            await ScheduleAsync(db);

            var batch = await CreateFollowUp(db).IssueNoticesAsync(
                new[] { _studentId },
                CollectionNoticeChannel.Paper,
                new CollectionFilter(new DateTime(2027, 5, 1), new DateTime(2027, 5, 31)));

            Assert.Empty(batch.Issued);
            Assert.Equal(1, batch.SkippedNothingOutstanding);
        }

        [Fact]
        public async Task Selecting_nobody_issues_nothing_rather_than_writing_to_the_whole_school()
        {
            using var db = CreateContext();
            await ScheduleAsync(db);

            var batch = await CreateFollowUp(db).IssueNoticesAsync(
                Array.Empty<int>(), CollectionNoticeChannel.Paper, new CollectionFilter());

            Assert.Empty(batch.Issued);
            Assert.Empty(CreateContext().CollectionNotices);
        }

        [Fact]
        public async Task The_roll_reports_when_a_family_was_last_written_to()
        {
            using var db = CreateContext();
            await ScheduleAsync(db);
            await CreateFollowUp(db).IssueNoticesAsync(new[] { _studentId }, CollectionNoticeChannel.Paper, new CollectionFilter());

            var row = Assert.Single((await CreateFollowUp(CreateContext()).GetRollAsync(new CollectionFilter())).Rows);

            Assert.Equal(_clock.UtcNow, row.LastNoticeAtUtc);
            Assert.Equal(CollectionNoticeChannel.Paper, row.LastNoticeChannel);
        }
    }
}
