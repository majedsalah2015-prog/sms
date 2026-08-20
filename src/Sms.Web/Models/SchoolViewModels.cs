using System;
using System.Collections.Generic;
using Sms.Application.Rollover;
using Sms.Domain.Rollover;
using Sms.Domain.Schools;

namespace Sms.Web.Models
{
    /// <summary>doc/Modules/02 §8.1 School profile (tabs: Identity, License & Ministry, Contacts & Location).</summary>
    public sealed class SchoolProfileViewModel
    {
        public int? SchoolId { get; set; }

        public string? NameAr { get; set; }

        public string? NameEn { get; set; }

        public string? LicenseNumber { get; set; }

        public string? MinistryCode { get; set; }

        public DateTime? LicenseExpiryDate { get; set; }

        public string? AddressLine { get; set; }

        public string? City { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        public string? ContactEmail { get; set; }

        public string? ContactPhone { get; set; }

        public string? Website { get; set; }

        public string? TimeZoneId { get; set; }

        public string? CurrencyCode { get; set; }

        public SchoolStatus? Status { get; set; }

        /// <summary>BR-SCH-002: identity edits require a reason.</summary>
        public string? Reason { get; set; }

        public string ActiveTab { get; set; } = "identity";

        public IReadOnlyList<(string Ar, string En, int Grades)> StagesOffered { get; set; } = Array.Empty<(string, string, int)>();
    }

    /// <summary>doc/Modules/02 §8.2 Signatories per document class with history (BR-SCH-004).</summary>
    public sealed class SignatoriesViewModel
    {
        public static readonly (string Code, string En, string Ar)[] DocumentClasses =
        {
            ("CERT", "Certificates", "الشهادات"),
            ("FIN", "Financial documents", "المستندات المالية"),
            ("LETTER", "Official letters", "الخطابات الرسمية"),
            ("REPORT", "Report cards", "كشوف الدرجات"),
        };

        public IReadOnlyList<Signatory> Signatories { get; set; } = Array.Empty<Signatory>();

        public string? DocumentClassCode { get; set; }

        public string? NameAr { get; set; }

        public string? NameEn { get; set; }

        public string? TitleAr { get; set; }

        public string? TitleEn { get; set; }

        public DateTime? EffectiveFrom { get; set; }
    }

    /// <summary>doc/Modules/02 §8.3 School status console with reason capture (BR-SCH-005).</summary>
    public sealed class SchoolStatusViewModel
    {
        public School? School { get; set; }

        public IReadOnlyList<SchoolStatus> AllowedTargets { get; set; } = Array.Empty<SchoolStatus>();

        public SchoolStatus? Target { get; set; }

        public string? Reason { get; set; }
    }

    /// <summary>doc/Modules/03 §8.1 Year list & status board.</summary>
    public sealed class YearBoardViewModel
    {
        public sealed class Row
        {
            public AcademicYear Year { get; set; } = null!;

            public int Semesters { get; set; }

            public int Terms { get; set; }

            /// <summary>Student enrollments in this year; edit/delete are offered only when zero.</summary>
            public int Enrollments { get; set; }

            public bool CanEditOrDelete => Enrollments == 0;

            public RolloverBatch? IncomingBatch { get; set; }

            public IReadOnlyList<ChecklistItem> OpeningChecklist { get; set; } = Array.Empty<ChecklistItem>();

            public RolloverBatch? OutgoingBatch { get; set; }

            public IReadOnlyList<ChecklistItem> ClosingChecklist { get; set; } = Array.Empty<ChecklistItem>();
        }

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        public int WorkingYearId { get; set; }

        public AcademicYear? WorkingYear { get; set; }

        public AcademicYear? ActiveYear { get; set; }

        public bool SetupComplete { get; set; }
    }

    /// <summary>doc/Modules/03 §8.2 Year definition + semester/term builder.</summary>
    public sealed class YearDefinitionViewModel
    {
        public int? YearId { get; set; }

        public AcademicYear? Year { get; set; }

        public string? LabelAr { get; set; }

        public string? LabelEn { get; set; }

        public string? HijriLabel { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        /// <summary>Optional audit reason for edits (T2 audit — not mandatory).</summary>
        public string? Reason { get; set; }

        public IReadOnlyList<Semester> Semesters { get; set; } = Array.Empty<Semester>();

        public IReadOnlyList<Term> Terms { get; set; } = Array.Empty<Term>();

        // semester form
        public int? SemesterSequence { get; set; }

        public string? SemesterNameAr { get; set; }

        public string? SemesterNameEn { get; set; }

        public DateTime? SemesterStart { get; set; }

        public DateTime? SemesterEnd { get; set; }

        // term form
        public int? TermSemesterId { get; set; }

        public int? TermSequence { get; set; }

        public string? TermNameAr { get; set; }

        public string? TermNameEn { get; set; }

        public DateTime? TermStart { get; set; }

        public DateTime? TermEnd { get; set; }
    }
}
