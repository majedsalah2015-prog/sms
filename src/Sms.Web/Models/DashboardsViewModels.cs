using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Application.ReadModels;
using Sms.Domain.Certificates;
using Sms.Domain.Dashboards;
using Sms.Domain.Employees;
using Sms.Domain.Grades;
using Sms.Domain.Schools;
using Sms.Domain.Sections;
using Sms.Domain.Security;
using Sms.Domain.Students;

namespace Sms.Web.Models
{
    // ---------------------------------------------------------------- Dashboards (doc/Modules/31 §8, engine from E-702, screens this pass)

    /// <summary>Bilingual labels for the dashboard enums (mirrors FinanceLabels' shape/placement rationale).</summary>
    public static class DashboardLabels
    {
        public static string RefreshClass(WidgetRefreshClass c, bool ar) => c switch
        {
            WidgetRefreshClass.Live => ar ? "لحظي" : "Live",
            WidgetRefreshClass.Cached15Min => ar ? "مخزَّن 15 دقيقة" : "Cached 15 min",
            WidgetRefreshClass.Daily => ar ? "يومي" : "Daily",
            _ => c.ToString(),
        };

        public static string RefreshBadge(WidgetRefreshClass c) => c switch
        {
            WidgetRefreshClass.Live => "text-bg-success",
            WidgetRefreshClass.Cached15Min => "text-bg-info",
            _ => "text-bg-light border",
        };

        /// <summary>Named Verb, not Action, so the name never competes with System.Action in a type position.</summary>
        public static string Verb(ActionVerb a, bool ar) => a switch
        {
            ActionVerb.View => ar ? "عرض" : "View",
            ActionVerb.Create => ar ? "إنشاء" : "Create",
            ActionVerb.Edit => ar ? "تعديل" : "Edit",
            ActionVerb.Deactivate => ar ? "إلغاء تنشيط" : "Deactivate",
            ActionVerb.Submit => ar ? "تقديم" : "Submit",
            ActionVerb.Approve => ar ? "اعتماد" : "Approve",
            ActionVerb.Post => ar ? "ترحيل" : "Post",
            ActionVerb.Print => ar ? "طباعة" : "Print",
            ActionVerb.Export => ar ? "تصدير" : "Export",
            ActionVerb.Import => ar ? "استيراد" : "Import",
            ActionVerb.Configure => ar ? "تهيئة" : "Configure",
            _ => a.ToString(),
        };

        public static string Bucket(AgingBucket b, bool ar) => b switch
        {
            AgingBucket.Current => ar ? "جارٍ" : "Current",
            AgingBucket.Days1To30 => ar ? "1–30 يوماً" : "1–30 days",
            AgingBucket.Days31To60 => ar ? "31–60 يوماً" : "31–60 days",
            AgingBucket.Days61To90 => ar ? "61–90 يوماً" : "61–90 days",
            AgingBucket.Over90 => ar ? "أكثر من 90 يوماً" : "Over 90 days",
            _ => b.ToString(),
        };

        public static string CertificateStatus(CertificateRequestStatus s, bool ar) => s switch
        {
            CertificateRequestStatus.Requested => ar ? "مطلوبة" : "Requested",
            CertificateRequestStatus.Approved => ar ? "معتمدة" : "Approved",
            CertificateRequestStatus.Issued => ar ? "صادرة" : "Issued",
            CertificateRequestStatus.Rejected => ar ? "مرفوضة" : "Rejected",
            _ => s.ToString(),
        };

        public static string RoleName(Role r, bool ar) => ar ? r.Name.NameAr : r.Name.NameEn;

        public static string PermissionLabel(Permission? p, bool ar) =>
            p == null ? (ar ? "صلاحية غير موجودة" : "missing permission") : $"{p.ModuleCode} · {p.ScreenCode} · {Verb(p.Action, ar)}";

        /// <summary>The as-of stamp BR-DSH-002 requires on every cached number.</summary>
        public static string AsOf(DateTime? utc, bool ar) => utc == null
            ? (ar ? "لم تُحتسب بعد" : "not computed yet")
            : (ar ? $"حتى {utc.Value:yyyy-MM-dd HH:mm} ت.ع.م" : $"as of {utc.Value:yyyy-MM-dd HH:mm} UTC");

        /// <summary>BR-DSH-007: a masked aggregate prints the threshold, never the real small count.</summary>
        public static string Masked(int? count, int threshold, bool ar) =>
            count?.ToString("N0") ?? (ar ? $"أقل من {threshold}" : $"fewer than {threshold}");
    }

