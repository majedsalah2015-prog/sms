using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Application.Rollover;
using Sms.Domain.Audit;
using Sms.Domain.Rollover;
using Sms.Domain.Schools;

namespace Sms.Web.Models
{
    /// <summary>
    /// The School record's own fields and lifecycle, named in both languages —
    /// doc/Modules/02 §8. Two jobs, and they are the same job: the history panel
    /// stores raw invariant values (BR-AUD-005 keeps one stored truth and
    /// localizes at display), and the profile form has to say <em>which</em> field
    /// it is refusing to save without a reason. Both need the field's name as the
    /// screen calls it rather than as the CLR names it, so the table lives once.
    /// <para>
    /// <see cref="RequiresReason"/> mirrors the <c>[RequiresAuditReason]</c>
    /// attributes on <see cref="School"/> — the five fields that are T1-audited
    /// with a mandatory reason (BR-SCH-002 for the four identity fields, doc 02 §4
    /// for the status). It is a mirror, not the enforcement: the enforcement is the
    /// attribute, and the screen only uses this to say so <em>before</em> the save
    /// is refused instead of after.
    /// </para>
    /// </summary>
    public static class SchoolFieldLabels
    {
        /// <summary>The <c>[RequiresAuditReason]</c> set on <see cref="School"/>, in screen order.</summary>
        public static readonly string[] ReasonBearing =
        {
            nameof(School.NameAr),
            nameof(School.NameEn),
            nameof(School.LicenseNumber),
            nameof(School.MinistryCode),
            nameof(School.Status),
        };

        public static bool RequiresReason(string field) => ReasonBearing.Contains(field, StringComparer.Ordinal);

        public static string Name(string field, bool arabic) => field switch
        {
            nameof(School.NameAr) => arabic ? "الاسم الرسمي (عربي)" : "Official name (Arabic)",
            nameof(School.NameEn) => arabic ? "الاسم الرسمي (إنجليزي)" : "Official name (English)",
            nameof(School.LicenseNumber) => arabic ? "رقم الترخيص" : "Licence number",
            nameof(School.MinistryCode) => arabic ? "الرمز الوزاري" : "Ministry code",
            nameof(School.LicenseExpiryDate) => arabic ? "انتهاء الترخيص" : "Licence expiry",
            nameof(School.AddressLine) => arabic ? "العنوان" : "Address",
            nameof(School.City) => arabic ? "المدينة" : "City",
            nameof(School.ContactEmail) => arabic ? "البريد الرسمي" : "Official email",
            nameof(School.ContactPhone) => arabic ? "الهاتف" : "Phone",
            nameof(School.Website) => arabic ? "الموقع الإلكتروني" : "Website",
            nameof(School.TimeZoneId) => arabic ? "المنطقة الزمنية" : "Time zone",
            nameof(School.CurrencyCode) => arabic ? "العملة" : "Currency",
            nameof(School.Status) => arabic ? "حالة المدرسة" : "School status",
            nameof(School.Latitude) => arabic ? "خط العرض" : "Latitude",
            nameof(School.Longitude) => arabic ? "خط الطول" : "Longitude",
            nameof(School.CountryPackId) => arabic ? "حزمة الدولة" : "Country pack",
            nameof(School.SetupCompletedAtUtc) => arabic ? "اكتمال الإعداد" : "Setup completed",
            nameof(School.SchoolGroupId) => arabic ? "مجموعة المدارس" : "School group",
            _ => field,
        };

        /// <summary>
        /// The stored value as a reader should meet it. Only the status column carries
        /// a code the reader cannot be expected to know; everything else is already the
        /// text that was typed, and inventing a rendering for it would only lose detail.
        /// </summary>
        public static string Display(string? field, string? rawValue, bool arabic)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return "—";
            }

            if (string.Equals(field, nameof(School.Status), StringComparison.Ordinal)
                && Enum.TryParse<SchoolStatus>(rawValue, out var status))
            {
                return SchoolStatusLabels.Name(status, arabic);
            }

