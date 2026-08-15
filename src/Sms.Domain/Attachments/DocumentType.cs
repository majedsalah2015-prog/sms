using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Attachments
{
    /// <summary>
    /// doc.DocumentType (doc 10 §2/§4): the taxonomy entry driving BR-ATT-002/003
    /// (formats/size) and BR-ATT-004 (restricted-category access inheritance).
    /// </summary>
    [Audited(AuditTier.T2)]
    public class DocumentType : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        /// <summary>e.g. "STU-BIRTH-CERT". Owning module carried separately for catalog browsing.</summary>
        public string Code { get; set; } = string.Empty;

        public string ModuleCode { get; set; } = string.Empty;

        public LocalizedName Name { get; set; } = new();

        public DocumentFormat AllowedFormats { get; set; } = DocumentFormat.Pdf | DocumentFormat.Jpg | DocumentFormat.Png;

        /// <summary>BR-ATT-003: null = product default (10 MB); never exceeds the 25 MB product ceiling (enforced by the policy engine, not stored here).</summary>
        public int? MaxSizeBytes { get; set; }

        public bool IsMandatoryByDefault { get; set; }

        /// <summary>BR-ATT-008.</summary>
        public bool IsExpiryTracked { get; set; }

        /// <summary>BR-ATT-004/BR-GLB-072.</summary>
        public bool IsRestricted { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
