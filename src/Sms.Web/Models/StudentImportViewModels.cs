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

        /// <summary>
        /// How many rows each table holds, shown beside its name in the pickers. Missing for a table
        /// whose count could not be taken, which is why it is a lookup rather than a parallel list:
        /// a name with no size is a name shown without one, never a name left out.
        /// </summary>
        public IReadOnlyDictionary<string, int> TableRowCounts { get; set; } = new Dictionary<string, int>();

        public string? Table { get; set; }

        public IReadOnlyList<string> Columns { get; set; } = Array.Empty<string>();

        /// <summary>
        /// True once the mapping below has been shown to the operator, so what comes back is their
        /// answer rather than an unanswered form.
        /// <para>
        /// It exists so that "— none —" means none. The screen guesses the mapping from the column
        /// names, and the guess re-ran on every post, which re-filled any field the operator had
        /// just emptied: a wrong guess could be pointed somewhere else but never cleared. On a
        /// register whose <c>owner_*</c> columns are a guardian for some schools and a bus driver
        /// for others, that is the difference between an import that can be corrected and one that
        /// cannot.
        /// </para>
        /// </summary>
        public bool MappingChosen { get; set; }

        // ---- the student's own fields. Everything else in the old register stays in the old register.

        public string? FirstNameColumn { get; set; }

        public string? FatherNameColumn { get; set; }

        public string? GrandfatherNameColumn { get; set; }

        public string? FamilyNameColumn { get; set; }

        /// <summary>Optional: one column holding the whole quad name, split on spaces when the four are not separate.</summary>
        public string? FullNameColumn { get; set; }

        public string? DateOfBirthColumn { get; set; }

        public string? GenderColumn { get; set; }

        public string? IdNumberColumn { get; set; }

        /// <summary>مكان الميلاد — free text, stored as the register wrote it.</summary>
        public string? PlaceOfBirthColumn { get; set; }

        /// <summary>
        /// عدد الأخوة. Deliberately not the household-size field: a register that says 0 siblings is
        /// saying the child is an only child, while a household of 0 people is not a fact about
        /// anybody, and Module 22 reads household size for hardship.
        /// </summary>
        public string? SiblingCountColumn { get; set; }

        /// <summary>ترتيب الطالب بين الأخوة, 1 = eldest.</summary>
        public string? BirthOrderColumn { get; set; }

        /// <summary>The student's own line — the guardians' mobiles are mapped separately below.</summary>
        public string? MobileColumn { get; set; }

        // ---- the guardians (owner request, 2026-08-24).
        //
        // Each becomes a Parent row linked to the student by StudentGuardianLink, not a set of columns
        // on the student: an old register holds one line per child, so a family of four arrives as the
        // same father typed four times, and four copies of a man is not four men. The ID number is what
        // says they are the same person (BR-PAR-002's strongest match), which is why it is the one
        // guardian field worth insisting on.

        public string? FatherFullNameColumn { get; set; }

        public string? FatherIdNumberColumn { get; set; }

        public string? FatherOccupationColumn { get; set; }

        public string? FatherMobileColumn { get; set; }

        /// <summary>Free text in the old register; matched against the "EducationLevel" lookup, never invented.</summary>
        public string? FatherEducationColumn { get; set; }

        public string? MotherFullNameColumn { get; set; }

        public string? MotherIdNumberColumn { get; set; }

        public string? MotherOccupationColumn { get; set; }

        public string? MotherMobileColumn { get; set; }

        public string? MotherEducationColumn { get; set; }

        // ---- code tables.
        //
        // An old register does not keep an occupation as "مهندس"; it keeps 12, and a table elsewhere
        // in the same file that says 12 is مهندس. Read the cell without that table and every parent
        // in the school ends up employed as "12". One setting for both parents each, because both
        // columns point at the same table — they always do.

        /// <summary>The table whose rows translate an occupation code into a name; null when the column already holds words.</summary>
        public string? OccupationCodeTable { get; set; }

        /// <summary>The same for the qualification column.</summary>
        public string? EducationCodeTable { get; set; }

        // ---- what every imported row gets

        public int? NationalityLookupId { get; set; }

        public int? IdTypeLookupId { get; set; }

        public IReadOnlyList<(int Id, string Ar, string En)> Nationalities { get; set; } = Array.Empty<(int, string, string)>();

        public IReadOnlyList<(int Id, string Ar, string En)> IdTypes { get; set; } = Array.Empty<(int, string, string)>();

        /// <summary>The catalogue an imported qualification cell is matched against; shown so the operator can see what it can hit.</summary>
        public IReadOnlyList<(int Id, string Ar, string En)> EducationLevels { get; set; } = Array.Empty<(int, string, string)>();

        // ---- preview and outcome

        public IReadOnlyList<PreviewRow> Preview { get; set; } = Array.Empty<PreviewRow>();

        public int TotalRows { get; set; }

        public int ReadyRows { get; set; }

        public int SkippedRows { get; set; }

        /// <summary>How many distinct guardians the mapped columns describe, once the repeats are folded together.</summary>
        public int GuardianCount { get; set; }

        /// <summary>Guardian rows carrying no ID number, which is how many cannot be recognised across siblings.</summary>
        public int GuardiansWithoutId { get; set; }

        /// <summary>Qualification cells nothing in the catalogue answered to; they import blank rather than guessed.</summary>
        public int UnmatchedEducations { get; set; }

        /// <summary>
        /// Which qualifications those were, and how many rows said each — the difference between
        /// "733 blank" and "add جامعي to the Education Level list and 698 of them land". Named
        /// rather than counted, because the fix is two minutes' work only if the operator can see
        /// what to add.
        /// </summary>
        public IReadOnlyList<(string Text, int Rows)> UnmatchedEducationNames { get; set; } = Array.Empty<(string, int)>();

        /// <summary>True once any guardian column is mapped — the guardian half of the preview only appears then.</summary>
        public bool MapsGuardians =>
            FatherFullNameColumn != null || FatherIdNumberColumn != null || FatherOccupationColumn != null
            || FatherMobileColumn != null || FatherEducationColumn != null
            || MotherFullNameColumn != null || MotherIdNumberColumn != null || MotherOccupationColumn != null
            || MotherMobileColumn != null || MotherEducationColumn != null;

        /// <summary>True once any of the student's own extra columns is mapped; the preview grows those columns only then.</summary>
        public bool MapsSocial =>
            PlaceOfBirthColumn != null || SiblingCountColumn != null
            || BirthOrderColumn != null || MobileColumn != null;

        /// <summary>One row as it will be written, or the reason it will not be.</summary>
        public sealed record PreviewRow(
            int Number, string FirstName, string FatherName, string GrandfatherName, string FamilyName,
            string? DateOfBirth, string? Gender, string? IdNumber, string? Problem,
            GuardianCandidate? Father, GuardianCandidate? Mother, SocialCandidate Social);

        /// <summary>
        /// The student's own particulars beyond identity, as the mapped columns read them. Never
        /// null — an unmapped column is a null member, which is what will be stored, rather than an
        /// absent record the view would have to test for.
        /// </summary>
        public sealed record SocialCandidate(
            string? PlaceOfBirth, int? SiblingCount, int? BirthOrder, string? Mobile)
        {
            /// <summary>Nothing was read, so the commit can skip the extra save entirely.</summary>
            public bool IsEmpty =>
                PlaceOfBirth == null && SiblingCount == null && BirthOrder == null && Mobile == null;
        }

        /// <summary>
        /// One guardian as the mapped columns describe them. <paramref name="EducationLookupId"/> is
        /// what will be stored; <paramref name="EducationText"/> is what the cell said, kept so an
        /// unmatched qualification can be shown as the words nobody could place rather than as a blank.
        /// </summary>
        public sealed record GuardianCandidate(
            string Name, string? IdNumber, string? Occupation, string? Mobile,
            int? EducationLookupId, string? EducationText, bool EducationUnmatched);
    }
}
