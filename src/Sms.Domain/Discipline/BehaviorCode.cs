using System.Collections.Generic;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Discipline
{
    /// <summary>
    /// svc.BehaviorCode (doc/Modules/25 §7, BR-DCP-001): the school's
    /// year-versioned behavior code — violation types with severity and
    /// points, merit types, the consequence catalog, and the default
    /// consequence ladder per severity × repetition. Country-pack starter
    /// content (KSA لائحة السلوك والمواظبة) is doc Q1 — the structure is
    /// pack-neutral; MaxSuspensionDays is the pack legal limit, supplied
    /// per code rather than invented here.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class BehaviorCode : AuditableEntity, ISchoolScoped, IYearScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        public int Version { get; set; } = 1;

        /// <summary>BR-DCP-004: pack legal cap on suspension-class actions; null = no cap configured (action still needs Principal).</summary>
        public int? MaxSuspensionDays { get; set; }

        /// <summary>BR-DCP-006: parent appeal window.</summary>
        public int AppealWindowDays { get; set; } = 7;

        /// <summary>BR-DCP-001: published to families (portal handbook).</summary>
        public bool IsPublished { get; set; }

        public bool IsActive { get; set; } = true;

        public List<ViolationType> ViolationTypes { get; set; } = new();

        public List<MeritType> MeritTypes { get; set; } = new();

        public List<ConsequenceType> ConsequenceTypes { get; set; } = new();

        public List<LadderStep> Ladder { get; set; } = new();
    }

    public class ViolationType : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int BehaviorCodeId { get; set; }

        /// <summary>BR-DCP-003: decisions must cite the code article.</summary>
        public string ArticleRef { get; set; } = string.Empty;

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        /// <summary>1 minor … 4 gravest (BR-DCP-001).</summary>
        public int Severity { get; set; }

        public int Points { get; set; }
    }

    public class MeritType : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int BehaviorCodeId { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        public int Points { get; set; }

        /// <summary>doc §9: merit points within type bounds.</summary>
        public int MaxPointsPerAward { get; set; }
    }

    public class ConsequenceType : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int BehaviorCodeId { get; set; }

        public ConsequenceKind Kind { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        /// <summary>Ordering weight for "above/below the proposal" comparisons (BR-DCP-005): higher = harsher.</summary>
        public int SeverityRank { get; set; }

        /// <summary>BR-DCP-004: suspension-class actions always require Principal.</summary>
        public bool IsSuspensionClass { get; set; }
    }

    /// <summary>BR-DCP-001/005 default ladder: for a severity, the Nth repetition (1st, 2nd, 3rd+) proposes this consequence.</summary>
    public class LadderStep : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int BehaviorCodeId { get; set; }

        public int Severity { get; set; }

        public int RepetitionCount { get; set; }

        public int ConsequenceTypeId { get; set; }
    }
}
