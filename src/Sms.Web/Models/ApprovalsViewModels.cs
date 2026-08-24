using System;
using System.Collections.Generic;
using Sms.Domain.Workflow;

namespace Sms.Web.Models
{
    /// <summary>
    /// The three cross-module workflow surfaces of doc 05 §7 — the "My Approvals"
    /// inbox (BR-WF-011), the submitter's request tracker, and the history panel —
    /// plus a read-only view of the seeded catalogue. None of them belongs to a
    /// module: an approver works one queue across every module that routed
    /// something to them.
    /// </summary>
    public sealed class ApprovalInboxViewModel
    {
        /// <summary>One waiting item and what this user may do with it.</summary>
        public sealed record Row(
            int InstanceId,
            string WorkflowCode,
            string WorkflowName,
            string EntityLabel,
            string StateName,
            string? RequestedBy,
            DateTime PendingSinceUtc,
            int AgeDays,
            bool CanApprove,
            bool CanReject,
            bool CanReturn,
            bool ApprovalNeedsReason,
            bool IsOwnSubmission);

        public IReadOnlyList<Row> Items { get; set; } = Array.Empty<Row>();

        /// <summary>How many workflows the school has catalogued — an empty inbox reads differently when the catalogue is empty too.</summary>
        public int CatalogueCount { get; set; }

        /// <summary>Instances running anywhere in the school, whether or not this user may act on them.</summary>
        public int OpenInstanceCount { get; set; }
    }

    public sealed class MyRequestsViewModel
    {
        public sealed record Row(
            int InstanceId,
            string WorkflowCode,
            string WorkflowName,
            string EntityLabel,
            string StateName,
            bool IsClosed,
            bool IsEditableNow,
            int ReturnCount,
            DateTime StartedAtUtc);

        public IReadOnlyList<Row> Items { get; set; } = Array.Empty<Row>();
    }

    public sealed class WorkflowHistoryViewModel
    {
        public sealed record Step(
            DateTime OccurredAtUtc,
            WorkflowActionType Action,
            string FromStateName,
            string ToStateName,
            string? Actor,
            string? Reason,
            bool IsDelegated);

        public int InstanceId { get; set; }

        public string WorkflowCode { get; set; } = string.Empty;

        public string WorkflowName { get; set; } = string.Empty;

        public string EntityLabel { get; set; } = string.Empty;

        public string StateName { get; set; } = string.Empty;

        public bool IsClosed { get; set; }

        public decimal? RoutingValue { get; set; }

        public string? SubmittedBy { get; set; }

        public IReadOnlyList<Step> Steps { get; set; } = Array.Empty<Step>();
    }

    public sealed class WorkflowCatalogViewModel
    {
        /// <summary>One approval level of a definition, in the order the chain walks them.</summary>
        public sealed record LevelRow(string FromStateName, string ToStateName, string? RoleName, string? Gate, string Band);

        public sealed record Row(
            int DefinitionId,
            string Code,
            string Name,
            string EntityTypeName,
            int Version,
            bool IsActive,
            int StateCount,
            int OpenInstances,
            IReadOnlyList<LevelRow> Levels);

        public IReadOnlyList<Row> Items { get; set; } = Array.Empty<Row>();
    }
}
