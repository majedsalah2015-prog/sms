using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Numbering
{
    /// <summary>
    /// core.NumberingSeries (doc 08 §2-3, BR-NUM-005 T1-audited config). A
    /// format is locked once it has issued its first number; a further
    /// change opens a new version row effective from a chosen date — old
    /// versions stay loadable for historical/continuity reporting (doc 08
    /// §7), so this is deliberately NOT ISoftActiveFiltered, same reasoning
    /// as <see cref="Workflow.WorkflowDefinition"/>.
    /// </summary>
    [Audited(AuditTier.T1)]
    public class NumberingSeries : AuditableEntity, ISchoolScoped, IActivatable
    {
        public int SchoolId { get; set; }

        /// <summary>Logical identity shared across versions (e.g. "STU", "RCP" — doc 08 §4 catalog).</summary>
        public string Code { get; set; } = string.Empty;

        public int Version { get; set; } = 1;

        public bool IsActive { get; set; } = true;

        /// <summary>Descriptive only (e.g. "Student", "Receipt") — the entity this series numbers.</summary>
        public string EntityName { get; set; } = string.Empty;

        /// <summary>{SCHOOL}/{YEAR}/{GYEAR}/{SEQ:n} tokens plus literal text (doc 08 §2).</summary>
        [RequiresAuditReason]
        public string FormatTemplate { get; set; } = string.Empty;

        public ResetPolicy ResetPolicy { get; set; }

        public GapPolicy GapPolicy { get; set; }

        public DateTime EffectiveFromUtc { get; set; }

        /// <summary>Flips true on first issuance (doc 08 §3); further format edits must cut over to a new version instead.</summary>
        public bool IsLocked { get; set; }
    }
}
