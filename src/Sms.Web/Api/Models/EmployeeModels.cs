using System;
using System.Collections.Generic;
using Sms.Web.Models;

namespace Sms.Web.Api.Models
{
    /// <summary>One row of the staff directory (doc/Modules/12 §8.1).</summary>
    public sealed class ApiEmployeeRow
    {
        public int EmployeeId { get; set; }

        public string EmployeeNo { get; set; } = string.Empty;

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        /// <summary>Active / Suspended / Terminated.</summary>
        public string Status { get; set; } = string.Empty;

        public string? Mobile { get; set; }

        public string? OrgUnitName { get; set; }

        public string? PositionName { get; set; }
    }

    /// <summary>
    /// The staff file. Salary is <b>not</b> here: BR-EMP-003 / BR-EMP-010 make
    /// pay a restricted category behind <c>Employees/Contracts</c>, and folding
    /// a figure into the file response would hand it to everyone who may read a
    /// staff name. Contracts are their own endpoint, with their own permission.
    /// </summary>
    public sealed class ApiEmployeeFile
    {
        public int EmployeeId { get; set; }

        public string EmployeeNo { get; set; } = string.Empty;

        public string FirstNameAr { get; set; } = string.Empty;

        public string FatherNameAr { get; set; } = string.Empty;

        public string GrandfatherNameAr { get; set; } = string.Empty;

        public string FamilyNameAr { get; set; } = string.Empty;

        public string FirstNameEn { get; set; } = string.Empty;

        public string FatherNameEn { get; set; } = string.Empty;

        public string GrandfatherNameEn { get; set; } = string.Empty;

        public string FamilyNameEn { get; set; } = string.Empty;

        public string Gender { get; set; } = string.Empty;

        public DateTime DateOfBirth { get; set; }

        public int NationalityLookupId { get; set; }

        public string? NationalityName { get; set; }

        public int? PrimaryIdTypeLookupId { get; set; }

        public string? PrimaryIdNo { get; set; }

        public DateTime? PrimaryIdExpiry { get; set; }

        public string Status { get; set; } = string.Empty;

        public string? Mobile { get; set; }

        public string? WhatsAppNumber { get; set; }

        public bool HasPhoto { get; set; }

        /// <summary>The live position, if there is one (BR-EMP-002).</summary>
        public ApiEmployeeAssignment? Assignment { get; set; }

        public IReadOnlyList<ApiQualification> Qualifications { get; set; } = Array.Empty<ApiQualification>();
    }

    /// <summary>The live org-chart position (BR-EMP-002).</summary>
    public sealed class ApiEmployeeAssignment
    {
        public int AssignmentId { get; set; }

        public int OrgUnitId { get; set; }

        public string? OrgUnitName { get; set; }

        public int PositionLookupId { get; set; }

        public string? PositionName { get; set; }

        public int? ManagerEmployeeId { get; set; }

        public DateTime EffectiveFromUtc { get; set; }
    }

    /// <summary>BR-EMP-004 — one qualification on the file.</summary>
    public sealed class ApiQualification
    {
        public int QualificationId { get; set; }

        public string TitleAr { get; set; } = string.Empty;

        public string TitleEn { get; set; } = string.Empty;

        public string? InstitutionName { get; set; }

        public DateTime DateAwarded { get; set; }

        public bool IsTeachingRelevant { get; set; }

        public int? EducationLookupId { get; set; }

        public int? UniversityLookupId { get; set; }

        public int? SpecializationLookupId { get; set; }

        public int? AcademicGradeLookupId { get; set; }

        public decimal? Gpa { get; set; }

        public int? DocumentAttachmentId { get; set; }
    }

    /// <summary>
    /// One employment contract. Behind <c>Employees/Contracts</c> — this is the
    /// restricted salary category (BR-EMP-003, BR-EMP-010).
    /// </summary>
    public sealed class ApiContract
    {
        public int ContractId { get; set; }

        public int EmployeeId { get; set; }

