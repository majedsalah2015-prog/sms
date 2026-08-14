using System.Collections.Generic;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Workflow
{
    /// <summary>
    /// A named, versioned state machine attached to an entity type (doc 05 §2),
    /// per school. New versions are new rows (BR-WF-008): in-flight instances
    /// stay pinned to the version they started on, so old versions are
    /// deactivated, never filtered out or deleted — deliberately NOT
    /// ISoftActiveFiltered.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class WorkflowDefinition : AuditableEntity, ISchoolScoped, IActivatable
    {
        public int SchoolId { get; set; }

        /// <summary>Catalog code from doc 05 §5 (e.g. "WF-04").</summary>
        public string Code { get; set; } = string.Empty;

        public int Version { get; set; }

        public bool IsActive { get; set; } = true;

        public string EntityTypeName { get; set; } = string.Empty;

        public LocalizedName Name { get; set; } = new();

        public List<WorkflowState> States { get; } = new();

        public List<WorkflowTransition> Transitions { get; } = new();
    }
}
