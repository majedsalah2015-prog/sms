using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using Sms.Application.Security;
using Sms.Domain.Grades;
using Sms.Domain.Reports;
using Sms.Domain.Schools;
using Sms.Domain.Sections;
using Sms.Domain.Security;

namespace Sms.Web.Models
{
    // ---------------------------------------------------------------- Reporting platform (doc/Modules/30 §8, engine from E-701, screens this pass)

    /// <summary>Bilingual labels for the reporting enums (mirrors FinanceLabels'/InstallmentLabels' shape and placement).</summary>
    public static class ReportLabels
    {
        public static string Sensitivity(ReportSensitivity s, bool ar) => s switch
        {
            ReportSensitivity.Normal => ar ? "عادي" : "Normal",
            ReportSensitivity.PersonalData => ar ? "بيانات شخصية" : "Personal data",
            ReportSensitivity.Restricted => ar ? "مقيَّد" : "Restricted",
            _ => s.ToString(),
        };

        public static string SensitivityBadge(ReportSensitivity s) => s switch
        {
            ReportSensitivity.Restricted => "text-bg-danger",
            ReportSensitivity.PersonalData => "text-bg-warning",
            _ => "text-bg-light border",
        };

        /// <summary>A single format. Callers pass one flag at a time — see <see cref="SplitFormats"/>.</summary>
        public static string Format(OutputFormat f, bool ar) => f switch
        {
            OutputFormat.Html => ar ? "عرض على الشاشة (HTML)" : "On-screen (HTML)",
            OutputFormat.Pdf => "PDF",
            OutputFormat.Xlsx => "Excel (XLSX)",
            OutputFormat.Csv => "CSV",
            _ => f.ToString(),
        };

        public static string ExecutionStatus(ReportExecutionStatus s, bool ar) => s switch
        {
            ReportExecutionStatus.Completed => ar ? "مكتمل" : "Completed",
            ReportExecutionStatus.Queued => ar ? "في الطابور" : "Queued",
            ReportExecutionStatus.Failed => ar ? "فشل" : "Failed",
            _ => s.ToString(),
        };

        public static string ExecutionStatusBadge(ReportExecutionStatus s) => s switch
        {
            ReportExecutionStatus.Completed => "text-bg-success",
            ReportExecutionStatus.Queued => "text-bg-info",
            ReportExecutionStatus.Failed => "text-bg-danger",
            _ => "text-bg-secondary",
        };

        public static string Frequency(SubscriptionFrequency f, bool ar) => f switch
        {
            SubscriptionFrequency.Daily => ar ? "يومي" : "Daily",
            SubscriptionFrequency.Weekly => ar ? "أسبوعي" : "Weekly",
            SubscriptionFrequency.Monthly => ar ? "شهري" : "Monthly",
            _ => f.ToString(),
        };

        public static string Channel(DeliveryChannel c, bool ar) => c switch
        {
            DeliveryChannel.Email => ar ? "بريد إلكتروني" : "Email",
            DeliveryChannel.Portal => ar ? "بوابة" : "Portal",
            _ => c.ToString(),
        };

        /// <summary>The flags actually set, in catalogue order, so a view can render one control per supported format.</summary>
        public static IReadOnlyList<OutputFormat> SplitFormats(OutputFormat formats) => AllFormats.Where(f => formats.HasFlag(f)).ToList();

        public static readonly IReadOnlyList<OutputFormat> AllFormats = new[] { OutputFormat.Html, OutputFormat.Pdf, OutputFormat.Xlsx, OutputFormat.Csv };

        /// <summary>HTML is the on-screen render; the rest leave the building as a file, which is what BR-RPT-003 is about.</summary>
        public static bool IsFileFormat(OutputFormat f) => f != OutputFormat.Html;
    }

