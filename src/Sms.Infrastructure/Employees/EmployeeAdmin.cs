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

        public async Task<Employee> UpdateEmployeeAsync(
            int employeeId, string firstNameAr, string fatherNameAr, string grandfatherNameAr, string familyNameAr,
            string firstNameEn, string fatherNameEn, string grandfatherNameEn, string familyNameEn,
            Gender gender, DateTime dateOfBirth, int nationalityLookupId, int? userAccountId = null,
            int? primaryIdTypeLookupId = null, string? primaryIdNo = null, DateTime? primaryIdExpiry = null,
            CancellationToken cancellationToken = default)
        {
            var employee = await _db.Employees.SingleAsync(e => e.Id == employeeId, cancellationToken);

            employee.UserAccountId = userAccountId;
            employee.FirstNameAr = firstNameAr;
            employee.FatherNameAr = fatherNameAr;
            employee.GrandfatherNameAr = grandfatherNameAr;
            employee.FamilyNameAr = familyNameAr;
            employee.FirstNameEn = firstNameEn;
            employee.FatherNameEn = fatherNameEn;
            employee.GrandfatherNameEn = grandfatherNameEn;
            employee.FamilyNameEn = familyNameEn;
            employee.Gender = gender;
            employee.DateOfBirth = dateOfBirth;
            employee.NationalityLookupId = nationalityLookupId;
            employee.PrimaryIdTypeLookupId = primaryIdTypeLookupId;
            employee.PrimaryIdNo = primaryIdNo;
            employee.PrimaryIdExpiry = primaryIdExpiry;

            await _db.SaveChangesAsync(cancellationToken);
            return employee;
        }

        public async Task<Employee> UpdatePersonalDetailsAsync(
            int employeeId, MaritalStatus? maritalStatus, string? bankName, string? bankAccountNo,
            CancellationToken cancellationToken = default)
        {
            var employee = await _db.Employees.SingleAsync(e => e.Id == employeeId, cancellationToken);

            // Blank means "not recorded", not "recorded as empty": a register that left the column
            // out should leave the field null rather than storing an empty string that reads as an
            // answer in every report and picker afterwards.
            employee.MaritalStatus = maritalStatus;
            employee.BankName = string.IsNullOrWhiteSpace(bankName) ? null : bankName.Trim();
            employee.BankAccountNo = string.IsNullOrWhiteSpace(bankAccountNo) ? null : bankAccountNo.Trim();

            await _db.SaveChangesAsync(cancellationToken);
            return employee;
        }

        public async Task<Contract> UpdateContractAsync(
            int contractId, ContractType type, DateTime startDate, DateTime endDate, decimal salaryBasic,
            decimal? salaryAllowances = null, CancellationToken cancellationToken = default)
        {
            var contract = await _db.Contracts.SingleAsync(c => c.Id == contractId, cancellationToken);
            if (contract.Status != ContractStatus.Draft)
            {
                throw new ContractNotEditableException(contractId, contract.Status);
            }

            var otherRanges = await _db.Contracts
                .Where(c => c.EmployeeId == contract.EmployeeId && c.Id != contractId && c.Status != ContractStatus.Terminated)
                .Select(c => new { c.StartDate, c.EndDate })
                .ToListAsync(cancellationToken);

            if (otherRanges.Any(r => ContractOverlapGuard.Overlaps(startDate, endDate, r.StartDate, r.EndDate)))
            {
                throw new OverlappingContractException(contract.EmployeeId);
            }

            contract.Type = type;
            contract.StartDate = startDate;
            contract.EndDate = endDate;
            contract.SalaryBasic = salaryBasic;
            contract.SalaryAllowances = salaryAllowances;

            await _db.SaveChangesAsync(cancellationToken);
            return contract;
        }

        public async Task<OrgUnit> DefineOrgUnitAsync(string nameAr, string nameEn, int? parentOrgUnitId = null, CancellationToken cancellationToken = default)
        {
            if (parentOrgUnitId.HasValue)
            {
                await _db.OrgUnits.SingleAsync(u => u.Id == parentOrgUnitId.Value, cancellationToken);
            }

            var unit = new OrgUnit
            {
                NameAr = nameAr,
                NameEn = nameEn,
                ParentOrgUnitId = parentOrgUnitId,
            };
            _db.OrgUnits.Add(unit);

            await _db.SaveChangesAsync(cancellationToken);
            return unit;
        }

        public async Task<OrgUnit> UpdateOrgUnitAsync(int orgUnitId, string nameAr, string nameEn, int? parentOrgUnitId = null, CancellationToken cancellationToken = default)
        {
            var unit = await _db.OrgUnits.SingleAsync(u => u.Id == orgUnitId, cancellationToken);

            if (parentOrgUnitId.HasValue)
            {
                if (parentOrgUnitId.Value == orgUnitId)
                {
                    throw new OrgUnitInUseException(orgUnitId, "an org unit cannot be its own parent (BR-EMP-002)");
                }

                // Walk up from the proposed parent � moving under one of our own descendants would create a cycle.
                var units = await _db.OrgUnits.Select(u => new { u.Id, u.ParentOrgUnitId }).ToListAsync(cancellationToken);
                var byId = units.ToDictionary(u => u.Id, u => u.ParentOrgUnitId);
                if (!byId.ContainsKey(parentOrgUnitId.Value))
                {
                    throw new OrgUnitInUseException(orgUnitId, $"parent org unit {parentOrgUnitId.Value} does not exist");
                }

                var cursor = parentOrgUnitId;
                while (cursor.HasValue && byId.TryGetValue(cursor.Value, out var next))
                {
                    if (cursor.Value == orgUnitId)
                    {
                        throw new OrgUnitInUseException(orgUnitId, $"org unit {parentOrgUnitId.Value} is one of its descendants (BR-EMP-002)");
                    }

                    cursor = next;
                }
            }

            unit.NameAr = nameAr;
            unit.NameEn = nameEn;
            unit.ParentOrgUnitId = parentOrgUnitId;

            await _db.SaveChangesAsync(cancellationToken);
            return unit;
        }

        public async Task DeleteOrgUnitAsync(int orgUnitId, CancellationToken cancellationToken = default)
        {
            var unit = await _db.OrgUnits.SingleAsync(u => u.Id == orgUnitId, cancellationToken);

            var children = await _db.OrgUnits.CountAsync(u => u.ParentOrgUnitId == orgUnitId, cancellationToken);
            if (children > 0)
            {
                throw new OrgUnitInUseException(orgUnitId, $"{children} child org unit(s) exist");
            }

            var assignments = await _db.EmployeeAssignments.CountAsync(a => a.OrgUnitId == orgUnitId, cancellationToken);
            if (assignments > 0)
            {
                throw new OrgUnitInUseException(orgUnitId, $"{assignments} employee assignment(s) reference it");
            }

            _db.OrgUnits.Remove(unit);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                throw new OrgUnitInUseException(orgUnitId, "other records still reference it (" + (ex.InnerException?.Message ?? ex.Message) + ")");
            }
        }

        public async Task DeleteEmployeeAsync(int employeeId, CancellationToken cancellationToken = default)
        {
            var employee = await _db.Employees.SingleAsync(e => e.Id == employeeId, cancellationToken);

            var profiles = await _db.TeacherProfiles.Where(p => p.EmployeeId == employeeId).ToListAsync(cancellationToken);
            var profileIds = profiles.Select(p => p.Id).ToList();
            if (await _db.Placements.AnyAsync(p => profileIds.Contains(p.TeacherProfileId), cancellationToken))
            {
                throw new InvalidOperationException("Employee has timetable placements as a teacher; remove them first.");
            }
            if (await _db.Substitutions.AnyAsync(s => profileIds.Contains(s.SubstituteTeacherProfileId), cancellationToken))
            {
                throw new InvalidOperationException("Employee is recorded as a substitute teacher; remove those substitutions first.");
            }
            if (await _db.EmployeeAssignments.AnyAsync(a => a.ManagerEmployeeId == employeeId && a.EmployeeId != employeeId, cancellationToken))
            {
                throw new InvalidOperationException("Employee is the manager of other employees' assignments; reassign them first.");
            }

            _db.TeacherAssignments.RemoveRange(await _db.TeacherAssignments.Where(a => profileIds.Contains(a.TeacherProfileId)).ToListAsync(cancellationToken));
            _db.TeacherProfiles.RemoveRange(profiles);
            _db.Contracts.RemoveRange(await _db.Contracts.Where(c => c.EmployeeId == employeeId).ToListAsync(cancellationToken));
            _db.EmployeeAssignments.RemoveRange(await _db.EmployeeAssignments.Where(a => a.EmployeeId == employeeId).ToListAsync(cancellationToken));
            _db.Qualifications.RemoveRange(await _db.Qualifications.Where(q => q.EmployeeId == employeeId).ToListAsync(cancellationToken));
            _db.Employees.Remove(employee);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException("Employee cannot be deleted: other records still reference it (" + (ex.InnerException?.Message ?? ex.Message) + ").");
            }
        }
    }
}
