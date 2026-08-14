using Sms.Domain.Common;

namespace Sms.Domain.Workflow
{
    /// <summary>
    /// A named status within a definition (doc 05 §2). Codes reuse the standard
    /// vocabulary of §3 (Draft/Submitted/Under Review/…) where semantics match.
    /// </summary>
    public class WorkflowState : AuditableEntity
    {
        public int WorkflowDefinitionId { get; set; }

        public string Code { get; set; } = string.Empty;

        public LocalizedName Name { get; set; } = new();

        public bool IsInitial { get; set; }

        public bool IsFinal { get; set; }

        public bool IsEditableInState { get; set; }

        public bool IsPortalVisible { get; set; }
    }
}
