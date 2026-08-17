using System;
using System.Collections.Generic;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>doc/Modules/30 §9: a required parameter key wasn't supplied.</summary>
    public class MissingRequiredParametersException : InvalidOperationException
    {
        public MissingRequiredParametersException(int reportDefinitionId, IReadOnlyList<string> missingKeys)
            : base($"Report {reportDefinitionId} is missing required parameters: {string.Join(", ", missingKeys)} (doc/Modules/30 §9).")
        {
        }
    }

    /// <summary>BR-RPT-002: the runner does not hold the report's View permission — deny by default.</summary>
    public class ReportPermissionDeniedException : InvalidOperationException
    {
        public ReportPermissionDeniedException(int userId, int reportDefinitionId)
            : base($"User {userId} does not hold the View permission for report {reportDefinitionId} (BR-RPT-002).")
        {
        }
    }

    /// <summary>BR-RPT-003: personal-data/restricted reports require the Export permission for file output.</summary>
    public class ReportExportNotAllowedException : InvalidOperationException
    {
        public ReportExportNotAllowedException(int userId, int reportDefinitionId)
            : base($"User {userId} does not hold the Export permission for report {reportDefinitionId} (BR-RPT-003).")
        {
        }
    }

    /// <summary>BR-RPT-003: restricted reports never schedule to email — portal-delivery only.</summary>
    public class RestrictedReportEmailDeliveryException : InvalidOperationException
    {
        public RestrictedReportEmailDeliveryException(int reportDefinitionId)
            : base($"Report {reportDefinitionId} is restricted and cannot schedule to email delivery (BR-RPT-003).")
        {
        }
    }

    /// <summary>BR-RPT-006: subscription recipients must hold the report's View permission — no permission laundering via subscriptions.</summary>
    public class SubscriptionRecipientNotAuthorizedException : InvalidOperationException
    {
        public SubscriptionRecipientNotAuthorizedException(int subscriberUserId, int reportDefinitionId)
            : base($"User {subscriberUserId} does not hold the View permission for report {reportDefinitionId} and cannot subscribe (BR-RPT-006).")
        {
        }
    }

    /// <summary>BR-RPT-005: CompleteQueuedRunAsync only applies to an execution actually sitting in Queued status.</summary>
    public class ReportExecutionNotQueuedException : InvalidOperationException
    {
        public ReportExecutionNotQueuedException(int reportExecutionId)
            : base($"Report execution {reportExecutionId} is not Queued (BR-RPT-005).")
        {
        }
    }
}
