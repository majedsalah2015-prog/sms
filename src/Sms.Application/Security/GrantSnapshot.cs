using System.Collections.Generic;
using Sms.Domain.Security;

namespace Sms.Application.Security
{
    /// <summary>A (Module, Screen, Action) triple held by a role.</summary>
    public sealed record PermissionTriple(string ModuleCode, string ScreenCode, ActionVerb Action);

    /// <summary>One scope grant row of an assignment; null ValueId = dynamic (doc 06 §4.2).</summary>
    public sealed record ScopeGrantValue(ScopeDimension Dimension, int? ValueId);

    /// <summary>
    /// One active role assignment of the user, flattened for evaluation:
    /// the role's permission triples plus the assignment's scope grants.
    /// </summary>
    public sealed class AssignmentSnapshot
    {
        public AssignmentSnapshot(
            IReadOnlyCollection<PermissionTriple> permissions,
            IReadOnlyCollection<ScopeGrantValue> scopeGrants)
        {
            Permissions = permissions;
            ScopeGrants = scopeGrants;
        }

        public IReadOnlyCollection<PermissionTriple> Permissions { get; }

        public IReadOnlyCollection<ScopeGrantValue> ScopeGrants { get; }
    }
}
