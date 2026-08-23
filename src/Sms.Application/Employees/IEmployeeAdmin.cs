using System;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Common;
using Sms.Domain.Employees;

namespace Sms.Application.Employees
{
    /// <summary>doc/Modules/12 §8 Employee File / Org chart / Contract manager screens backing (screens deferred, the operations are core). Issues the permanent number via E-006's INumberIssuer (series "EMP").</summary>
    public interface IEmployeeAdmin
    {
        Task<Employee> RegisterEmployeeAsync(
            string firstNameAr, string fatherNameAr, string grandfatherNameAr, string familyNameAr,
            string firstNameEn, string fatherNameEn, string grandfatherNameEn, string familyNameEn,
            Gender gender, DateTime dateOfBirth, int nationalityLookupId, int? userAccountId = null,
            int? primaryIdTypeLookupId = null, string? primaryIdNo = null, DateTime? primaryIdExpiry = null,
            CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.InvalidEmployeeStatusTransitionException"/>.</summary>
        Task ChangeStatusAsync(int employeeId, EmployeeStatus newStatus, CancellationToken cancellationToken = default);

        /// <summary>BR-EMP-002: closes the employee's current assignment (if any) and opens a new one.</summary>
        Task<EmployeeAssignment> AssignPositionAsync(
            int employeeId, int orgUnitId, int positionLookupId, int? managerEmployeeId, DateTime effectiveFromUtc,
            CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.OverlappingContractException"/> (BR-EMP-003).</summary>
        Task<Contract> DefineContractAsync(
            int employeeId, ContractType type, DateTime startDate, DateTime endDate, decimal salaryBasic,
            decimal? salaryAllowances = null, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.InvalidContractStatusTransitionException"/>.</summary>
        Task ChangeContractStatusAsync(int contractId, ContractStatus newStatus, CancellationToken cancellationToken = default);

        Task<Qualification> AddQualificationAsync(
            int employeeId, string titleAr, string titleEn, DateTime dateAwarded, bool isTeachingRelevant,
            string? institutionName = null, int? documentAttachmentId = null, CancellationToken cancellationToken = default);

        /// <summary>Corrects identity/ID fields (T1 — the ambient audit reason is required for name changes, BR-EMP-001).</summary>
        Task<Employee> UpdateEmployeeAsync(
            int employeeId, string firstNameAr, string fatherNameAr, string grandfatherNameAr, string familyNameAr,
            string firstNameEn, string fatherNameEn, string grandfatherNameEn, string familyNameEn,
            Gender gender, DateTime dateOfBirth, int nationalityLookupId, int? userAccountId = null,
            int? primaryIdTypeLookupId = null, string? primaryIdNo = null, DateTime? primaryIdExpiry = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Marital status and where the salary is paid — the three fields doc/Modules/12 §7 does
        /// not list, added at the owner's request (2026-08-23).
        /// <para>
        /// Its own method rather than three more parameters on
        /// <see cref="UpdateEmployeeAsync"/>, which already takes fifteen. The precedent is
        /// <c>IStudentAdmin.UpdateSocialProfileAsync</c>: a block of fields that belongs to a
        /// different region of the file, is likely to end up behind a permission of its own, and
        /// has no business lengthening the identity signature.
        /// </para>
        /// <para>
        /// All three are T1 with a required reason, so <c>IAuditContext.Reason</c> must be set
        /// before this is called on an existing row — a bank account that changes with no stated
        /// reason is the one edit on this record nobody should be able to make quietly.
        /// </para>
        /// </summary>
        Task<Employee> UpdatePersonalDetailsAsync(
            int employeeId, MaritalStatus? maritalStatus, string? bankName, string? bankAccountNo,
            CancellationToken cancellationToken = default);

        /// <summary>Edits a Draft contract (dates/type/salary) — active contracts are immutable documents; throws <see cref="Common.Exceptions.OverlappingContractException"/>.</summary>
        Task<Contract> UpdateContractAsync(
            int contractId, ContractType type, DateTime startDate, DateTime endDate, decimal salaryBasic,
            decimal? salaryAllowances = null, CancellationToken cancellationToken = default);

        /// <summary>BR-EMP-002 org tree.</summary>
        Task<OrgUnit> DefineOrgUnitAsync(string nameAr, string nameEn, int? parentOrgUnitId = null, CancellationToken cancellationToken = default);

        Task<OrgUnit> UpdateOrgUnitAsync(int orgUnitId, string nameAr, string nameEn, int? parentOrgUnitId = null, CancellationToken cancellationToken = default);

        /// <summary>Hard-deletes an org unit that has no child units and no assignment history.</summary>
        Task DeleteOrgUnitAsync(int orgUnitId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Hard-deletes an employee with its contracts, position assignments, qualifications and (if designated)
        /// the teacher profile + teaching assignments. Refused (InvalidOperationException) while timetable
        /// placements / substitutions, activity supervision, transport staffing or "manager of" links still
        /// reference the employee.
        /// </summary>
        Task DeleteEmployeeAsync(int employeeId, CancellationToken cancellationToken = default);
    }
}
