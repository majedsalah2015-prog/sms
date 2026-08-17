using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Health
{
    /// <summary>
    /// svc.ClinicVisit (doc/Modules/24 §7, BR-HLT-005): numbered (doc 08
    /// "MED", seeded by E-010), attributable to the recording nurse.
    /// Sent-home requires authorized-pickup verification (BR-PAR-008) or
    /// a documented exception; Emergency triggers the urgent protocol
    /// notification. Append-only in effect (BR-HLT-010): T2 logs edits,
    /// nothing here deletes.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class ClinicVisit : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int MedicalFileId { get; set; }

        public int StudentId { get; set; }

        public string VisitNo { get; set; } = string.Empty;

        public int NurseUserId { get; set; }

        public DateTime ArrivedAtUtc { get; set; }

        public string Reason { get; set; } = string.Empty;

        public string? TriageNotes { get; set; }

        public decimal? TemperatureC { get; set; }

        public int? PulseBpm { get; set; }

        public string? BloodPressure { get; set; }

        public ClinicVisitOutcome Outcome { get; set; }

        /// <summary>BR-HLT-005 sent-home: who collected the child (verified pickup-authorized) or the documented exception.</summary>
        public string? PickupVerifiedByName { get; set; }

        public string? PickupExceptionNote { get; set; }
    }
}
