using System;
using System.Collections.Generic;
using Sms.Web.Models;

namespace Sms.Web.Api.Models
{
    /// <summary>
    /// One (offering, section) pair the caller may author against — BR-LRN-002
    /// reach, resolved by the port so the app's pickers offer only what the
    /// server will accept.
    /// </summary>
    public sealed class ApiTeachingReach
    {
        public int CurriculumOfferingId { get; set; }

        public int? SectionId { get; set; }

        public string SubjectNameAr { get; set; } = string.Empty;

        public string SubjectNameEn { get; set; } = string.Empty;

        public string? SectionName { get; set; }

        public string? GradeCode { get; set; }
    }

    /// <summary>doc/Modules/37 §8.1 — one lesson on the planner.</summary>
    public sealed class ApiLesson
    {
        public int LessonId { get; set; }

        public int CurriculumOfferingId { get; set; }

        public int? SessionId { get; set; }

        public int WeekNumber { get; set; }

        public string TitleAr { get; set; } = string.Empty;

        public string TitleEn { get; set; } = string.Empty;

        public string? ObjectivesAr { get; set; }

        public string? ObjectivesEn { get; set; }

        public string SubjectNameAr { get; set; } = string.Empty;

        public string SubjectNameEn { get; set; } = string.Empty;

        /// <summary>Draft / Published / Retired. BR-LRN-003: only Published reaches a family.</summary>
        public string Status { get; set; } = string.Empty;

        public DateTime? PublishedAtUtc { get; set; }

        public string? RetiredReason { get; set; }

        public IReadOnlyList<ApiLessonResource> Resources { get; set; } = Array.Empty<ApiLessonResource>();
    }

    /// <summary>
    /// doc/Modules/37 §8.2 — one item of material on a lesson, as the teacher
    /// sees it. Unlike the portal's view this one lists material whose scan has
    /// not cleared, because the teacher who uploaded it needs to know that.
    /// </summary>
    public sealed class ApiLessonResource
    {
        public int ResourceId { get; set; }

        public int AttachmentId { get; set; }

        public string TitleAr { get; set; } = string.Empty;

        public string TitleEn { get; set; } = string.Empty;

        public int DisplayOrder { get; set; }

        /// <summary>BR-LRN-006. False means the file exists but is not being served to anyone yet.</summary>
        public bool IsScanClean { get; set; }

        public string DownloadUrl { get; set; } = string.Empty;
    }

    /// <summary>doc/Modules/37 §8.3 — one piece of work on the homework desk.</summary>
    public sealed class ApiHomework
    {
        public int HomeworkId { get; set; }

        public int CurriculumOfferingId { get; set; }

        public int SectionId { get; set; }

        public string TitleAr { get; set; } = string.Empty;

        public string TitleEn { get; set; } = string.Empty;

        public string? InstructionsAr { get; set; }

        public string? InstructionsEn { get; set; }

        public string SubjectNameAr { get; set; } = string.Empty;

        public string SubjectNameEn { get; set; } = string.Empty;

        public string? SectionName { get; set; }

        public DateTime DueDate { get; set; }

        /// <summary>BR-LRN-004: null is ungraded practice, and must stay null unless a blueprint component is named.</summary>
        public decimal? MaxMarks { get; set; }

        public int? BlueprintComponentId { get; set; }

        /// <summary>Refuse / AcceptWithoutPenalty / AcceptWithPenalty.</summary>
        public string LatenessPolicy { get; set; } = string.Empty;

        public decimal? LatePenaltyPercent { get; set; }

        /// <summary>Draft / Issued / Released / Withdrawn.</summary>
        public string Status { get; set; } = string.Empty;

        public DateTime? IssuedAtUtc { get; set; }

        public string? WithdrawnReason { get; set; }
    }

    /// <summary>Create a lesson (§8.1). It starts as a Draft and no family can see it.</summary>
    public sealed class ApiCreateLessonRequest
    {
        public int CurriculumOfferingId { get; set; }

        public int WeekNumber { get; set; }

        [RequiredField("Arabic title", "العنوان بالعربية")]
        public string TitleAr { get; set; } = string.Empty;

        [RequiredField("English title", "العنوان بالإنجليزية")]
        public string TitleEn { get; set; } = string.Empty;

        public string? ObjectivesAr { get; set; }

        public string? ObjectivesEn { get; set; }

        /// <summary>BR-LRN-001: when given, the session must actually teach this offering.</summary>
        public int? SessionId { get; set; }
    }

    /// <summary>Edit a Draft or Published lesson in place (BR-LRN-016 covers the heavy path).</summary>
    public sealed class ApiUpdateLessonRequest
    {
        public int WeekNumber { get; set; }

        [RequiredField("Arabic title", "العنوان بالعربية")]
        public string TitleAr { get; set; } = string.Empty;

        [RequiredField("English title", "العنوان بالإنجليزية")]
        public string TitleEn { get; set; } = string.Empty;

        public string? ObjectivesAr { get; set; }

        public string? ObjectivesEn { get; set; }

        public int? SessionId { get; set; }
    }

    /// <summary>BR-LRN-016: withdrawing content states why, because someone read it yesterday.</summary>
    public sealed class ApiReasonRequest
    {
        [RequiredField("reason", "السبب")]
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>Set work for a section (§8.3). Draft — BR-LRN-004 is applied at issue, not here.</summary>
    public sealed class ApiCreateHomeworkRequest
    {
        public int CurriculumOfferingId { get; set; }

        public int SectionId { get; set; }

        [RequiredField("Arabic title", "العنوان بالعربية")]
        public string TitleAr { get; set; } = string.Empty;

        [RequiredField("English title", "العنوان بالإنجليزية")]
        public string TitleEn { get; set; } = string.Empty;

        public DateTime DueDate { get; set; }

        public string? InstructionsAr { get; set; }

        public string? InstructionsEn { get; set; }

        /// <summary>BR-LRN-004: a mark means a blueprint component; no mark means none.</summary>
        public decimal? MaxMarks { get; set; }

        public int? BlueprintComponentId { get; set; }

        /// <summary>Refuse / AcceptWithoutPenalty / AcceptWithPenalty. Defaults to accepting late work without penalty.</summary>
        public string? LatenessPolicy { get; set; }

        public decimal? LatePenaltyPercent { get; set; }
    }

    /// <summary>Edit work. Allowed after issue on purpose — a typo the morning after is ordinary.</summary>
    public sealed class ApiUpdateHomeworkRequest
    {
        [RequiredField("Arabic title", "العنوان بالعربية")]
        public string TitleAr { get; set; } = string.Empty;

        [RequiredField("English title", "العنوان بالإنجليزية")]
        public string TitleEn { get; set; } = string.Empty;

        public DateTime DueDate { get; set; }

        public string? InstructionsAr { get; set; }

        public string? InstructionsEn { get; set; }

        public decimal? MaxMarks { get; set; }

        public int? BlueprintComponentId { get; set; }

        public string? LatenessPolicy { get; set; }

        public decimal? LatePenaltyPercent { get; set; }
    }

    /// <summary>Attach an already-uploaded document to a lesson (§8.2).</summary>
    public sealed class ApiAttachResourceRequest
    {
        /// <summary>A doc.Attachment id. The attachment pipeline owns typing, size and scanning.</summary>
        public int AttachmentId { get; set; }

        [RequiredField("Arabic title", "العنوان بالعربية")]
        public string TitleAr { get; set; } = string.Empty;

        [RequiredField("English title", "العنوان بالإنجليزية")]
        public string TitleEn { get; set; } = string.Empty;

        public int DisplayOrder { get; set; }
    }
}
