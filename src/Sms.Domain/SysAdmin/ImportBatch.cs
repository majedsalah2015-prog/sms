using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.SysAdmin
{
    /// <summary>
    /// core.ImportBatch (BR-SYS-003): onboarding import framework. Actual
    /// file parsing/schema validation/dedup engagement (BR-PAR-002) is a
    /// standalone content-authoring concern out of this slice's scope —
    /// this models the batch lifecycle that framework must obey: dry-run
    /// mandatory, commit takes a pre-op snapshot first (BR-BAK-004),
    /// rollback only while no dependent transactions exist against the
    /// imported rows (approximated here as "while still the most recently
    /// committed batch for its template in this school").
    /// </summary>
    [Audited(AuditTier.T1)]
    public class ImportBatch : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public string TemplateCode { get; set; } = string.Empty;

        public int RowCount { get; set; }

        public int ErrorCount { get; set; }

        public ImportBatchStatus Status { get; set; } = ImportBatchStatus.DryRun;

        public DateTime? CommittedAtUtc { get; set; }

        public DateTime? RolledBackAtUtc { get; set; }
    }
}
