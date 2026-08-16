using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Employees;
using Sms.Application.Numbering;
using Sms.Domain.Common;
using Sms.Domain.Employees;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Employees
{
    /// <summary>
    /// Standalone admin operations — save themselves, no larger transaction
    /// to ride. RegisterEmployeeAsync composes with E-006's INumberIssuer
    /// the same way StudentAdmin/ParentAdmin do (series "EMP").
    /// </summary>
    public class EmployeeAdmin : IEmployeeAdmin
    {
        private readonly AppDbContext _db;
        private readonly INumberIssuer _numberIssuer;

        public EmployeeAdmin(AppDbContext db, INumberIssuer numberIssuer)
        {
            _db = db;
            _numberIssuer = numberIssuer;
        }

        public async Task<Employee> RegisterEmployeeAsync(
            string firstNameAr, string fatherNameAr, string grandfatherNameAr, string familyNameAr,
            string firstNameEn, string fatherNameEn, string grandfatherNameEn, string familyNameEn,
            Gender gender, DateTime dateOfBirth, int nationalityLookupId, int? userAccountId = null,
            int? primaryIdTypeLookupId = null, string? primaryIdNo = null, DateTime? primaryIdExpiry = null,
            CancellationToken cancellationToken = default)
        {
            var employeeNo = await _numberIssuer.IssueAsync("EMP", cancellationToken);

            var employee = new Employee
            {
                EmployeeNo = employeeNo,
                UserAccountId = userAccountId,
                FirstNameAr = firstNameAr,
                FatherNameAr = fatherNameAr,
                GrandfatherNameAr = grandfatherNameAr,
                FamilyNameAr = familyNameAr,
                FirstNameEn = firstNameEn,
                FatherNameEn = fatherNameEn,
                GrandfatherNameEn = grandfatherNameEn,
                FamilyNameEn = familyNameEn,
                Gender = gender,
                DateOfBirth = dateOfBirth,
                NationalityLookupId = nationalityLookupId,
                PrimaryIdTypeLookupId = primaryIdTypeLookupId,
                PrimaryIdNo = primaryIdNo,
                PrimaryIdExpiry = primaryIdExpiry,
            };
            _db.Employees.Add(employee);

            await _db.SaveChangesAsync(cancellationToken);
            return employee;
        }

        public async Task ChangeStatusAsync(int employeeId, EmployeeStatus newStatus, CancellationToken cancellationToken = default)
        {
            var employee = await _db.Employees.SingleAsync(e => e.Id == employeeId, cancellationToken);
            if (!EmployeeStatusTransitions.CanTransition(employee.Status, newStatus))
            {
                throw new InvalidEmployeeStatusTransitionException(employee.Status, newStatus);
            }

            employee.Status = newStatus;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<EmployeeAssignment> AssignPositionAsync(
            int employeeId, int orgUnitId, int positionLookupId, int? managerEmployeeId, DateTime effectiveFromUtc,
            CancellationToken cancellationToken = default)
        {
            var current = await _db.EmployeeAssignments
                .Where(a => a.EmployeeId == employeeId && a.EffectiveToUtc == null)
                .SingleOrDefaultAsync(cancellationToken);
            if (current != null)
            {
                current.EffectiveToUtc = effectiveFromUtc;
            }

            var assignment = new EmployeeAssignment
            {
                EmployeeId = employeeId,
                OrgUnitId = orgUnitId,
                PositionLookupId = positionLookupId,
                ManagerEmployeeId = managerEmployeeId,
                EffectiveFromUtc = effectiveFromUtc,
            };
            _db.EmployeeAssignments.Add(assignment);

            await _db.SaveChangesAsync(cancellationToken);
            return assignment;
        }

        public async Task<Contract> DefineContractAsync(
            int employeeId, ContractType type, DateTime startDate, DateTime endDate, decimal salaryBasic,
            decimal? salaryAllowances = null, CancellationToken cancellationToken = default)
        {
            var existingRanges = await _db.Contracts
                .Where(c => c.EmployeeId == employeeId && c.Status != ContractStatus.Terminated)
                .Select(c => new { c.StartDate, c.EndDate })
                .ToListAsync(cancellationToken);

            if (existingRanges.Any(r => ContractOverlapGuard.Overlaps(startDate, endDate, r.StartDate, r.EndDate)))
            {
                throw new OverlappingContractException(employeeId);
            }

            var contract = new Contract
            {
                EmployeeId = employeeId,
                Type = type,
                StartDate = startDate,
                EndDate = endDate,
                SalaryBasic = salaryBasic,
                SalaryAllowances = salaryAllowances,
                Status = ContractStatus.Draft,
            };
            _db.Contracts.Add(contract);

            await _db.SaveChangesAsync(cancellationToken);
            return contract;
        }

        public async Task ChangeContractStatusAsync(int contractId, ContractStatus newStatus, CancellationToken cancellationToken = default)
        {
            var contract = await _db.Contracts.SingleAsync(c => c.Id == contractId, cancellationToken);
            if (!ContractStatusTransitions.CanTransition(contract.Status, newStatus))
            {
                throw new InvalidContractStatusTransitionException(contract.Status, newStatus);
            }

            contract.Status = newStatus;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<Qualification> AddQualificationAsync(
            int employeeId, string titleAr, string titleEn, DateTime dateAwarded, bool isTeachingRelevant,
            string? institutionName = null, int? documentAttachmentId = null, CancellationToken cancellationToken = default)
        {
            var qualification = new Qualification
            {
                EmployeeId = employeeId,
                TitleAr = titleAr,
                TitleEn = titleEn,
                InstitutionName = institutionName,
                DateAwarded = dateAwarded,
                IsTeachingRelevant = isTeachingRelevant,
                DocumentAttachmentId = documentAttachmentId,
            };
            _db.Qualifications.Add(qualification);

            await _db.SaveChangesAsync(cancellationToken);
            return qualification;
        }
    }
}
