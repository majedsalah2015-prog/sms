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
using Sms.Domain.Grading;
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
    /// doc/Modules/37 §8.7 (BR-LRN-007/008) over a real Sqlite-backed
    /// AppDbContext. The reconciliation tests are the point: BR-LRN-008 refuses a
    /// paper that does not match its Module 17 component, and the refusal has to
    /// carry both numbers.
    /// </summary>
    public sealed class OnlinePaperAdminTests : IDisposable
    {
        private const int TeacherUserId = 500;
        private const int HeadUserId = 600;

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
        private readonly int _componentId;
        private readonly int _lessonOneId;

        /// <summary>The component is worth 20. Every reconciliation test is measured against it.</summary>
        private const decimal ComponentMaxScore = 20m;

        public OnlinePaperAdminTests()
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
            db.Subjects.Add(math);
            db.SaveChanges();

            var offering = new CurriculumOffering
            {
                SchoolId = 1, AcademicYearId = year.Id, GradeYearProfileId = profile.Id, SubjectId = math.Id,
                WeeklyPeriods = 5, IsAssessable = true, GpaWeight = 1m, EffectiveFromUtc = new DateTime(2026, 9, 1),
            };
            db.CurriculumOfferings.Add(offering);

            var section = new Section { SchoolId = 1, AcademicYearId = year.Id, GradeYearProfileId = profile.Id, NameAr = "ثالث-أ", NameEn = "3-A", Capacity = 25, GenderPolicy = GenderPolicy.Mixed };
            db.Sections.Add(section);
            db.SaveChanges();

            var scale = new GradingScale { SchoolId = 1, AcademicYearId = year.Id, StageId = stage.Id, NameAr = "مئوي", NameEn = "Percentage" };
            db.GradingScales.Add(scale);
            db.SaveChanges();

            var blueprint = new Blueprint
            {
                SchoolId = 1, AcademicYearId = year.Id, CurriculumOfferingId = offering.Id,
                TermId = 1, GradingScaleId = scale.Id, IsLocked = true,
            };
            db.Blueprints.Add(blueprint);
            db.SaveChanges();

            var component = new BlueprintComponent
            {
                SchoolId = 1, BlueprintId = blueprint.Id, NameAr = "اختبار قصير", NameEn = "Quiz",
                Weight = 20m, MaxScore = ComponentMaxScore,
            };
            db.BlueprintComponents.Add(component);

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

            db.Placements.Add(new Placement
            {
                SchoolId = 1, TimetableVersionId = published.Id, SectionId = section.Id,
                PeriodSlotId = slot.Id, CurriculumOfferingId = offering.Id, TeacherProfileId = teacher.Id,
            });

            var lesson = new Lesson
            {
                SchoolId = 1, AcademicYearId = year.Id, CurriculumOfferingId = offering.Id,
                WeekNumber = 1, TitleAr = "الدرس الأول", TitleEn = "Lesson one",
            };
            db.Lessons.Add(lesson);
            db.SaveChanges();

            _mathOfferingId = offering.Id;
            _componentId = component.Id;
            _lessonOneId = lesson.Id;
        }

        public void Dispose() => _connection.Dispose();

        // ---------------------------------------------------------------- building

        [Fact]
        [BusinessRule("BR-LRN-008")]
        public async Task A_new_paper_names_the_component_it_will_fill()
        {
            using var db = CreateContext();
            var (_, paper) = await NewPaperAsync(db);

            Assert.Equal(_componentId, paper.BlueprintComponentId);
            Assert.Equal(OnlinePaperStatus.Draft, paper.Status);

            var reconciliation = await CreatePapers(db).ReconciliationAsync(paper.Id);
            Assert.Equal(0, reconciliation.ItemCount);
            Assert.Equal(ComponentMaxScore, reconciliation.ComponentMaxScore);
            Assert.False(reconciliation.Reconciles);
        }

        [Fact]
        [BusinessRule("BR-LRN-008")]
        public async Task A_question_brings_its_own_marks_unless_the_paper_says_otherwise()
        {
            using var db = CreateContext();
            var (bank, paper) = await NewPaperAsync(db);
            var question = await AddQuestionAsync(db, bank.Id, marks: 4m);

            var byDefault = await CreatePapers(db).AddItemAsync(paper.Id, question.Id);
            Assert.Equal(4m, byDefault.Marks);

            var second = await AddQuestionAsync(db, bank.Id, marks: 4m);
            var overridden = await CreatePapers(db).AddItemAsync(paper.Id, second.Id, marks: 6m);
            Assert.Equal(6m, overridden.Marks);
        }

        [Fact]
        [BusinessRule("BR-LRN-001")]
        public async Task A_question_from_another_bank_cannot_be_put_on_this_paper()
        {
            using var db = CreateContext();
            var (_, paper) = await NewPaperAsync(db);

            var otherBank = await CreateBanks(db).CreateBankAsync(_mathOfferingId, "بنك آخر", "Other bank");
            var stranger = await AddQuestionAsync(db, otherBank.Id, marks: 5m);

            await Assert.ThrowsAsync<QuestionNotInBankException>(
                () => CreatePapers(db).AddItemAsync(paper.Id, stranger.Id));
        }

        [Fact]
        [BusinessRule("BR-LRN-008")]
        public async Task The_same_question_cannot_be_asked_twice_on_one_paper()
        {
            using var db = CreateContext();
            var (bank, paper) = await NewPaperAsync(db);
            var question = await AddQuestionAsync(db, bank.Id, marks: 5m);

            await CreatePapers(db).AddItemAsync(paper.Id, question.Id);

            // Bypasses the service: a student asked the same question twice would
            // be marked for it twice, and that must be the database's answer.
            db.PaperItems.Add(new PaperItem
            {
                SchoolId = 1, AcademicYearId = paper.AcademicYearId, OnlinePaperId = paper.Id,
                QuestionId = question.Id, DisplayOrder = 99, Marks = 5m,
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        // ---------------------------------------------------------------- the freeze

        [Fact]
        [BusinessRule("BR-LRN-007")]
        public async Task A_paper_item_pins_the_question_version_it_was_added_from()
        {
            using var db = CreateContext();
            var (bank, paper) = await NewPaperAsync(db);
            var first = await AddQuestionAsync(db, bank.Id, marks: 20m, stemEn: "Original wording");

            await CreatePapers(db).AddItemAsync(paper.Id, first.Id);

            // The author reworks the question in the bank afterwards.
            var revised = DraftFor(20m);
            revised.StemEn = "Reworded";
            revised.StemAr = "صياغة جديدة";
            await CreateBanks(db).ReviseQuestionAsync(first.Id, revised);

            var items = await CreatePapers(db).ItemsAsync(paper.Id);

            // The paper still carries the wording it was built from. This is the
            // whole of BR-LRN-007: not a rule the code remembers, but a foreign
            // key pointing at a row nobody edited.
            Assert.Equal("Original wording", Assert.Single(items).Question.StemEn);
            Assert.Equal(1, items[0].Question.Version);
        }

        // ---------------------------------------------------------------- reconciliation

        [Fact]
        [BusinessRule("BR-LRN-008")]
        public async Task A_paper_short_of_its_component_is_refused_and_the_refusal_carries_both_numbers()
        {
            using var db = CreateContext();
            var (bank, paper) = await NewPaperAsync(db);
            await CreatePapers(db).AddItemAsync(paper.Id, (await AddQuestionAsync(db, bank.Id, marks: 15m)).Id);

            var ex = await Assert.ThrowsAsync<PaperRefusedException>(
                () => CreatePapers(db).SubmitForApprovalAsync(paper.Id));

            Assert.Equal(PaperRefusal.MarksDoNotReconcile, ex.Refusal);
            Assert.Equal(15m, ex.PaperTotalMarks);
            Assert.Equal(ComponentMaxScore, ex.ComponentMaxScore);
        }

        [Fact]
        [BusinessRule("BR-LRN-008")]
        public async Task A_paper_over_its_component_is_refused_too()
        {
            using var db = CreateContext();
            var (bank, paper) = await NewPaperAsync(db);
            await CreatePapers(db).AddItemAsync(paper.Id, (await AddQuestionAsync(db, bank.Id, marks: 25m)).Id);

            var ex = await Assert.ThrowsAsync<PaperRefusedException>(
                () => CreatePapers(db).SubmitForApprovalAsync(paper.Id));

            Assert.Equal(PaperRefusal.MarksDoNotReconcile, ex.Refusal);
            Assert.Equal(25m, ex.PaperTotalMarks);
        }

        [Fact]
        [BusinessRule("BR-LRN-008")]
        public async Task An_empty_paper_is_refused()
        {
            using var db = CreateContext();
            var (_, paper) = await NewPaperAsync(db);

            var ex = await Assert.ThrowsAsync<PaperRefusedException>(
                () => CreatePapers(db).SubmitForApprovalAsync(paper.Id));

            Assert.Equal(PaperRefusal.NoItems, ex.Refusal);
        }

        [Fact]
        [BusinessRule("BR-LRN-008")]
        public async Task A_paper_that_adds_up_goes_to_the_head_of_department()
        {
            using var db = CreateContext();
            var paper = await ReconciledPaperAsync(db);

            await CreatePapers(db).SubmitForApprovalAsync(paper.Id);

            var stored = await db.OnlinePapers.AsNoTracking().SingleAsync(p => p.Id == paper.Id);
            Assert.Equal(OnlinePaperStatus.PendingApproval, stored.Status);
        }

        [Fact]
        [BusinessRule("BR-LRN-007")]
        public async Task A_question_withdrawn_after_it_was_added_blocks_the_paper()
        {
            using var db = CreateContext();
            var (bank, paper) = await NewPaperAsync(db);
            var question = await AddQuestionAsync(db, bank.Id, marks: ComponentMaxScore);
            await CreatePapers(db).AddItemAsync(paper.Id, question.Id);

            await CreateBanks(db).DeprecateQuestionAsync(question.Id, "الصياغة مضلِّلة");

            var ex = await Assert.ThrowsAsync<PaperRefusedException>(
                () => CreatePapers(db).SubmitForApprovalAsync(paper.Id));

            Assert.Equal(PaperRefusal.ContainsWithdrawnQuestion, ex.Refusal);
            Assert.Equal(1, ex.WithdrawnQuestionCount);
        }

        // ---------------------------------------------------------------- approval

        [Fact]
        [BusinessRule("BR-LRN-008")]
        public async Task Approval_stamps_who_signed_it_and_when()
        {
            using var db = CreateContext();
            var paper = await ReconciledPaperAsync(db);
            await CreatePapers(db).SubmitForApprovalAsync(paper.Id);

            _user.UserId = HeadUserId;
            await CreatePapers(db).ApproveAsync(paper.Id, hasSchoolWideReach: true);

            var stored = await db.OnlinePapers.AsNoTracking().SingleAsync(p => p.Id == paper.Id);
            Assert.Equal(OnlinePaperStatus.Approved, stored.Status);
            Assert.Equal(HeadUserId, stored.ApprovedByUserId);
            Assert.Equal(_clock.UtcNow, stored.ApprovedAtUtc);
        }

        [Fact]
        [BusinessRule("BR-LRN-008")]
        public async Task A_draft_cannot_be_approved_without_being_sent_first()
        {
            using var db = CreateContext();
            var paper = await ReconciledPaperAsync(db);

            // A lifecycle answer rather than an arithmetic one: the paper adds up
            // perfectly and still cannot be approved, because nobody sent it.
            var ex = await Assert.ThrowsAsync<OnlinePaperTransitionException>(
                () => CreatePapers(db).ApproveAsync(paper.Id));

            Assert.Equal(OnlinePaperStatus.Draft, ex.From);
            Assert.Equal(OnlinePaperStatus.Approved, ex.To);
        }

        [Fact]
        [BusinessRule("BR-LRN-008")]
        public async Task Approval_re_checks_the_arithmetic_rather_than_trusting_the_submission()
        {
            using var db = CreateContext();
            var (bank, paper) = await NewPaperAsync(db);
            var question = await AddQuestionAsync(db, bank.Id, marks: ComponentMaxScore);
            await CreatePapers(db).AddItemAsync(paper.Id, question.Id);
            await CreatePapers(db).SubmitForApprovalAsync(paper.Id);

            // Withdrawn from the bank while it sat in the head of department's
            // queue. The approval is the signature that matters, so it is the
            // moment that must be checked.
            await CreateBanks(db).DeprecateQuestionAsync(question.Id, "خطأ في الصياغة");

            var ex = await Assert.ThrowsAsync<PaperRefusedException>(
                () => CreatePapers(db).ApproveAsync(paper.Id));

            Assert.Equal(PaperRefusal.ContainsWithdrawnQuestion, ex.Refusal);
        }

        [Fact]
        [BusinessRule("BR-LRN-008")]
        public async Task An_approved_papers_questions_are_frozen()
        {
            using var db = CreateContext();
            var (bank, paper) = await NewPaperAsync(db);
            await CreatePapers(db).AddItemAsync(paper.Id, (await AddQuestionAsync(db, bank.Id, marks: ComponentMaxScore)).Id);
            await CreatePapers(db).SubmitForApprovalAsync(paper.Id);
            await CreatePapers(db).ApproveAsync(paper.Id);

            var extra = await AddQuestionAsync(db, bank.Id, marks: 1m);

            var ex = await Assert.ThrowsAsync<PaperNotEditableException>(
                () => CreatePapers(db).AddItemAsync(paper.Id, extra.Id));

            Assert.Equal(OnlinePaperStatus.Approved, ex.Status);
        }

        [Fact]
        [BusinessRule("BR-LRN-008")]
        public async Task A_rejected_paper_goes_back_to_being_a_draft()
        {
            using var db = CreateContext();
            var paper = await ReconciledPaperAsync(db);
            await CreatePapers(db).SubmitForApprovalAsync(paper.Id);

            await CreatePapers(db).RejectAsync(paper.Id, "السؤال الثالث خارج المنهج");

            var stored = await db.OnlinePapers.AsNoTracking().SingleAsync(p => p.Id == paper.Id);
            Assert.Equal(OnlinePaperStatus.Draft, stored.Status);
        }

        [Fact]
        [BusinessRule("BR-LRN-008")]
        public async Task Handing_a_paper_back_says_what_needs_changing()
        {
            using var db = CreateContext();
            var paper = await ReconciledPaperAsync(db);
            await CreatePapers(db).SubmitForApprovalAsync(paper.Id);

            await Assert.ThrowsAsync<ArgumentException>(() => CreatePapers(db).RejectAsync(paper.Id, "  "));
        }

        [Fact]
        [BusinessRule("BR-LRN-016")]
        public async Task A_paper_is_withdrawn_with_a_reason_and_never_deleted()
        {
            using var db = CreateContext();
            var paper = await ReconciledPaperAsync(db);

            await CreatePapers(db).WithdrawAsync(paper.Id, "أُلغي الاختبار");

            var stored = await db.OnlinePapers.AsNoTracking().SingleAsync(p => p.Id == paper.Id);
            Assert.Equal(OnlinePaperStatus.Withdrawn, stored.Status);
            Assert.Equal("أُلغي الاختبار", stored.WithdrawnReason);

            await Assert.ThrowsAsync<OnlinePaperTransitionException>(
                () => CreatePapers(db).SubmitForApprovalAsync(paper.Id));
        }

        // ---------------------------------------------------------------- generation

        [Fact]
        [BusinessRule("BR-LRN-008")]
        public async Task Generation_respects_the_rule_it_was_given()
        {
            using var db = CreateContext();
            var (bank, paper) = await NewPaperAsync(db);

            await AddQuestionAsync(db, bank.Id, marks: 2m, difficulty: QuestionDifficulty.Easy, lessonId: _lessonOneId);
            await AddQuestionAsync(db, bank.Id, marks: 2m, difficulty: QuestionDifficulty.Easy, lessonId: _lessonOneId);
            await AddQuestionAsync(db, bank.Id, marks: 2m, difficulty: QuestionDifficulty.Hard, lessonId: _lessonOneId);
            await AddQuestionAsync(db, bank.Id, marks: 2m, difficulty: QuestionDifficulty.Easy);

            var added = await CreatePapers(db).GenerateAsync(
                paper.Id, 10, lessonId: _lessonOneId, difficulty: QuestionDifficulty.Easy);

            // Two match the lesson AND the difficulty; ten were asked for.
            Assert.Equal(2, added);
        }

        [Fact]
        [BusinessRule("BR-LRN-008")]
        public async Task Generation_says_how_many_it_actually_added()
        {
            using var db = CreateContext();
            var (bank, paper) = await NewPaperAsync(db);
            await AddQuestionAsync(db, bank.Id, marks: 2m);

            // Asking for five from a bank of one must not look like success.
            Assert.Equal(1, await CreatePapers(db).GenerateAsync(paper.Id, 5));
            Assert.Equal(0, await CreatePapers(db).GenerateAsync(paper.Id, 5));
        }

        [Fact]
        [BusinessRule("BR-LRN-007")]
        public async Task Generation_never_picks_a_withdrawn_question()
        {
            using var db = CreateContext();
            var (bank, paper) = await NewPaperAsync(db);
            var question = await AddQuestionAsync(db, bank.Id, marks: 2m);
            await CreateBanks(db).DeprecateQuestionAsync(question.Id, "غير صالح");

            Assert.Equal(0, await CreatePapers(db).GenerateAsync(paper.Id, 5));
        }

        [Fact]
        [BusinessRule("BR-LRN-008")]
        public async Task Generation_is_refused_once_the_paper_has_left_the_author()
        {
            using var db = CreateContext();
            var paper = await ReconciledPaperAsync(db);
            await CreatePapers(db).SubmitForApprovalAsync(paper.Id);

            await Assert.ThrowsAsync<PaperNotEditableException>(
                () => CreatePapers(db).GenerateAsync(paper.Id, 1));
        }

        // ---------------------------------------------------------------- helpers

        private async Task<(QuestionBank Bank, OnlinePaper Paper)> NewPaperAsync(AppDbContext db)
        {
            var bank = await CreateBanks(db).CreateBankAsync(_mathOfferingId, "بنك", "Bank");
            var paper = await CreatePapers(db).CreateAsync(bank.Id, _componentId, "ورقة", "Paper");
            return (bank, paper);
        }

        /// <summary>A paper carrying exactly what the component expects.</summary>
        private async Task<OnlinePaper> ReconciledPaperAsync(AppDbContext db)
        {
            var (bank, paper) = await NewPaperAsync(db);
            var question = await AddQuestionAsync(db, bank.Id, marks: ComponentMaxScore);
            await CreatePapers(db).AddItemAsync(paper.Id, question.Id);
            return paper;
        }

        private async Task<Question> AddQuestionAsync(
            AppDbContext db,
            int bankId,
            decimal marks,
            QuestionDifficulty difficulty = QuestionDifficulty.Medium,
            int? lessonId = null,
            string stemEn = "Question")
        {
            var draft = DraftFor(marks);
            draft.Difficulty = difficulty;
            draft.LessonId = lessonId;
            draft.StemEn = stemEn;

            return await CreateBanks(db).AddQuestionAsync(bankId, draft);
        }

        private static QuestionDraft DraftFor(decimal marks) => new()
        {
            Type = QuestionType.SingleChoice,
            StemAr = "سؤال",
            StemEn = "Question",
            Marks = marks,
            Options = new[]
            {
                new QuestionDraftOption("أ", "A", true),
                new QuestionDraftOption("ب", "B", false),
            },
        };

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private QuestionBankAdmin CreateBanks(AppDbContext db)
            => new(db, _user, new HomeworkAdmin(db, _clock, _user, _setup));

        private OnlinePaperAdmin CreatePapers(AppDbContext db)
            => new(db, _clock, _user, CreateBanks(db));
    }
}
