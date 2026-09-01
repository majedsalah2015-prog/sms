using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Learning
{
    /// <summary>
    /// lrn.Homework (doc/Modules/37 §7, BR-LRN-001/003/004/016): work set to one
    /// named section against a <c>CurriculumOffering</c> — never a raw Subject,
    /// so it is year-correct by construction (BR-SUB-002/005).
    ///
    /// <para>
    /// Where a <see cref="Lesson"/> is anchored on the offering alone, homework
    /// names the <see cref="SectionId"/> it is set to. That difference is
    /// BR-LRN-002's: authoring content needs reach over the offering, issuing
    /// work needs the (offering, section) pair the teacher actually stands in
    /// front of.
    /// </para>
    ///
    /// <para>
    /// BR-LRN-004: <see cref="MaxMarks"/> is optional and it is what the row
    /// means. Null is ungraded practice that never reaches Module 17; set, the
    /// homework is graded and must name the <see cref="BlueprintComponentId"/>
    /// it will feed <em>before</em> it is issued — a mark with nowhere to land
    /// is discovered at release, which is far too late.
    /// </para>
    ///
    /// No <c>IActivatable</c>: <see cref="Status"/> is the lifecycle, following
    /// <see cref="Lesson"/>, <c>Marksheet</c> and <c>TimetableVersion</c>. T2 per
    /// BR-LRN-015 — this is a definition, so it is field-level audited; the
    /// marks it later carries are Module 17's T1 concern, not this row's.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class Homework : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        /// <summary>BR-LRN-001: the anchor. Never a raw SubjectId.</summary>
        public int CurriculumOfferingId { get; set; }

        /// <summary>BR-LRN-002: work is issued to one named section, not to an offering at large.</summary>
        public int SectionId { get; set; }

        public string TitleAr { get; set; } = string.Empty;

        public string TitleEn { get; set; } = string.Empty;

        public string? InstructionsAr { get; set; }

        public string? InstructionsEn { get; set; }

        /// <summary>BR-LRN-004: inside the academic year (BR-GLB-051) and on a working day (BR-GLB-052). Validated against the school calendar before issue.</summary>
        public DateTime DueDate { get; set; }

        /// <summary>BR-LRN-004: null = ungraded practice, which is legitimate and never reaches Module 17.</summary>
        public decimal? MaxMarks { get; set; }

        /// <summary>BR-LRN-004/012: required once <see cref="MaxMarks"/> is set — the Module 17 component this homework's marks will feed.</summary>
        public int? BlueprintComponentId { get; set; }

        /// <summary>BR-LRN-005: how late work is treated. Never whether it is accepted — it always is.</summary>
        public LatenessPolicy LatenessPolicy { get; set; } = LatenessPolicy.AcceptWithoutPenalty;

        /// <summary>BR-LRN-005: the penalty percentage when <see cref="LatenessPolicy"/> is <c>AcceptWithPenalty</c>.</summary>
        public decimal? LatePenaltyPercent { get; set; }

        public HomeworkStatus Status { get; set; } = HomeworkStatus.Draft;

        /// <summary>BR-LRN-003: set on issue — the moment the section's families see it and notifications may fire (§12 <c>HomeworkPublished</c>).</summary>
        public DateTime? IssuedAtUtc { get; set; }

        /// <summary>BR-LRN-016: withdrawing states why, because anyone who already submitted is told (§12 <c>HomeworkWithdrawn</c>).</summary>
        public string? WithdrawnReason { get; set; }

        public DateTime? WithdrawnAtUtc { get; set; }
    }
}
