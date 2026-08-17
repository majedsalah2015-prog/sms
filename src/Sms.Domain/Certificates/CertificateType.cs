using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Certificates
{
    /// <summary>
    /// ppl.CertificateType (doc/Modules/18 §7, BR-CRT-001): enrollment
    /// proof, TC, completion, transcript, conduct, honor, custom letters…
    /// Prerequisites are the two this slice can check for real —
    /// published results (E-302's TermResult) and fee clearance (E-303's
    /// IFeeAdmin.ComputeStudentPositionAsync, per BR-CRT-008's rule) —
    /// WF-03 withdrawal clearance (for TC) doesn't exist yet (deferred
    /// since E-202), so no RequiresWithdrawalClearance flag is modeled.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class CertificateType : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        /// <summary>BR-CRT-001 document class; BR-CRT-008's legal gate keys on this.</summary>
        public CertificateKind Kind { get; set; } = CertificateKind.CustomLetter;

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        public bool RequiresPublishedResults { get; set; }

        /// <summary>BR-CRT-008: config per type per school. Non-Disabled only allowed where the country pack permits gating this Kind.</summary>
        public FeeClearanceRule FeeClearanceRule { get; set; } = FeeClearanceRule.Disabled;

        public bool IsPortalRequestable { get; set; }

        /// <summary>Null = doesn't expire (BR-CRT-001).</summary>
        public int? ValidityDays { get; set; }

        /// <summary>doc 08 series code (e.g. "CERT" or "TC", both already seeded by E-010) — lets different types number from different series.</summary>
        public string NumberingSeriesCode { get; set; } = "CERT";

        public bool IsActive { get; set; } = true;
    }
}
