using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Workflow;
using Sms.Domain.Workflow;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Workflow
{
    /// <summary>
    /// The unified "My Approvals" feed (BR-WF-011): open instances whose
    /// current state has an outgoing transition the user's roles may take,
    /// minus approvals of their own submissions (BR-WF-003 preview).
    /// Tenant-filtered through the instance/definition query filters.
    /// </summary>
    public class ApprovalInboxQuery : IApprovalInboxQuery
    {
        private readonly AppDbContext _db;

        public ApprovalInboxQuery(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<PendingApproval>> GetPendingForAsync(int userId, CancellationToken cancellationToken = default)
        {
            var roleIds = await _db.RoleAssignments
                .Where(a => a.UserAccountId == userId)
                .Select(a => a.RoleId)
                .ToListAsync(cancellationToken);

            var rows = await (
                from i in _db.WorkflowInstances
                where !i.IsClosed
                join d in _db.WorkflowDefinitions on i.WorkflowDefinitionId equals d.Id
                join t in _db.WorkflowTransitions on i.WorkflowDefinitionId equals t.WorkflowDefinitionId
                where t.FromStateId == i.CurrentStateId
                      && t.RequiredRoleId != null
                      && roleIds.Contains(t.RequiredRoleId.Value)
                      && !(t.Action == WorkflowActionType.Approve && i.SubmittedByUserId == userId)
                join s in _db.WorkflowStates on i.CurrentStateId equals s.Id
                select new
                {
                    i.Id,
                    d.Code,
                    i.EntityTypeName,
                    i.BusinessKey,
                    StateCode = s.Code,
                    i.SubmittedByUserId,
                    i.ModifiedAtUtc,
                    i.CreatedAtUtc,
                }).ToListAsync(cancellationToken);

            // One row per instance even when several transitions are actionable.
            return rows
                .GroupBy(r => r.Id)
                .Select(g => g.First())
                .Select(r => new PendingApproval(
                    r.Id, r.Code, r.EntityTypeName, r.BusinessKey, r.StateCode, r.SubmittedByUserId, r.ModifiedAtUtc ?? r.CreatedAtUtc))
                .ToList();
        }
    }
}
