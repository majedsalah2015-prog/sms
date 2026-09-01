using System;
using System.Collections.Generic;
using Sms.Domain.Common;
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

        /// <summary>
        /// True once the mapping form itself has been posted, as opposed to the file having only
        /// just been read.
        /// <para>
        /// It exists so that "— none —" means none. The screen guesses the mapping from the column
        /// names, and a guess that re-ran on every post would fill any field the operator had just
        /// emptied — leaving a wrong guess re-pointable but never clearable, which on this screen
        /// is the difference between an import that can be corrected and one that cannot.
        /// </para>
        /// </summary>
        public bool MappingChosen { get; set; }

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

        /// <summary>رقم الجوال — the staff register's own contact column, which is where the
        /// directory's mobile comes from for everyone already employed.</summary>
        public string? MobileColumn { get; set; }

        /// <summary>
        /// رقم الواتس اب. A column of its own rather than a copy of the mobile, for the reason
        /// <c>Employee.WhatsAppNumber</c> exists at all: plenty of staff carry a second line for it,
        /// and a register that lists both is the only place that difference is written down.
        /// </summary>
        public string? WhatsAppColumn { get; set; }

        /// <summary>العنوان — one written line, as the register holds it.</summary>
        public string? AddressColumn { get; set; }

        /// <summary>البلدة الأصلية — a different question from where the employee lives now.</summary>
        public string? OriginTownColumn { get; set; }

        /// <summary>رقم هوية الزوج/الزوجة. Its document type is chosen once for the file, below.</summary>
        public string? SpouseIdNoColumn { get; set; }

        public string? PalPayWalletColumn { get; set; }

        public string? JawwalPayWalletColumn { get; set; }

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
        /// The document type a spouse's ID number is recorded against, chosen once for the file.
        /// A staff register writes the number and never the kind of document it came from, and the
        /// kind is the same for every row of one school's register — so asking once is the whole
        /// question, and asking per row would be asking a question the file cannot answer.
        /// </summary>
        public int? SpouseIdTypeLookupId { get; set; }

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

        /// <summary>
        /// What a row gets when its gender cell is empty or says something unreadable. Left unset
        /// the row is skipped instead, which is the behaviour to keep unless a school's list simply
        /// does not record it — plenty of Excel staff lists carry a name, a job and a phone number
        /// and nothing else, and refusing all of them over a column the file never had is refusing
        /// the import rather than protecting the data. Set, every row it applies to is marked in the
        /// preview, so what was assumed is on the screen before it is written.
        /// </summary>
        public Gender? DefaultGender { get; set; }

        /// <summary>
        /// The same for the birth date, and the one field on this screen worth hesitating over: a
        /// date this system invents is indistinguishable afterwards from one the school supplied.
        /// It is offered because <c>Employee.DateOfBirth</c> has no "unknown" and the alternative is
        /// losing the employee entirely — never as a default, and always noted per row.
        /// </summary>
        public DateTime? DefaultDateOfBirth { get; set; }

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
            string? DateOfBirth, string? Gender, string? IdNumber, string? Mobile, bool IsActive,
            string? MaritalStatus, string? BankName, string? BankAccountNo,
            string? HireDate, string? ContractType, decimal? Salary,
            string? Position, string? Qualification, string? University, string? GraduationDate,
            string? WhatsApp, string? Address, string? OriginTown, string? SpouseIdNo,
            string? PalPayWalletNo, string? JawwalPayWalletNo,
            string? Problem, IReadOnlyList<string> Notes);
    }
}
