using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Common;
using Sms.Domain.Employees;
using Sms.Domain.Grades;
using Sms.Domain.Numbering;
using Sms.Domain.Schools;
using Sms.Domain.Sections;
using Sms.Domain.Subjects;
using Sms.Domain.Teachers;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Employees;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Teachers;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// S2/E-203 (slice: Teachers, doc/Modules/13, BR-TCH-001/002/004/005)
    /// over a real Sqlite-backed AppDbContext.
    /// </summary>
    public sealed class TeacherAdminTests : IDisposable
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

            public int AcademicYearId => 2027;
        }

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();
        private int _yearId;
        private int _sectionId;
        private int _sectionBId;

        public TeacherAdminTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();

            db.NumberingSeries.Add(new NumberingSeries
            {
                Code = "EMP", EntityName = "Employee", FormatTemplate = "EMP-{SEQ:5}",
                ResetPolicy = ResetPolicy.Never, GapPolicy = GapPolicy.Normal, EffectiveFromUtc = _clock.UtcNow, IsActive = true,
            });

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

            var subject = new Subject { SchoolId = 1, Code = "MATH", Name = new LocalizedName("رياضيات", "Math"), Category = "core" };
            db.Subjects.Add(subject);
            db.SaveChanges();

            var offering = new CurriculumOffering
            {
                SchoolId = 1, AcademicYearId = year.Id, GradeYearProfileId = profile.Id, SubjectId = subject.Id,
                WeeklyPeriods = 5, IsAssessable = true, GpaWeight = 1m, EffectiveFromUtc = new DateTime(2026, 9, 1),
            };
            db.CurriculumOfferings.Add(offering);

            var section = new Section { SchoolId = 1, AcademicYearId = year.Id, GradeYearProfileId = profile.Id, NameAr = "ثالث-أ", NameEn = "3-A", Capacity = 25, GenderPolicy = GenderPolicy.Mixed };
            var sectionB = new Section { SchoolId = 1, AcademicYearId = year.Id, GradeYearProfileId = profile.Id, NameAr = "ثالث-ب", NameEn = "3-B", Capacity = 25, GenderPolicy = GenderPolicy.Mixed };
            db.Sections.Add(section);
            db.Sections.Add(sectionB);
            db.SaveChanges();

            _yearId = year.Id;
            _sectionId = section.Id;
            _sectionBId = sectionB.Id;
            _offeringId = offering.Id;
        }

        private int _offeringId;

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private async Task<Employee> RegisterEmployeeWithActiveContract(AppDbContext db, string suffix = "1")
        {
            var employeeAdmin = new EmployeeAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));
            var employee = await employeeAdmin.RegisterEmployeeAsync(
                "معلم" + suffix, "أب", "جد", "عائلة", "Teacher" + suffix, "Father", "Grandfather", "Family",
                Gender.Male, new DateTime(1985, 1, 1), nationalityLookupId: 1);

            var contract = await employeeAdmin.DefineContractAsync(
                employee.Id, ContractType.FullTime, new DateTime(2026, 9, 1), new DateTime(2027, 6, 30), salaryBasic: 9000m);
            await employeeAdmin.ChangeContractStatusAsync(contract.Id, ContractStatus.Active);

            return employee;
        }

        // --- BR-TCH-001 designation ----------------------------------------------

        [Fact]
        [BusinessRule("BR-TCH-001")]
        public async Task Designating_an_employee_with_an_active_contract_creates_a_teacher_profile()
        {
            using var db = CreateContext();
            var employee = await RegisterEmployeeWithActiveContract(db);
            var teacherAdmin = new TeacherAdmin(db, _clock);

            var profile = await teacherAdmin.DesignateTeacherAsync(employee.Id, maxWeeklyPeriods: 24);

            Assert.Equal(employee.Id, profile.EmployeeId);
        }

        [Fact]
        [BusinessRule("BR-TCH-001")]
        public async Task Designating_an_employee_without_an_active_contract_is_rejected()
        {
            using var db = CreateContext();
            var employeeAdmin = new EmployeeAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));
            var employee = await employeeAdmin.RegisterEmployeeAsync(
                "معلم", "أب", "جد", "عائلة", "Teacher", "Father", "Grandfather", "Family",
                Gender.Male, new DateTime(1985, 1, 1), nationalityLookupId: 1); // no contract
            var teacherAdmin = new TeacherAdmin(db, _clock);

            await Assert.ThrowsAsync<EmployeeNotEligibleForTeachingException>(() =>
                teacherAdmin.DesignateTeacherAsync(employee.Id, maxWeeklyPeriods: 24));
        }

        // --- BR-TCH-002/005 assignment -------------------------------------------

        [Fact]
        [BusinessRule("BR-TCH-002")]
        public async Task Assigning_a_teacher_to_an_offering_and_section_stamps_the_offerings_year()
        {
            using var db = CreateContext();
            var employee = await RegisterEmployeeWithActiveContract(db);
            var teacherAdmin = new TeacherAdmin(db, _clock);
            var profile = await teacherAdmin.DesignateTeacherAsync(employee.Id, maxWeeklyPeriods: 24);

            var assignment = await teacherAdmin.AssignAsync(profile.Id, _offeringId, _sectionId, TeacherRole.Primary, new DateTime(2026, 9, 1));

            Assert.Equal(_yearId, assignment.AcademicYearId);
        }

        [Fact]
        [BusinessRule("BR-TCH-005")]
        public async Task A_second_primary_teacher_for_the_same_offering_and_section_is_rejected()
        {
            using var db = CreateContext();
            var first = await RegisterEmployeeWithActiveContract(db, "1");
            var second = await RegisterEmployeeWithActiveContract(db, "2");
            var teacherAdmin = new TeacherAdmin(db, _clock);
            var firstProfile = await teacherAdmin.DesignateTeacherAsync(first.Id, maxWeeklyPeriods: 24);
            var secondProfile = await teacherAdmin.DesignateTeacherAsync(second.Id, maxWeeklyPeriods: 24);
            await teacherAdmin.AssignAsync(firstProfile.Id, _offeringId, _sectionId, TeacherRole.Primary, new DateTime(2026, 9, 1));

            await Assert.ThrowsAsync<DuplicatePrimaryTeacherException>(() =>
                teacherAdmin.AssignAsync(secondProfile.Id, _offeringId, _sectionId, TeacherRole.Primary, new DateTime(2026, 9, 1)));
        }

        [Fact]
        [BusinessRule("BR-TCH-005")]
        public async Task A_co_teacher_can_be_assigned_alongside_an_existing_primary()
        {
            using var db = CreateContext();
            var first = await RegisterEmployeeWithActiveContract(db, "1");
            var second = await RegisterEmployeeWithActiveContract(db, "2");
            var teacherAdmin = new TeacherAdmin(db, _clock);
            var firstProfile = await teacherAdmin.DesignateTeacherAsync(first.Id, maxWeeklyPeriods: 24);
            var secondProfile = await teacherAdmin.DesignateTeacherAsync(second.Id, maxWeeklyPeriods: 24);
            await teacherAdmin.AssignAsync(firstProfile.Id, _offeringId, _sectionId, TeacherRole.Primary, new DateTime(2026, 9, 1));

            var coAssignment = await teacherAdmin.AssignAsync(secondProfile.Id, _offeringId, _sectionId, TeacherRole.CoTeacher, new DateTime(2026, 9, 1));

            Assert.Equal(TeacherRole.CoTeacher, coAssignment.Role);
        }

        // --- BR-TCH-004 load ------------------------------------------------------

        [Fact]
        [BusinessRule("BR-TCH-004")]
        public async Task Exceeding_max_weekly_periods_is_rejected_without_an_override()
        {
            using var db = CreateContext();
            var employee = await RegisterEmployeeWithActiveContract(db);
            var teacherAdmin = new TeacherAdmin(db, _clock);
            var profile = await teacherAdmin.DesignateTeacherAsync(employee.Id, maxWeeklyPeriods: 5);
            await teacherAdmin.AssignAsync(profile.Id, _offeringId, _sectionId, TeacherRole.Primary, new DateTime(2026, 9, 1)); // load = 5

            await Assert.ThrowsAsync<LoadExceededException>(() =>
                teacherAdmin.AssignAsync(profile.Id, _offeringId, _sectionBId, TeacherRole.Primary, new DateTime(2026, 9, 1))); // would be 10 > 5
        }

        [Fact]
        [BusinessRule("BR-TCH-004")]
        public async Task Exceeding_max_weekly_periods_succeeds_with_an_explicit_override()
        {
            using var db = CreateContext();
            var employee = await RegisterEmployeeWithActiveContract(db);
            var teacherAdmin = new TeacherAdmin(db, _clock);
            var profile = await teacherAdmin.DesignateTeacherAsync(employee.Id, maxWeeklyPeriods: 5);
            await teacherAdmin.AssignAsync(profile.Id, _offeringId, _sectionId, TeacherRole.Primary, new DateTime(2026, 9, 1)); // load = 5

            var assignment = await teacherAdmin.AssignAsync(
                profile.Id, _offeringId, _sectionBId, TeacherRole.Primary, new DateTime(2026, 9, 1), overrideLoad: true);

            Assert.NotNull(assignment);
        }

        // --- E-203 screen support: reassignment + load edit ---------------------

        [Fact]
        [BusinessRule("BR-TCH-007")]
        public async Task Ending_an_assignment_keeps_history_and_frees_the_primary_slot_for_another_teacher()
        {
            using var db = CreateContext();
            var first = await RegisterEmployeeWithActiveContract(db, "1");
            var second = await RegisterEmployeeWithActiveContract(db, "2");
            var teacherAdmin = new TeacherAdmin(db, _clock);
            var firstProfile = await teacherAdmin.DesignateTeacherAsync(first.Id, maxWeeklyPeriods: 24);
            var secondProfile = await teacherAdmin.DesignateTeacherAsync(second.Id, maxWeeklyPeriods: 24);
            var a1 = await teacherAdmin.AssignAsync(firstProfile.Id, _offeringId, _sectionId, TeacherRole.Primary, new DateTime(2026, 9, 1));

            await teacherAdmin.EndAssignmentAsync(a1.Id, new DateTime(2026, 12, 1));
            await teacherAdmin.EndAssignmentAsync(a1.Id, new DateTime(2027, 1, 1)); // idempotent — first close stands
            Assert.Equal(new DateTime(2026, 12, 1), db.TeacherAssignments.Single(a => a.Id == a1.Id).EffectiveToUtc);

            var a2 = await teacherAdmin.AssignAsync(secondProfile.Id, _offeringId, _sectionId, TeacherRole.Primary, new DateTime(2026, 12, 1));
            Assert.Null(db.TeacherAssignments.Single(a => a.Id == a2.Id).EffectiveToUtc);
            Assert.Equal(2, db.TeacherAssignments.Count(a => a.CurriculumOfferingId == _offeringId && a.SectionId == _sectionId));
        }

        [Fact]
        [BusinessRule("BR-TCH-004")]
        public async Task Raising_max_load_lets_a_previously_rejected_assignment_through()
        {
            using var db = CreateContext();
            var employee = await RegisterEmployeeWithActiveContract(db);
            var teacherAdmin = new TeacherAdmin(db, _clock);
            var profile = await teacherAdmin.DesignateTeacherAsync(employee.Id, maxWeeklyPeriods: 1);

            await Assert.ThrowsAsync<LoadExceededException>(() =>
                teacherAdmin.AssignAsync(profile.Id, _offeringId, _sectionId, TeacherRole.Primary, new DateTime(2026, 9, 1)));

            await teacherAdmin.UpdateMaxLoadAsync(profile.Id, 24);
            var assignment = await teacherAdmin.AssignAsync(profile.Id, _offeringId, _sectionId, TeacherRole.Primary, new DateTime(2026, 9, 1));
            Assert.NotNull(assignment);
        }
    }
}
