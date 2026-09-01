using System;
using System.Collections.Generic;
using Sms.Domain.Learning;

namespace Sms.Web.Models
{
    /// <summary>
    /// Module 37 §8.1-2 screens. Slice 1 only: the planner and the resource
    /// library. Homework, question banks, papers, sittings and the portal
    /// surfaces are later slices.
    /// </summary>
    public sealed class LessonPlannerViewModel
    {
        public IReadOnlyList<OfferingOption> Offerings { get; init; } = Array.Empty<OfferingOption>();

        /// <summary>Null until the teacher picks one — the planner is meaningless without an offering.</summary>
        public int? SelectedOfferingId { get; init; }

        public IReadOnlyList<WeekGroup> Weeks { get; init; } = Array.Empty<WeekGroup>();

        /// <summary>Dated sessions of the selected offering, for BR-LRN-001's optional bind.</summary>
        public IReadOnlyList<SessionOption> Sessions { get; init; } = Array.Empty<SessionOption>();

        public bool CanCreate { get; init; }

        public bool CanEdit { get; init; }

        public bool CanPublish { get; init; }

        public bool CanRetire { get; init; }

        /// <summary>
        /// True when the signed-in user reaches no offering at all. The empty
        /// state then explains BR-LRN-002 rather than showing an empty picker
        /// that looks broken.
        /// </summary>
        public bool HasNoReach => Offerings.Count == 0;
    }

    public sealed record OfferingOption(int Id, string Label);

    public sealed record SessionOption(int Id, DateTime Date, string Label);

    public sealed class WeekGroup
    {
        public int WeekNumber { get; init; }

        public IReadOnlyList<LessonRow> Lessons { get; init; } = Array.Empty<LessonRow>();
    }

    /// <summary>
    /// <paramref name="Title"/> and <paramref name="Objectives"/> are already
    /// resolved to the reader's language for display. The raw pairs travel
    /// alongside them because the edit form has to post BOTH languages back —
    /// filling both inputs from the resolved value would quietly overwrite the
    /// other language with a copy of this one, and BR-GLB-001 requires the pair.
    /// </summary>
    public sealed record LessonRow(
        int Id,
        int WeekNumber,
        string Title,
        string? Objectives,
        string TitleAr,
        string TitleEn,
        string? ObjectivesAr,
        string? ObjectivesEn,
        LessonStatus Status,
        DateTime? PublishedAtUtc,
        DateTime? SessionDate,
        int? SessionId,
        string? RetiredReason,
        int ResourceCount);

    public sealed class LessonResourcesViewModel
    {
        public int LessonId { get; init; }

        public string LessonTitle { get; init; } = string.Empty;

        public LessonStatus LessonStatus { get; init; }

        public int OfferingId { get; init; }

        public IReadOnlyList<ResourceRow> Resources { get; init; } = Array.Empty<ResourceRow>();

        /// <summary>
        /// The document types this library files under (doc 10). One live
        /// attachment per (lesson, type) — a re-upload of the same type is a new
        /// version of that document, which is what §8.2 means by "versioned".
        /// </summary>
        public IReadOnlyList<ResourceTypeOption> Types { get; init; } = Array.Empty<ResourceTypeOption>();

        public bool CanUpload { get; init; }

        public bool CanWithdraw { get; init; }
    }

    public sealed record ResourceTypeOption(string Code, string Label);

    /// <summary>
    /// <paramref name="IsServable"/> is BR-LRN-006: an unscanned or infected
    /// file is listed but never handed over, and the row says which it is rather
    /// than failing silently at the download.
    /// </summary>
    public sealed record ResourceRow(
        int Id,
        string Title,
        string TypeLabel,
        int AttachmentId,
        int DisplayOrder,
        bool IsServable,
        string ScanStateLabel);

    /// <summary>
    /// doc/Modules/37 §8.2 read across an offering rather than one lesson — the
    /// screen a teacher needs to answer "what material is filed against this
    /// subject, and where are the worksheets?".
    /// </summary>
    public sealed class MaterialsLibraryViewModel
    {
        public IReadOnlyList<OfferingOption> Offerings { get; init; } = Array.Empty<OfferingOption>();

        /// <summary>Null until an offering is chosen — a library with no subject is not a shorter list, it is a different question.</summary>
        public int? SelectedOfferingId { get; set; }

        /// <summary>Null means "every kind". Only a code the type list knows survives binding.</summary>
        public string? SelectedTypeCode { get; init; }

        /// <summary>
        /// Every kind with its count in the chosen offering, counted <em>before</em>
        /// the filter — so a chip reading zero tells the teacher nothing of that
        /// kind is filed, rather than disappearing and telling them nothing.
        /// </summary>
        public IReadOnlyList<MaterialTypeFilter> Types { get; set; } = Array.Empty<MaterialTypeFilter>();

        public IReadOnlyList<MaterialRow> Materials { get; set; } = Array.Empty<MaterialRow>();

        /// <summary>Material in the offering irrespective of the type filter — lets the empty state say which of the two emptinesses this is.</summary>
        public int TotalInOffering { get; set; }

        /// <summary>BR-LRN-002: the teacher holds no placement and heads no department.</summary>
        public bool HasNoReach { get; init; }

        public bool CanUpload { get; init; }
    }

    public sealed record MaterialTypeFilter(string Code, string Label, int Count);

    /// <summary>
    /// One filed item. Carries its lesson because the library's whole purpose is
    /// to be read away from the lesson — a row that could not say which week it
    /// belongs to would send the teacher back to the planner to find out.
    /// </summary>
    public sealed record MaterialRow(
        int ResourceId,
        int LessonId,
        int WeekNumber,
        string LessonTitle,
        LessonStatus LessonStatus,
        string Title,
        string TypeLabel,
        bool IsServable,
        string ScanStateLabel,
        int DisplayOrder);
}
