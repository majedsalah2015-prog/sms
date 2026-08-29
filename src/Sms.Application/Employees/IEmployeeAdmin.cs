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
            string? mobile = null, string? whatsAppNumber = null, CancellationToken cancellationToken = default);

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

        /// <summary>
        /// BR-EMP-004. Identified either by the written title or by <paramref name="educationLookupId"/>
        /// — throws <see cref="InvalidOperationException"/> when neither is given, because a
        /// qualification row that names nothing is a row nobody can read afterwards.
        /// <para>
        /// The four lookup ids and the GPA are the owner's 2026-08-27 addition: the school picks the
        /// qualification, university, specialization and classification from catalogues rather than
        /// typing them, so two spellings of one university stop being two universities. All
        /// optional — the Excel import still writes title + institution as text.
        /// </para>
        /// </summary>
        Task<Qualification> AddQualificationAsync(
            int employeeId, string titleAr, string titleEn, DateTime dateAwarded, bool isTeachingRelevant,
            string? institutionName = null, int? documentAttachmentId = null,
            int? educationLookupId = null, int? universityLookupId = null, int? specializationLookupId = null,
            int? academicGradeLookupId = null, decimal? gpa = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Corrects a qualification in place (T2 — field-level audit, no reason required).
        /// <para>
        /// Six fields chosen from four dropdowns is six chances to pick the wrong row, and this
        /// product has no delete to undo it with (BR-GLB-005). Same identification rule as
        /// <see cref="AddQualificationAsync"/>.
        /// </para>
        /// </summary>
        Task<Qualification> UpdateQualificationAsync(
            int qualificationId, string titleAr, string titleEn, DateTime dateAwarded, bool isTeachingRelevant,
            string? institutionName, int? educationLookupId, int? universityLookupId, int? specializationLookupId,
            int? academicGradeLookupId, decimal? gpa, CancellationToken cancellationToken = default);

        /// <summary>
        /// Corrects identity/ID fields (T1 — the ambient audit reason is required for name changes, BR-EMP-001).
        /// <para>
        /// <paramref name="mobile"/> and <paramref name="whatsAppNumber"/> ride here rather than on
        /// <see cref="UpdatePersonalDetailsAsync"/> because they are not that method's restricted
        /// fields: the directory is the staff contact card (doc/Modules/12 §8.1), the numbers sit on
        /// the same form as the name, and neither needs an audit reason of its own.
        /// </para>
        /// </summary>
        Task<Employee> UpdateEmployeeAsync(
            int employeeId, string firstNameAr, string fatherNameAr, string grandfatherNameAr, string familyNameAr,
            string firstNameEn, string fatherNameEn, string grandfatherNameEn, string familyNameEn,
            Gender gender, DateTime dateOfBirth, int nationalityLookupId, int? userAccountId = null,
            int? primaryIdTypeLookupId = null, string? primaryIdNo = null, DateTime? primaryIdExpiry = null,
            string? mobile = null, string? whatsAppNumber = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// The personal block of the HR file — marital status and the spouse's document, the two
        /// address lines, and every destination the employee's pay can be sent to. None of it is in
        /// doc/Modules/12 §7; it is here at the owner's request (2026-08-23, extended 2026-08-27).
        /// <para>
        /// Every field is written on every call, so a caller that wants to change one must supply
        /// the rest. That is deliberate and the reason none of them has a default: a three-argument
        /// convenience overload would blank an employee's address the day someone edited their
        /// marital status through it.
        /// </para>
        /// <para>
        /// Its own method rather than nine more parameters on
        /// <see cref="UpdateEmployeeAsync"/>, which already takes seventeen. The precedent is
        /// <c>IStudentAdmin.UpdateSocialProfileAsync</c>: a block of fields that belongs to a
        /// different region of the file, is likely to end up behind a permission of its own, and
        /// has no business lengthening the identity signature.
        /// </para>
        /// <para>
        /// The marital status and the three payment destinations are T1 with a required reason, so
        /// <c>IAuditContext.Reason</c> must be set before this is called on an existing row — an
        /// account or wallet that changes with no stated reason is the one edit on this record
        /// nobody should be able to make quietly. The address, the origin town and the spouse's
        /// document are facts being recorded rather than decisions being defended, and are captured
        /// field-level without one.
        /// </para>
        /// </summary>
        Task<Employee> UpdatePersonalDetailsAsync(
            int employeeId, MaritalStatus? maritalStatus, string? bankName, string? bankAccountNo,
            string? address, string? originTown, int? spouseIdTypeLookupId, string? spouseIdNo,
            string? palPayWalletNo, string? jawwalPayWalletNo,
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
