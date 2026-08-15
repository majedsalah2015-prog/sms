using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Schools
{
    /// <summary>
    /// core.Signatory (BR-SCH-004): per document class (e.g. "Certificate",
    /// "Financial"), effective-dated so reissued documents stay faithful to
    /// who signed at the time — never overwritten in place.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class Signatory : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        /// <summary>Free-text code, not a closed enum — doc names Certificate/Financial as examples, not an exhaustive list.</summary>
        public string DocumentClassCode { get; set; } = string.Empty;

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        public string TitleAr { get; set; } = string.Empty;

        public string TitleEn { get; set; } = string.Empty;

        public DateTime EffectiveFromUtc { get; set; }

        /// <summary>Null = the current signatory for this document class.</summary>
        public DateTime? EffectiveToUtc { get; set; }
    }
}
