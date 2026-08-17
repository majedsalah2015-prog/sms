using System;
using System.Collections.Generic;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Transport
{
    /// <summary>
    /// svc.Bus (doc/Modules/23 §7, BR-TRN-001): plate, capacity, type,
    /// plus mandatory expiry-tracked documents. "Unroadworthy" is DERIVED
    /// (RoadworthinessEvaluator over the documents as of a date), never
    /// stored — an expired document blocks trip assignment unless a
    /// Principal overrides (T1 + reason, emergency only, logged as a
    /// SafetyEvent). Document files themselves would attach via E-008
    /// (AttachmentId optional) — expiry is what the rule needs.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class Bus : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public string PlateNo { get; set; } = string.Empty;

        public int Capacity { get; set; }

        public BusType Type { get; set; } = BusType.Standard;

        public LicenseClass RequiredLicenseClass { get; set; } = LicenseClass.Medium;

        public bool IsActive { get; set; } = true;

        public List<BusDocument> Documents { get; set; } = new();
    }

    /// <summary>BR-TRN-001 / BR-ATT-008: one expiry-tracked document per kind (latest row wins per kind).</summary>
    public class BusDocument : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int BusId { get; set; }

        public BusDocumentKind Kind { get; set; }

        public DateTime ExpiryDate { get; set; }

        public int? AttachmentId { get; set; }
    }
}