        /// <summary>FullTime / PartTime / Term.</summary>
        public string Type { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public decimal SalaryBasic { get; set; }

        public decimal? SalaryAllowances { get; set; }

        /// <summary>Draft / Active / Terminated. Natural expiry is derived from <see cref="EndDate"/>, never stored.</summary>
        public string Status { get; set; } = string.Empty;

        public string Currency { get; set; } = string.Empty;
    }

    /// <summary>Register a member of staff. The employee number is issued on this call's own commit.</summary>
    public class ApiRegisterEmployeeRequest
    {
        [RequiredField("Arabic first name", "الاسم الأول بالعربية")]
        public string FirstNameAr { get; set; } = string.Empty;

        [RequiredField("Arabic father's name", "اسم الأب بالعربية")]
        public string FatherNameAr { get; set; } = string.Empty;

        [RequiredField("Arabic grandfather's name", "اسم الجد بالعربية")]
        public string GrandfatherNameAr { get; set; } = string.Empty;

        [RequiredField("Arabic family name", "اسم العائلة بالعربية")]
        public string FamilyNameAr { get; set; } = string.Empty;

        [RequiredField("English first name", "الاسم الأول بالإنجليزية")]
        public string FirstNameEn { get; set; } = string.Empty;

        [RequiredField("English father's name", "اسم الأب بالإنجليزية")]
        public string FatherNameEn { get; set; } = string.Empty;

        [RequiredField("English grandfather's name", "اسم الجد بالإنجليزية")]
        public string GrandfatherNameEn { get; set; } = string.Empty;

        [RequiredField("English family name", "اسم العائلة بالإنجليزية")]
        public string FamilyNameEn { get; set; } = string.Empty;

        [RequiredField("gender", "الجنس")]
        public string Gender { get; set; } = string.Empty;

        public DateTime DateOfBirth { get; set; }

        public int NationalityLookupId { get; set; }

        /// <summary>Links this person to a sign-in account, when they have one.</summary>
        public int? UserAccountId { get; set; }

        public int? PrimaryIdTypeLookupId { get; set; }

        public string? PrimaryIdNo { get; set; }

        public DateTime? PrimaryIdExpiry { get; set; }

        public string? Mobile { get; set; }

        public string? WhatsAppNumber { get; set; }
    }

    /// <summary>BR-EMP-001: a name change is T1 and needs a stated reason.</summary>
    public sealed class ApiUpdateEmployeeRequest : ApiRegisterEmployeeRequest
    {
        [RequiredField("reason", "السبب")]
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>Active / Suspended / Terminated. The engine decides which moves are legal.</summary>
    public sealed class ApiChangeEmployeeStatusRequest
    {
        [RequiredField("status", "الحالة")]
        public string Status { get; set; } = string.Empty;

        public string? Reason { get; set; }
    }

    /// <summary>BR-EMP-002: closes the current assignment and opens a new one.</summary>
    public sealed class ApiAssignPositionRequest
    {
        public int OrgUnitId { get; set; }

        public int PositionLookupId { get; set; }

        public int? ManagerEmployeeId { get; set; }

        public DateTime? EffectiveFromUtc { get; set; }
    }

    /// <summary>BR-EMP-003: an overlapping contract is refused.</summary>
    public sealed class ApiContractRequest
    {
        /// <summary>FullTime / PartTime / Term.</summary>
        [RequiredField("contract type", "نوع العقد")]
        public string Type { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public decimal SalaryBasic { get; set; }

        public decimal? SalaryAllowances { get; set; }
    }

    /// <summary>Draft / Active / Terminated.</summary>
    public sealed class ApiContractStatusRequest
    {
        [RequiredField("status", "الحالة")]
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>
    /// BR-EMP-004. Either the written title or <see cref="EducationLookupId"/>
    /// must identify the qualification — a row that names nothing is a row
    /// nobody can read afterwards.
    /// </summary>
    public sealed class ApiQualificationRequest
    {
        public string TitleAr { get; set; } = string.Empty;

        public string TitleEn { get; set; } = string.Empty;

        public DateTime DateAwarded { get; set; }

        public bool IsTeachingRelevant { get; set; }

