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
using Sms.Application.Notifications;
using Sms.Application.Setup;
using Sms.Domain.Attachments;
using Sms.Domain.Common;
using Sms.Domain.Employees;
using Sms.Domain.Grades;
using Sms.Domain.Grading;
using Sms.Domain.Learning;
using Sms.Domain.Parents;
using Sms.Domain.Schools;
using Sms.Domain.Sections;
using Sms.Domain.Security;
using Sms.Domain.Setup;
using Sms.Domain.Students;
using Sms.Domain.Subjects;
using Sms.Domain.Teachers;
using Sms.Domain.Timetable;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Grading;
using Sms.Infrastructure.Learning;
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// Module 37 slice 4 (doc/Modules/37 §8.4/§8.5/§8.10,
    /// BR-LRN-005/011/012/013) over a real Sqlite-backed AppDbContext — the
    /// homework loop from a student handing in to a raw mark landing in Module
    /// 17's marksheet.
    ///
    /// <para>
    /// The portal submitter is exercised here rather than in a file of its own
    /// because there is no other way to create a submission: handing work in is
    /// the only door, which is itself BR-LRN-013 holding.
    /// </para>
    /// </summary>
    public sealed class HomeworkSubmissionAdminTests : IDisposable
    {
        private const int TeacherUserId = 500;
        private const int HeadOfDepartmentUserId = 600;
        private const int StrangerUserId = 700;

        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
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

        /// <summary>Answers the one setting BR-GLB-052 needs and refuses the rest, exactly as <see cref="HomeworkAdminTests"/> does.</summary>
        private sealed class FixedSetup : ISystemSetupAdmin
        {
            public string? WorkingDays { get; set; } = "Sunday,Monday,Tuesday,Wednesday,Thursday";

            public Task<string?> GetSettingAsync(string key, int? academicYearId = null, CancellationToken cancellationToken = default)
                => Task.FromResult(key == SettingKeys.WorkingDays ? WorkingDays : null);

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

        private readonly int _yearId;
        private readonly int _mathOfferingId;
        private readonly int _artOfferingId;
        private readonly int _sectionAId;
        private readonly int _sectionBId;
        private readonly int _blueprintId;
        private readonly int _componentId;
        private readonly int _marksheetId;
        private readonly int _attachmentOneId;
        private readonly int _attachmentTwoId;

        // Three students in 3-A. Two hold portal accounts; the third holds none,
        // which is the ordinary case for a younger grade and must still appear on
        // the tracker.
        private readonly int _studentOneAccountId;
        private readonly int _studentTwoAccountId;
        private readonly int _parentAccountId;
        private readonly int _enrollmentOneId;
        private readonly int _enrollmentTwoId;
        private readonly int _enrollmentThreeId;

        // 2026-10-05 is a Monday, inside the year, and a working day.
        private static readonly DateTime GoodDueDate = new(2026, 10, 5);

        public HomeworkSubmissionAdminTests()
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

            var profile = new GradeYearProfile { GradeLevelId = grade.Id, AcademicYearId = year.Id, GenderPolicy = GenderPolicy.Mixed, TargetSections = 2, TargetSectionSize = 25 };
            db.GradeYearProfiles.Add(profile);
            db.SaveChanges();

            var artDepartment = new Department { SchoolId = 1, Name = new LocalizedName("الفنون", "Arts"), HeadTeacherUserId = HeadOfDepartmentUserId };
            db.Departments.Add(artDepartment);
            db.SaveChanges();

            var math = new Subject { SchoolId = 1, Code = "MATH", Name = new LocalizedName("رياضيات", "Math"), Category = "core" };
            var art = new Subject { SchoolId = 1, Code = "ART", Name = new LocalizedName("فنون", "Art"), Category = "core", DepartmentId = artDepartment.Id };
            db.Subjects.AddRange(math, art);
            db.SaveChanges();

            var mathOffering = NewOffering(year.Id, profile.Id, math.Id);
            var artOffering = NewOffering(year.Id, profile.Id, art.Id);
            db.CurriculumOfferings.AddRange(mathOffering, artOffering);

            var sectionA = NewSection(year.Id, profile.Id, "ثالث-أ", "3-A");
            var sectionB = NewSection(year.Id, profile.Id, "ثالث-ب", "3-B");
            db.Sections.AddRange(sectionA, sectionB);
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

            // The teacher holds Math in 3-A only — the pair BR-LRN-002 measures.
            db.Placements.AddRange(
                new Placement { SchoolId = 1, TimetableVersionId = published.Id, SectionId = sectionA.Id, PeriodSlotId = slot.Id, CurriculumOfferingId = mathOffering.Id, TeacherProfileId = teacher.Id },
                new Placement { SchoolId = 1, TimetableVersionId = published.Id, SectionId = sectionB.Id, PeriodSlotId = slot.Id, CurriculumOfferingId = mathOffering.Id, TeacherProfileId = teacher.Id + 99 },
                new Placement { SchoolId = 1, TimetableVersionId = published.Id, SectionId = sectionA.Id, PeriodSlotId = slot.Id, CurriculumOfferingId = artOffering.Id, TeacherProfileId = teacher.Id + 99 });
            db.SaveChanges();

            // Module 17: a blueprint component for the graded path, and the
            // marksheet BR-LRN-012's marks have to land in.
            var scale = new GradingScale { SchoolId = 1, AcademicYearId = year.Id, StageId = stage.Id, NameAr = "مئوي", NameEn = "Percentage" };
            db.GradingScales.Add(scale);
            db.SaveChanges();

            var blueprint = new Blueprint
            {
                SchoolId = 1, AcademicYearId = year.Id, CurriculumOfferingId = mathOffering.Id,
                TermId = 1, GradingScaleId = scale.Id, IsLocked = true,
            };
            db.Blueprints.Add(blueprint);
            db.SaveChanges();

            var component = new BlueprintComponent
            {
                SchoolId = 1, BlueprintId = blueprint.Id, NameAr = "واجبات", NameEn = "Homework",
                Weight = 20m, MaxScore = 20m,
            };
            db.BlueprintComponents.Add(component);
            db.SaveChanges();

            var marksheet = new Marksheet
            {
                SchoolId = 1, AcademicYearId = year.Id, BlueprintId = blueprint.Id,
                SectionId = sectionA.Id, Status = MarksheetStatus.Draft,
            };
            db.Marksheets.Add(marksheet);
            db.SaveChanges();

            var studentAccountOne = new UserAccount { UserName = "student1", AccountType = AccountType.Student };
            var studentAccountTwo = new UserAccount { UserName = "student2", AccountType = AccountType.Student };
            var parentAccount = new UserAccount { UserName = "parent1", AccountType = AccountType.Parent };
            db.UserAccounts.AddRange(studentAccountOne, studentAccountTwo, parentAccount);
            db.SaveChanges();

            var studentOne = NewStudent("STU-001", "أحمد", "Ahmed", studentAccountOne.Id);
            var studentTwo = NewStudent("STU-002", "بدر", "Badr", studentAccountTwo.Id);
            var studentThree = NewStudent("STU-003", "خالد", "Khalid", null);
            db.Students.AddRange(studentOne, studentTwo, studentThree);
            db.SaveChanges();

            var enrollmentOne = NewEnrollment(year.Id, profile.Id, studentOne.Id);
            var enrollmentTwo = NewEnrollment(year.Id, profile.Id, studentTwo.Id);
            var enrollmentThree = NewEnrollment(year.Id, profile.Id, studentThree.Id);
            db.Enrollments.AddRange(enrollmentOne, enrollmentTwo, enrollmentThree);
            db.SaveChanges();

            db.SectionMemberships.AddRange(
                NewMembership(year.Id, sectionA.Id, enrollmentOne.Id),
                NewMembership(year.Id, sectionA.Id, enrollmentTwo.Id),
                NewMembership(year.Id, sectionA.Id, enrollmentThree.Id));
            db.SaveChanges();

            // Module 17 pre-seeds one stub per section member x component; the
            // handoff writes into those and never creates rows of its own.
            foreach (var enrollmentId in new[] { enrollmentOne.Id, enrollmentTwo.Id, enrollmentThree.Id })
            {
                db.MarkEntries.Add(new MarkEntry
                {
                    SchoolId = 1, MarksheetId = marksheet.Id, BlueprintComponentId = component.Id, EnrollmentId = enrollmentId,
                });
            }

            // A real guardian, linked and portal-visible: the parent refused below
            // is refused for being a parent, not for being a stranger.
            var parent = new Parent { ParentFileNo = "PAR-000001", NameAr = "ولي أمر", NameEn = "Guardian", PrimaryMobile = "0500000000", UserAccountId = parentAccount.Id };
            db.Parents.Add(parent);
            db.SaveChanges();

            db.StudentGuardianLinks.Add(new StudentGuardianLink
            {
                StudentId = studentOne.Id, ParentId = parent.Id, RelationshipLookupId = 1,
                IsPrimaryContact = true, IsFinanciallyResponsible = true, IsPickupAuthorized = true, IsPortalVisible = true,
                EffectiveFromUtc = new DateTime(2026, 9, 1),
            });

            var documentType = new DocumentType { SchoolId = 1, Code = "HW-SUB", ModuleCode = "LRN", Name = new LocalizedName("تسليم واجب", "Homework submission") };
            db.DocumentTypes.Add(documentType);
            db.SaveChanges();

            var attachmentOne = NewAttachment(documentType.Id);
            var attachmentTwo = NewAttachment(documentType.Id);
            db.Attachments.AddRange(attachmentOne, attachmentTwo);
            db.SaveChanges();

            _yearId = year.Id;
            _mathOfferingId = mathOffering.Id;
            _artOfferingId = artOffering.Id;
            _sectionAId = sectionA.Id;
            _sectionBId = sectionB.Id;
            _blueprintId = blueprint.Id;
            _componentId = component.Id;
            _marksheetId = marksheet.Id;
            _attachmentOneId = attachmentOne.Id;
            _attachmentTwoId = attachmentTwo.Id;
            _studentOneAccountId = studentAccountOne.Id;
            _studentTwoAccountId = studentAccountTwo.Id;
            _parentAccountId = parentAccount.Id;
            _enrollmentOneId = enrollmentOne.Id;
            _enrollmentTwoId = enrollmentTwo.Id;
            _enrollmentThreeId = enrollmentThree.Id;
        }

        public void Dispose() => _connection.Dispose();

        // ---------------------------------------------------------------- fixture helpers

        private static CurriculumOffering NewOffering(int yearId, int profileId, int subjectId) => new()
        {
            SchoolId = 1, AcademicYearId = yearId, GradeYearProfileId = profileId, SubjectId = subjectId,
            WeeklyPeriods = 5, IsAssessable = true, GpaWeight = 1m, EffectiveFromUtc = new DateTime(2026, 9, 1),
        };

        private static Section NewSection(int yearId, int profileId, string nameAr, string nameEn) => new()
        {
            SchoolId = 1, AcademicYearId = yearId, GradeYearProfileId = profileId,
            NameAr = nameAr, NameEn = nameEn, Capacity = 25, GenderPolicy = GenderPolicy.Mixed,
        };

        private static Student NewStudent(string studentNo, string firstAr, string firstEn, int? userAccountId) => new()
        {
            SchoolId = 1, StudentNo = studentNo, UserAccountId = userAccountId,
            FirstNameAr = firstAr, FatherNameAr = "أب", GrandfatherNameAr = "جد", FamilyNameAr = "عائلة",
            FirstNameEn = firstEn, FatherNameEn = "Father", GrandfatherNameEn = "Grandfather", FamilyNameEn = "Family",
            Gender = Gender.Male, DateOfBirth = new DateTime(2018, 1, 1), NationalityLookupId = 1,
        };

        private static Enrollment NewEnrollment(int yearId, int profileId, int studentId) => new()
        {
            SchoolId = 1, AcademicYearId = yearId, StudentId = studentId, GradeYearProfileId = profileId,
            EnrollmentDate = new DateTime(2026, 9, 1), SourceType = EnrollmentSourceType.Admission,
        };

        private static SectionMembership NewMembership(int yearId, int sectionId, int enrollmentId) => new()
        {
            SchoolId = 1, AcademicYearId = yearId, SectionId = sectionId, EnrollmentId = enrollmentId,
            EffectiveFromUtc = new DateTime(2026, 9, 1),
        };

        private static Attachment NewAttachment(int documentTypeId) => new()
        {
            SchoolId = 1, DocumentTypeId = documentTypeId, OwningEntityType = "HomeworkSubmission", OwningEntityId = 0,
            Status = AttachmentStatus.PendingScan, CurrentVersionNumber = 1,
        };

        // ---------------------------------------------------------------- §8.4 the chase

        [Fact]
        [BusinessRule("BR-LRN-005")]
        public async Task Chasing_reaches_the_student_and_their_family()
        {
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db);

            var chased = await CreateAdmin(db).ChaseAsync(homework.Id, new[] { _enrollmentOneId });

            Assert.Equal(1, chased);

            var (code, recipients, payload) = Assert.Single(_published.Published);
            Assert.Equal("HomeworkOverdue", code);

            // The student's own portal account AND the guardian's: a family is
            // told the work is missing, and the student is told directly when
            // they are old enough to hold an account.
            Assert.Contains(recipients, r => r.UserId == _studentOneAccountId);
            Assert.Contains(recipients, r => r.UserId == _parentAccountId);
            Assert.Equal(homework.TitleAr, payload["Homework"]);
        }

        [Fact]
        [BusinessRule("BR-LRN-005")]
        public async Task A_student_who_has_since_handed_in_is_not_chased()
        {
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db);

            // The roster on the teacher's screen said "missing"; the work landed
            // between rendering it and pressing the button.
            await CreateSubmitter(db).SubmitAsync(_studentOneAccountId, homework.Id, "وصلت متأخراً");
            _published.Published.Clear();

            var chased = await CreateAdmin(db).ChaseAsync(homework.Id, new[] { _enrollmentOneId });

            Assert.Equal(0, chased);
            Assert.Empty(_published.Published);
        }

        [Fact]
        [BusinessRule("BR-LRN-002")]
        public async Task A_teacher_without_reach_cannot_chase_another_classs_students()
        {
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db);

            _user.UserId = StrangerUserId;

            await Assert.ThrowsAsync<TeachingReachException>(
                () => CreateAdmin(db).ChaseAsync(homework.Id, new[] { _enrollmentOneId }));
            Assert.Empty(_published.Published);
        }

        [Fact]
        [BusinessRule("BR-LRN-002")]
        public async Task An_enrolment_from_outside_the_homeworks_own_class_is_ignored()
        {
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db);

            // A hand-edited form naming somebody who is not on this roster. The
            // request is not refused — it simply reaches nobody, because the
            // roster, not the form, decides who exists here.
            var chased = await CreateAdmin(db).ChaseAsync(homework.Id, new[] { _enrollmentOneId + 9999 });

            Assert.Equal(0, chased);
            Assert.Empty(_published.Published);
        }

        [Fact]
        [BusinessRule("BR-LRN-012")]
        public async Task Releasing_tells_the_families_whose_work_was_marked_and_nobody_else()
        {
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db);

            var submission = await CreateSubmitter(db).SubmitAsync(_studentOneAccountId, homework.Id, "إجابتي");
            var admin = CreateAdmin(db);
            await admin.ScoreAsync(submission.Id, 18m, "أحسنت");
            await admin.BeginMarkingAsync(homework.Id);
            _published.Published.Clear();

            await admin.ReleaseAsync(homework.Id);

            var released = Assert.Single(_published.Published, p => p.EventCode == "MarkReleased");

            // Student two handed nothing in, so no mark of theirs was released and
            // their family is told nothing — BR-LRN-012 left that row to Module 17
            // rather than posting a zero from here.
            Assert.Contains(released.Recipients, r => r.UserId == _studentOneAccountId);
            Assert.DoesNotContain(released.Recipients, r => r.UserId == _studentTwoAccountId);
        }

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private HomeworkAdmin CreateDesk(AppDbContext db) => new(db, _clock, _user, _setup);

        private HomeworkSubmissionAdmin CreateAdmin(AppDbContext db)
            => new(db, _clock, _user, CreateDesk(db), new GradingAdmin(db, _clock, _audit), _published);

        private PortalHomeworkSubmitter CreateSubmitter(AppDbContext db) => new(db, _clock, _published);

        private readonly RecordingPublisher _published = new();

        /// <summary>
        /// Module 33's publisher, recorded rather than run. What matters to module
        /// 37 is which event was raised and to whom — the delivery machinery is
        /// Module 33's own concern and has its own tests. Recording it here is
        /// what lets a test assert that a chase reached a family, and that a
        /// student who had already handed in was not among them.
        /// </summary>
        private sealed class RecordingPublisher : INotificationPublisher
        {
            public List<(string EventCode, IReadOnlyCollection<NotificationRecipient> Recipients, IReadOnlyDictionary<string, string> Payload)> Published { get; }
                = new();

            public Task PublishAsync(
                string eventCode,
                IReadOnlyCollection<NotificationRecipient> recipients,
                IReadOnlyDictionary<string, string> payload,
                CancellationToken cancellationToken = default)
            {
                Published.Add((eventCode, recipients, payload));
                return Task.CompletedTask;
            }
        }

        /// <summary>An issued Math homework for 3-A, graded out of 20 against the Module 17 component unless told otherwise.</summary>
        private async Task<Homework> IssueMathHomeworkAsync(
            AppDbContext db,
            decimal? maxMarks = 20m,
            bool nameComponent = true,
            LatenessPolicy latenessPolicy = LatenessPolicy.AcceptWithoutPenalty,
            decimal? latePenaltyPercent = null)
        {
            var previousUser = _user.UserId;
            _user.UserId = TeacherUserId;
            try
            {
                var desk = CreateDesk(db);
                var homework = await desk.CreateAsync(
                    _mathOfferingId, _sectionAId, "واجب الكسور", "Fractions homework", GoodDueDate,
                    maxMarks: maxMarks,
                    blueprintComponentId: nameComponent && maxMarks is > 0m ? _componentId : null,
                    latenessPolicy: latenessPolicy,
                    latePenaltyPercent: latePenaltyPercent);

                await desk.IssueAsync(homework.Id);
                return homework;
            }
            finally
            {
                _user.UserId = previousUser;
            }
        }

        // ---------------------------------------------------------------- §8.4 the tracker

        [Fact]
        [BusinessRule("BR-LRN-005")]
        public async Task The_tracker_lists_every_student_in_the_section_including_the_ones_who_handed_nothing_in()
        {
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db);
            await CreateSubmitter(db).SubmitAsync(_studentOneAccountId, homework.Id, "٢/٤ = ١/٢");

            var roster = await CreateAdmin(db).RosterAsync(homework.Id);

            Assert.Equal(3, roster.Count);
            Assert.Equal(new[] { "STU-001", "STU-002", "STU-003" }, roster.Select(r => r.StudentNo));

            var handedIn = roster.Single(r => r.StudentNo == "STU-001");
            Assert.True(handedIn.HasSubmitted);
            Assert.Equal(_enrollmentOneId, handedIn.EnrollmentId);
            Assert.Equal(1, handedIn.VersionCount);
            Assert.Equal(SubmissionStatus.Submitted, handedIn.Status);

            // "Missing" is the absence of a row, not a status - the students the
            // teacher opened this screen to find.
            var missing = roster.Where(r => !r.HasSubmitted).ToList();
            Assert.Equal(2, missing.Count);
            Assert.All(missing, r => Assert.Null(r.Status));
            Assert.All(missing, r => Assert.False(r.IsLate));
        }

        [Fact]
        [BusinessRule("BR-LRN-005")]
        public async Task The_tracker_carries_the_name_in_both_languages()
        {
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db);

            var row = (await CreateAdmin(db).RosterAsync(homework.Id)).First();

            Assert.Equal("أحمد أب عائلة", row.StudentNameAr);
            Assert.Equal("Ahmed Father Family", row.StudentNameEn);
        }

        [Fact]
        [BusinessRule("BR-LRN-002")]
        public async Task A_teacher_without_reach_cannot_open_another_classs_tracker()
        {
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db);

            _user.UserId = StrangerUserId;

            await Assert.ThrowsAsync<TeachingReachException>(() => CreateAdmin(db).RosterAsync(homework.Id));
        }

        [Fact]
        [BusinessRule("BR-LRN-002")]
        public async Task School_wide_reach_opens_any_classs_tracker()
        {
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db);

            _user.UserId = StrangerUserId;
            var roster = await CreateAdmin(db).RosterAsync(homework.Id, hasSchoolWideReach: true);

            Assert.Equal(3, roster.Count);
        }

        // ---------------------------------------------------------------- BR-LRN-005 one live row

        [Fact]
        [BusinessRule("BR-LRN-005")]
        public async Task Two_live_submissions_for_one_student_are_refused_by_the_database()
        {
            // Deliberately bypasses the service: "one live submission per student
            // per homework" is only a guarantee if the database holds it, and a
            // service check proves nothing about two concurrent requests.
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db);

            db.HomeworkSubmissions.Add(NewRawSubmission(homework.Id, _enrollmentOneId));
            await db.SaveChangesAsync();

            db.HomeworkSubmissions.Add(NewRawSubmission(homework.Id, _enrollmentOneId));

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        [Fact]
        [BusinessRule("BR-LRN-005")]
        public async Task Two_students_may_each_hold_a_live_submission_for_the_same_homework()
        {
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db);

            db.HomeworkSubmissions.Add(NewRawSubmission(homework.Id, _enrollmentOneId));
            db.HomeworkSubmissions.Add(NewRawSubmission(homework.Id, _enrollmentTwoId));

            await db.SaveChangesAsync();

            Assert.Equal(2, await db.HomeworkSubmissions.CountAsync(s => s.HomeworkId == homework.Id));
        }

        private HomeworkSubmission NewRawSubmission(int homeworkId, int enrollmentId) => new()
        {
            SchoolId = 1, AcademicYearId = _yearId, HomeworkId = homeworkId, EnrollmentId = enrollmentId,
            SubmittedAtUtc = _clock.UtcNow, Status = SubmissionStatus.Submitted, VersionCount = 1,
        };

        // ---------------------------------------------------------------- BR-LRN-013 the portal write

        [Fact]
        [BusinessRule("BR-LRN-013")]
        public async Task A_student_hands_in_their_own_work()
        {
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db);

            var submission = await CreateSubmitter(db).SubmitAsync(_studentOneAccountId, homework.Id, "إجابتي");

            Assert.Equal(_enrollmentOneId, submission.EnrollmentId);
            Assert.Equal(SubmissionStatus.Submitted, submission.Status);
            Assert.Equal(_clock.UtcNow, submission.SubmittedAtUtc);
            Assert.Equal(1, submission.VersionCount);
            Assert.False(submission.IsLate);

            var version = await db.SubmissionVersions.AsNoTracking().SingleAsync(v => v.HomeworkSubmissionId == submission.Id);
            Assert.Equal(1, version.VersionNumber);
            Assert.Equal("إجابتي", version.TextResponse);
            Assert.Equal(_yearId, version.AcademicYearId);
            Assert.Equal(1, version.SchoolId);
        }

        [Fact]
        [BusinessRule("BR-LRN-013")]
        public async Task A_parent_account_is_refused_even_for_their_own_child()
        {
            // The parent is a real, linked, portal-visible guardian of STU-001 -
            // they may read everything about this homework and hand in none of it.
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db);

            var ex = await Assert.ThrowsAsync<PortalSubmissionIdentityException>(
                () => CreateSubmitter(db).SubmitAsync(_parentAccountId, homework.Id, "سأسلم عن ابني"));

            Assert.Equal(_parentAccountId, ex.RequestingUserAccountId);
            Assert.Empty(await db.HomeworkSubmissions.ToListAsync());
        }

        [Fact]
        [BusinessRule("BR-LRN-013")]
        public async Task A_staff_account_cannot_hand_in_a_students_work()
        {
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db);

            await Assert.ThrowsAsync<PortalSubmissionIdentityException>(
                () => CreateSubmitter(db).SubmitAsync(TeacherUserId, homework.Id, "نيابة عن الطالب"));
        }

        [Fact]
        [BusinessRule("BR-LRN-013")]
        public async Task Work_set_to_another_section_is_not_this_students_to_hand_in()
        {
            using var db = CreateContext();

            _user.UserId = HeadOfDepartmentUserId;
            var desk = CreateDesk(db);
            var elsewhere = await desk.CreateAsync(_artOfferingId, _sectionBId, "واجب فني", "Art homework", GoodDueDate);
            await desk.IssueAsync(elsewhere.Id);
            _user.UserId = TeacherUserId;

            await Assert.ThrowsAsync<HomeworkNotOfferedToStudentException>(
                () => CreateSubmitter(db).SubmitAsync(_studentOneAccountId, elsewhere.Id, "إجابتي"));
        }

        [Fact]
        [BusinessRule("BR-LRN-003")]
        public async Task A_draft_is_invisible_in_the_portal_and_cannot_be_submitted_to()
        {
            using var db = CreateContext();
            var draft = await CreateDesk(db).CreateAsync(
                _mathOfferingId, _sectionAId, "مسودة", "Draft", GoodDueDate);

            await Assert.ThrowsAsync<HomeworkNotOfferedToStudentException>(
                () => CreateSubmitter(db).SubmitAsync(_studentOneAccountId, draft.Id, "إجابتي"));
        }

        [Fact]
        [BusinessRule("BR-SEC-010")]
        public async Task An_unknown_homework_answers_exactly_as_one_that_is_not_yours()
        {
            // Otherwise the difference between the two answers is a way to
            // enumerate the school's homework from a student's account.
            using var db = CreateContext();

            await Assert.ThrowsAsync<HomeworkNotOfferedToStudentException>(
                () => CreateSubmitter(db).SubmitAsync(_studentOneAccountId, 987654, "إجابتي"));
        }

        [Fact]
        [BusinessRule("BR-LRN-003")]
        public async Task The_first_hand_in_moves_the_homework_from_issued_to_collecting()
        {
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db);

            await CreateSubmitter(db).SubmitAsync(_studentOneAccountId, homework.Id, "إجابتي");

            var reloaded = await db.Homeworks.AsNoTracking().SingleAsync(h => h.Id == homework.Id);
            Assert.Equal(HomeworkStatus.Collecting, reloaded.Status);
        }

        // ---------------------------------------------------------------- BR-LRN-005 lateness

        [Fact]
        [BusinessRule("BR-LRN-005")]
        public async Task Late_work_is_accepted_and_flagged_never_refused()
        {
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db, latenessPolicy: LatenessPolicy.AcceptWithPenalty, latePenaltyPercent: 25m);

            _clock.UtcNow = new DateTime(2026, 10, 8, 19, 0, 0, DateTimeKind.Utc);
            var submission = await CreateSubmitter(db).SubmitAsync(_studentOneAccountId, homework.Id, "متأخر");

            Assert.True(submission.IsLate);
            Assert.Equal(SubmissionStatus.Submitted, submission.Status);

            // Nothing was deducted at submit — BR-LRN-005 puts the penalty at
            // marking, and there is no mark yet to reduce.
            Assert.Null(submission.Score);
        }

        [Fact]
        [BusinessRule("BR-LRN-005")]
        public async Task Work_handed_in_on_the_due_day_itself_is_not_late()
        {
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db);

            _clock.UtcNow = new DateTime(2026, 10, 5, 23, 30, 0, DateTimeKind.Utc);
            var submission = await CreateSubmitter(db).SubmitAsync(_studentOneAccountId, homework.Id, "في الموعد");

            Assert.False(submission.IsLate);
        }

        // ---------------------------------------------------------------- BR-LRN-005 supersede + retain

        [Fact]
        [BusinessRule("BR-LRN-005")]
        public async Task A_resubmission_supersedes_the_live_row_and_keeps_the_prior_version()
        {
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db);
            var submitter = CreateSubmitter(db);

            await submitter.SubmitAsync(_studentOneAccountId, homework.Id, "المحاولة الأولى");

            _clock.UtcNow = new DateTime(2026, 10, 2, 20, 0, 0, DateTimeKind.Utc);
            var second = await submitter.SubmitAsync(_studentOneAccountId, homework.Id, "المحاولة الثانية");

            // One live row (BR-LRN-005), moved to the latest hand-in.
            var live = await db.HomeworkSubmissions.AsNoTracking().SingleAsync(s => s.HomeworkId == homework.Id);
            Assert.Equal(second.Id, live.Id);
            Assert.Equal(2, live.VersionCount);
            Assert.Equal(new DateTime(2026, 10, 2, 20, 0, 0, DateTimeKind.Utc), live.SubmittedAtUtc);

            // And the earlier hand-in is retained, verbatim, underneath it.
            var versions = await db.SubmissionVersions.AsNoTracking()
                .Where(v => v.HomeworkSubmissionId == live.Id).OrderBy(v => v.VersionNumber).ToListAsync();
            Assert.Equal(2, versions.Count);
            Assert.Equal("المحاولة الأولى", versions[0].TextResponse);
            Assert.Equal(new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc), versions[0].SubmittedAtUtc);
            Assert.Equal("المحاولة الثانية", versions[1].TextResponse);
        }

        [Fact]
        [BusinessRule("BR-LRN-005")]
        public async Task A_resubmission_clears_a_mark_already_entered()
        {
            // The mark described work that has just been replaced. Carrying it
            // forward would release to Module 17 a mark for something the teacher
            // never saw.
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db);
            var submitter = CreateSubmitter(db);

            var first = await submitter.SubmitAsync(_studentOneAccountId, homework.Id, "المحاولة الأولى");
            await CreateAdmin(db).ScoreAsync(first.Id, 15m, "جيد");

            await submitter.SubmitAsync(_studentOneAccountId, homework.Id, "المحاولة الثانية");

            var live = await db.HomeworkSubmissions.AsNoTracking().SingleAsync(s => s.Id == first.Id);
            Assert.Null(live.Score);
            Assert.Null(live.MarkedAtUtc);
            Assert.Equal(SubmissionStatus.Submitted, live.Status);

            // The teacher's words survive: they are still what they said, and they
            // will overwrite them when they re-mark.
            Assert.Equal("جيد", live.Feedback);
        }

        [Fact]
        [BusinessRule("BR-LRN-005")]
        public async Task A_superseded_version_keeps_the_files_that_were_handed_in_with_it()
        {
            // The §7 deviation earning its place: attachments hang off the
            // version, so a resubmission cannot orphan or re-attribute them.
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db);
            var submitter = CreateSubmitter(db);

            var submission = await submitter.SubmitAsync(
                _studentOneAccountId, homework.Id, "صور العمل", new[] { _attachmentOneId, _attachmentTwoId });

            _clock.UtcNow = new DateTime(2026, 10, 2, 20, 0, 0, DateTimeKind.Utc);
            await submitter.SubmitAsync(_studentOneAccountId, homework.Id, "نص فقط");

            var versions = await db.SubmissionVersions.AsNoTracking()
                .Where(v => v.HomeworkSubmissionId == submission.Id).OrderBy(v => v.VersionNumber).ToListAsync();
            var firstVersionFiles = await db.SubmissionAttachments.AsNoTracking()
                .Where(a => a.SubmissionVersionId == versions[0].Id).Select(a => a.AttachmentId).ToListAsync();
            var secondVersionFiles = await db.SubmissionAttachments.AsNoTracking()
                .Where(a => a.SubmissionVersionId == versions[1].Id).ToListAsync();

            Assert.Equal(2, firstVersionFiles.Count);
            Assert.Contains(_attachmentOneId, firstVersionFiles);
            Assert.Contains(_attachmentTwoId, firstVersionFiles);
            Assert.Empty(secondVersionFiles);
        }

        [Fact]
        [BusinessRule("BR-LRN-006")]
        public async Task The_same_file_posted_twice_in_one_hand_in_is_a_double_click_not_two_files()
        {
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db);

            var submission = await CreateSubmitter(db).SubmitAsync(
                _studentOneAccountId, homework.Id, null, new[] { _attachmentOneId, _attachmentOneId });

            var version = await db.SubmissionVersions.AsNoTracking().SingleAsync(v => v.HomeworkSubmissionId == submission.Id);
            Assert.Single(await db.SubmissionAttachments.AsNoTracking().Where(a => a.SubmissionVersionId == version.Id).ToListAsync());
        }

        [Fact]
        [BusinessRule("BR-LRN-006")]
        public async Task A_file_that_does_not_exist_is_refused_before_anything_is_written()
        {
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db);

            await Assert.ThrowsAsync<ArgumentException>(
                () => CreateSubmitter(db).SubmitAsync(_studentOneAccountId, homework.Id, null, new[] { 987654 }));

            Assert.Empty(await db.HomeworkSubmissions.ToListAsync());
        }

        // ---------------------------------------------------------------- §4 marking closes the door

        [Fact]
        [BusinessRule("BR-LRN-005")]
        public async Task A_homework_being_marked_no_longer_accepts_work()
        {
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db);
            var submitter = CreateSubmitter(db);
            await submitter.SubmitAsync(_studentOneAccountId, homework.Id, "إجابتي");

            await CreateAdmin(db).BeginMarkingAsync(homework.Id);

            var ex = await Assert.ThrowsAsync<HomeworkClosedToSubmissionsException>(
                () => submitter.SubmitAsync(_studentTwoAccountId, homework.Id, "متأخر جدا"));
            Assert.Equal(HomeworkStatus.Marking, ex.Status);
        }

        [Fact]
        [BusinessRule("BR-LRN-003")]
        public async Task Marking_cannot_begin_on_a_draft()
        {
            using var db = CreateContext();
            var draft = await CreateDesk(db).CreateAsync(_mathOfferingId, _sectionAId, "مسودة", "Draft", GoodDueDate);

            await Assert.ThrowsAsync<HomeworkTransitionException>(() => CreateAdmin(db).BeginMarkingAsync(draft.Id));
        }

        // ---------------------------------------------------------------- §8.5 marking

        [Fact]
        [BusinessRule("BR-LRN-005")]
        public async Task The_late_penalty_is_applied_at_marking_not_at_submit()
        {
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db, latenessPolicy: LatenessPolicy.AcceptWithPenalty, latePenaltyPercent: 25m);

            _clock.UtcNow = new DateTime(2026, 10, 8, 19, 0, 0, DateTimeKind.Utc);
            var submission = await CreateSubmitter(db).SubmitAsync(_studentOneAccountId, homework.Id, "متأخر");

            await CreateAdmin(db).ScoreAsync(submission.Id, 18m, "عمل جيد لكنه متأخر");

            var marked = await db.HomeworkSubmissions.AsNoTracking().SingleAsync(s => s.Id == submission.Id);
            Assert.Equal(13.5m, marked.Score);
            Assert.Equal(SubmissionStatus.Marked, marked.Status);
            Assert.Equal(TeacherUserId, marked.MarkedByUserAccountId);
            Assert.Equal(_clock.UtcNow, marked.MarkedAtUtc);
        }

        [Fact]
        [BusinessRule("BR-LRN-005")]
        public async Task On_time_work_keeps_the_mark_the_teacher_entered()
        {
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db, latenessPolicy: LatenessPolicy.AcceptWithPenalty, latePenaltyPercent: 25m);
            var submission = await CreateSubmitter(db).SubmitAsync(_studentOneAccountId, homework.Id, "في الموعد");

            await CreateAdmin(db).ScoreAsync(submission.Id, 18m, null);

            var marked = await db.HomeworkSubmissions.AsNoTracking().SingleAsync(s => s.Id == submission.Id);
            Assert.Equal(18m, marked.Score);
        }

        [Fact]
        [BusinessRule("BR-LRN-004")]
        public async Task A_mark_above_the_homeworks_maximum_is_refused()
        {
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db);
            var submission = await CreateSubmitter(db).SubmitAsync(_studentOneAccountId, homework.Id, "إجابتي");

            var ex = await Assert.ThrowsAsync<SubmissionScoreOutOfRangeException>(
                () => CreateAdmin(db).ScoreAsync(submission.Id, 30m, null));

            Assert.Equal(20m, ex.MaxMarks);
            Assert.Null((await db.HomeworkSubmissions.AsNoTracking().SingleAsync(s => s.Id == submission.Id)).Score);
        }

        [Fact]
        [BusinessRule("BR-LRN-004")]
        public async Task A_negative_mark_is_refused()
        {
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db);
            var submission = await CreateSubmitter(db).SubmitAsync(_studentOneAccountId, homework.Id, "إجابتي");

            await Assert.ThrowsAsync<SubmissionScoreOutOfRangeException>(
                () => CreateAdmin(db).ScoreAsync(submission.Id, -1m, null));
        }

        [Fact]
        [BusinessRule("BR-LRN-004")]
        public async Task Ungraded_practice_takes_feedback_but_refuses_a_mark()
        {
            using var db = CreateContext();
            var practice = await IssueMathHomeworkAsync(db, maxMarks: null);
            var submission = await CreateSubmitter(db).SubmitAsync(_studentOneAccountId, practice.Id, "تدريب");
            var admin = CreateAdmin(db);

            await admin.ScoreAsync(submission.Id, null, "أحسنت، راجع السؤال الثالث");

            var reviewed = await db.HomeworkSubmissions.AsNoTracking().SingleAsync(s => s.Id == submission.Id);
            Assert.Equal("أحسنت، راجع السؤال الثالث", reviewed.Feedback);
            Assert.Null(reviewed.Score);
            Assert.Equal(SubmissionStatus.Submitted, reviewed.Status);

            var ex = await Assert.ThrowsAsync<SubmissionScoreOutOfRangeException>(
                () => admin.ScoreAsync(submission.Id, 5m, null));
            Assert.Null(ex.MaxMarks);
        }

        [Fact]
        [BusinessRule("BR-LRN-002")]
        public async Task A_teacher_without_reach_cannot_mark()
        {
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db);
            var submission = await CreateSubmitter(db).SubmitAsync(_studentOneAccountId, homework.Id, "إجابتي");

            _user.UserId = StrangerUserId;

            await Assert.ThrowsAsync<TeachingReachException>(() => CreateAdmin(db).ScoreAsync(submission.Id, 10m, null));
        }

        // ---------------------------------------------------------------- BR-LRN-011/012 release

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public async Task Release_is_refused_while_one_hand_in_is_unscored_and_says_how_many()
        {
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db);
            var submitter = CreateSubmitter(db);
            var first = await submitter.SubmitAsync(_studentOneAccountId, homework.Id, "إجابتي");
            await submitter.SubmitAsync(_studentTwoAccountId, homework.Id, "إجابتي أنا");

            var admin = CreateAdmin(db);
            await admin.ScoreAsync(first.Id, 18m, null);
            await admin.BeginMarkingAsync(homework.Id);

            var ex = await Assert.ThrowsAsync<HomeworkReleaseRefusedException>(() => admin.ReleaseAsync(homework.Id));

            Assert.Equal(HomeworkReleaseRefusal.SubmissionsUnscored, ex.Reason);
            Assert.Equal(1, ex.UnscoredSubmissionCount);

            // And nothing reached Module 17.
            var entries = await db.MarkEntries.AsNoTracking().Where(e => e.MarksheetId == _marksheetId).ToListAsync();
            Assert.All(entries, e => Assert.Null(e.Score));
        }

        [Fact]
        [BusinessRule("BR-LRN-012")]
        public async Task Releasing_writes_the_raw_mark_into_module_17s_marksheet()
        {
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db);
            var submitter = CreateSubmitter(db);
            var first = await submitter.SubmitAsync(_studentOneAccountId, homework.Id, "إجابتي");
            var second = await submitter.SubmitAsync(_studentTwoAccountId, homework.Id, "إجابتي أنا");

            var admin = CreateAdmin(db);
            await admin.ScoreAsync(first.Id, 18m, "ممتاز");
            await admin.ScoreAsync(second.Id, 12m, null);
            await admin.BeginMarkingAsync(homework.Id);

            await admin.ReleaseAsync(homework.Id);

            var entries = await db.MarkEntries.AsNoTracking()
                .Where(e => e.MarksheetId == _marksheetId && e.BlueprintComponentId == _componentId)
                .ToListAsync();
            Assert.Equal(18m, entries.Single(e => e.EnrollmentId == _enrollmentOneId).Score);
            Assert.Equal(12m, entries.Single(e => e.EnrollmentId == _enrollmentTwoId).Score);

            // The student who handed nothing in is left alone, not written as
            // zero: that judgement is Module 17's to record, not this module's.
            var missing = entries.Single(e => e.EnrollmentId == _enrollmentThreeId);
            Assert.Null(missing.Score);
            Assert.False(missing.IsAbsent);

            var reloaded = await db.Homeworks.AsNoTracking().SingleAsync(h => h.Id == homework.Id);
            Assert.Equal(HomeworkStatus.Released, reloaded.Status);
            Assert.All(
                await db.HomeworkSubmissions.AsNoTracking().Where(s => s.HomeworkId == homework.Id).ToListAsync(),
                s => Assert.Equal(SubmissionStatus.Released, s.Status));
        }

        [Fact]
        [BusinessRule("BR-LRN-012")]
        public async Task The_mark_module_17_receives_is_the_one_the_lateness_policy_left()
        {
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db, latenessPolicy: LatenessPolicy.AcceptWithPenalty, latePenaltyPercent: 25m);

            _clock.UtcNow = new DateTime(2026, 10, 8, 19, 0, 0, DateTimeKind.Utc);
            var submission = await CreateSubmitter(db).SubmitAsync(_studentOneAccountId, homework.Id, "متأخر");

            var admin = CreateAdmin(db);
            await admin.ScoreAsync(submission.Id, 18m, null);
            await admin.BeginMarkingAsync(homework.Id);
            await admin.ReleaseAsync(homework.Id);

            var entry = await db.MarkEntries.AsNoTracking()
                .SingleAsync(e => e.MarksheetId == _marksheetId && e.EnrollmentId == _enrollmentOneId);
            Assert.Equal(13.5m, entry.Score);
        }

        [Fact]
        [BusinessRule("BR-LRN-012")]
        public async Task Releasing_twice_is_refused()
        {
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db);
            var submission = await CreateSubmitter(db).SubmitAsync(_studentOneAccountId, homework.Id, "إجابتي");

            var admin = CreateAdmin(db);
            await admin.ScoreAsync(submission.Id, 18m, null);
            await admin.BeginMarkingAsync(homework.Id);
            await admin.ReleaseAsync(homework.Id);

            var ex = await Assert.ThrowsAsync<HomeworkReleaseRefusedException>(() => admin.ReleaseAsync(homework.Id));
            Assert.Equal(HomeworkReleaseRefusal.NotBeingMarked, ex.Reason);
        }

        [Fact]
        [BusinessRule("BR-LRN-012")]
        public async Task A_released_homework_can_no_longer_be_re_marked_here()
        {
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db);
            var submission = await CreateSubmitter(db).SubmitAsync(_studentOneAccountId, homework.Id, "إجابتي");

            var admin = CreateAdmin(db);
            await admin.ScoreAsync(submission.Id, 18m, null);
            await admin.BeginMarkingAsync(homework.Id);
            await admin.ReleaseAsync(homework.Id);

            var ex = await Assert.ThrowsAsync<SubmissionMarkingClosedException>(
                () => admin.ScoreAsync(submission.Id, 19m, null));
            Assert.Equal(HomeworkStatus.Released, ex.Status);
        }

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public async Task Ungraded_practice_has_nothing_to_release()
        {
            using var db = CreateContext();
            var practice = await IssueMathHomeworkAsync(db, maxMarks: null);
            await CreateSubmitter(db).SubmitAsync(_studentOneAccountId, practice.Id, "تدريب");

            var admin = CreateAdmin(db);
            await admin.BeginMarkingAsync(practice.Id);

            var ex = await Assert.ThrowsAsync<HomeworkReleaseRefusedException>(() => admin.ReleaseAsync(practice.Id));
            Assert.Equal(HomeworkReleaseRefusal.UngradedPractice, ex.Reason);
        }

        [Fact]
        [BusinessRule("BR-LRN-012")]
        public async Task Release_refuses_rather_than_inventing_a_mark_store_when_no_marksheet_exists()
        {
            using var db = CreateContext();

            // A second Module 17 blueprint — a real one, for the next term of the
            // same offering — that nobody has opened a marksheet against. The
            // blueprint has to exist: pointing the component at an id that does
            // not resolve is refused by the foreign key long before release is
            // reached, which would test the database rather than the rule.
            var sibling = await db.Blueprints.AsNoTracking().SingleAsync(b => b.Id == _blueprintId);
            var nextTerm = new Blueprint
            {
                SchoolId = 1, AcademicYearId = _yearId, CurriculumOfferingId = _mathOfferingId,
                TermId = sibling.TermId + 1, GradingScaleId = sibling.GradingScaleId, IsLocked = true,
            };
            db.Blueprints.Add(nextTerm);
            await db.SaveChangesAsync();

            // Its component is where this homework's mark would go, and there is
            // no marksheet for 3-A against it — the mark has nowhere to land.
            var orphan = new BlueprintComponent
            {
                SchoolId = 1, BlueprintId = nextTerm.Id, NameAr = "بلا كشف", NameEn = "No marksheet",
                Weight = 10m, MaxScore = 10m,
            };
            db.BlueprintComponents.Add(orphan);
            await db.SaveChangesAsync();

            var homework = await CreateDesk(db).CreateAsync(
                _mathOfferingId, _sectionAId, "واجب", "Homework", GoodDueDate, maxMarks: 10m, blueprintComponentId: orphan.Id);
            await CreateDesk(db).IssueAsync(homework.Id);

            var submission = await CreateSubmitter(db).SubmitAsync(_studentOneAccountId, homework.Id, "إجابتي");
            var admin = CreateAdmin(db);
            await admin.ScoreAsync(submission.Id, 9m, null);
            await admin.BeginMarkingAsync(homework.Id);

            var ex = await Assert.ThrowsAsync<HomeworkMarksheetUnresolvedException>(() => admin.ReleaseAsync(homework.Id));
            Assert.Equal(orphan.Id, ex.BlueprintComponentId);
            Assert.Null(ex.EnrollmentId);

            // The homework stayed in Marking: nothing was half-released.
            Assert.Equal(HomeworkStatus.Marking, (await db.Homeworks.AsNoTracking().SingleAsync(h => h.Id == homework.Id)).Status);
        }

        [Fact]
        [BusinessRule("BR-LRN-012")]
        public async Task Release_refuses_when_the_marksheet_does_not_cover_a_student_who_submitted()
        {
            // The student joined 3-A after Module 17 seeded the sheet, so no stub
            // exists for them. Refused before the first write, so half a class
            // cannot be released.
            using var db = CreateContext();
            var stub = await db.MarkEntries.SingleAsync(e => e.EnrollmentId == _enrollmentTwoId && e.MarksheetId == _marksheetId);
            db.MarkEntries.Remove(stub);
            await db.SaveChangesAsync();

            var homework = await IssueMathHomeworkAsync(db);
            var submitter = CreateSubmitter(db);
            var first = await submitter.SubmitAsync(_studentOneAccountId, homework.Id, "إجابتي");
            var second = await submitter.SubmitAsync(_studentTwoAccountId, homework.Id, "إجابتي أنا");

            var admin = CreateAdmin(db);
            await admin.ScoreAsync(first.Id, 18m, null);
            await admin.ScoreAsync(second.Id, 12m, null);
            await admin.BeginMarkingAsync(homework.Id);

            var ex = await Assert.ThrowsAsync<HomeworkMarksheetUnresolvedException>(() => admin.ReleaseAsync(homework.Id));
            Assert.Equal(_enrollmentTwoId, ex.EnrollmentId);

            var covered = await db.MarkEntries.AsNoTracking().SingleAsync(e => e.EnrollmentId == _enrollmentOneId && e.MarksheetId == _marksheetId);
            Assert.Null(covered.Score);
        }

        [Fact]
        [BusinessRule("BR-LRN-012")]
        public async Task Release_into_an_already_published_marksheet_is_refused()
        {
            // Writing into it would change a mark a family has already been shown,
            // bypassing Module 17's WF-08 correction — the "never bypasses the
            // approval chain" half of BR-LRN-012.
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db);
            var submission = await CreateSubmitter(db).SubmitAsync(_studentOneAccountId, homework.Id, "إجابتي");

            var admin = CreateAdmin(db);
            await admin.ScoreAsync(submission.Id, 18m, null);
            await admin.BeginMarkingAsync(homework.Id);

            var marksheet = await db.Marksheets.SingleAsync(m => m.Id == _marksheetId);
            marksheet.Status = MarksheetStatus.Published;
            marksheet.PublishedAtUtc = _clock.UtcNow;
            await db.SaveChangesAsync();

            var ex = await Assert.ThrowsAsync<HomeworkReleaseMarksheetPublishedException>(() => admin.ReleaseAsync(homework.Id));
            Assert.Equal(_marksheetId, ex.MarksheetId);
        }

        // ---------------------------------------------------------------- §9 withdrawal, now that submissions exist

        [Fact]
        [BusinessRule("BR-LRN-016")]
        public async Task Withdrawing_after_the_due_date_is_blocked_once_a_student_has_handed_in()
        {
            // The desk's guard has existed since the homework slice and returned a
            // hard-coded zero because no submission could exist. It starts holding
            // here.
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db);
            await CreateSubmitter(db).SubmitAsync(_studentOneAccountId, homework.Id, "إجابتي");

            _clock.UtcNow = new DateTime(2026, 10, 9, 8, 0, 0, DateTimeKind.Utc);

            var ex = await Assert.ThrowsAsync<HomeworkWithdrawalBlockedException>(
                () => CreateDesk(db).WithdrawAsync(homework.Id, "أُلغيت الحصة"));
            Assert.Equal(1, ex.SubmissionCount);
        }

        [Fact]
        [BusinessRule("BR-LRN-016")]
        public async Task Withdrawing_before_the_due_date_is_still_allowed_with_submissions_in()
        {
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db);
            await CreateSubmitter(db).SubmitAsync(_studentOneAccountId, homework.Id, "إجابتي");

            await CreateDesk(db).WithdrawAsync(homework.Id, "أُلغيت الحصة");

            Assert.Equal(HomeworkStatus.Withdrawn, (await db.Homeworks.AsNoTracking().SingleAsync(h => h.Id == homework.Id)).Status);
        }

        // ---------------------------------------------------------------- tenancy

        [Fact]
        [BusinessRule("BR-GLB-010")]
        public async Task Every_row_of_a_hand_in_carries_the_acting_tenant()
        {
            // The tenant filter must hold at every level, not only at the
            // aggregate root.
            using var db = CreateContext();
            var homework = await IssueMathHomeworkAsync(db);

            var submission = await CreateSubmitter(db).SubmitAsync(
                _studentOneAccountId, homework.Id, "إجابتي", new[] { _attachmentOneId });

            var version = await db.SubmissionVersions.AsNoTracking().SingleAsync(v => v.HomeworkSubmissionId == submission.Id);
            var file = await db.SubmissionAttachments.AsNoTracking().SingleAsync(a => a.SubmissionVersionId == version.Id);

            Assert.Equal(_tenant.SchoolId, submission.SchoolId);
            Assert.Equal(_tenant.SchoolId, version.SchoolId);
            Assert.Equal(_tenant.SchoolId, file.SchoolId);
            Assert.Equal(_yearId, submission.AcademicYearId);
            Assert.Equal(_yearId, version.AcademicYearId);
            Assert.Equal(_yearId, file.AcademicYearId);
        }
    }
}