    /// <summary>
    /// One built-in overview panel. <see cref="Code"/> follows the doc's own
    /// "DSH-&lt;MOD&gt;-###" registry convention (BR-DSH-001): register a
    /// <see cref="WidgetDefinition"/> under the same code and this panel becomes
    /// permission-gated, role-orderable and user-personalizable through the
    /// engine; until then it renders in built-in order for everyone. The panels
    /// are hard-wired rather than registry-driven because widget *content*
    /// (widget → data source → drill path) is the doc's own Phase 9 deliverable —
    /// the registry has no data binding to drive a panel from yet.
    /// </summary>
    public sealed record DashboardPanel(
        string Code,
        string OwningModuleCode,
        string TitleEn,
        string TitleAr,
        string Icon,
        WidgetRefreshClass RefreshClass,
        bool IsPortalEligible,
        bool IsRestrictedCategory,
        string ColumnClass,
        string? DrillController = null,
        string? DrillAction = null)
    {
        public string Title(bool ar) => ar ? TitleAr : TitleEn;

        /// <summary>doc §7's free-text DrillTargetCode, filled with the real MVC route so BR-DSH-002's "every number clicks through" is checkable.</summary>
        public string DrillTargetCode => DrillController == null ? string.Empty : $"{DrillController}/{DrillAction}";
    }

    /// <summary>The overview's fixed panel set, in the order a principal reads them.</summary>
    public static class DashboardPanels
    {
        public const string Attendance = "DSH-ATT-001";
        public const string Receivables = "DSH-FEE-001";
        public const string Collections = "DSH-INS-001";
        public const string Certificates = "DSH-CRT-001";
        public const string Seats = "DSH-GRD-001";
        public const string TeacherLoad = "DSH-TCH-001";
        public const string Restricted = "DSH-DIS-001";

        /// <summary>doc/Modules/01 §11 — the Sys Admin dashboard's setup-completeness figure.</summary>
        public const string SetupCompleteness = "DSH-SET-001";

        public static readonly IReadOnlyList<DashboardPanel> All = new[]
        {
            new DashboardPanel(Attendance, "ATT", "Attendance today", "حضور اليوم", "bi-check2-square", WidgetRefreshClass.Cached15Min, false, false, "col-xl-7", "Attendance", "Index"),
            new DashboardPanel(Certificates, "CRT", "Certificate requests", "طلبات الشهادات", "bi-award", WidgetRefreshClass.Live, false, false, "col-xl-5"),
            new DashboardPanel(Receivables, "FEE", "Receivables outstanding", "الذمم المستحقة", "bi-cash-stack", WidgetRefreshClass.Daily, false, false, "col-xl-7", "Fees", "Index"),
            new DashboardPanel(Collections, "INS", "Collection calendar", "تقويم التحصيل", "bi-calendar-check", WidgetRefreshClass.Daily, false, false, "col-xl-5", "Installments", "Family"),
            new DashboardPanel(Seats, "GRD", "Seats and pipeline", "المقاعد وخط القبول", "bi-layers", WidgetRefreshClass.Daily, false, false, "col-xl-7", "Grades", "Index"),
            new DashboardPanel(TeacherLoad, "TCH", "Teacher load", "نصاب المعلمين", "bi-person-workspace", WidgetRefreshClass.Daily, false, false, "col-xl-5", "Teachers", "Load"),
            new DashboardPanel(SetupCompleteness, "SET", "Setup completeness", "اكتمال الإعداد", "bi-list-check", WidgetRefreshClass.Live, false, false, "col-xl-5", "Setup", "Index"),
            new DashboardPanel(Restricted, "DIS", "Restricted categories", "الفئات المقيَّدة", "bi-shield-lock", WidgetRefreshClass.Live, false, true, "col-xl-12"),
        };

        public static DashboardPanel? Find(string code) => All.FirstOrDefault(p => p.Code == code);
    }

    /// <summary>Where a panel's order and visibility came from (BR-DSH-003's three-layer fallback).</summary>
    public enum PanelSource
    {
        BuiltIn = 0,
        RoleTemplate = 1,
        Personal = 2,
    }

    // ---- §8.1 Dashboard shell — the executive overview + personalization mode ----

    public sealed class DashboardOverviewViewModel
    {
        /// <summary>
        /// <paramref name="IsPermitted"/> is BR-DSH-001 deny-by-default: once a panel
        /// carries a registered <see cref="WidgetDefinition"/>, the user must hold that
        /// widget's permission or the panel simply does not render.
        /// </summary>
        public sealed record PanelState(
            DashboardPanel Panel, WidgetDefinition? Definition, int SortOrder, bool IsVisible, bool IsPermitted, PanelSource Source);