        public string? InstitutionName { get; set; }

        public int? DocumentAttachmentId { get; set; }

        public int? EducationLookupId { get; set; }

        public int? UniversityLookupId { get; set; }

        public int? SpecializationLookupId { get; set; }

        public int? AcademicGradeLookupId { get; set; }

        public decimal? Gpa { get; set; }
    }

    /// <summary>مسير الرواتب الشهري — one month's payroll register.</summary>
    public sealed class ApiPayrollRegister
    {
        public int RunId { get; set; }

        public string RunNo { get; set; } = string.Empty;

        public int PeriodYear { get; set; }

        public int PeriodMonth { get; set; }

        public DateTime PaymentDate { get; set; }

        /// <summary>Draft / Approved / Paid / Cancelled — the run's own vocabulary.</summary>
        public string Status { get; set; } = string.Empty;

        public string Currency { get; set; } = string.Empty;

        public decimal TotalBasic { get; set; }

        public decimal TotalAllowances { get; set; }

        public decimal TotalAdditions { get; set; }

        public decimal TotalDeductions { get; set; }

        public decimal TotalAdvanceDeduction { get; set; }

        public decimal TotalGross { get; set; }

        public decimal TotalNet { get; set; }

        public IReadOnlyList<ApiPayrollRegisterLine> Lines { get; set; } = Array.Empty<ApiPayrollRegisterLine>();
    }

    /// <summary>One employee's line on the register.</summary>
    public sealed class ApiPayrollRegisterLine
    {
        public int LineId { get; set; }

        public int EmployeeId { get; set; }

        public string EmployeeNo { get; set; } = string.Empty;

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        public decimal BasicSalary { get; set; }

        public decimal Allowances { get; set; }

        public decimal AdditionsTotal { get; set; }

        public decimal DeductionsTotal { get; set; }

        public decimal AdvanceDeduction { get; set; }

        public decimal GrossPay { get; set; }

        public decimal NetPay { get; set; }
    }

    /// <summary>قسيمة راتب الموظف — one payslip, every figure broken out.</summary>
    public sealed class ApiPayslip
    {
        public int LineId { get; set; }

        public int RunId { get; set; }

        public string RunNo { get; set; } = string.Empty;

        public int PeriodYear { get; set; }

        public int PeriodMonth { get; set; }

        public DateTime PaymentDate { get; set; }

        public string RunStatus { get; set; } = string.Empty;

        public int EmployeeId { get; set; }

        public string EmployeeNo { get; set; } = string.Empty;

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        /// <summary>Where the money went — the commonest question a payslip is handed back with.</summary>
        public string? BankName { get; set; }

        public string? BankAccountNo { get; set; }

        public string Currency { get; set; } = string.Empty;

        public decimal BasicSalary { get; set; }

        public decimal Allowances { get; set; }

        public decimal AdditionsTotal { get; set; }

        public decimal DeductionsTotal { get; set; }

        public decimal AdvanceDeduction { get; set; }

        public decimal GrossPay { get; set; }

        public decimal NetPay { get; set; }

        public string? Notes { get; set; }

        public IReadOnlyList<ApiPayslipAdjustment> Adjustments { get; set; } = Array.Empty<ApiPayslipAdjustment>();

        public IReadOnlyList<ApiPayslipAdvanceInstallment> AdvanceInstallments { get; set; } = Array.Empty<ApiPayslipAdvanceInstallment>();
    }

    /// <summary>A hand-entered addition or deduction.</summary>
    public sealed class ApiPayslipAdjustment
    {
        /// <summary>Addition / Deduction.</summary>
        public string Kind { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public decimal Amount { get; set; }
    }

    /// <summary>The advance instalment this month recovered, named so the employee can check it.</summary>
    public sealed class ApiPayslipAdvanceInstallment
    {
        public string AdvanceNo { get; set; } = string.Empty;

        public int SequenceNo { get; set; }

        public int InstallmentCount { get; set; }

        public decimal Amount { get; set; }

        public decimal RemainingAfterThis { get; set; }
    }
}
