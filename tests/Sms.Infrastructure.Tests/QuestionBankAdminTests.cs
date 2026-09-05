using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Learning;
using Sms.Application.Setup;
using Sms.Domain.Common;
using Sms.Domain.Employees;
using Sms.Domain.Grades;
using Sms.Domain.Learning;
using Sms.Domain.Schools;
using Sms.Domain.Sections;
using Sms.Domain.Setup;
using Sms.Domain.Subjects;
using Sms.Domain.Teachers;
using Sms.Domain.Timetable;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Learning;
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// doc/Modules/37 §8.6 (BR-LRN-001/002/007/011) over a real Sqlite-backed
    /// AppDbContext. The versioning tests are the point of the file: BR-LRN-007
    /// promises a past paper renders as it was answered, and the only way to know
    /// that holds is to revise a question and then read the old row back.
    /// </summary>
    public sealed class QuestionBankAdminTests : IDisposable
    {
        private const int TeacherUserId = 500;
        private const int StrangerUserId = 700;

        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2027, 1, 20, 8, 0, 0, DateTimeKind.Utc);
        }

        private sealed class FixedUser : ICurrentUser
        {
            public int UserId { get; set; } = TeacherUserId;
        }

        private sealed class FixedTenant : ITenantContext, IWorkingYearContext
        {
            public int SchoolId => 1;

            public int AcademicYearId => 2027;
        }

        private sealed class FixedSetup : ISystemSetupAdmin
        {
            public Task<string?> GetSettingAsync(string key, int? academicYearId = null, CancellationToken cancellationToken = default)
                => Task.FromResult<string?>(key == SettingKeys.WorkingDays ? "Sunday,Monday,Tuesday,Wednesday,Thursday" : null);

            public Task<CountryPack> DefineCountryPackAsync(CountryPackDefinition definition, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task BindCountryPackAsync(string packCode, string? reason = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<CountryPack?> GetBoundCountryPackAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<SchoolSetting> SetSettingAsync(string key, string value, int? academicYearId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<IReadOnlyList<SchoolSetting>> ListSettingsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task SetFeatureAsync(string featureCode, bool enabled, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<IReadOnlyDictionary<string, bool>> GetFeatureStatesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<IReadOnlyList<StepState>> GetChecklistAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task CompleteStepAsync(string stepCode, string? notes = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task DeclareSetupCompleteAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<bool> IsSetupCompleteAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        }

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();
        private readonly FixedSetup _setup = new();

        private readonly int _mathOfferingId;
        private readonly int _artOfferingId;

        public QuestionBankAdminTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();

            var year = new AcademicYear
            {
                LabelAr = "٢٠٢٦-٢٠٢٧", LabelEn = "2026-2027", HijriLabel = "١٤٤٨هـ",
                StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2027, 6, 30), Status = AcademicYearStatus.Active,
            };
            db.AcademicYears.Add(year);

            var stage = new Stage { Name = new LocalizedName("الابتدائية", "Elementary"), SequenceOrder = 1, DefaultGenderPolicy = GenderPolicy.Mixed };
            db.Stages.Add(stage);
            db.SaveChanges();

            var grade = new GradeLevel { StageId = stage.Id, Code = "G3", Name = new LocalizedName("ثالث", "Grade 3"), SequenceOrder = 3 };
            db.GradeLevels.Add(grade);
            db.SaveChanges();

            var profile = new GradeYearProfile { GradeLevelId = grade.Id, AcademicYearId = year.Id, GenderPolicy = GenderPolicy.Mixed, TargetSections = 1, TargetSectionSize = 25 };
            db.GradeYearProfiles.Add(profile);
            db.SaveChanges();

            var math = new Subject { SchoolId = 1, Code = "MATH", Name = new LocalizedName("رياضيات", "Math"), Category = "core" };
            var art = new Subject { SchoolId = 1, Code = "ART", Name = new LocalizedName("فنون", "Art"), Category = "core" };
            db.Subjects.AddRange(math, art);
            db.SaveChanges();

            var mathOffering = NewOffering(year.Id, profile.Id, math.Id);
            var artOffering = NewOffering(year.Id, profile.Id, art.Id);
            db.CurriculumOfferings.AddRange(mathOffering, artOffering);

            var section = new Section { SchoolId = 1, AcademicYearId = year.Id, GradeYearProfileId = profile.Id, NameAr = "ثالث-أ", NameEn = "3-A", Capacity = 25, GenderPolicy = GenderPolicy.Mixed };
            db.Sections.Add(section);
            db.SaveChanges();

            var employee = new Employee
            {
                SchoolId = 1, EmployeeNo = "EMP-1", UserAccountId = TeacherUserId,
                FirstNameAr = "معلم", FatherNameAr = "أب", GrandfatherNameAr = "جد", FamilyNameAr = "عائلة",
                FirstNameEn = "Teacher", FatherNameEn = "Father", GrandfatherNameEn = "Grandfather", FamilyNameEn = "Family",
                Gender = Gender.Male, DateOfBirth = new DateTime(1990, 1, 1), NationalityLookupId = 1,
            };
            db.Employees.Add(employee);
            db.SaveChanges();

            var teacher = new TeacherProfile { SchoolId = 1, EmployeeId = employee.Id, MaxWeeklyPeriods = 24 };
            db.TeacherProfiles.Add(teacher);

            var shape = new TimetableShape { SchoolId = 1, AcademicYearId = year.Id, StageId = stage.Id };
            db.TimetableShapes.Add(shape);
            db.SaveChanges();

            var slot = new PeriodSlot
            {
                SchoolId = 1, TimetableShapeId = shape.Id, DayOfWeek = DayOfWeek.Sunday, SequenceNumber = 1,
                StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(8, 45, 0),
            };
            db.PeriodSlots.Add(slot);

            var published = new TimetableVersion
            {
                SchoolId = 1, AcademicYearId = year.Id, Status = TimetableVersionStatus.Published,
                PublishedAtUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            };
            db.TimetableVersions.Add(published);
            db.SaveChanges();

            // The teacher holds Math and nothing else — Art is the class next door
            // and is what BR-LRN-002's refusal is measured against.
            db.Placements.Add(new Placement
            {
                SchoolId = 1, TimetableVersionId = published.Id, SectionId = section.Id,
                PeriodSlotId = slot.Id, CurriculumOfferingId = mathOffering.Id, TeacherProfileId = teacher.Id,
            });
            db.SaveChanges();

            _mathOfferingId = mathOffering.Id;
            _artOfferingId = artOffering.Id;
        }

        public void Dispose() => _connection.Dispose();

        // ---------------------------------------------------------------- reach

        [Fact]
        [BusinessRule("BR-LRN-002")]
        public async Task A_teacher_authors_for_the_subjects_they_teach()
        {
            using var db = CreateContext();

            var reachable = await CreateAdmin(db).ReachableOfferingsAsync();

            Assert.Equal(new[] { _mathOfferingId }, reachable);
        }

        [Fact]
        [BusinessRule("BR-LRN-002")]
        public async Task A_teacher_cannot_open_a_bank_for_a_subject_they_do_not_teach()
            => await Assert.ThrowsAsync<TeachingReachException>(async () =>
            {
                using var db = CreateContext();
                await CreateAdmin(db).CreateBankAsync(_artOfferingId, "بنك", "Bank");
            });

        [Fact]
        [BusinessRule("BR-LRN-002")]
        public async Task A_stranger_reaches_nothing()
        {
            using var db = CreateContext();
            _user.UserId = StrangerUserId;

            Assert.Empty(await CreateAdmin(db).ReachableOfferingsAsync());
        }

        // ---------------------------------------------------------------- sharing

        [Fact]
        [BusinessRule("BR-LRN-007")]
        public async Task A_bank_kept_private_is_still_the_authors_own_to_see()
        {
            using var db = CreateContext();
            await CreateAdmin(db).CreateBankAsync(_mathOfferingId, "خاص", "Private", QuestionShareScope.AuthorOnly);

            Assert.Single(await CreateAdmin(db).BanksAsync(_mathOfferingId));
        }

        [Fact]
        [BusinessRule("BR-LRN-007")]
        public async Task A_private_bank_is_invisible_to_the_subjects_other_teachers()
        {
            using var db = CreateContext();
            await CreateAdmin(db).CreateBankAsync(_mathOfferingId, "خاص", "Private", QuestionShareScope.AuthorOnly);

            // A colleague on the same offering. Reach is not the question here —
            // sharing is.
            _user.UserId = TeacherUserId + 1;
            AddPlacementFor(db, TeacherUserId + 1);

            Assert.Empty(await CreateAdmin(db).BanksAsync(_mathOfferingId));
        }

        [Fact]
        [BusinessRule("BR-LRN-007")]
        public async Task A_bank_shared_to_the_subject_is_visible_to_its_other_teachers()
        {
            using var db = CreateContext();
            await CreateAdmin(db).CreateBankAsync(_mathOfferingId, "مشترك", "Shared", QuestionShareScope.Offering);

            _user.UserId = TeacherUserId + 1;
            AddPlacementFor(db, TeacherUserId + 1);

            Assert.Single(await CreateAdmin(db).BanksAsync(_mathOfferingId));
        }

        // ---------------------------------------------------------------- shape

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public async Task A_question_that_cannot_be_marked_never_reaches_the_bank()
        {
            using var db = CreateContext();
            var bank = await CreateAdmin(db).CreateBankAsync(_mathOfferingId, "بنك", "Bank");

            var ex = await Assert.ThrowsAsync<QuestionShapeException>(() => CreateAdmin(db).AddQuestionAsync(
                bank.Id,
                new QuestionDraft
                {
                    Type = QuestionType.SingleChoice,
                    StemAr = "سؤال", StemEn = "Question", Marks = 1m,
                    Options = new[] { new QuestionDraftOption("أ", "A", false), new QuestionDraftOption("ب", "B", false) },
                }));

            Assert.Equal(QuestionShapeRefusal.NoCorrectOption, ex.Refusal);
            Assert.Empty(await db.Questions.ToListAsync());
        }

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public async Task A_well_formed_question_is_stored_with_its_options()
        {
            using var db = CreateContext();
            var bank = await CreateAdmin(db).CreateBankAsync(_mathOfferingId, "بنك", "Bank");

            var question = await CreateAdmin(db).AddQuestionAsync(bank.Id, SingleChoiceDraft());

            Assert.Equal(1, question.Version);
            Assert.True(question.IsCurrentVersion);

            // Version 1 is its own root, which is what every later revision hangs
            // off — a root pointing at nothing would make the history unfindable.
            Assert.Equal(question.Id, question.RootQuestionId);

            var (options, _) = await CreateAdmin(db).DetailAsync(question.Id);
            Assert.Equal(3, options.Count);
            Assert.Single(options, o => o.IsCorrect);
        }

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public async Task Short_text_answers_are_stored_for_the_marker_to_match_against()
        {
            using var db = CreateContext();
            var bank = await CreateAdmin(db).CreateBankAsync(_mathOfferingId, "بنك", "Bank");

            var question = await CreateAdmin(db).AddQuestionAsync(bank.Id, new QuestionDraft
            {
                Type = QuestionType.ShortText,
                StemAr = "ما رمز الماء؟", StemEn = "What is the formula for water?",
                Marks = 2m,
                AcceptedAnswers = new[] { "H2O", "ماء", "  " },
            });

            var (_, answers) = await CreateAdmin(db).DetailAsync(question.Id);

            // The blank line the author left behind is not an accepted answer.
            Assert.Equal(2, answers.Count);
        }

        // ---------------------------------------------------------------- versioning

        [Fact]
        [BusinessRule("BR-LRN-007")]
        public async Task Revising_a_question_leaves_the_answered_version_exactly_as_it_was()
        {
            using var db = CreateContext();
            var bank = await CreateAdmin(db).CreateBankAsync(_mathOfferingId, "بنك", "Bank");
            var first = await CreateAdmin(db).AddQuestionAsync(bank.Id, SingleChoiceDraft());

            var revised = SingleChoiceDraft();
            revised.StemAr = "سؤال معدَّل";
            revised.StemEn = "Reworded question";

            var second = await CreateAdmin(db).ReviseQuestionAsync(first.Id, revised);

            Assert.Equal(2, second.Version);
            Assert.Equal(first.RootQuestionId, second.RootQuestionId);
            Assert.True(second.IsCurrentVersion);

            // The row a paper froze is untouched — same wording, still there.
            var original = await db.Questions.AsNoTracking().SingleAsync(q => q.Id == first.Id);
            Assert.Equal("Question", original.StemEn);
            Assert.False(original.IsCurrentVersion);
        }

        [Fact]
        [BusinessRule("BR-LRN-007")]
        public async Task A_revision_brings_its_own_options_rather_than_sharing_the_old_ones()
        {
            using var db = CreateContext();
            var bank = await CreateAdmin(db).CreateBankAsync(_mathOfferingId, "بنك", "Bank");
            var first = await CreateAdmin(db).AddQuestionAsync(bank.Id, SingleChoiceDraft());

            var revised = SingleChoiceDraft();
            revised.Options = new[]
            {
                new QuestionDraftOption("خيار جديد", "New option", true),
                new QuestionDraftOption("آخر", "Other", false),
            };

            var second = await CreateAdmin(db).ReviseQuestionAsync(first.Id, revised);

            var (oldOptions, _) = await CreateAdmin(db).DetailAsync(first.Id);
            var (newOptions, _) = await CreateAdmin(db).DetailAsync(second.Id);

            // This is the half of BR-LRN-007 that is easy to get wrong: rewriting
            // the choices a student was shown last term would make the paper they
            // sat render as a paper they never saw.
            Assert.Equal(3, oldOptions.Count);
            Assert.Equal(2, newOptions.Count);
            Assert.Contains(oldOptions, o => o.TextEn == "A");
            Assert.DoesNotContain(newOptions, o => o.TextEn == "A");
        }

        [Fact]
        [BusinessRule("BR-LRN-007")]
        public async Task Only_the_current_version_appears_in_the_bank()
        {
            using var db = CreateContext();
            var bank = await CreateAdmin(db).CreateBankAsync(_mathOfferingId, "بنك", "Bank");
            var first = await CreateAdmin(db).AddQuestionAsync(bank.Id, SingleChoiceDraft());
            await CreateAdmin(db).ReviseQuestionAsync(first.Id, SingleChoiceDraft());

            var live = await CreateAdmin(db).QuestionsAsync(bank.Id);
            var history = await CreateAdmin(db).VersionsAsync(first.RootQuestionId);

            Assert.Single(live);
            Assert.Equal(2, live[0].Version);
            Assert.Equal(2, history.Count);
        }

        [Fact]
        [BusinessRule("BR-LRN-007")]
        public async Task An_earlier_version_is_never_the_base_of_an_edit()
        {
            using var db = CreateContext();
            var bank = await CreateAdmin(db).CreateBankAsync(_mathOfferingId, "بنك", "Bank");
            var first = await CreateAdmin(db).AddQuestionAsync(bank.Id, SingleChoiceDraft());
            await CreateAdmin(db).ReviseQuestionAsync(first.Id, SingleChoiceDraft());

            await Assert.ThrowsAsync<QuestionNotCurrentVersionException>(
                () => CreateAdmin(db).ReviseQuestionAsync(first.Id, SingleChoiceDraft()));
        }

        [Fact]
        [BusinessRule("BR-LRN-007")]
        public async Task The_root_and_version_pair_is_guaranteed_by_the_database()
        {
            using var db = CreateContext();
            var bank = await CreateAdmin(db).CreateBankAsync(_mathOfferingId, "بنك", "Bank");
            var first = await CreateAdmin(db).AddQuestionAsync(bank.Id, SingleChoiceDraft());

            // Bypasses the service deliberately: "version 2 of this question" is
            // only a guarantee if the database holds it.
            db.Questions.Add(new Question
            {
                SchoolId = 1, AcademicYearId = first.AcademicYearId, QuestionBankId = bank.Id,
                RootQuestionId = first.RootQuestionId, Version = 1, Type = QuestionType.Essay,
                StemAr = "مكرر", StemEn = "Duplicate", Marks = 1m,
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        // ---------------------------------------------------------------- deprecation

        [Fact]
        [BusinessRule("BR-LRN-007")]
        public async Task A_withdrawn_question_leaves_the_pick_list_and_stays_readable()
        {
            using var db = CreateContext();
            var bank = await CreateAdmin(db).CreateBankAsync(_mathOfferingId, "بنك", "Bank");
            var question = await CreateAdmin(db).AddQuestionAsync(bank.Id, SingleChoiceDraft());

            await CreateAdmin(db).DeprecateQuestionAsync(question.Id, "الصياغة مضلِّلة");

            Assert.Empty(await CreateAdmin(db).QuestionsAsync(bank.Id));
            Assert.Single(await CreateAdmin(db).QuestionsAsync(bank.Id, includeDeprecated: true));
        }

        [Fact]
        [BusinessRule("BR-LRN-007")]
        public async Task Withdrawing_states_a_reason()
        {
            using var db = CreateContext();
            var bank = await CreateAdmin(db).CreateBankAsync(_mathOfferingId, "بنك", "Bank");
            var question = await CreateAdmin(db).AddQuestionAsync(bank.Id, SingleChoiceDraft());

            await Assert.ThrowsAsync<ArgumentException>(
                () => CreateAdmin(db).DeprecateQuestionAsync(question.Id, "   "));
        }

        [Fact]
        [BusinessRule("BR-LRN-007")]
        public async Task A_withdrawn_question_is_not_revived_by_editing_it()
        {
            using var db = CreateContext();
            var bank = await CreateAdmin(db).CreateBankAsync(_mathOfferingId, "بنك", "Bank");
            var question = await CreateAdmin(db).AddQuestionAsync(bank.Id, SingleChoiceDraft());
            await CreateAdmin(db).DeprecateQuestionAsync(question.Id, "الصياغة مضلِّلة");

            await Assert.ThrowsAsync<QuestionDeprecatedException>(
                () => CreateAdmin(db).ReviseQuestionAsync(question.Id, SingleChoiceDraft()));
        }

        // ---------------------------------------------------------------- retirement

        [Fact]
        [BusinessRule("BR-GLB-006")]
        public async Task A_retired_bank_takes_no_new_questions_and_keeps_the_ones_it_has()
        {
            using var db = CreateContext();
            var bank = await CreateAdmin(db).CreateBankAsync(_mathOfferingId, "بنك", "Bank");
            await CreateAdmin(db).AddQuestionAsync(bank.Id, SingleChoiceDraft());

            await CreateAdmin(db).RetireBankAsync(bank.Id);

            await Assert.ThrowsAsync<QuestionBankRetiredException>(
                () => CreateAdmin(db).AddQuestionAsync(bank.Id, SingleChoiceDraft()));

            // Still readable: its questions may sit on a paper already answered.
            Assert.Single(await CreateAdmin(db).QuestionsAsync(bank.Id));
            Assert.Single(await CreateAdmin(db).BanksAsync(_mathOfferingId, includeRetired: true));
            Assert.Empty(await CreateAdmin(db).BanksAsync(_mathOfferingId));
        }

        [Fact]
        [BusinessRule("BR-LRN-001")]
        public async Task Every_row_of_a_question_carries_the_acting_tenant()
        {
            using var db = CreateContext();
            var bank = await CreateAdmin(db).CreateBankAsync(_mathOfferingId, "بنك", "Bank");
            var question = await CreateAdmin(db).AddQuestionAsync(bank.Id, SingleChoiceDraft());

            Assert.Equal(1, bank.SchoolId);
            Assert.Equal(1, question.SchoolId);
            Assert.All(await db.QuestionOptions.ToListAsync(), o => Assert.Equal(1, o.SchoolId));
        }

        // ---------------------------------------------------------------- helpers

        private static QuestionDraft SingleChoiceDraft() => new()
        {
            Type = QuestionType.SingleChoice,
            StemAr = "سؤال",
            StemEn = "Question",
            Marks = 2m,
            Difficulty = QuestionDifficulty.Medium,
            Options = new[]
            {
                new QuestionDraftOption("أ", "A", true),
                new QuestionDraftOption("ب", "B", false),
                new QuestionDraftOption("ج", "C", false),
            },
        };

        private static CurriculumOffering NewOffering(int yearId, int profileId, int subjectId) => new()
        {
            SchoolId = 1, AcademicYearId = yearId, GradeYearProfileId = profileId, SubjectId = subjectId,
            WeeklyPeriods = 5, IsAssessable = true, GpaWeight = 1m, EffectiveFromUtc = new DateTime(2026, 9, 1),
        };

        /// <summary>A second teacher on the same offering — the colleague sharing is measured against.</summary>
        private void AddPlacementFor(AppDbContext db, int userAccountId)
        {
            var employee = new Employee
            {
                SchoolId = 1, EmployeeNo = $"EMP-{userAccountId}", UserAccountId = userAccountId,
                FirstNameAr = "زميل", FatherNameAr = "أب", GrandfatherNameAr = "جد", FamilyNameAr = "عائلة",
                FirstNameEn = "Colleague", FatherNameEn = "Father", GrandfatherNameEn = "Grandfather", FamilyNameEn = "Family",
                Gender = Gender.Male, DateOfBirth = new DateTime(1990, 1, 1), NationalityLookupId = 1,
            };
            db.Employees.Add(employee);
            db.SaveChanges();

            var profile = new TeacherProfile { SchoolId = 1, EmployeeId = employee.Id, MaxWeeklyPeriods = 24 };
            db.TeacherProfiles.Add(profile);
            db.SaveChanges();

            var existing = db.Placements.AsNoTracking().First();
            db.Placements.Add(new Placement
            {
                SchoolId = 1, TimetableVersionId = existing.TimetableVersionId, SectionId = existing.SectionId,
                PeriodSlotId = existing.PeriodSlotId, CurriculumOfferingId = existing.CurriculumOfferingId,
                TeacherProfileId = profile.Id,
            });
            db.SaveChanges();
        }

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private QuestionBankAdmin CreateAdmin(AppDbContext db)
            => new(db, _user, new HomeworkAdmin(db, _clock, _user, _setup));
    }
}