        public IReadOnlyList<AcademicYear> Years { get; set; } = Array.Empty<AcademicYear>();

        public AcademicYear? Year { get; set; }

        /// <summary>The day the attendance panel reports on (defaults to today, BR-DSH-005 within the working year).</summary>
        public DateTime Date { get; set; }

        public int AnonymityThreshold { get; set; }

        public bool PersonalizeMode { get; set; }

        public IReadOnlyList<PanelState> Panels { get; set; } = Array.Empty<PanelState>();

        public IReadOnlyList<PanelState> Rendered => Panels
            .Where(p => p.IsVisible && p.IsPermitted)
            .OrderBy(p => p.SortOrder)
            .ToList();

        public int HiddenByPermission => Panels.Count(p => !p.IsPermitted);

        public int HiddenByChoice => Panels.Count(p => p.IsPermitted && !p.IsVisible);

        public bool IsPersonalized => Panels.Any(p => p.Source == PanelSource.Personal);

        /// <summary>The role whose LayoutTemplate supplied the default order, when one did (BR-DSH-003).</summary>
        public Role? TemplateRole { get; set; }

        public AttendanceTodayView? Attendance { get; set; }

        public ReceivablesView? Receivables { get; set; }

        public CollectionsView? Collections { get; set; }

        public CertificateQueueView? Certificates { get; set; }

        public SeatsView? Seats { get; set; }

        public TeacherLoadView? TeacherLoad { get; set; }

        public RestrictedView? Restricted { get; set; }

        public SetupCompletenessView? Setup { get; set; }

        /// <summary>
        /// True when a snapshot-backed panel rendered and not one of them carries an
        /// as-of — the overview is empty for a reason the operator can fix in one click.
        /// Guarded on at least one having been computed, so switching those panels off
        /// does not raise the warning.
        /// </summary>
        public bool SnapshotsNeverRefreshed =>
            (Attendance != null || Receivables != null || Collections != null)
            && Attendance?.AsOfUtc == null && Receivables?.AsOfUtc == null && Collections?.AsOfUtc == null;
    }

    /// <summary>snap_DailyAttendanceSummary for one date. Percentages come from AttendancePercentageCalculator (BR-ATD-009), never averaged.</summary>
    public sealed record AttendanceTodayView(
        DateTime? AsOfUtc,
        int SectionsCaptured,
        int SectionsExpected,
        int Scheduled,
        int Absent,
        int Exempted,
        int Late,
        decimal PresentPercent,
        IReadOnlyList<AttendanceStageRow> Stages,
        IReadOnlyList<AttendanceSectionRow> Worst)
    {
        public int Uncaptured => Math.Max(0, SectionsExpected - SectionsCaptured);
    }

    public sealed record AttendanceStageRow(Stage? Stage, int Scheduled, int Absent, int Exempted, int Late, decimal PresentPercent);

    public sealed record AttendanceSectionRow(Section? Section, GradeLevel? Grade, int Scheduled, int Absent, int Late, decimal PresentPercent);

    /// <summary>snap_AgedReceivables (RPT-FEE-004) rolled up to the school.</summary>
    public sealed record ReceivablesView(
        DateTime? AsOfUtc,
        decimal Total,
        decimal Current,
        decimal Days1To30,
        decimal Days31To60,
        decimal Days61To90,
        decimal Over90,
        int PayerCount,
        int StudentCount,
        IReadOnlyList<ReceivablesGradeRow> ByGrade,
        decimal? LiveTotal)
    {
        public decimal PastDue => Total - Current;
    }

    public sealed record ReceivablesGradeRow(GradeLevel? Grade, decimal Total, decimal Over90);

    /// <summary>snap_CollectionCalendar (RPT-INS-001): the cashflow the collection desk is working against.</summary>
    public sealed record CollectionsView(
        DateTime? AsOfUtc,
        decimal OverdueOutstanding,
        int OverdueInstallments,
        decimal DueSoonOutstanding,
        int DueSoonInstallments,
        int HorizonDays,
        IReadOnlyList<CollectionDayRow> Days);

    public sealed record CollectionDayRow(DateTime DueDate, int InstallmentCount, decimal Scheduled, decimal Paid, decimal Outstanding, int OverdueCount)
    {
        public decimal CollectedPercent => Scheduled <= 0m ? 0m : Paid / Scheduled * 100m;
    }

