using System;
using System.Collections.Generic;
using Sms.Domain.Common;
using Sms.Domain.Grades;
using Sms.Domain.Schools;
using Sms.Domain.Sections;

namespace Sms.Web.Models
{
    /// <summary>
    /// doc/Modules/06 §8.3 — one grade's sections side by side with every student in
    /// them, plus the column of students who are in none. The board is the only screen
    /// in the product where a registrar can see a whole grade's distribution at once,
    /// which is the thing they are actually deciding about.
    /// </summary>
    public sealed class SectionBoardViewModel
    {
        /// <summary>A student card. Gender travels with it because the board validates gender live, before anything is posted.</summary>
        public sealed record Card(int EnrollmentId, string StudentNo, string NameAr, string NameEn, Gender Gender);

        /// <summary>One section column, with the two numbers a reader compares: how full it is and how full it may get.</summary>
        public sealed record Column(Section Section, int Capacity, IReadOnlyList<Card> Students, string? HomeroomTeacher, string? RoomName);

        public sealed record GradeOption(int ProfileId, string NameAr, string NameEn, int Sections, int Students);

        public IReadOnlyList<AcademicYear> Years { get; set; } = Array.Empty<AcademicYear>();

        public AcademicYear? Year { get; set; }

        public IReadOnlyList<GradeOption> Grades { get; set; } = Array.Empty<GradeOption>();

        public GradeOption? Grade { get; set; }

        public GenderPolicy GradeGenderPolicy { get; set; } = GenderPolicy.Mixed;

        public IReadOnlyList<Column> Columns { get; set; } = Array.Empty<Column>();

        /// <summary>Enrollments of this grade sitting in no section at all — the column that must be empty after rollover (doc/Modules/06 §11).</summary>
        public IReadOnlyList<Card> Unassigned { get; set; } = Array.Empty<Card>();

        /// <summary>
        /// A proposal waiting for a human to confirm or discard (BR-SCN-008: rules
        /// propose, humans confirm). Null on a plain page load.
        /// </summary>
        public BoardProposalViewModel? Proposal { get; set; }

        public string? ReasonCode { get; set; }

        public DateTime EffectiveDate { get; set; }

        /// <summary>BR-SCN-005's reason codes. The stored value is the English code; the label is chosen at render time.</summary>
        public static readonly string[] ReasonCodes = { "balancing", "behavioral", "parent-request", "medical" };

        public static string ReasonLabel(string? code, bool arabic) => (code ?? string.Empty).ToLowerInvariant() switch
        {
            "balancing" => arabic ? "موازنة الأعداد" : "Balancing",
            "behavioral" => arabic ? "سلوكي" : "Behavioural",
            "parent-request" => arabic ? "طلب ولي الأمر" : "Parent request",
            "medical" => arabic ? "طبي" : "Medical",
            _ => code ?? string.Empty,
        };
    }

    /// <summary>
    /// doc/Modules/06 §8.5 — the merge/close wizard. Closing a section is not a button;
    /// it is a decision about where thirty children go, taken with the cost in view.
    /// The screen puts the impact and the target mapping on one page because they are
    /// the same decision, and reading the second without the first is how a section
    /// gets closed on top of a published timetable.
    /// </summary>
    public sealed class SectionCloseViewModel
    {
        public sealed record MemberRow(int EnrollmentId, string StudentNo, string Name, Gender Gender, int? SuggestedSectionId);

        public sealed record TargetOption(int SectionId, string Name, int Current, int Capacity, GenderPolicy GenderPolicy);

        public sealed record Impact(string LabelEn, string LabelAr, int Count, bool Blocking);

        public Section Section { get; set; } = null!;

        public GradeLevel Grade { get; set; } = null!;

        public AcademicYear Year { get; set; } = null!;

        public IReadOnlyList<MemberRow> Members { get; set; } = Array.Empty<MemberRow>();

        public IReadOnlyList<TargetOption> Targets { get; set; } = Array.Empty<TargetOption>();

        public IReadOnlyList<Impact> Impacts { get; set; } = Array.Empty<Impact>();

        /// <summary>Students the sibling sections have no compatible room for — the wizard cannot close until they have somewhere to go.</summary>
        public IReadOnlyList<MemberRow> Unplaceable { get; set; } = Array.Empty<MemberRow>();

        public string? HomeroomTeacher { get; set; }

        public string? ReasonCode { get; set; } = "balancing";

        public DateTime EffectiveDate { get; set; }
    }

    /// <summary>
    /// The proposal diff (doc/Modules/06 §8.3 "proposal diff view"). It shows moves and
    /// the headcount each section ends at, because "seven moves" says nothing about
    /// whether the grade finishes level — which is the only reason to press confirm.
    /// </summary>
    public sealed class BoardProposalViewModel
    {
        public sealed record Row(int EnrollmentId, string StudentNo, string StudentName, string? FromSection, string ToSection, int ToSectionId);

        public sealed record Tally(string SectionName, int Before, int After, int Capacity);

        public IReadOnlyList<Row> Moves { get; set; } = Array.Empty<Row>();

        public IReadOnlyList<Tally> Tallies { get; set; } = Array.Empty<Tally>();

        /// <summary>Students no compatible section had room for — reported, never force-placed (BR-SCN-002).</summary>
        public IReadOnlyList<Row> Unplaced { get; set; } = Array.Empty<Row>();

        /// <summary>True when the run was asked to move students who already had a section, not only to seat those who had none.</summary>
        public bool Rebalanced { get; set; }

        /// <summary>The placement to post if the human confirms: "enrollmentId:sectionId" pairs, comma separated.</summary>
        public string Payload { get; set; } = string.Empty;
    }
}
