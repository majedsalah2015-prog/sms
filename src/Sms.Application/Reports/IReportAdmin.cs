using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Reports;

namespace Sms.Application.Reports
{
    /// <summary>
    /// doc/Modules/30 §8 Report catalog admin / Run report / Subscriptions
    /// screens backing (screens deferred, the operations are core). Report
    /// *content* — the 150+ catalog reports themselves — is Phase 9,
    /// out of scope; this is the registry + run/subscribe platform doc §2
    /// describes.
    /// </summary>
    public interface IReportAdmin
    {
        Task<ReportDefinition> DefineReportAsync(
            string code, string owningModuleCode, string titleAr, string titleEn,
            OutputFormat supportedFormats, ReportSensitivity sensitivity, int permissionId,
            string? requiredParameterKeysCsv, CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-RPT-002 (deny by default without the definition's View permission),
        /// doc §9 (missing required parameters throws), BR-RPT-003 (export without
        /// the Export permission throws), BR-RPT-005 (queues rather than completing
        /// inline once estimatedRowCount reaches heavyRowThreshold).
        /// </summary>
        Task<ReportExecution> RunReportAsync(
            int reportDefinitionId, int executedByUserId, string parametersJson, IEnumerable<string> suppliedParameterKeys,
            OutputFormat format, bool isExport, int estimatedRowCount, int heavyRowThreshold = 5000, CancellationToken cancellationToken = default);

        /// <summary>BR-RPT-005: simulates the queue processor finishing a Queued run.</summary>
        Task<ReportExecution> CompleteQueuedRunAsync(int executionId, int rowCount, int durationMs, CancellationToken cancellationToken = default);

        /// <summary>BR-RPT-006 (recipient must hold the definition's View permission), BR-RPT-003 (restricted reports refuse Email delivery).</summary>
        Task<ReportSubscription> SubscribeAsync(
            int reportDefinitionId, int subscriberUserId, SubscriptionFrequency frequency, string parametersJson,
            OutputFormat format, DeliveryChannel deliveryChannel, CancellationToken cancellationToken = default);

        Task CancelSubscriptionAsync(int subscriptionId, CancellationToken cancellationToken = default);
    }
}
