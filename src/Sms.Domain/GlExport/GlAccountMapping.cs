using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.GlExport
{
    /// <summary>
    /// The O3 "mapping table": one row per journal key → the school's GL
    /// account code (doc/Modules/19 §14 Q2 + Implementation 01 O3
    /// assumption: generic journal-summary export, no named-system
    /// adapter; the pilot school's ERP decides the first adapter later).
    /// Keys are the fixed set in Sms.Application.GlExport.GlAccountKeys
    /// plus one revenue key per FeeCategory (FeeCategory.GlExportCode
    /// from E-303 is that key). T2: account codes change rarely but are
    /// audit-relevant.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class GlAccountMapping : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public string Key { get; set; } = string.Empty;

        public string AccountCode { get; set; } = string.Empty;

        public string AccountNameAr { get; set; } = string.Empty;

        public string AccountNameEn { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }
}
