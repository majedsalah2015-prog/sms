using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Health
{
    /// <summary>
    /// svc.MedicationAuthorization (doc/Modules/24 §7, BR-HLT-006): parent
    /// authorization (+ physician note per policy) with dosage and
    /// schedule; administration happens only against it, within its date
    /// window and dosage; IsControlled feeds the controlled-storage list.
    /// </summary>
    [Audited(AuditTier.T1)]
    public class MedicationAuthorization : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int MedicalFileId { get; set; }

        public string MedicationName { get; set; } = string.Empty;

        /// <summary>Dose per administration, in the medication's own unit (free-text unit keeps this pack-neutral).</summary>
        public decimal DosePerAdministration { get; set; }

        public string DoseUnit { get; set; } = string.Empty;

        /// <summary>Comma-separated HH:mm scheduled times (e.g. "10:00,14:00").</summary>
        public string ScheduleTimes { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public int AuthorizedByParentId { get; set; }

        public int? PhysicianNoteAttachmentId { get; set; }

        public bool IsControlled { get; set; }
    }

    /// <summary>svc.AdministrationLog (BR-HLT-006): dose, time, nurse; missed/refused too; a deviation from dosage/schedule carries a mandatory reason. Append-only — not [Audited].</summary>
    public class AdministrationLog : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int MedicationAuthorizationId { get; set; }

        public DateTime AtUtc { get; set; }

        public int NurseUserId { get; set; }

        public decimal DoseGiven { get; set; }

        public AdministrationStatus Status { get; set; }

        public bool IsDeviation { get; set; }

        public string? DeviationReason { get; set; }
    }
}
