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
    }
}
