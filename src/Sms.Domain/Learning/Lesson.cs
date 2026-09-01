using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Learning
{
    /// <summary>
    /// lrn.Lesson (doc/Modules/37 §7, BR-LRN-001/003/016): one planned unit of
    /// teaching against a <c>CurriculumOffering</c> — never a raw Subject, so a
    /// lesson is year-correct by construction (BR-SUB-002/005).
    ///
    /// BR-LRN-001: <see cref="SessionId"/> is optional and it is what the row
    /// means. Unbound, this is a syllabus entry for a week; bound to a dated
    /// timetable session it is "what happened that period". An offering that is
    /// later end-dated (BR-SUB-004) keeps its lessons readable — content is
    /// never orphaned by a curriculum change.
    ///
    /// No <c>IActivatable</c>: the status enum is the lifecycle, following
    /// <c>Marksheet</c> and <c>TimetableVersion</c>. T2 per BR-LRN-015 —
    /// definitions are field-level audited; only marks are T1.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class Lesson : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        /// <summary>BR-LRN-001: the anchor. Never a raw SubjectId.</summary>
        public int CurriculumOfferingId { get; set; }

        /// <summary>BR-LRN-001: null = syllabus entry; set = bound to a dated Module 15 session.</summary>
        public int? SessionId { get; set; }

        /// <summary>doc/Modules/37 §8.1 — the planner is an offering x week grid.</summary>
        public int WeekNumber { get; set; }

        public string TitleAr { get; set; } = string.Empty;

        public string TitleEn { get; set; } = string.Empty;

        public string? ObjectivesAr { get; set; }

        public string? ObjectivesEn { get; set; }

        public LessonStatus Status { get; set; } = LessonStatus.Draft;

        /// <summary>BR-LRN-003: set on publication — the moment the lesson becomes visible in the portal and the moment notifications may fire.</summary>
        public DateTime? PublishedAtUtc { get; set; }

        /// <summary>BR-LRN-016: retiring states why, because a student who saw the lesson yesterday will ask.</summary>
        public string? RetiredReason { get; set; }

        public DateTime? RetiredAtUtc { get; set; }
    }
}
