using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Application.Students;
using Sms.Domain.Common;
using Sms.Domain.Fees;
using Sms.Domain.Grades;
using Sms.Domain.Parents;
using Sms.Domain.Schools;
using Sms.Domain.Students;
using Sms.Domain.Transport;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Persistence;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// <see cref="StudentSearch"/> against a real provider. The engine's own unit tests run it over an
    /// in-memory queryable, which proves the rule and nothing about the database; two things can only
    /// fail here.
    /// <para>
    /// The first is translation. EF Core 5 throws rather than silently evaluating a Where on the
    /// client, so a ToLower().Contains() the provider cannot render is a runtime failure on a screen
    /// that compiled — the class of defect this repository keeps paying for.
    /// </para>
    /// <para>
    /// The second is the transport subscription list's search, which used to run in memory over an
    /// already-capped page: the 500 newest subscriptions were fetched and then filtered, so a student
    /// outside that page could not be found at all. The screen answered "no subscriptions match" for a
    /// child who plainly had one. What went wrong there is the order of two operations rather than the
    /// number of rows, so it is asserted over a handful of them against a deliberately small cap.
    /// </para>
    /// </summary>
    public sealed class StudentSearchQueryTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2026, 10, 5, 5, 0, 0, DateTimeKind.Utc);
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

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();
        private readonly int _yearId;
        private readonly int _profileId;
        private readonly int _payerId;

        public StudentSearchQueryTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();

            var year = new AcademicYear
            {
                LabelAr = "العام", LabelEn = "2026-2027", HijriLabel = "1448",
                StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2027, 6, 30), Status = AcademicYearStatus.Active,
            };
            db.AcademicYears.Add(year);
            var stage = new Stage { Name = new LocalizedName("مرحلة", "Elementary"), SequenceOrder = 1, DefaultGenderPolicy = GenderPolicy.Mixed };
            db.Stages.Add(stage);
            db.SaveChanges();
            _tenant.AcademicYearId = year.Id;
            _yearId = year.Id;

            var grade = new GradeLevel { StageId = stage.Id, Code = "G3", Name = new LocalizedName("ثالث", "Grade 3"), SequenceOrder = 3 };
            db.GradeLevels.Add(grade);
            db.SaveChanges();
            var profile = new GradeYearProfile { GradeLevelId = grade.Id, AcademicYearId = year.Id, GenderPolicy = GenderPolicy.Mixed, TargetSections = 1, TargetSectionSize = 25 };
            db.GradeYearProfiles.Add(profile);
            db.SaveChanges();
            _profileId = profile.Id;

            var parent = new Parent { ParentFileNo = "PAR-000001", NameAr = "ولي", NameEn = "Guardian", PrimaryMobile = "0500000000", UserAccountId = 42 };
            db.Parents.Add(parent);
            db.SaveChanges();
            var payer = new Payer { Type = PayerType.Parent, ParentId = parent.Id };
            db.Payers.Add(payer);
            db.SaveChanges();
            _payerId = payer.Id;
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        /// <summary>A child with a full four-part name in each language, unsaved.</summary>
        private static Student Child(
            string no,
            string firstAr, string fatherAr, string grandAr, string familyAr,
            string firstEn, string fatherEn, string grandEn, string familyEn) => new()
        {
            StudentNo = no,
            FirstNameAr = firstAr, FatherNameAr = fatherAr, GrandfatherNameAr = grandAr, FamilyNameAr = familyAr,
            FirstNameEn = firstEn, FatherNameEn = fatherEn, GrandfatherNameEn = grandEn, FamilyNameEn = familyEn,
            Gender = Gender.Male, DateOfBirth = new DateTime(2018, 1, 1), NationalityLookupId = 1,
        };

        /// <summary>Adds a child and enrols them for the year.</summary>
        private Student AddChild(
            AppDbContext db, string no,
            string firstAr, string fatherAr, string grandAr, string familyAr,
            string firstEn, string fatherEn, string grandEn, string familyEn)
        {
            var student = Child(no, firstAr, fatherAr, grandAr, familyAr, firstEn, fatherEn, grandEn, familyEn);
            db.Students.Add(student);
            db.SaveChanges();
            db.Enrollments.Add(new Enrollment
            {
                AcademicYearId = _yearId, StudentId = student.Id, GradeYearProfileId = _profileId,
                EnrollmentDate = new DateTime(2026, 9, 1), SourceType = EnrollmentSourceType.Admission,
            });
            db.SaveChanges();
            return student;
        }

        // ================================================================== translation

        /// <summary>
        /// Every provider-level claim in one context, deliberately. Each test method here builds a
        /// fresh in-memory database and <c>EnsureCreated()</c> raises the entire model's schema, so a
        /// method is expensive in a way an engine unit test is not — and this assembly runs its
        /// classes in parallel with <see cref="PerfGateTests"/>, whose attendance-save gate is a p95
        /// against a one-second budget. Nine cheap-looking methods were enough to push it over.
        /// </summary>
        [Fact]
        public async Task The_search_translates_to_SQL_folds_case_and_narrows_word_by_word()
        {
            using var db = CreateContext();
            AddChild(db, "STU-0231", "محمد", "أحمد", "سعيد", "الغامدي", "Mohammed", "Ahmed", "Saeed", "Alghamdi");
            AddChild(db, "STU-0232", "محمد", "خالد", "سعيد", "القحطاني", "Mohammed", "Khaled", "Saeed", "Alqahtani");
            AddChild(db, "STU-0777", "سارة", "عمر", "فهد", "الحربي", "Sara", "Omar", "Fahd", "Alharbi");

            async Task<string[]> Match(string term) =>
                await StudentSearch.Matching(db.Students.AsNoTracking(), term)
                    .OrderBy(s => s.StudentNo).Select(s => s.StudentNo).ToArrayAsync();

            // EF Core 5 refuses to evaluate a Where on the client, so reaching an answer at all is the
            // assertion for translation: the whole expression rendered to SQL.
            Assert.Equal(new[] { "STU-0231" }, await Match("الغامدي"));

            // Sqlite renders Contains as a case-sensitive instr while SQL Server's default collation is
            // case-insensitive. Folding inside the expression is what makes the two agree, and this is
            // the half of that claim the engine's own unit tests cannot make.
            foreach (var typed in new[] { "mohammed", "MOHAMMED", "Mohammed" })
            {
                Assert.Equal(new[] { "STU-0231", "STU-0232" }, await Match(typed));
            }

            foreach (var typed in new[] { "alghamdi", "ALGHAMDI", "stu-0231" })
            {
                Assert.Equal(new[] { "STU-0231" }, await Match(typed));
            }

            // And the AND-semantics survive translation too: a second word narrows in SQL exactly as
            // it narrows in memory.
            Assert.Equal(new[] { "STU-0231", "STU-0232" }, await Match("محمد"));
            Assert.Equal(new[] { "STU-0231" }, await Match("محمد أحمد"));
            Assert.Empty(await Match("محمد زيد"));
        }

        // ================================================================== the subscription list's search

        [Fact]
        public async Task A_subscription_older_than_the_page_cap_is_still_found_by_a_search()
        {
            using var db = CreateContext();

            // The defect is the order of two operations, not the size of the table: filtering after the
            // cap searches only the page, filtering before it searches the year. A cap of three over six
            // subscriptions demonstrates that exactly as the screen's five hundred would, and does it
            // without seeding fifteen hundred rows beside a p95 perf gate that shares this assembly's
            // CPU — a test heavy enough to slow its neighbours is a test that fails other people's work.
            const int SmallCap = 3;

            var wanted = Child("STU-0001", "ريما", "بدر", "ناصر", "الدوسري", "Reema", "Bader", "Nasser", "Aldosari");
            var register = new List<Student> { wanted };
            for (var i = 1; i <= SmallCap + 2; i++)
            {
                register.Add(Child(
                    "STU-" + i.ToString("D4", CultureInfo.InvariantCulture) + "-N",
                    "طالب", "والد", "جد", "عائلة", "Pupil", "Father", "Grand", "Family"));
            }

            db.Students.AddRange(register);
            await db.SaveChangesAsync();

            db.Enrollments.AddRange(register.Select(s => new Enrollment
            {
                AcademicYearId = _yearId, StudentId = s.Id, GradeYearProfileId = _profileId,
                EnrollmentDate = new DateTime(2026, 9, 1), SourceType = EnrollmentSourceType.Admission,
            }));
            await db.SaveChangesAsync();

            // The wanted child subscribes first and so sorts last: OrderByDescending(StartDate) puts
            // them beyond the page the screen fetches. Everyone else is noise with a different name.
            var enrollmentByStudent = await db.Enrollments.AsNoTracking()
                .ToDictionaryAsync(e => e.StudentId, e => e.Id);
            var start = new DateTime(2026, 9, 1);

            db.TransportSubscriptions.AddRange(register.Select((s, index) =>
                NewSubscription(s.Id, enrollmentByStudent[s.Id], start.AddDays(index))));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            // What the screen does now: narrow in the database, then take the page.
            var matchingIds = StudentSearch.Matching(db.Students.AsNoTracking(), "الدوسري").Select(s => s.Id);
            var page = await db.TransportSubscriptions.AsNoTracking()
                .Where(s => s.AcademicYearId == _yearId)
                .Where(s => matchingIds.Contains(s.StudentId))
                .OrderByDescending(s => s.StartDate).Take(SmallCap).ToListAsync();

            Assert.Equal(new[] { wanted.Id }, page.Select(s => s.StudentId).ToArray());

            // What it used to do, kept as the contrast: take the page, then filter it. The child has a
            // subscription and the screen said there was none.
            var oldWay = (await db.TransportSubscriptions.AsNoTracking()
                    .Where(s => s.AcademicYearId == _yearId)
                    .OrderByDescending(s => s.StartDate).Take(SmallCap).ToListAsync())
                .Where(s => s.StudentId == wanted.Id).ToList();

            Assert.Empty(oldWay);
        }

        private TransportSubscription NewSubscription(int studentId, int enrollmentId, DateTime start) => new()
        {
            AcademicYearId = _yearId, EnrollmentId = enrollmentId, StudentId = studentId, PayerId = _payerId,
            StartDate = start, Status = TransportSubscriptionStatus.Active,
        };

        // ================================================================== the subscribe form's picker

        [Fact]
        public async Task The_picker_offers_only_children_enrolled_in_the_working_year()
        {
            using var db = CreateContext();
            var enrolled = AddChild(db, "STU-0231", "محمد", "أحمد", "سعيد", "الغامدي", "Mohammed", "Ahmed", "Saeed", "Alghamdi");

            // A registered but unenrolled child — a transfer-in whose enrolment has not been made yet.
            db.Students.Add(new Student
            {
                StudentNo = "STU-0999", FirstNameAr = "محمد", FatherNameAr = "سالم", GrandfatherNameAr = "علي", FamilyNameAr = "الغامدي",
                FirstNameEn = "Mohammed", FatherNameEn = "Salem", GrandfatherNameEn = "Ali", FamilyNameEn = "Alghamdi",
                Gender = Gender.Male, DateOfBirth = new DateTime(2018, 1, 1), NationalityLookupId = 1,
            });
            await db.SaveChangesAsync();

            var enrolledIds = await db.Enrollments.AsNoTracking()
                .Where(e => e.AcademicYearId == _yearId && e.Status == EnrollmentStatus.Active)
                .Select(e => e.StudentId).Distinct().ToListAsync();

            var offered = await StudentSearch
                .Matching(db.Students.AsNoTracking().Where(s => enrolledIds.Contains(s.Id)), "الغامدي")
                .Select(s => s.Id).ToListAsync();

            // Both children answer the search; only one of them is a subscription this desk could make,
            // because SubscribeAsync loads an enrolment before it does anything else. Offering the other
            // is offering a refusal.
            Assert.Equal(new[] { enrolled.Id }, offered);
        }
    }
}
