using System;
using System.Collections.Generic;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Health
{
    /// <summary>
    /// svc.MedicalFile (doc/Modules/24 §7, BR-HLT-001/002/003/010): one per
    /// student, restricted (🔒 BR-GLB-072). T0 read-audit is not an entity
    /// tier in this codebase — HealthAdmin logs an AuditAction.View event
    /// on every full-file open (BR-HLT-001); the emergency banner is the
    /// denormalized, nurse-curated subset displayed at fixed product
    /// points WITHOUT opening the file (BR-HLT-002), so reading it is not
    /// audited. T1 on file changes (BR-HLT-010).
    /// </summary>
    [Audited(AuditTier.T1)]
    public class MedicalFile : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int StudentId { get; set; }

        public string? BloodType { get; set; }

        /// <summary>BR-HLT-002: nurse-curated banner text (severe allergies, critical conditions, emergency instructions) — never auto-extracted.</summary>
        public string? EmergencyBannerAr { get; set; }

        public string? EmergencyBannerEn { get; set; }

        /// <summary>BR-HLT-003: parent-declared intake, nurse-verified.</summary>
        public DateTime? IntakeVerifiedAtUtc { get; set; }

        /// <summary>BR-HLT-003: the academic year in which the parent last re-confirmed the file (nag at re-registration when stale).</summary>
        public int? LastReconfirmedAcademicYearId { get; set; }

        public List<Allergy> Allergies { get; set; } = new();

        public List<MedicalCondition> Conditions { get; set; } = new();
    }

    /// <summary>Allergy with mandatory severity (doc §9).</summary>
    public class Allergy : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int MedicalFileId { get; set; }

        public string Substance { get; set; } = string.Empty;

        public AllergySeverity Severity { get; set; }

        public string? Notes { get; set; }
    }

    public class MedicalCondition : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int MedicalFileId { get; set; }

        public string Name { get; set; } = string.Empty;

        public bool IsCritical { get; set; }

        public string? Notes { get; set; }
    }

    /// <summary>svc.CarePlan (BR-HLT-007): structured chronic-care plan linked to the banner; annual review flag.</summary>
    [Audited(AuditTier.T1)]
    public class CarePlan : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int MedicalFileId { get; set; }

        public string ConditionName { get; set; } = string.Empty;

        public string Triggers { get; set; } = string.Empty;

        public string ResponseSteps { get; set; } = string.Empty;

        public string? EmergencyContactsNote { get; set; }

        public bool IsLinkedToBanner { get; set; } = true;

        public DateTime ReviewDueDate { get; set; }

        public DateTime? LastReviewedAtUtc { get; set; }
    }
}
