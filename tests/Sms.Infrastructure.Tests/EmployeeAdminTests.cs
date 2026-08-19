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