            return rawValue!;
        }

        public static string ActionName(AuditAction action, bool arabic) => action switch
        {
            AuditAction.Create => arabic ? "إنشاء" : "Created",
            AuditAction.Update => arabic ? "تعديل" : "Changed",
            AuditAction.StatusChange => arabic ? "تغيير حالة" : "Status change",
            AuditAction.View => arabic ? "اطّلاع" : "Viewed",
            AuditAction.Print => arabic ? "طباعة" : "Printed",
            AuditAction.Export => arabic ? "تصدير" : "Exported",
            _ => arabic ? "حدث" : "Event",
        };
    }

    /// <summary>
    /// BR-SCH-005's four states, plus what each one is understood to allow and
    /// forbid (doc/Modules/02 §4). <see cref="EnforcedToday"/> is the honest half:
    /// the transitions are enforced by <c>SchoolStatusTransitions</c>, but nothing
    /// in the request pipeline yet reads the school's status, so "Suspended blocks
    /// the portal" is a declared policy and not something this build does. A screen
    /// that states an enforcement the product does not perform is worse than one
    /// that admits the gap, because the operator plans around it.
    /// </summary>
    public static class SchoolStatusLabels
    {
        public static string Name(SchoolStatus status, bool arabic) => status switch
        {
            SchoolStatus.Setup => arabic ? "إعداد" : "Setup",
            SchoolStatus.Active => arabic ? "نشطة" : "Active",
            SchoolStatus.Suspended => arabic ? "موقوفة" : "Suspended",
            SchoolStatus.Closed => arabic ? "مغلقة" : "Closed",
            _ => status.ToString(),
        };

        public static string Badge(SchoolStatus status) => status switch
        {
            SchoolStatus.Active => "text-bg-success",
            SchoolStatus.Suspended => "text-bg-warning",
            SchoolStatus.Closed => "text-bg-dark",
            _ => "text-bg-secondary",
        };

        public static string Icon(SchoolStatus status) => status switch
        {
            SchoolStatus.Active => "bi-play-circle-fill",
            SchoolStatus.Suspended => "bi-pause-circle-fill",
            SchoolStatus.Closed => "bi-lock-fill",
            _ => "bi-tools",
        };

        /// <summary>One line on what the state is for.</summary>
        public static string Summary(SchoolStatus status, bool arabic) => status switch
        {
            SchoolStatus.Setup => arabic
                ? "مرحلة المعالج — تُدخَل فيها بيانات المدرسة قبل بدء التشغيل."
                : "The wizard phase — the school's own data is entered before it goes live.",
            SchoolStatus.Active => arabic ? "التشغيل العادي." : "Normal operation.",
            SchoolStatus.Suspended => arabic
                ? "إيقاف مؤقت قابل للرجوع — عادةً لسبب تعاقدي أو تنظيمي."
                : "A reversible pause — usually for a contractual or regulatory reason.",
            SchoolStatus.Closed => arabic
                ? "نهاية دورة الحياة — لا طريق للعودة من هذه الحالة."
                : "The end of the lifecycle — there is no path back from this state.",
            _ => string.Empty,
        };

        /// <summary>What the state is meant to permit (doc/Modules/02 §4, BR-SCH-005).</summary>
        public static IReadOnlyList<string> Allows(SchoolStatus status, bool arabic) => status switch
        {
            SchoolStatus.Setup => arabic
                ? new[] { "إدخال بيانات الإعداد وملف المدرسة", "تعريف الهيكل الدراسي والمستخدمين", "الانتقال إلى «نشطة» بعد اكتمال المعالج" }
                : new[] { "Entering setup data and the school profile", "Defining the stage structure and the users", "Moving to Active once the wizard is complete" },
            SchoolStatus.Active => arabic
                ? new[] { "كل عمليات المنتج", "تفعيل الأعوام الدراسية والقيد", "التحصيل والإصدار والتقارير" }
                : new[] { "Every operation the product offers", "Activating academic years and enrolling", "Collection, issuance and reporting" },
            SchoolStatus.Suspended => arabic
                ? new[] { "اطّلاع الموظفين على البيانات", "التحصيل المالي (قابل للضبط)", "إعادة التفعيل إلى «نشطة»" }
                : new[] { "Staff reading the data", "Fee collection (configurable)", "Reactivation back to Active" },
            SchoolStatus.Closed => arabic
                ? new[] { "الاطّلاع والتصدير فقط", "الاحتفاظ بالبيانات حسب حزمة الدولة" }
                : new[] { "Reading and export only", "Retention of the data per the country pack" },
            _ => Array.Empty<string>(),
        };

        /// <summary>What the state is meant to withhold (doc/Modules/02 §4, BR-SCH-005).</summary>
        public static IReadOnlyList<string> Forbids(SchoolStatus status, bool arabic) => status switch
        {
            SchoolStatus.Setup => arabic
                ? new[] { "تفعيل أول عام دراسي قبل إعلان اكتمال الإعداد (BR-SET-003)" }
                : new[] { "Activating the first academic year before setup is declared complete (BR-SET-003)" },
            SchoolStatus.Active => Array.Empty<string>(),
            SchoolStatus.Suspended => arabic
                ? new[] { "دخول بوابة أولياء الأمور والطلاب", "المعاملات الجديدة" }
                : new[] { "The parent and student portal", "New transactions" },
            SchoolStatus.Closed => arabic
                ? new[] { "كل كتابة، بلا استثناء", "العودة إلى أي حالة أخرى — الحالة نهائية" }
                : new[] { "Every write, without exception", "Returning to any other state — the status is terminal" },
            _ => Array.Empty<string>(),
        };

        /// <summary>
        /// Whether this build actually performs what <see cref="Forbids"/> describes.
        /// Only the transition rules are enforced today (<c>SchoolStatusTransitions</c>);
        /// no filter, pipeline or portal check reads <see cref="School.Status"/> yet, so
        /// Suspended and Closed are declarations of intent that an operator must not plan
        /// around. The screen says so rather than implying an enforcement that is absent.
        /// </summary>
        public static bool EnforcedToday(SchoolStatus status) =>
            status == SchoolStatus.Setup || status == SchoolStatus.Active;

        /// <summary>Closed is the only move the product cannot undo (BR-SCH-005).</summary>
        public static bool IsIrreversible(SchoolStatus target) => target == SchoolStatus.Closed;
    }

    /// <summary>
    /// One line of a screen's "what is still missing" list. <see cref="Required"/>
    /// separates a rule's own precondition — BR-SCH-001's four identity fields,
    /// which the school cannot be activated without — from a field that merely
    /// makes the product work better, so the operator can tell a gate from advice.
    /// </summary>
    public sealed record SchoolChecklistRow(
        string TitleEn,
        string TitleAr,
        string WhyEn,
        string WhyAr,
        bool Done,
        bool Required,
        string? Tab = null,
        string? Controller = null,
        string? Action = null);

    /// <summary>
    /// One audit row as the history panel shows it (BR-AUD-008 one-click history,
    /// BR-GLB-007 created/modified). The stored values stay raw; the localization
    /// happens here at display, per BR-AUD-005.
    /// </summary>
    public sealed record SchoolChangeRow(
        DateTime AtUtc,
        AuditAction Action,
        string? Field,
        string? OldValue,
        string? NewValue,
        string? Reason,
        int ActorUserId,
        string? ActorName);

    /// <summary>doc/Modules/02 §8.1 School profile (tabs: Identity, Licence &amp; Ministry, Contacts &amp; Location, Branding, Stages offered, History).</summary>
    public sealed class SchoolProfileViewModel
    {
        /// <summary>A stage the school teaches, with how many grades hang off it (BR-SCH-003).</summary>
        public sealed record StageRow(string Ar, string En, int Grades, bool IsActive);

        public int? SchoolId { get; set; }

        public string? NameAr { get; set; }

        public string? NameEn { get; set; }

        public string? LicenseNumber { get; set; }

        public string? MinistryCode { get; set; }

        public DateTime? LicenseExpiryDate { get; set; }

        public string? AddressLine { get; set; }

        public string? City { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        public string? ContactEmail { get; set; }

        public string? ContactPhone { get; set; }

        public string? Website { get; set; }

        public string? TimeZoneId { get; set; }

        public string? CurrencyCode { get; set; }

        public SchoolStatus? Status { get; set; }

        /// <summary>BR-SCH-002: identity edits require a reason.</summary>
        public string? Reason { get; set; }

        public string ActiveTab { get; set; } = "identity";

        /// <summary>The clock's today, so the licence countdown is not a view calling <c>DateTime.UtcNow</c>.</summary>
        public DateTime TodayUtc { get; set; }

        // ---------------------------------------------------------------- read-only context

        /// <summary>BR-GLB-007 stamps, shown inline the way every record in the product should show them.</summary>
        public DateTime? CreatedAtUtc { get; set; }

        public DateTime? ModifiedAtUtc { get; set; }

        public int? ModifiedByUserId { get; set; }

        public string? ModifiedByUserName { get; set; }

        /// <summary>E-101 / BR-SET-003: stamped by the wizard's "Setup Complete"; null while the wizard is unfinished.</summary>
        public DateTime? SetupCompletedAtUtc { get; set; }

        public string? CountryPackNameAr { get; set; }

        public string? CountryPackNameEn { get; set; }

        public string? CountryPackCode { get; set; }

        public IReadOnlyList<StageRow> StagesOffered { get; set; } = Array.Empty<StageRow>();

        /// <summary>Document classes with no signatory in force — printed documents would carry no signature block (BR-SCH-004).</summary>
        public IReadOnlyList<string> DocumentClassesWithoutSignatory { get; set; } = Array.Empty<string>();

        public IReadOnlyList<SchoolChecklistRow> Checklist { get; set; } = Array.Empty<SchoolChecklistRow>();

        public IReadOnlyList<SchoolChangeRow> History { get; set; } = Array.Empty<SchoolChangeRow>();

        // ---------------------------------------------------------------- derived

        public bool Exists => SchoolId != null;

        public IEnumerable<SchoolChecklistRow> Missing => Checklist.Where(c => !c.Done);

        public int RequiredMissing => Checklist.Count(c => c.Required && !c.Done);

        public int CompletionPercent => Checklist.Count == 0
            ? 0
            : (int)Math.Round(Checklist.Count(c => c.Done) * 100.0 / Checklist.Count);

        /// <summary>BR-SCH-001: the four identity fields are mandatory before the school may be activated.</summary>
        public bool IdentityComplete => RequiredMissing == 0;

        /// <summary>Days until the licence lapses; negative once it has. Null when no expiry was entered.</summary>
        public int? LicenceDaysRemaining(DateTime todayUtc) =>
            LicenseExpiryDate == null ? null : (int)(LicenseExpiryDate.Value.Date - todayUtc.Date).TotalDays;
    }

    /// <summary>doc/Modules/02 §8.2 Signatories per document class with history (BR-SCH-004).</summary>
    public sealed class SignatoriesViewModel
    {
        public static readonly (string Code, string En, string Ar)[] DocumentClasses =
        {
            ("CERT", "Certificates", "الشهادات"),
            ("FIN", "Financial documents", "المستندات المالية"),
            ("LETTER", "Official letters", "الخطابات الرسمية"),
            ("REPORT", "Report cards", "كشوف الدرجات"),
        };

        public static string ClassName(string? code, bool arabic)
        {
            foreach (var c in DocumentClasses)
            {
                if (string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase))
                {
                    return arabic ? c.Ar : c.En;
                }
            }

            return code ?? string.Empty;
        }

        public IReadOnlyList<Signatory> Signatories { get; set; } = Array.Empty<Signatory>();

        public string? DocumentClassCode { get; set; }

        public string? NameAr { get; set; }

        public string? NameEn { get; set; }

        public string? TitleAr { get; set; }

        public string? TitleEn { get; set; }

        public DateTime? EffectiveFrom { get; set; }

        /// <summary>The signatory in force for a class right now, or null when the class would print unsigned.</summary>
        public Signatory? Current(string code) =>
            Signatories.FirstOrDefault(s => s.DocumentClassCode == code && s.EffectiveToUtc == null);

        public IEnumerable<Signatory> ForClass(string code) =>
            Signatories.Where(s => s.DocumentClassCode == code);

        public int CoveredClasses => DocumentClasses.Count(c => Current(c.Code) != null);
    }

    /// <summary>doc/Modules/02 §8.3 School status console with reason capture (BR-SCH-005).</summary>
    public sealed class SchoolStatusViewModel
    {
        /// <summary>
        /// One row of the transition map. Every state is listed, not only the legal
        /// ones: a console that silently omits a move leaves the operator guessing
        /// whether it is forbidden or merely missing, so the refusal is shown with
        /// the reason for it.
        /// </summary>
        public sealed record TransitionRow(SchoolStatus Target, bool Allowed, bool Irreversible);

        public School? School { get; set; }

        public IReadOnlyList<SchoolStatus> AllowedTargets { get; set; } = Array.Empty<SchoolStatus>();

        public IReadOnlyList<TransitionRow> Transitions { get; set; } = Array.Empty<TransitionRow>();

        public SchoolStatus? Target { get; set; }

        public string? Reason { get; set; }

        /// <summary>
        /// The operator's explicit acknowledgement, demanded for a move that cannot be
        /// undone and for one the docs gate on a condition this build does not check
        /// (doc/Modules/02 §4 vs <c>SchoolStatusTransitions</c>). It is a deliberate
        /// override affordance, not a second Save button.
        /// </summary>
        public bool Acknowledged { get; set; }

        /// <summary>BR-SCH-001 and BR-SET-003 — what the docs expect before Setup → Active.</summary>
        public IReadOnlyList<SchoolChecklistRow> ActivationReadiness { get; set; } = Array.Empty<SchoolChecklistRow>();

        public IReadOnlyList<SchoolChangeRow> History { get; set; } = Array.Empty<SchoolChangeRow>();

        public bool ReadyToActivate => ActivationReadiness.All(r => r.Done || !r.Required);
    }

    /// <summary>doc/Modules/03 §8.1 Year list & status board.</summary>
    public sealed class YearBoardViewModel
    {
        public sealed class Row
        {
            public AcademicYear Year { get; set; } = null!;

            public int Semesters { get; set; }

            public int Terms { get; set; }

            /// <summary>Student enrollments in this year; edit/delete are offered only when zero.</summary>
            public int Enrollments { get; set; }

            public bool CanEditOrDelete => Enrollments == 0;

            public RolloverBatch? IncomingBatch { get; set; }

            public IReadOnlyList<ChecklistItem> OpeningChecklist { get; set; } = Array.Empty<ChecklistItem>();

            public RolloverBatch? OutgoingBatch { get; set; }

            public IReadOnlyList<ChecklistItem> ClosingChecklist { get; set; } = Array.Empty<ChecklistItem>();
        }

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        public int WorkingYearId { get; set; }

        public AcademicYear? WorkingYear { get; set; }

        public AcademicYear? ActiveYear { get; set; }

        public bool SetupComplete { get; set; }
    }

    /// <summary>doc/Modules/03 §8.2 Year definition + semester/term builder.</summary>
    public sealed class YearDefinitionViewModel
    {
        public int? YearId { get; set; }

        public AcademicYear? Year { get; set; }

        public string? LabelAr { get; set; }

        public string? LabelEn { get; set; }

        public string? HijriLabel { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        /// <summary>Optional audit reason for edits (T2 audit — not mandatory).</summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Student enrolments in this year. Non-zero locks the dates — semesters,
        /// terms, calendar days, timetables and attendance all nest inside the
        /// span — while leaving the labels editable, since renaming a year moves
        /// nothing.
        /// </summary>
        public int EnrolledCount { get; set; }

        public bool DatesLocked => EnrolledCount > 0;

        public IReadOnlyList<Semester> Semesters { get; set; } = Array.Empty<Semester>();

        public IReadOnlyList<Term> Terms { get; set; } = Array.Empty<Term>();

        // semester form
        public int? SemesterSequence { get; set; }

        public string? SemesterNameAr { get; set; }

        public string? SemesterNameEn { get; set; }

        public DateTime? SemesterStart { get; set; }

        public DateTime? SemesterEnd { get; set; }

        // term form
        public int? TermSemesterId { get; set; }

        public int? TermSequence { get; set; }

        public string? TermNameAr { get; set; }

        public string? TermNameEn { get; set; }

        public DateTime? TermStart { get; set; }

        public DateTime? TermEnd { get; set; }
    }
}
