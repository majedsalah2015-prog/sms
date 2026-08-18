using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Attendance;
using Sms.Application.Audit;
using Sms.Application.Calendar;
using Sms.Application.Common.Interfaces;
using Sms.Application.Employees;
using Sms.Application.Fees;
using Sms.Application.Grades;
using Sms.Application.Parents;
using Sms.Application.Schools;
using Sms.Application.Seeding;
using Sms.Application.Setup;
using Sms.Application.Sections;
using Sms.Application.Students;
using Sms.Application.Subjects;
using Sms.Application.Teachers;
using Sms.Domain.Attendance;
using Sms.Domain.Calendar;
using Sms.Domain.Common;
using Sms.Domain.Employees;
using Sms.Domain.Fees;
using Sms.Domain.Grades;
using Sms.Domain.Schools;
using Sms.Domain.Students;
using Sms.Domain.Teachers;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Seeding
{
    /// <summary>
    /// S3/E-305 "demo seed complete" — the WBS's pilot-readiness deliverable
    /// (doc 02 §9, IP-02 §2: "the same fixture sales/QA/perf tests use").
    /// Composes essentially every S0-S3 admin service into ONE coherent
    /// demo tenant: a KSA school, an active academic year, one grade's
    /// structure down to a section, a subject offering, a teacher (with
    /// contract), a student (with parent and enrollment), a couple of
    /// attendance days, and a VAT-aware fee structure with a posted
    /// charge. This is both content (a usable demo/sales fixture) and a
    /// smoke test proving the whole built stack composes end to end.
    ///
    /// Idempotency here is a single top-level gate (does any School exist
    /// yet) rather than per-entity checks — ISchoolAdmin.DefineSchoolAsync
    /// has no natural business key to upsert against (unlike lookups/
    /// numbering series), so re-running against an already-seeded tenant
    /// would otherwise duplicate the whole demo school.
    /// </summary>
    public class DemoSeedContributor : ISeedContributor
    {
        private readonly AppDbContext _db;
        private readonly IAuditContext _audit;
        private readonly IClock _clock;
        private readonly ISchoolAdmin _schoolAdmin;
        private readonly IAcademicYearAdmin _yearAdmin;
        private readonly IGradeStructureAdmin _gradeAdmin;
        private readonly ISectionAdmin _sectionAdmin;
        private readonly ISubjectAdmin _subjectAdmin;
        private readonly ICalendarAdmin _calendarAdmin;
        private readonly IEmployeeAdmin _employeeAdmin;
        private readonly ITeacherAdmin _teacherAdmin;
        private readonly IParentAdmin _parentAdmin;
        private readonly IStudentAdmin _studentAdmin;
        private readonly IAttendanceAdmin _attendanceAdmin;
        private readonly IFeeAdmin _feeAdmin;
        private readonly ISystemSetupAdmin _setupAdmin;

        public DemoSeedContributor(
            AppDbContext db, IAuditContext audit, IClock clock, ISchoolAdmin schoolAdmin, IAcademicYearAdmin yearAdmin, IGradeStructureAdmin gradeAdmin,
            ISectionAdmin sectionAdmin, ISubjectAdmin subjectAdmin, ICalendarAdmin calendarAdmin, IEmployeeAdmin employeeAdmin,
            ITeacherAdmin teacherAdmin, IParentAdmin parentAdmin, IStudentAdmin studentAdmin, IAttendanceAdmin attendanceAdmin,
            IFeeAdmin feeAdmin, ISystemSetupAdmin setupAdmin)
        {
            _setupAdmin = setupAdmin;
            _db = db;
            _audit = audit;
            _clock = clock;
            _schoolAdmin = schoolAdmin;
            _yearAdmin = yearAdmin;
            _gradeAdmin = gradeAdmin;
            _sectionAdmin = sectionAdmin;
            _subjectAdmin = subjectAdmin;
            _calendarAdmin = calendarAdmin;
            _employeeAdmin = employeeAdmin;
            _teacherAdmin = teacherAdmin;
            _parentAdmin = parentAdmin;
            _studentAdmin = studentAdmin;
            _attendanceAdmin = attendanceAdmin;
            _feeAdmin = feeAdmin;
        }

        public string Name => "Demo tenant (pilot-readiness fixture)";

        public int Order => 50;

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            if (await _db.Schools.AnyAsync(cancellationToken))
            {
                return;
            }

            _audit.Reason = "Demo tenant seed";

            var school = await _schoolAdmin.DefineSchoolAsync(
                null, "مدرسة الأندلس النموذجية", "Al-Andalus Demo School", "LIC-DEMO-0001", "MIN-DEMO-0001",
                "Arab Standard Time", "SAR", city: "Riyadh", cancellationToken: cancellationToken);
            // E-101 / BR-SET-003: the Setup Wizard must be complete before the first
            // year activates — the demo school walks every mandatory step for real.
            var stage = await _gradeAdmin.DefineStageAsync("الابتدائية", "Elementary", sequenceOrder: 1, GenderPolicy.Mixed, cancellationToken);
            var grade = await _gradeAdmin.DefineGradeLevelAsync(
                stage.Id, "G3", "الصف الثالث", "Grade 3", sequenceOrder: 3, promotionTargetGradeLevelId: null, isGraduating: false, cancellationToken);
            await _setupAdmin.BindCountryPackAsync(Ksa01ContentPackSeedContributor.PackCode, cancellationToken: cancellationToken);
            await _setupAdmin.SetSettingAsync(SettingKeys.EnabledLanguages, "ar,en", cancellationToken: cancellationToken);
            await _setupAdmin.SetSettingAsync(SettingKeys.DefaultLanguage, "ar", cancellationToken: cancellationToken);
            await _setupAdmin.SetSettingAsync(SettingKeys.CalendarType, "Both", cancellationToken: cancellationToken);
            await _setupAdmin.SetSettingAsync(SettingKeys.FirstDayOfWeek, "Sunday", cancellationToken: cancellationToken);
            foreach (var step in SetupWizardSteps.All)
            {
                await _setupAdmin.CompleteStepAsync(step.Code, "Demo tenant seed", cancellationToken);
            }

            await _setupAdmin.DeclareSetupCompleteAsync(cancellationToken);
            await _schoolAdmin.ChangeStatusAsync(school.Id, SchoolStatus.Active, cancellationToken);

            var yearStart = new DateTime(2027, 9, 1);
            var yearEnd = new DateTime(2028, 6, 30);
            var year = await _yearAdmin.DefineYearAsync("٢٠٢٧-٢٠٢٨", "2027-2028", "١٤٤٩هـ", yearStart, yearEnd, cancellationToken);
            await _yearAdmin.ActivateAsync(year.Id, cancellationToken);
            var profile = await _gradeAdmin.DefineGradeYearProfileAsync(
                grade.Id, year.Id, GenderPolicy.Mixed, targetSections: 1, targetSectionSize: 25, cancellationToken: cancellationToken);

            var section = await _sectionAdmin.DefineSectionAsync(profile.Id, "ثالث-أ", "3-A", capacity: 25, GenderPolicy.Mixed, cancellationToken: cancellationToken);

            var subject = await _subjectAdmin.DefineSubjectAsync("MATH", "رياضيات", "Mathematics", category: "core", cancellationToken: cancellationToken);
            var offering = await _subjectAdmin.DefineOfferingAsync(
                profile.Id, subject.Id, weeklyPeriods: 5, isAssessable: true, gpaWeight: 1m,
                isElective: false, electiveGroupTag: null, effectiveFromUtc: yearStart, cancellationToken: cancellationToken);

            await _calendarAdmin.DefineEventAsync(
                year.Id, "اليوم الوطني السعودي", "Saudi National Day", CalendarEventCategory.National,
                new DateTime(2027, 9, 23), new DateTime(2027, 9, 23), cancellationToken: cancellationToken);

            var teacher = await _employeeAdmin.RegisterEmployeeAsync(
                "سارة", "أحمد", "محمد", "العتيبي", "Sara", "Ahmed", "Mohammed", "Alotaibi",
                Gender.Female, new DateTime(1990, 1, 1), nationalityLookupId: 1, cancellationToken: cancellationToken);
            // Backdate to "now" when the seed runs before the year actually starts (the normal pilot-onboarding
            // case) so ITeacherAdmin's active-contract-as-of-today check (BR-TCH-001) doesn't reject the demo teacher.
            var contractStart = _clock.UtcNow < yearStart ? _clock.UtcNow.Date : yearStart;
            var contract = await _employeeAdmin.DefineContractAsync(
                teacher.Id, ContractType.FullTime, contractStart, yearEnd, salaryBasic: 8000m, cancellationToken: cancellationToken);
            await _employeeAdmin.ChangeContractStatusAsync(contract.Id, ContractStatus.Active, cancellationToken);
            var teacherProfile = await _teacherAdmin.DesignateTeacherAsync(teacher.Id, maxWeeklyPeriods: 24, cancellationToken: cancellationToken);
            await _teacherAdmin.AssignAsync(teacherProfile.Id, offering.Id, section.Id, TeacherRole.Primary, yearStart, cancellationToken: cancellationToken);

            var parent = await _parentAdmin.RegisterParentAsync("ولي الأمر", "Guardian", primaryMobile: "0500000001", cancellationToken: cancellationToken);
            var student = await _studentAdmin.RegisterStudentAsync(
                "خالد", "عبدالله", "سالم", "القحطاني", "Khalid", "Abdullah", "Salem", "Alqahtani",
                Gender.Male, new DateTime(2018, 1, 1), nationalityLookupId: 1, cancellationToken: cancellationToken);
            var enrollment = await _studentAdmin.EnrollAsync(student.Id, profile.Id, yearStart, EnrollmentSourceType.Admission, cancellationToken);
            await _studentAdmin.LinkGuardianAsync(
                student.Id, parent.Id, relationshipLookupId: 1, isPrimaryContact: true, isFinanciallyResponsible: true,
                isPickupAuthorized: true, isPortalVisible: true, effectiveFromUtc: yearStart, cancellationToken: cancellationToken);
            await _sectionAdmin.AssignMembershipAsync(section.Id, enrollment.Id, yearStart, cancellationToken);

            await _attendanceAdmin.CaptureAsync(enrollment.Id, yearStart, AttendanceStatus.Present, capturedByUserId: 1, cancellationToken: cancellationToken);
            await _attendanceAdmin.CaptureAsync(enrollment.Id, yearStart.AddDays(1), AttendanceStatus.Present, capturedByUserId: 1, cancellationToken: cancellationToken);

            // Two categories deliberately at different VAT treatments (doc/Modules/19 §14 Q2): education is
            // VAT-exempt (null rate) in KSA, service-linked categories like transport are standard-rated.
            var tuition = await _feeAdmin.DefineCategoryAsync(
                "رسوم دراسية", "Tuition", vatRate: null, isMandatory: true, isRefundable: false, isServiceLinked: false, cancellationToken: cancellationToken);
            await _feeAdmin.DefineCategoryAsync(
                "رسوم النقل", "Transport", vatRate: KsaVatRates.Standard, isMandatory: false, isRefundable: true, isServiceLinked: true, cancellationToken: cancellationToken);

            var tuitionLine = await _feeAdmin.DefineStructureLineAsync(profile.Id, tuition.Id, amount: 12000m, cancellationToken: cancellationToken);
            await _feeAdmin.ApproveStructureLineAsync(tuitionLine.Id, cancellationToken);

            var payer = new Payer { Type = PayerType.Parent, ParentId = parent.Id };
            _db.Payers.Add(payer);
            await _db.SaveChangesAsync(cancellationToken);

            await _feeAdmin.PostChargeAsync(student.Id, payer.Id, profile.Id, tuition.Id, ChargeSourceType.Registration, cancellationToken);
        }
    }
}
