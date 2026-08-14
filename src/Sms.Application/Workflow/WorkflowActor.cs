using System.Collections.Generic;
using Sms.Application.Security;

namespace Sms.Application.Workflow
{
    /// <summary>
    /// The acting user as the engine sees them: identity, active role ids, and
    /// — when the transition binds a permission — the effective data scope
    /// resolved for it (null = permission not granted, deny by default).
    /// </summary>
    public sealed class WorkflowActor
    {
        public WorkflowActor(int userId, IReadOnlyCollection<int> roleIds, EffectiveScope? scope = null)
        {
            UserId = userId;
            RoleIds = roleIds;
            Scope = scope;
        }

        public int UserId { get; }

        public IReadOnlyCollection<int> RoleIds { get; }

        public EffectiveScope? Scope { get; }
    }
}
