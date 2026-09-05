using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Learning
{
    /// <summary>
    /// lrn.OnlinePaper (doc/Modules/37 §7, §8.7, BR-LRN-008): a set of questions
    /// drawn from one bank, built to fill exactly one Module 17
    /// <c>BlueprintComponent</c>.
    ///
    /// <para>
    /// <b>The component is the point, not a detail.</b> BR-LRN-008 makes the
    /// paper's mark total reconcile to what Module 17 expects that component to
    /// be worth, and refuses approval on a mismatch. Naming the component when
    /// the paper is created — rather than when it is scheduled — is what lets the
    /// meter be live while the paper is being built, instead of a surprise at the
    /// end.
    /// </para>
    ///
    /// <para>
    /// No <see cref="IActivatable"/> and deliberately no
    /// <c>ISoftActiveFiltered</c>: §7 puts versioned catalogs outside the filter,
    /// and a paper a class has answered must stay loadable forever.
    /// <see cref="Status"/> is the lifecycle, following <see cref="Homework"/>.
    /// </para>
    ///
    /// T2 per BR-LRN-015 — a definition. The marks it later produces are Module
    /// 17's T1 concern.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class OnlinePaper : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        /// <summary>The bank its questions are drawn from — and, through it, BR-LRN-001's offering anchor.</summary>
        public int QuestionBankId { get; set; }

        /// <summary>BR-LRN-008: the Module 17 component this paper fills. The number the meter reconciles against.</summary>
        public int BlueprintComponentId { get; set; }

        public string TitleAr { get; set; } = string.Empty;

        public string TitleEn { get; set; } = string.Empty;

        public OnlinePaperStatus Status { get; set; } = OnlinePaperStatus.Draft;

        /// <summary>Set when the head of department approves (§4 P2).</summary>
        public int? ApprovedByUserId { get; set; }

        public DateTime? ApprovedAtUtc { get; set; }

        /// <summary>BR-LRN-016: withdrawal states why, because a paper somebody was going to sit is being taken away.</summary>
        public string? WithdrawnReason { get; set; }

        public DateTime? WithdrawnAtUtc { get; set; }
    }
}
