using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Notifications;
using Sms.Domain.Notifications;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Notifications
{
    /// <summary>doc/Modules/33 (Notifications Administration) — standalone admin operations, save themselves, no larger transaction to ride.</summary>
    public class NotificationOpsAdmin : INotificationOpsAdmin
    {
        private readonly AppDbContext _db;

        public NotificationOpsAdmin(AppDbContext db)
        {
            _db = db;
        }

        public async Task MarkTemplateVersionTestSentAsync(int templateVersionId, CancellationToken cancellationToken = default)
        {
            var version = await _db.TemplateVersions.SingleAsync(v => v.Id == templateVersionId, cancellationToken);
            if (!TemplatePublishTransitions.CanTransition(version.PublishStatus, TemplatePublishStatus.TestSent))
            {
                throw new InvalidTemplatePublishTransitionException(version.PublishStatus, TemplatePublishStatus.TestSent);
            }

            version.PublishStatus = TemplatePublishStatus.TestSent;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task PublishTemplateVersionAsync(int templateVersionId, CancellationToken cancellationToken = default)
        {
            var version = await _db.TemplateVersions.SingleAsync(v => v.Id == templateVersionId, cancellationToken);
            if (!TemplatePublishTransitions.CanTransition(version.PublishStatus, TemplatePublishStatus.Published))
            {
                throw new InvalidTemplatePublishTransitionException(version.PublishStatus, TemplatePublishStatus.Published);
            }

            version.PublishStatus = TemplatePublishStatus.Published;

            var template = await _db.Templates.SingleAsync(t => t.Id == version.TemplateId, cancellationToken);
            template.CurrentVersionNumber = version.VersionNumber;

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task SetSubscriptionStatutoryAsync(int subscriptionRuleId, bool isStatutory, CancellationToken cancellationToken = default)
        {
            var rule = await _db.SubscriptionRules.SingleAsync(r => r.Id == subscriptionRuleId, cancellationToken);
            rule.IsStatutory = isStatutory;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task DisableSubscriptionAsync(int subscriptionRuleId, bool principalApprovalGranted = false, CancellationToken cancellationToken = default)
        {
            var rule = await _db.SubscriptionRules.SingleAsync(r => r.Id == subscriptionRuleId, cancellationToken);
            if (!StatutorySubscriptionGuard.CanDisable(rule.IsStatutory, principalApprovalGranted))
            {
                throw new StatutorySubscriptionChangeDeniedException(subscriptionRuleId);
            }

            rule.IsActive = false;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<BudgetCheckResult> EvaluateBudgetAsync(
            NotificationChannel channel, string periodKey, int budgetLimit, bool isSafetyClass, CancellationToken cancellationToken = default)
        {
            var counter = await _db.BudgetCounters.SingleOrDefaultAsync(
                c => c.Channel == channel && c.PeriodKey == periodKey, cancellationToken);
            var count = counter?.MessageCount ?? 0;

            return new BudgetCheckResult
            {
                CurrentCount = count,
                ShouldAlert = BudgetThresholdEvaluator.ShouldAlert(count, budgetLimit),
                ShouldBlock = BudgetThresholdEvaluator.ShouldBlock(count, budgetLimit, isSafetyClass),
            };
        }
    }
}
