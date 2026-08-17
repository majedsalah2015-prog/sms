using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Reports
{
    /// <summary>
    /// core.ReportDefinition (doc/Modules/30 §7, BR-RPT-001): the registry
    /// row every catalog report (Phase 9's separate 150+ report deliverable,
    /// explicitly out of this module's scope) would register against. The
    /// catalog content itself is not modeled here — this slice is the
    /// platform doc §2 describes, not the reports. RequiredParameterKeysCsv
    /// is a simple comma-separated list rather than a full ReportParameter
    /// child entity — parameter *typing* is Phase 9/10 content-authoring
    /// work, this platform only needs to know which keys are mandatory
    /// before a run (doc §9's validation rule).
    /// </summary>
    [Audited(AuditTier.T2)]
    public class ReportDefinition : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        /// <summary>doc's own "RPT-&lt;MOD&gt;-###" catalog code convention.</summary>
        public string Code { get; set; } = string.Empty;

        public string TitleAr { get; set; } = string.Empty;

        public string TitleEn { get; set; } = string.Empty;

        public string OwningModuleCode { get; set; } = string.Empty;

        public OutputFormat SupportedFormats { get; set; }

        public ReportSensitivity Sensitivity { get; set; }

        /// <summary>References sec.Permission — the View action on this permission's (Module, Screen) pair gates running the report; the same pair's Export action separately gates file output for Personal-data/Restricted reports (BR-RPT-003).</summary>
        public int PermissionId { get; set; }

        public string? RequiredParameterKeysCsv { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
