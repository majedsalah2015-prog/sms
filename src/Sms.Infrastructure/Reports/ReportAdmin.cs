using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Reports;
using Sms.Application.Security;
using Sms.Domain.Reports;
using Sms.Domain.Security;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Reports
{
    /// <summary>Standalone admin operations — save themselves, no larger transaction to ride.</summary>
    public class ReportAdmin : IReportAdmin
    {
        private readonly AppDbContext _db;
        private readonly IClock _clock;

        public ReportAdmin(AppDbContext db, IClock clock)
        {
            _db = db;
            _clock = clock;
        }

        public async Task<ReportDefinition> DefineReportAsync(
            string code, string owningModuleCode, string titleAr, string titleEn,
            OutputFormat supportedFormats, ReportSensitivity sensitivity, int permissionId,
            string? requiredParameterKeysCsv, CancellationToken cancellationToken = default)
        {
            var definition = new ReportDefinition
            {
                Code = code, OwningModuleCode = owningModuleCode, TitleAr = titleAr, TitleEn = titleEn,
                SupportedFormats = supportedFormats, Sensitivity = sensitivity, PermissionId = permissionId,
                RequiredParameterKeysCsv = requiredParameterKeysCsv,
            };
            _db.ReportDefinitions.Add(definition);
            await _db.SaveChangesAsync(cancellationToken);
            return definition;
        }

        /// <summary>Mirrors Sms.Infrastructure.Dashboards.DashboardAdmin's hand-rolled target-user permission check — the ambient Sms.Infrastructure.Security.PermissionService only checks ICurrentUser, but View/Export gating here (and subscription-recipient gating) needs to verify an arbitrary user.</summary>
        private async Task<bool> UserHasActionAsync(int userAccountId, string moduleCode, string screenCode, ActionVerb action, CancellationToken cancellationToken)
        {
            var raw = await _db.RoleAssignments
                .Where(a => a.UserAccountId == userAccountId)
                .Select(a => new
                {
                    Permissions = a.Role!.Permissions.Select(rp => new
                    {
                        rp.Permission!.ModuleCode,
                        rp.Permission!.ScreenCode,
                        rp.Permission!.Action,
                    }).ToList(),
                    Grants = a.ScopeGrants.Select(g => new { g.Dimension, g.ScopeValueId }).ToList(),
                })
                .ToListAsync(cancellationToken);

            var snapshots = raw
                .Select(a => new AssignmentSnapshot(
                    a.Permissions.Select(p => new PermissionTriple(p.ModuleCode, p.ScreenCode, p.Action)).ToList(),
                    a.Grants.Select(g => new ScopeGrantValue(g.Dimension, g.ScopeValueId)).ToList()))
                .ToList();

            return PermissionEvaluator.HasPermission(snapshots, moduleCode, screenCode, action);
        }

        public async Task<ReportExecution> RunReportAsync(
            int reportDefinitionId, int executedByUserId, string parametersJson, IEnumerable<string> suppliedParameterKeys,
            OutputFormat format, bool isExport, int estimatedRowCount, int heavyRowThreshold = 5000, CancellationToken cancellationToken = default)
        {
            var definition = await _db.ReportDefinitions.SingleAsync(d => d.Id == reportDefinitionId, cancellationToken);
            var permission = await _db.Permissions.SingleAsync(p => p.Id == definition.PermissionId, cancellationToken);

            if (!await UserHasActionAsync(executedByUserId, permission.ModuleCode, permission.ScreenCode, ActionVerb.View, cancellationToken))
            {
                throw new ReportPermissionDeniedException(executedByUserId, reportDefinitionId);
            }

            var requiredKeys = RequiredParameterEvaluator.ParseRequiredKeys(definition.RequiredParameterKeysCsv);
            var missing = RequiredParameterEvaluator.FindMissing(requiredKeys, suppliedParameterKeys);
            if (missing.Count > 0)
            {
                throw new MissingRequiredParametersException(reportDefinitionId, missing);
            }

            if (isExport)
            {
                var hasExportPermission = await UserHasActionAsync(executedByUserId, permission.ModuleCode, permission.ScreenCode, ActionVerb.Export, cancellationToken);
                if (!ExportPermissionGate.CanExport(definition.Sensitivity, hasExportPermission))
                {
                    throw new ReportExportNotAllowedException(executedByUserId, reportDefinitionId);
                }
            }

            var now = _clock.UtcNow;
            var queued = HeavyReportQueueEvaluator.ShouldQueue(estimatedRowCount, heavyRowThreshold);
            var execution = new ReportExecution
            {
                SchoolId = definition.SchoolId,
                ReportDefinitionId = reportDefinitionId,
                ExecutedByUserId = executedByUserId,
                ParametersJson = parametersJson,
                Format = format,
                WasExport = isExport,
                ExecutedAtUtc = now,
                Status = queued ? ReportExecutionStatus.Queued : ReportExecutionStatus.Completed,
                RowCount = queued ? null : (int?)estimatedRowCount,
                CompletedAtUtc = queued ? null : (System.DateTime?)now,
            };
            _db.ReportExecutions.Add(execution);
            await _db.SaveChangesAsync(cancellationToken);
            return execution;
        }

        public async Task<ReportExecution> CompleteQueuedRunAsync(int executionId, int rowCount, int durationMs, CancellationToken cancellationToken = default)
        {
            var execution = await _db.ReportExecutions.SingleAsync(e => e.Id == executionId, cancellationToken);
            if (execution.Status != ReportExecutionStatus.Queued)
            {
                throw new ReportExecutionNotQueuedException(executionId);
            }

            execution.Status = ReportExecutionStatus.Completed;
            execution.RowCount = rowCount;
            execution.DurationMs = durationMs;
            execution.CompletedAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return execution;
        }

        public async Task<ReportSubscription> SubscribeAsync(
            int reportDefinitionId, int subscriberUserId, SubscriptionFrequency frequency, string parametersJson,
            OutputFormat format, DeliveryChannel deliveryChannel, CancellationToken cancellationToken = default)
        {
            var definition = await _db.ReportDefinitions.SingleAsync(d => d.Id == reportDefinitionId, cancellationToken);
            var permission = await _db.Permissions.SingleAsync(p => p.Id == definition.PermissionId, cancellationToken);

            if (!await UserHasActionAsync(subscriberUserId, permission.ModuleCode, permission.ScreenCode, ActionVerb.View, cancellationToken))
            {
                throw new SubscriptionRecipientNotAuthorizedException(subscriberUserId, reportDefinitionId);
            }

            if (!RestrictedDeliveryGate.IsChannelAllowed(definition.Sensitivity, deliveryChannel))
            {
                throw new RestrictedReportEmailDeliveryException(reportDefinitionId);
            }

            var subscription = new ReportSubscription
            {
                SchoolId = definition.SchoolId,
                ReportDefinitionId = reportDefinitionId,
                SubscriberUserId = subscriberUserId,
                Frequency = frequency,
                ParametersJson = parametersJson,
                Format = format,
                DeliveryChannel = deliveryChannel,
            };
            _db.ReportSubscriptions.Add(subscription);
            await _db.SaveChangesAsync(cancellationToken);
            return subscription;
        }

        public async Task CancelSubscriptionAsync(int subscriptionId, CancellationToken cancellationToken = default)
        {
            var subscription = await _db.ReportSubscriptions.SingleAsync(s => s.Id == subscriptionId, cancellationToken);
            subscription.IsActive = false;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
