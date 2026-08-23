using System;
using System.Collections.Generic;
using Sms.Domain.Employees;

namespace Sms.Web.Models
{
    /// <summary>
    /// Bringing a school's existing staff register across (owner request, 2026-08-23), in the same
    /// three steps the student import uses: choose the file, say which column is which, read the
    /// preview before anything is written.
    /// <para>
    /// Fifteen fields, and they do not all live on the same entity. The name, birth date, gender,
    /// ID number, marital status and bank details are the <c>Employee</c>; the hire date, contract
    /// type and salary are a <c>Contract</c>; the job title is an <c>EmployeeAssignment</c>; the
    /// qualification, university and graduation date are a <c>Qualification</c>. One imported row
    /// therefore becomes up to four records, and the preview says which of them each row will
    /// produce — a register that has a salary column but no hire date cannot have a contract, and
    /// the operator should learn that before the import and not after it.
    /// </para>
    /// </summary>
    public sealed class EmployeeImportViewModel
    {
        /// <summary>Names the uploaded copy on the server, so the later steps do not re-upload it.</summary>
        public string? Token { get; set; }

        public string? OriginalFileName { get; set; }

        public IReadOnlyList<string> Tables { get; set; } = Array.Empty<string>();

        public string? Table { get; set; }

        public IReadOnlyList<string> Columns { get; set; } = Array.Empty<string>();

        // ---- the mapping: the fifteen columns, grouped by the record each one ends up in.

        // Employee — identity
        public string? FirstNameColumn { get; set; }

        public string? FatherNameColumn { get; set; }

        public string? GrandfatherNameColumn { get; set; }

        public string? FamilyNameColumn { get; set; }

        /// <summary>Optional: one column holding the whole name, split on spaces when the parts are not separate.</summary>
        public string? FullNameColumn { get; set; }

        public string? IdNumberColumn { get; set; }

        public string? DateOfBirthColumn { get; set; }

        public string? GenderColumn { get; set; }

        /// <summary>فعال — anything the cell can say for "yes"; a blank column means every row is active.</summary>
        public string? ActiveColumn { get; set; }

        public string? MaritalStatusColumn { get; set; }

        public string? BankNameColumn { get; set; }

        public string? BankAccountColumn { get; set; }

        // Contract
        public string? HireDateColumn { get; set; }

        public string? ContractTypeColumn { get; set; }

        public string? SalaryColumn { get; set; }

        // Assignment
        public string? PositionColumn { get; set; }

        // Qualification
        public string? QualificationColumn { get; set; }

        public string? UniversityColumn { get; set; }

        public string? GraduationDateColumn { get; set; }

        // ---- what every imported row gets, chosen once by the operator

        public int? NationalityLookupId { get; set; }

        public int? IdTypeLookupId { get; set; }

        /// <summary>
        /// The unit an imported employee is assigned to. Required only when a job-title column is
        /// mapped: an assignment without a unit is not a thing this model can store.
        /// </summary>
        public int? OrgUnitId { get; set; }

        /// <summary>
        /// A contract needs an end date and a staff register never has one. Asked once here rather
        /// than invented per row — a date this system made up would be indistinguishable from one
        /// the school agreed to, and it is the date the expiry alerts fire on (BR-EMP-003).
        /// </summary>
        public DateTime? ContractEndDate { get; set; }

        /// <summary>Used when the register has no contract-type column, or the cell is unreadable.</summary>
        public ContractType DefaultContractType { get; set; } = ContractType.FullTime;

        public IReadOnlyList<(int Id, string Ar, string En)> Nationalities { get; set; } = Array.Empty<(int, string, string)>();

        public IReadOnlyList<(int Id, string Ar, string En)> IdTypes { get; set; } = Array.Empty<(int, string, string)>();

        public IReadOnlyList<(int Id, string Ar, string En)> Positions { get; set; } = Array.Empty<(int, string, string)>();

        public IReadOnlyList<(int Id, string Ar, string En)> OrgUnits { get; set; } = Array.Empty<(int, string, string)>();

        // ---- preview and outcome

        public IReadOnlyList<PreviewRow> Preview { get; set; } = Array.Empty<PreviewRow>();

        public int TotalRows { get; set; }

        public int ReadyRows { get; set; }

        public int SkippedRows { get; set; }

        /// <summary>
        /// One row as it will be written, or the reason it will not be. <see cref="Problem"/> is
        /// the refusal; <see cref="Notes"/> are the parts of the row that will be dropped without
        /// stopping it — an unreadable salary loses the contract, not the employee.
        /// </summary>
        public sealed record PreviewRow(
            int Number,
            string FirstName, string FatherName, string GrandfatherName, string FamilyName,
            string? DateOfBirth, string? Gender, string? IdNumber, bool IsActive,
            string? MaritalStatus, string? BankName, string? BankAccountNo,
            string? HireDate, string? ContractType, decimal? Salary,
            string? Position, string? Qualification, string? University, string? GraduationDate,
            string? Problem, IReadOnlyList<string> Notes);
    }
}
