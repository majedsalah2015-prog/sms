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

        /// <summary>Edits a Draft contract (dates/type/salary) — active contracts are immutable documents; throws <see cref="Common.Exceptions.OverlappingContractException"/>.</summary>
        Task<Contract> UpdateContractAsync(
            int contractId, ContractType type, DateTime startDate, DateTime endDate, decimal salaryBasic,
            decimal? salaryAllowances = null, CancellationToken cancellationToken = default);

        /// <summary>BR-EMP-002 org tree.</summary>
        Task<OrgUnit> DefineOrgUnitAsync(string nameAr, string nameEn, int? parentOrgUnitId = null, CancellationToken cancellationToken = default);

        Task<OrgUnit> UpdateOrgUnitAsync(int orgUnitId, string nameAr, string nameEn, int? parentOrgUnitId = null, CancellationToken cancellationToken = default);

        /// <summary>Hard-deletes an org unit that has no child units and no assignment history.</summary>
        Task DeleteOrgUnitAsync(int orgUnitId, CancellationToken cancellationToken = default);
    }
}
