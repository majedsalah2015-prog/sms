using System;
using System.Collections.Generic;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Discipline
{
    /// <summary>
    /// svc.Incident (doc/Modules/25 §7, BR-DCP-002/010): numbered (doc 08
    /// "INC"), restricted 🔒. Severity 1 may resolve teacher-level
    /// (recorded, no case); severity ≥ 2 opens a Case. ReporterUserId is
    /// protected from portal display (BR-DCP-010 teacher-protection stance).
    /// </summary>
    [Audited(AuditTier.T1)]
    public class Incident : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public string IncidentNo { get; set; } = string.Empty;

        public int StudentId { get; set; }

        public int? TermId { get; set; }

        public int ReporterUserId { get; set; }

        public int ViolationTypeId { get; set; }

        /// <summary>Severity snapshot at recording (the code may be re-versioned later).</summary>
        public int Severity { get; set; }

        public DateTime OccurredAtUtc { get; set; }

        public string Narrative { get; set; } = string.Empty;

        public int? EvidenceAttachmentId { get; set; }

        public bool IsTeacherResolved { get; set; }

        public int? CaseId { get; set; }
    }

    /// <summary>BR-DCP-002: merits record freely (P1) with points.</summary>
    [Audited(AuditTier.T2)]
    public class Merit : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int StudentId { get; set; }

        public int? TermId { get; set; }

        public int MeritTypeId { get; set; }

        public int Points { get; set; }

        public int RecordedByUserId { get; set; }

        public string? Note { get; set; }
    }

    /// <summary>svc.Case (BR-DCP-003 WF-11): investigation, decision citing a code article, action, appeal window, closure. T1 🔒.</summary>
    [Audited(AuditTier.T1)]
    public class DisciplineCase : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int IncidentId { get; set; }

        public int StudentId { get; set; }

        public int Severity { get; set; }

        public CaseStatus Status { get; set; } = CaseStatus.Reported;

        public int? OfficerUserId { get; set; }

        /// <summary>BR-DCP-003: severity 4 → + Principal / committee (P5).</summary>
        public bool RequiresPrincipal { get; set; }

        public int? ProposedConsequenceTypeId { get; set; }

        public int? DecidedConsequenceTypeId { get; set; }

        public string? DecisionArticleRef { get; set; }

        public int? DecidedByUserId { get; set; }

        public int? PrincipalUserId { get; set; }

        public DateTime? DecidedAtUtc { get; set; }

        /// <summary>BR-DCP-005: deviating below the ladder proposal needs a reason (T1).</summary>
        [RequiresAuditReason]
        public string? DeviationReason { get; set; }

        public DateTime? ClosedAtUtc { get; set; }

        public List<CaseStatement> Statements { get; set; } = new();
    }

    public class CaseStatement : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int DisciplineCaseId { get; set; }

        public StatementKind Kind { get; set; }

        public string Text { get; set; } = string.Empty;

        public DateTime RecordedAtUtc { get; set; }

        public int? AttachmentId { get; set; }
    }

    /// <summary>svc.ActionApplied (BR-DCP-004): the consequence with dates and completion; suspension-class needs Principal + pack cap.</summary>
    [Audited(AuditTier.T1)]
    public class ActionApplied : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int DisciplineCaseId { get; set; }

        public int ConsequenceTypeId { get; set; }

        public DateTime StartDate { get; set; }

        public int? Days { get; set; }

        public int? ApprovedByPrincipalUserId { get; set; }

        public DateTime? CompletedAtUtc { get; set; }
    }

    /// <summary>svc.Appeal (BR-DCP-006): one per case, within the window, reviewed by someone other than the original decider.</summary>
    [Audited(AuditTier.T1)]
    public class Appeal : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int DisciplineCaseId { get; set; }

        public int FiledByParentId { get; set; }

        public DateTime FiledAtUtc { get; set; }

        public string Grounds { get; set; } = string.Empty;

        public int? ReviewerUserId { get; set; }

        public AppealOutcome Outcome { get; set; } = AppealOutcome.Pending;

        public DateTime? DecidedAtUtc { get; set; }

        public string? DecisionNote { get; set; }
    }

    /// <summary>svc.PointLedger (BR-DCP-007): per student-term entries; aggregation derived.</summary>
    public class PointLedgerEntry : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int StudentId { get; set; }

        public int? TermId { get; set; }

        public PointSource Source { get; set; }

        public int SourceId { get; set; }

        /// <summary>Positive for merits, negative for violations.</summary>
        public int Points { get; set; }

        public DateTime OccurredAtUtc { get; set; }
    }

    /// <summary>svc.BehaviorContract (BR-DCP-009 / doc §9): needs signatures (portal e-ack or scanned pledge) before it is in force.</summary>
    [Audited(AuditTier.T1)]
    public class BehaviorContract : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int StudentId { get; set; }

        public int? DisciplineCaseId { get; set; }

        public string Terms { get; set; } = string.Empty;

        public DateTime? ParentSignedAtUtc { get; set; }

        public DateTime? StudentAcknowledgedAtUtc { get; set; }

        public int? PledgeAttachmentId { get; set; }

        public DateTime? EndsOn { get; set; }
    }

    /// <summary>BR-DCP-009: keep-apart pairs feed Sections balancing (BR-SCN-008) under the same restricted visibility.</summary>
    [Audited(AuditTier.T1)]
    public class KeepApartPair : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int StudentAId { get; set; }

        public int StudentBId { get; set; }

        public string Reason { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }

    /// <summary>svc.ParentMeeting: summons/meetings tied to a case or free-standing.</summary>
    [Audited(AuditTier.T2)]
    public class ParentMeeting : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int StudentId { get; set; }

        public int? DisciplineCaseId { get; set; }

        public DateTime ScheduledAtUtc { get; set; }

        public DateTime? HeldAtUtc { get; set; }

        public string? Notes { get; set; }
    }
}