    /// <summary>BR-DSH-004 action widget: a live count with the work behind it, not a cached figure.</summary>
    public sealed record CertificateQueueView(int Pending, IReadOnlyList<CertificateQueueRow> Oldest);

    public sealed record CertificateQueueRow(CertificateRequest Request, CertificateType? Type, Student? Student, int AgeDays);

    /// <summary>vw_SeatUtilization rolled up; <see cref="Tight"/> is the grades that are full or over.</summary>
    public sealed record SeatsView(int Planned, int Capacity, int Enrolled, int Pipeline, int FreeSeats, IReadOnlyList<SeatsGradeRow> Tight);

    public sealed record SeatsGradeRow(GradeLevel? Grade, int Planned, int Capacity, int Enrolled, int Pipeline, int FreeSeats);

    /// <summary>vw_TeacherLoad rolled up (BR-TCH-004).</summary>
    public sealed record TeacherLoadView(int Profiles, int Overloaded, IReadOnlyList<TeacherLoadRowView> Worst);

    public sealed record TeacherLoadRowView(Employee? Employee, int CurrentWeeklyPeriods, int MaxWeeklyPeriods)
    {
        public int Excess => CurrentWeeklyPeriods - MaxWeeklyPeriods;
    }

    /// <summary>BR-DSH-007: null means the real count was below the threshold and is masked, not that it was zero.</summary>
    public sealed record RestrictedView(int? ClinicVisitsToday, int? OpenDisciplineCases, int Threshold);

    /// <summary>
    /// doc/Modules/01 §11. The one figure on this dashboard that is about the
    /// product's own readiness rather than the school's day: how much of the setup
    /// wizard is done, and which mandatory step is still holding the first academic
    /// year shut (BR-SET-003). It reads live because it changes by hand, not by
    /// snapshot, and because a stale "83%" is worse than none.
    /// </summary>
    public sealed record SetupCompletenessView(
        int Percent,
        int MandatoryTotal,
        int MandatoryDone,
        bool IsDeclaredComplete,
        DateTime? DeclaredAtUtc,
        IReadOnlyList<SetupPendingStepView> Pending);

    public sealed record SetupPendingStepView(string Code, string TitleEn, string TitleAr)
    {
        public string Title(bool ar) => ar ? TitleAr : TitleEn;
    }

    // ---- §8.2 Layout administrator — widget registry half (BR-DSH-001) ----

    public sealed class WidgetRegistryViewModel
    {
        public sealed record Row(WidgetDefinition Widget, Permission? Permission, DashboardPanel? BuiltIn, int TemplateCount, int PersonalizationCount);

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        public IReadOnlyList<Permission> Permissions { get; set; } = Array.Empty<Permission>();

        /// <summary>Built-in overview panels with no WidgetDefinition yet — the one-click registration list.</summary>
        public IReadOnlyList<DashboardPanel> Unregistered { get; set; } = Array.Empty<DashboardPanel>();
    }

    // ---- §8.2 Layout administrator — role template editor with preview-as-role (BR-DSH-003) ----

    public sealed class LayoutAdminViewModel
    {
        public sealed record RoleRow(Role Role, LayoutTemplate? Template, int WidgetCount, int MemberCount, bool IsPortalRole);

        /// <summary>
        /// <paramref name="RoleGrantsPermission"/> is doc §9's save-time warning
        /// ("widgets most role members can't see"); <paramref name="PortalReachable"/>
        /// is BR-DSH-006 via <c>PortalWidgetGate</c> for the parent/student roles.
        /// </summary>
        public sealed record Entry(LayoutTemplateWidget Row, WidgetDefinition Widget, Permission? Permission, bool RoleGrantsPermission, bool PortalReachable);

        public IReadOnlyList<RoleRow> Roles { get; set; } = Array.Empty<RoleRow>();

        public Role? Selected { get; set; }

        public bool SelectedIsPortalRole { get; set; }

        public LayoutTemplate? Template { get; set; }

        public IReadOnlyList<Entry> Entries { get; set; } = Array.Empty<Entry>();

        public IReadOnlyList<WidgetDefinition> Addable { get; set; } = Array.Empty<WidgetDefinition>();

        public int NextSortOrder => Entries.Count == 0 ? 10 : Entries.Max(e => e.Row.SortOrder) + 10;

        public int UnseeableCount => Entries.Count(e => !e.RoleGrantsPermission);

        public int PortalBlockedCount => Entries.Count(e => !e.PortalReachable);
    }
}
