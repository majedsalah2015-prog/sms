using System;
using System.Collections.Generic;

namespace Sms.Web.Models
{
    /// <summary>
    /// Bringing a school's old Access register across, in three steps on one screen: choose the
    /// file, say which column is which, then read the preview before anything is written.
    /// <para>
    /// The preview is not decoration. An import is the one operation that can put a thousand wrong
    /// rows into a system in a second, and the only honest defence is showing the operator exactly
    /// what the first of them will look like — including the ones that will be refused and why.
    /// </para>
    /// </summary>
    public sealed class StudentImportViewModel
    {
        /// <summary>Names the uploaded copy on the server, so the later steps do not re-upload it.</summary>
        public string? Token { get; set; }

        public string? OriginalFileName { get; set; }

        public IReadOnlyList<string> Tables { get; set; } = Array.Empty<string>();

        public string? Table { get; set; }

        public IReadOnlyList<string> Columns { get; set; } = Array.Empty<string>();

        // ---- the mapping. Only the seven fields the owner asked for; everything else in the old
        // register stays in the old register.

        public string? FirstNameColumn { get; set; }

        public string? FatherNameColumn { get; set; }

        public string? GrandfatherNameColumn { get; set; }

        public string? FamilyNameColumn { get; set; }

        /// <summary>Optional: one column holding the whole quad name, split on spaces when the four are not separate.</summary>
        public string? FullNameColumn { get; set; }

        public string? DateOfBirthColumn { get; set; }

        public string? GenderColumn { get; set; }

        public string? IdNumberColumn { get; set; }

        // ---- what every imported row gets

        public int? NationalityLookupId { get; set; }

        public int? IdTypeLookupId { get; set; }

        public IReadOnlyList<(int Id, string Ar, string En)> Nationalities { get; set; } = Array.Empty<(int, string, string)>();

        public IReadOnlyList<(int Id, string Ar, string En)> IdTypes { get; set; } = Array.Empty<(int, string, string)>();

        // ---- preview and outcome

        public IReadOnlyList<PreviewRow> Preview { get; set; } = Array.Empty<PreviewRow>();

        public int TotalRows { get; set; }

        public int ReadyRows { get; set; }

        public int SkippedRows { get; set; }

        /// <summary>One row as it will be written, or the reason it will not be.</summary>
        public sealed record PreviewRow(
            int Number, string FirstName, string FatherName, string GrandfatherName, string FamilyName,
            string? DateOfBirth, string? Gender, string? IdNumber, string? Problem);
    }
}
