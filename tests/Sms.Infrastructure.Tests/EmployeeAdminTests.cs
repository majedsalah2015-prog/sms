using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Common;
using Sms.Domain.Employees;
using Sms.Domain.Numbering;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Employees;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// S2/E-203 (slice: Employees, doc/Modules/12, BR-EMP-001..004) over a
    /// real Sqlite-backed AppDbContext, including E-006's real
    /// INumberIssuer (the "EMP" series).
    /// </summary>
    public sealed class EmployeeAdminTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2026, 8, 15, 8, 0, 0, DateTimeKind.Utc);
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
        private int _orgUnitId;

        public EmployeeAdminTests()
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

            var orgUnit = new OrgUnit { SchoolId = 1, NameAr = "الإدارة", NameEn = "Administration" };
            db.OrgUnits.Add(orgUnit);
            db.SaveChanges();
            _orgUnitId = orgUnit.Id;
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private static Task<Employee> Register(EmployeeAdmin admin, string suffix = "1")
            => admin.RegisterEmployeeAsync(
                "موظف" + suffix, "أب", "جد", "عائلة", "Employee" + suffix, "Father", "Grandfather", "Family",
                Gender.Male, new DateTime(1990, 1, 1), nationalityLookupId: 1);

        // --- BR-EMP-001 registration + real numbering -------------------------

        [Fact]
        [BusinessRule("BR-EMP-001")]
        public async Task Registering_an_employee_issues_a_real_permanent_number_via_the_EMP_series()
        {
            using var db = CreateContext();
            var admin = new EmployeeAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));

            var employee = await Register(admin);

            Assert.Equal("EMP-00001", employee.EmployeeNo);
            Assert.Equal(EmployeeStatus.Active, employee.Status);
        }

        // --- personal details: marital status and where the salary is paid -----

        [Fact]
        [BusinessRule("BR-EMP-001")]
        public async Task Personal_details_are_stored_and_blanks_are_recorded_as_unknown_not_as_empty()
        {
            using var db = CreateContext();
            var admin = new EmployeeAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));
            var employee = await Register(admin);

            _audit.Reason = "imported from the staff register";
            await admin.UpdatePersonalDetailsAsync(
                employee.Id, MaritalStatus.Married, null, "  بنك فلسطين  ", " 0123456789 ",
                address: "  غزة - حي الرمال  ", originTown: " بيت دراس ", spouseIdTypeLookupId: 3, spouseIdNo: " 900100099 ",
                palPayWalletNo: " 0599100011 ", jawwalPayWalletNo: " 0567100011 ");

            var saved = await db.Employees.SingleAsync(e => e.Id == employee.Id);
            Assert.Equal(MaritalStatus.Married, saved.MaritalStatus);
            Assert.Equal("بنك فلسطين", saved.BankName);
            Assert.Equal("0123456789", saved.BankAccountNo);
            Assert.Equal("غزة - حي الرمال", saved.Address);
            Assert.Equal("بيت دراس", saved.OriginTown);
            Assert.Equal(3, saved.SpouseIdTypeLookupId);
            Assert.Equal("900100099", saved.SpouseIdNo);
            Assert.Equal("0599100011", saved.PalPayWalletNo);
            Assert.Equal("0567100011", saved.JawwalPayWalletNo);

            // A register that left the column out must leave the field unknown. An empty string
            // would read as an answer in every report and picker afterwards.
            await admin.UpdatePersonalDetailsAsync(
                employee.Id, null, null, "   ", string.Empty,
                address: "  ", originTown: null, spouseIdTypeLookupId: null, spouseIdNo: string.Empty,
                palPayWalletNo: " ", jawwalPayWalletNo: null);

            var cleared = await db.Employees.SingleAsync(e => e.Id == employee.Id);
            Assert.Null(cleared.MaritalStatus);
            Assert.Null(cleared.BankName);
            Assert.Null(cleared.BankAccountNo);
            Assert.Null(cleared.Address);
            Assert.Null(cleared.OriginTown);
            Assert.Null(cleared.SpouseIdTypeLookupId);
            Assert.Null(cleared.SpouseIdNo);
            Assert.Null(cleared.PalPayWalletNo);
            Assert.Null(cleared.JawwalPayWalletNo);
        }

        [Fact]
        [BusinessRule("BR-EMP-001")]
        public async Task Changing_the_account_that_receives_someones_pay_is_refused_without_a_reason()
        {
            using var db = CreateContext();
            var admin = new EmployeeAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));
            var employee = await Register(admin);

            // The point of putting [RequiresAuditReason] on the bank pair, and the reason this test
            // exists rather than the happy path alone: a silent change of where a salary is paid is
            // the one edit on this record nobody should be able to make without saying why.
            _audit.Reason = null;

            await Assert.ThrowsAsync<MissingAuditReasonException>(
                () => admin.UpdatePersonalDetailsAsync(
                    employee.Id, null, null, "بنك آخر", "9999999999",
                    address: null, originTown: null, spouseIdTypeLookupId: null, spouseIdNo: null,
                    palPayWalletNo: null, jawwalPayWalletNo: null));
        }

        [Fact]
        [BusinessRule("BR-EMP-001")]
        public async Task Changing_a_mobile_wallet_is_refused_without_a_reason_the_way_the_bank_account_is()
        {
            using var db = CreateContext();
            var admin = new EmployeeAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));
            var employee = await Register(admin);

            // The wallets are a destination for pay, so they carry the bank account's guard rather
            // than the address's silence. Asserted separately because the two were added a week
            // apart and it would be easy to add the third field without the attribute.
            _audit.Reason = null;

            await Assert.ThrowsAsync<MissingAuditReasonException>(
                () => admin.UpdatePersonalDetailsAsync(
                    employee.Id, null, null, null, null,
                    address: null, originTown: null, spouseIdTypeLookupId: null, spouseIdNo: null,
                    palPayWalletNo: "0599000000", jawwalPayWalletNo: null));
        }

        [Fact]
        [BusinessRule("BR-EMP-001")]
        public async Task An_address_can_be_recorded_without_stating_a_reason_because_it_is_a_fact_not_a_decision()
        {
            using var db = CreateContext();
            var admin = new EmployeeAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));
            var employee = await Register(admin);

            // The other half of the rule above. If the address ever acquired [RequiresAuditReason],
            // a registrar correcting a street name would be met with a mandatory justification box,
            // and this is the test that would say so.
            _audit.Reason = null;

            await admin.UpdatePersonalDetailsAsync(
                employee.Id, null, null, null, null,
                address: "خان يونس - حي الأمل", originTown: "يبنا", spouseIdTypeLookupId: 3, spouseIdNo: "900100088",
                palPayWalletNo: null, jawwalPayWalletNo: null);

            var saved = await db.Employees.SingleAsync(e => e.Id == employee.Id);
            Assert.Equal("خان يونس - حي الأمل", saved.Address);
            Assert.Equal("يبنا", saved.OriginTown);
            Assert.Equal("900100088", saved.SpouseIdNo);
        }

        // --- the bank, since it became a catalogue value ------------------------

        [Fact]
        [BusinessRule("BR-EMP-001")]
        public async Task Picking_a_bank_from_the_catalogue_supersedes_the_free_text_rather_than_sitting_beside_it()
        {
            using var db = CreateContext();
            var admin = new EmployeeAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));
            var employee = await Register(admin);

            _audit.Reason = "imported from the staff register";
            await admin.UpdatePersonalDetailsAsync(
                employee.Id, null, null, "بنك فلسطين", "0123456789",
                address: null, originTown: null, spouseIdTypeLookupId: null, spouseIdNo: null,
                palPayWalletNo: null, jawwalPayWalletNo: null);

            var typed = await db.Employees.SingleAsync(e => e.Id == employee.Id);
            Assert.Null(typed.BankLookupId);
            Assert.Equal("بنك فلسطين", typed.BankName);

            // The registrar then picks the catalogued row. Both columns surviving is the failure
            // this asserts against: the employee file reads the lookup and the payroll transfer
            // list falls back to the text, so a row holding two answers names one bank on screen
            // and another in the export.
            _audit.Reason = "catalogued";
            await admin.UpdatePersonalDetailsAsync(
                employee.Id, null, 42, "بنك فلسطين", "0123456789",
                address: null, originTown: null, spouseIdTypeLookupId: null, spouseIdNo: null,
                palPayWalletNo: null, jawwalPayWalletNo: null);

            var picked = await db.Employees.SingleAsync(e => e.Id == employee.Id);
            Assert.Equal(42, picked.BankLookupId);
            Assert.Null(picked.BankName);
        }

        [Fact]
        [BusinessRule("BR-EMP-001")]
        public async Task Changing_the_bank_the_salary_goes_to_is_refused_without_a_reason_even_though_it_is_now_a_picker()
        {
            using var db = CreateContext();
            var admin = new EmployeeAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));
            var employee = await Register(admin);

            // BankLookupId carries [RequiresAuditReason] for the same reason BankName always did.
            // Asserted separately because the field changed shape: it would be easy to add the
            // column, wire the picker, and leave the guard behind on the column it replaced.
            _audit.Reason = null;

            await Assert.ThrowsAsync<MissingAuditReasonException>(
                () => admin.UpdatePersonalDetailsAsync(
                    employee.Id, null, 42, null, null,
                    address: null, originTown: null, spouseIdTypeLookupId: null, spouseIdNo: null,
                    palPayWalletNo: null, jawwalPayWalletNo: null));
        }

        // --- the contact number the directory is built on ----------------------

        [Fact]
        [BusinessRule("BR-EMP-001")]
        public async Task The_mobile_is_kept_from_registration_trimmed_and_a_blank_clears_it_rather_than_emptying_it()
        {
            using var db = CreateContext();
            var admin = new EmployeeAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));

            var employee = await admin.RegisterEmployeeAsync(
                "موظف", "أب", "جد", "عائلة", "Employee", "Father", "Grandfather", "Family",
                Gender.Male, new DateTime(1990, 1, 1), nationalityLookupId: 1, mobile: "  0599123456  ");

            Assert.Equal("0599123456", (await db.Employees.SingleAsync(e => e.Id == employee.Id)).Mobile);

            // doc/Modules/12 §8.1: the directory is the staff contact card, so an employee who
            // gives up a number must end up with no number rather than with an empty one that
            // reads as an answer in every export afterwards.
            await admin.UpdateEmployeeAsync(
                employee.Id, "موظف", "أب", "جد", "عائلة", "Employee", "Father", "Grandfather", "Family",
                Gender.Male, new DateTime(1990, 1, 1), 1, mobile: "   ");

            Assert.Null((await db.Employees.SingleAsync(e => e.Id == employee.Id)).Mobile);
        }

        [Fact]
        [BusinessRule("BR-EMP-001")]
        public async Task The_whatsapp_number_is_its_own_field_and_follows_the_mobile_rules()
        {
            using var db = CreateContext();
            var admin = new EmployeeAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));

            // A second line is common enough that assuming the mobile reaches WhatsApp is how a
            // school messages the wrong number (owner request 2026-08-27). No audit reason, same as
            // the mobile: it changes when the handset does.
            _audit.Reason = null;
            var employee = await admin.RegisterEmployeeAsync(
                "موظف", "أب", "جد", "عائلة", "Employee", "Father", "Grandfather", "Family",
                Gender.Male, new DateTime(1990, 1, 1), nationalityLookupId: 1, mobile: "0599123456", whatsAppNumber: "  0567123456  ");

            var registered = await db.Employees.SingleAsync(e => e.Id == employee.Id);
            Assert.Equal("0599123456", registered.Mobile);
            Assert.Equal("0567123456", registered.WhatsAppNumber);

            await admin.UpdateEmployeeAsync(
                employee.Id, "موظف", "أب", "جد", "عائلة", "Employee", "Father", "Grandfather", "Family",
                Gender.Male, new DateTime(1990, 1, 1), 1, mobile: "0599123456", whatsAppNumber: "   ");

            Assert.Null((await db.Employees.SingleAsync(e => e.Id == employee.Id)).WhatsAppNumber);
        }

        [Fact]
        [BusinessRule("BR-EMP-001")]
        public async Task Changing_only_the_mobile_needs_no_audit_reason_but_is_still_recorded()
        {
            using var db = CreateContext();
            var admin = new EmployeeAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));
            var employee = await Register(admin);

            // Deliberately not [RequiresAuditReason], unlike the name and the bank account: people
            // change phones, and a reason box that has to be filled in for that teaches everyone
            // here to type a full stop into it — which is how the reason stops meaning anything on
            // the fields that need one.
            _audit.Reason = null;
            await admin.UpdateEmployeeAsync(
                employee.Id, "موظف1", "أب", "جد", "عائلة", "Employee1", "Father", "Grandfather", "Family",
                Gender.Male, new DateTime(1990, 1, 1), 1, mobile: "0599123456");

            Assert.Equal("0599123456", (await db.Employees.SingleAsync(e => e.Id == employee.Id)).Mobile);

            // T1 is field-level, so the change is still on the record — no reason required is not
            // the same thing as no trail.
            Assert.Contains(
                await db.AuditEntries.Where(a => a.EntityType == nameof(Employee) && a.EntityId == employee.Id).ToListAsync(),
                a => a.FieldName == nameof(Employee.Mobile) && a.NewValue == "0599123456");
        }

        // --- BR-EMP-001 status transitions -------------------------------------

        [Fact]
        [BusinessRule("BR-EMP-001")]
        public async Task Changing_status_along_a_legal_path_succeeds()
        {
            using var db = CreateContext();
            var admin = new EmployeeAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));
            var employee = await Register(admin);

            await admin.ChangeStatusAsync(employee.Id, EmployeeStatus.Suspended);

            Assert.Equal(EmployeeStatus.Suspended, db.Employees.Single(e => e.Id == employee.Id).Status);
        }

        [Fact]
        [BusinessRule("BR-EMP-001")]
        public async Task Reactivating_a_terminated_employee_is_rejected()
        {
            using var db = CreateContext();
            var admin = new EmployeeAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));
            var employee = await Register(admin);
            await admin.ChangeStatusAsync(employee.Id, EmployeeStatus.Terminated);

            await Assert.ThrowsAsync<InvalidEmployeeStatusTransitionException>(() =>
                admin.ChangeStatusAsync(employee.Id, EmployeeStatus.Active));
        }

        // --- BR-EMP-002 position assignment -------------------------------------

        [Fact]
        [BusinessRule("BR-EMP-002")]
        public async Task Reassigning_a_position_closes_out_the_prior_current_assignment()
        {
            using var db = CreateContext();
            var admin = new EmployeeAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));
            var employee = await Register(admin);

            var first = await admin.AssignPositionAsync(employee.Id, _orgUnitId, positionLookupId: 1, managerEmployeeId: null, new DateTime(2026, 1, 1));
            var second = await admin.AssignPositionAsync(employee.Id, _orgUnitId, positionLookupId: 2, managerEmployeeId: null, new DateTime(2026, 6, 1));

            Assert.Equal(new DateTime(2026, 6, 1), db.EmployeeAssignments.Single(a => a.Id == first.Id).EffectiveToUtc);
            Assert.Null(db.EmployeeAssignments.Single(a => a.Id == second.Id).EffectiveToUtc);
        }

        // --- BR-EMP-003 contracts ------------------------------------------------

        [Fact]
        [BusinessRule("BR-EMP-003")]
        public async Task Defining_a_contract_starts_it_in_draft_status()
        {
            using var db = CreateContext();
            var admin = new EmployeeAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));
            var employee = await Register(admin);

            var contract = await admin.DefineContractAsync(
                employee.Id, ContractType.FullTime, new DateTime(2026, 9, 1), new DateTime(2027, 6, 30), salaryBasic: 8000m);

            Assert.Equal(ContractStatus.Draft, contract.Status);
        }

        [Fact]
        [BusinessRule("BR-EMP-003")]
        public async Task An_overlapping_contract_for_the_same_employee_is_rejected()
        {
            using var db = CreateContext();
            var admin = new EmployeeAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));
            var employee = await Register(admin);
            await admin.DefineContractAsync(employee.Id, ContractType.FullTime, new DateTime(2026, 9, 1), new DateTime(2027, 6, 30), salaryBasic: 8000m);

            await Assert.ThrowsAsync<OverlappingContractException>(() =>
                admin.DefineContractAsync(employee.Id, ContractType.PartTime, new DateTime(2027, 1, 1), new DateTime(2027, 8, 1), salaryBasic: 3000m));
        }

        [Fact]
        [BusinessRule("BR-EMP-003")]
        public async Task Activating_a_draft_contract_succeeds_and_terminating_an_active_one_succeeds()
        {
            using var db = CreateContext();
            var admin = new EmployeeAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));
            var employee = await Register(admin);
            var contract = await admin.DefineContractAsync(employee.Id, ContractType.FullTime, new DateTime(2026, 9, 1), new DateTime(2027, 6, 30), salaryBasic: 8000m);

            await admin.ChangeContractStatusAsync(contract.Id, ContractStatus.Active);
            Assert.Equal(ContractStatus.Active, db.Contracts.Single(c => c.Id == contract.Id).Status);

            await admin.ChangeContractStatusAsync(contract.Id, ContractStatus.Terminated);
            Assert.Equal(ContractStatus.Terminated, db.Contracts.Single(c => c.Id == contract.Id).Status);
        }

        // --- BR-EMP-004 qualifications -------------------------------------------

        [Fact]
        [BusinessRule("BR-EMP-004")]
        public async Task Adding_a_qualification_records_it_against_the_employee()
        {
            using var db = CreateContext();
            var admin = new EmployeeAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));
            var employee = await Register(admin);

            var qualification = await admin.AddQualificationAsync(
                employee.Id, "بكالوريوس تربية", "B.Ed.", new DateTime(2015, 6, 1), isTeachingRelevant: true);

            Assert.Equal(employee.Id, db.Qualifications.Single(q => q.Id == qualification.Id).EmployeeId);
        }

        [Fact]
        [BusinessRule("BR-EMP-004")]
        public async Task A_qualification_records_the_catalogues_it_was_chosen_from_and_the_grade_point_average()
        {
            using var db = CreateContext();
            var admin = new EmployeeAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));
            var employee = await Register(admin);

            var qualification = await admin.AddQualificationAsync(
                employee.Id, string.Empty, string.Empty, new DateTime(2015, 6, 1), isTeachingRelevant: true,
                educationLookupId: 11, universityLookupId: 22, specializationLookupId: 33, academicGradeLookupId: 44, gpa: 87.40m);

            var saved = await db.Qualifications.SingleAsync(q => q.Id == qualification.Id);
            Assert.Equal(11, saved.EducationLookupId);
            Assert.Equal(22, saved.UniversityLookupId);
            Assert.Equal(33, saved.SpecializationLookupId);
            Assert.Equal(44, saved.AcademicGradeLookupId);
            Assert.Equal(87.40m, saved.Gpa);
        }

        [Fact]
        [BusinessRule("BR-EMP-004")]
        public async Task A_qualification_that_names_itself_neither_way_is_refused()
        {
            using var db = CreateContext();
            var admin = new EmployeeAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));
            var employee = await Register(admin);

            // The catalogued qualification or a written title — an entry with neither is a row
            // nobody can read afterwards, and the screen's dropdowns make an empty submit easy.
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => admin.AddQualificationAsync(employee.Id, "  ", string.Empty, new DateTime(2015, 6, 1), isTeachingRelevant: false));

            // A licence the catalogues cannot name still goes in on its written title alone.
            var licence = await admin.AddQualificationAsync(
                employee.Id, "شهادة السلامة المخبرية", "Laboratory Safety Certificate", new DateTime(2024, 3, 1), isTeachingRelevant: false,
                institutionName: "وزارة التربية والتعليم العالي");

            Assert.Null((await db.Qualifications.SingleAsync(q => q.Id == licence.Id)).EducationLookupId);
        }

        [Fact]
        [BusinessRule("BR-EMP-004")]
        public async Task A_qualification_can_be_corrected_in_place_because_there_is_no_delete_to_undo_a_wrong_pick()
        {
            using var db = CreateContext();
            var admin = new EmployeeAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));
            var employee = await Register(admin);

            var qualification = await admin.AddQualificationAsync(
                employee.Id, "بكالوريوس تربية", "B.Ed.", new DateTime(2015, 6, 1), isTeachingRelevant: true,
                educationLookupId: 11, universityLookupId: 22, gpa: 70m);

            await admin.UpdateQualificationAsync(
                qualification.Id, "بكالوريوس تربية", "B.Ed.", new DateTime(2016, 6, 1), isTeachingRelevant: false,
                institutionName: null, educationLookupId: 11, universityLookupId: 99, specializationLookupId: 33,
                academicGradeLookupId: 44, gpa: 88.50m);

            var saved = await db.Qualifications.SingleAsync(q => q.Id == qualification.Id);
            Assert.Equal(99, saved.UniversityLookupId);
            Assert.Equal(33, saved.SpecializationLookupId);
            Assert.Equal(88.50m, saved.Gpa);
            Assert.Equal(new DateTime(2016, 6, 1), saved.DateAwarded);
            Assert.False(saved.IsTeachingRelevant);

            // The same identity rule on the way through: a correction cannot empty the row's name.
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => admin.UpdateQualificationAsync(
                    qualification.Id, string.Empty, string.Empty, new DateTime(2016, 6, 1), isTeachingRelevant: false,
                    institutionName: null, educationLookupId: null, universityLookupId: null, specializationLookupId: null,
                    academicGradeLookupId: null, gpa: null));
        }

        // --- E-203 screen support: identity edit, draft-contract edit, org tree ---

        [Fact]
        [BusinessRule("BR-EMP-001")]
        public async Task Renaming_an_employee_requires_an_audit_reason_because_identity_is_T1()
        {
            using var db = CreateContext();
            var admin = new EmployeeAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));
            var employee = await Register(admin);

            _audit.Reason = null;
            await Assert.ThrowsAsync<MissingAuditReasonException>(() => admin.UpdateEmployeeAsync(
                employee.Id, "جديد", "أب", "جد", "عائلة", "Renamed", "Father", "Grandfather", "Family", Gender.Male, new DateTime(1990, 1, 1), 1));

            _audit.Reason = "ID card correction";
            var updated = await admin.UpdateEmployeeAsync(
                employee.Id, "جديد", "أب", "جد", "عائلة", "Renamed", "Father", "Grandfather", "Family", Gender.Male, new DateTime(1990, 1, 1), 1);
            Assert.Equal("Renamed", updated.FirstNameEn);
            _audit.Reason = null;
        }

        [Fact]
        [BusinessRule("BR-EMP-003")]
        public async Task Only_a_draft_contract_can_be_edited_and_edits_still_respect_overlap()
        {
            using var db = CreateContext();
            var admin = new EmployeeAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));
            var employee = await Register(admin);
            var first = await admin.DefineContractAsync(employee.Id, ContractType.FullTime, new DateTime(2026, 1, 1), new DateTime(2026, 6, 30), 8000m);
            var second = await admin.DefineContractAsync(employee.Id, ContractType.FullTime, new DateTime(2026, 7, 1), new DateTime(2026, 12, 31), 8000m);

            _audit.Reason = "negotiated";
            await admin.UpdateContractAsync(second.Id, ContractType.PartTime, new DateTime(2026, 8, 1), new DateTime(2026, 12, 31), 5000m);
            Assert.Equal(ContractType.PartTime, db.Contracts.Single(c => c.Id == second.Id).Type);

            await Assert.ThrowsAsync<OverlappingContractException>(() =>
                admin.UpdateContractAsync(second.Id, ContractType.PartTime, new DateTime(2026, 6, 1), new DateTime(2026, 12, 31), 5000m));

            await admin.ChangeContractStatusAsync(first.Id, ContractStatus.Active);
            await Assert.ThrowsAsync<ContractNotEditableException>(() =>
                admin.UpdateContractAsync(first.Id, ContractType.FullTime, new DateTime(2026, 1, 1), new DateTime(2026, 6, 30), 9000m));
            _audit.Reason = null;
        }

        [Fact]
        [BusinessRule("BR-EMP-002")]
        public async Task Org_units_form_an_acyclic_tree_and_cannot_be_deleted_while_in_use()
        {
            using var db = CreateContext();
            var admin = new EmployeeAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock));
            var root = await admin.DefineOrgUnitAsync("الشؤون الأكاديمية", "Academic Affairs");
            var child = await admin.DefineOrgUnitAsync("قسم العلوم", "Science Dept", root.Id);

            await Assert.ThrowsAsync<OrgUnitInUseException>(() => admin.UpdateOrgUnitAsync(root.Id, "x", "x", child.Id));
            await Assert.ThrowsAsync<OrgUnitInUseException>(() => admin.DeleteOrgUnitAsync(root.Id));

            var employee = await Register(admin);
            await admin.AssignPositionAsync(employee.Id, child.Id, positionLookupId: 1, managerEmployeeId: null, new DateTime(2026, 1, 1));
            await Assert.ThrowsAsync<OrgUnitInUseException>(() => admin.DeleteOrgUnitAsync(child.Id));

            var leaf = await admin.DefineOrgUnitAsync("مؤقت", "Temp", root.Id);
            await admin.DeleteOrgUnitAsync(leaf.Id);
            Assert.Empty(db.OrgUnits.Where(u => u.Id == leaf.Id));
        }
    }
}