    /// <summary>
    /// The parameter blob both a run and a subscription carry. It is a free
    /// dictionary rather than a typed set because the platform deliberately does
    /// not model parameter *types* — that is Phase 9/10 catalog-authoring work
    /// (see ReportDefinition's own remark); all this layer owes is a readable
    /// round-trip.
    /// </summary>
    public static class ReportParameters
    {
        // Arabic parameter values are stored as themselves rather than \u-escaped: this JSON is read by
        // humans in the execution log (BR-RPT-003 audits parameters), and an escaped Arabic value is
        // unreadable there. The blob is never interpolated into a page, so relaxed escaping is safe.
        private static readonly JsonSerializerOptions WriteOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        public static string ToJson(IEnumerable<KeyValuePair<string, string?>> pairs)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in pairs)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
                {
                    map[pair.Key.Trim()] = pair.Value!.Trim();
                }
            }

            return JsonSerializer.Serialize(map, WriteOptions);
        }

        /// <summary>Never throws: a malformed blob from an older run must still render as a row in the log, not a 500.</summary>
        public static IReadOnlyList<KeyValuePair<string, string>> Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return Array.Empty<KeyValuePair<string, string>>();
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return Array.Empty<KeyValuePair<string, string>>();
                }

                return document.RootElement.EnumerateObject()
                    .Select(p => new KeyValuePair<string, string>(
                        p.Name,
                        (p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.ToString()) ?? string.Empty))
                    .ToList();
            }
            catch (JsonException)
            {
                return Array.Empty<KeyValuePair<string, string>>();
            }
        }

        /// <summary>The keys a run actually supplied — what <c>RequiredParameterEvaluator.FindMissing</c> is fed.</summary>
        public static IReadOnlyList<string> Keys(string? json) => Parse(json).Select(p => p.Key).ToList();
    }

    public enum ReportParameterKind
    {
        Text = 0,
        Year = 1,
        Grade = 2,
        Section = 3,
        Date = 4,
    }

    /// <summary>One control on the runner's parameter bar.</summary>
    public sealed record ReportParameterField(string Key, string LabelEn, string LabelAr, ReportParameterKind Kind, bool IsRequired)
    {
        public string Label(bool ar) => ar ? LabelAr : LabelEn;
    }

    /// <summary>
    /// The standard parameter bar BR-RPT-001 names by hand: year / grade /
    /// section / date range. They are offered on every report because the rule
    /// says so, and marked required only when the definition's
    /// RequiredParameterKeysCsv names them — the definition, not this list,
    /// decides what is mandatory (doc §9).
    /// </summary>
    public static class StandardReportParameters
    {
        public static readonly IReadOnlyList<ReportParameterField> Fields = new[]
        {
            new ReportParameterField("academicYearId", "Academic year", "العام الدراسي", ReportParameterKind.Year, false),
            new ReportParameterField("gradeLevelId", "Grade", "الصف", ReportParameterKind.Grade, false),
            new ReportParameterField("sectionId", "Section", "الشعبة", ReportParameterKind.Section, false),
            new ReportParameterField("dateFrom", "From date", "من تاريخ", ReportParameterKind.Date, false),
            new ReportParameterField("dateTo", "To date", "إلى تاريخ", ReportParameterKind.Date, false),
        };

        public static bool IsStandard(string key) => Fields.Any(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// What the gates say about one definition for one user, resolved once in
    /// the controller so the catalogue, the runner and the subscription form all
    /// answer the same way the engine will. Nothing here authorizes anything:
    /// <c>ReportAdmin</c> re-checks every one of these on the way in, and these
    /// values only decide what the screen offers and how it explains itself.
    /// </summary>
    public sealed record ReportAccess(
        Permission? Permission,
        bool HoldsViewPermission,
        bool HoldsExportPermission,
        bool ExportAllowed,
        bool EmailDeliveryAllowed)
    {
        /// <summary>A definition can outlive the sec.Permission row it names — there is no FK. RunReportAsync would throw a raw InvalidOperationException on it, so the screens catch it first.</summary>
        public bool PermissionMissing => Permission == null;

        public bool CanRun => Permission != null && HoldsViewPermission;
    }

    // ---- §8.1 Report center ----

    public sealed class ReportCenterViewModel
    {
        public sealed record Row(
            ReportDefinition Definition,
            ReportAccess Access,
            IReadOnlyList<string> RequiredKeys,
            int RunCount,
            ReportExecution? LastRun,
            int SubscriptionCount);

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        public string? Q { get; set; }

        public string? ModuleCode { get; set; }

        public ReportSensitivity? Sensitivity { get; set; }

        /// <summary>Owning-module codes that actually carry a definition, for the filter — the full 36-module list belongs to the register form.</summary>
        public IReadOnlyList<string> UsedModuleCodes { get; set; } = Array.Empty<string>();

        /// <summary>sec.Permission rows a new definition may point at. Empty means the registry cannot accept a definition at all — the register form says so rather than failing on submit.</summary>
        public IReadOnlyList<Permission> Permissions { get; set; } = Array.Empty<Permission>();

        public int RunnableCount => Rows.Count(r => r.Access.CanRun);
    }

    // ---- §8.2 Report runner ----

    public sealed class ReportRunViewModel
    {
        public ReportDefinition Definition { get; set; } = null!;

        public ReportAccess Access { get; set; } = null!;

        public IReadOnlyList<string> RequiredKeys { get; set; } = Array.Empty<string>();

        public IReadOnlyList<ReportParameterField> Fields { get; set; } = Array.Empty<ReportParameterField>();

        public IReadOnlyList<AcademicYear> Years { get; set; } = Array.Empty<AcademicYear>();

        public IReadOnlyList<GradeLevel> Grades { get; set; } = Array.Empty<GradeLevel>();

        public IReadOnlyList<Section> Sections { get; set; } = Array.Empty<Section>();

        /// <summary>Section names are ambiguous without their grade, and the enrolment-side hop (Section → GradeYearProfile → GradeLevel) is done once here.</summary>
        public IReadOnlyDictionary<int, string> SectionGradeNames { get; set; } = new Dictionary<int, string>();

        public sealed record HistoryRow(ReportExecution Execution, string RunBy);

        public IReadOnlyList<HistoryRow> History { get; set; } = Array.Empty<HistoryRow>();

        public int HeavyRowThreshold { get; set; }

        /// <summary>Sticky across a failed submit so the operator does not retype the estimate.</summary>
        public int EstimatedRowCount { get; set; }

        public int? WorkingYearId { get; set; }

        public bool WouldQueue { get; set; }
    }

    // ---- §8.3 Subscription manager ----

    public sealed class ReportSubscriptionsViewModel
    {
        public sealed record Row(
            ReportSubscription Subscription,
            ReportDefinition? Definition,
            string SubscriberName,
            bool SubscriberStillAuthorized);

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        public bool ShowCancelled { get; set; }

        public IReadOnlyList<ReportDefinition> Definitions { get; set; } = Array.Empty<ReportDefinition>();

        public ReportDefinition? Selected { get; set; }

        public ReportAccess? SelectedAccess { get; set; }

        public IReadOnlyList<string> SelectedRequiredKeys { get; set; } = Array.Empty<string>();

        /// <summary>BR-RPT-006: only these may be picked. Offering the rest and letting the save fail would be a worse way to teach the same rule.</summary>
        public IReadOnlyList<UserAccountInfo> AuthorizedSubscribers { get; set; } = Array.Empty<UserAccountInfo>();

        public int UnauthorizedCount { get; set; }

        public int RevokedCount => Rows.Count(r => r.Subscription.IsActive && !r.SubscriberStillAuthorized);
    }

    // ---- §8.4 Execution log ----

    public sealed class ReportLogViewModel
    {
        public sealed record Row(ReportExecution Execution, ReportDefinition? Definition, string RunBy);

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        public IReadOnlyList<ReportDefinition> Definitions { get; set; } = Array.Empty<ReportDefinition>();

        public IReadOnlyList<UserAccountInfo> Users { get; set; } = Array.Empty<UserAccountInfo>();

        public int? ReportDefinitionId { get; set; }

        public ReportExecutionStatus? Status { get; set; }

        public bool ExportsOnly { get; set; }

        public int? UserId { get; set; }

        public DateTime? From { get; set; }

        public DateTime? To { get; set; }

        public int QueuedCount => Rows.Count(r => r.Execution.Status == ReportExecutionStatus.Queued);

        public int ExportCount => Rows.Count(r => r.Execution.WasExport);

        /// <summary>BR-RPT-003/BR-SEC-021: the runs whose row IS the T0 audit record, and the reason this log exists.</summary>
        public int SensitiveCount => Rows.Count(r => r.Definition != null && r.Definition.Sensitivity != ReportSensitivity.Normal);

        /// <summary>NF-P5's ≤10 s interactive target, measured only where a duration was recorded.</summary>
        public int SlowCount => Rows.Count(r => r.Execution.DurationMs is int ms && ms > 10_000);
    }
}
