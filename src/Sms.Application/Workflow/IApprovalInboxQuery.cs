using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sms.Application.Workflow
{
    /// <summary>One pending item in the unified "My Approvals" inbox (BR-WF-011).</summary>
    public sealed record PendingApproval(
        int InstanceId,
        string WorkflowCode,
        string EntityTypeName,
        string? BusinessKey,
        string CurrentStateCode,
        int? SubmittedByUserId,
        DateTime PendingSinceUtc);

    /// <summary>Cross-module pending-approvals query for the current school.</summary>
    public interface IApprovalInboxQuery
    {
        Task<IReadOnlyList<PendingApproval>> GetPendingForAsync(int userId, CancellationToken cancellationToken = default);
    }
}
