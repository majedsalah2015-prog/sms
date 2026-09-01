using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Application.Discounts;
using Sms.Domain.Discounts;
using Sms.Domain.Fees;
using Sms.Domain.Schools;
using Sms.Domain.Students;

namespace Sms.Web.Models
{
    // ---------------------------------------------------------------- Discounts & Grants (doc/Modules/22 §8, E-502 screens)

    /// <summary>Bilingual labels for the Discounts enums (kept here — shared Labels.cs belongs to the parallel session, same convention as FinanceLabels).</summary>
    public static class DiscountLabels
    {
        public static string Basis(DiscountBasis b, bool ar) => b switch
        {
            DiscountBasis.Percentage => ar ? "نسبة مئوية" : "Percentage",
            DiscountBasis.FixedAmount => ar ? "مبلغ ثابت" : "Fixed amount",
            _ => b.ToString(),
        };

        public static string Stage(DiscountComputationStage s, bool ar) => s switch
        {
            DiscountComputationStage.BeforeVat => ar ? "قبل الضريبة" : "Before VAT",
            DiscountComputationStage.AfterVat => ar ? "بعد الضريبة" : "After VAT",
            _ => s.ToString(),
        };

        public static string EligibilityMode(DiscountEligibilityMode m, bool ar) => m switch
        {
            DiscountEligibilityMode.Automatic => ar ? "تلقائي" : "Automatic",
            DiscountEligibilityMode.Manual => ar ? "يدوي" : "Manual",
            DiscountEligibilityMode.Scholarship => ar ? "منحة دراسية" : "Scholarship",
            _ => m.ToString(),
        };

        public static string RenewalMode(DiscountRenewalMode m, bool ar) => m switch
        {
            DiscountRenewalMode.AutoReevaluate => ar ? "إعادة تقييم تلقائية" : "Auto re-evaluate",
            DiscountRenewalMode.ManualRegrant => ar ? "إعادة منح يدوية" : "Manual regrant",
            _ => m.ToString(),
        };

        public static string GrantSource(DiscountGrantSource s, bool ar) => s switch
        {
            DiscountGrantSource.Automatic => ar ? "تلقائي" : "Automatic",
            DiscountGrantSource.Manual => ar ? "يدوي" : "Manual",
            DiscountGrantSource.Scholarship => ar ? "منحة دراسية" : "Scholarship",
            DiscountGrantSource.Renewal => ar ? "تجديد" : "Renewal",
            _ => s.ToString(),
        };

        public static string GrantStatus(DiscountGrantStatus s, bool ar) => s switch
        {
            DiscountGrantStatus.Proposed => ar ? "مقترح" : "Proposed",
            DiscountGrantStatus.Approved => ar ? "معتمد" : "Approved",
            DiscountGrantStatus.Rejected => ar ? "مرفوض" : "Rejected",
            DiscountGrantStatus.Revoked => ar ? "ملغى" : "Revoked",
            _ => s.ToString(),
        };

        public static string Tier(ApprovalTier t, bool ar) => t switch
        {
            ApprovalTier.FinanceManager => ar ? "مدير مالي" : "Finance Manager",
            ApprovalTier.Principal => ar ? "مدير المدرسة" : "Principal",
            ApprovalTier.Owner => ar ? "المالك" : "Owner",
            ApprovalTier.Committee => ar ? "لجنة" : "Committee",
            _ => t.ToString(),
        };

        public static string WaiverKindLabel(WaiverKind k, bool ar) => k switch
        {
            WaiverKind.LateFee => ar ? "غرامة تأخير" : "Late fee",
            WaiverKind.BounceFee => ar ? "غرامة ارتداد شيك" : "Bounce fee",
            WaiverKind.Misc => ar ? "أخرى" : "Misc",
            _ => k.ToString(),
        };

        public static string WaiverStatusLabel(WaiverStatus s, bool ar) => s switch
        {
            WaiverStatus.Proposed => ar ? "مقترح" : "Proposed",
            WaiverStatus.Approved => ar ? "معتمد" : "Approved",
            WaiverStatus.Rejected => ar ? "مرفوض" : "Rejected",
            _ => s.ToString(),
        };

        public static string RenewalDecisionLabel(RenewalDecision d, bool ar) => d switch
        {
            RenewalDecision.Pending => ar ? "قيد الانتظار" : "Pending",
            RenewalDecision.Approved => ar ? "معتمد" : "Approved",
            RenewalDecision.Adjusted => ar ? "معدَّل" : "Adjusted",
            RenewalDecision.Dropped => ar ? "أُسقط" : "Dropped",
            _ => d.ToString(),
        };

        public static string StudentName(Student s, bool ar) => ar ? $"{s.FirstNameAr} {s.FatherNameAr} {s.FamilyNameAr}" : $"{s.FirstNameEn} {s.FatherNameEn} {s.FamilyNameEn}";
    }

    /// <summary>Year picker + type catalog every Discounts screen needs.</summary>
    public abstract class DiscountsPageViewModel
    {
        public IReadOnlyList<AcademicYear> Years { get; set; } = Array.Empty<AcademicYear>();

        public AcademicYear? Year { get; set; }

        public IReadOnlyList<DiscountType> Types { get; set; } = Array.Empty<DiscountType>();

        public IReadOnlyList<FeeCategory> Categories { get; set; } = Array.Empty<FeeCategory>();
    }

    public sealed record StudentOption(Student Student)
    {
        public string Label(bool ar) => $"{DiscountLabels.StudentName(Student, ar)} · {Student.StudentNo}";
    }

    // ---- Grant desk ----

    public sealed class GrantDeskViewModel : DiscountsPageViewModel
    {
        /// <summary>
        /// One desk row. <paramref name="Preview"/> is doc/Modules/22 §8.3's gross/net
        /// preview — what the grant would apply if it were approved now, and the family
        /// position BR-DIS-002 reads it against. Null only when the grant's type has
        /// been lost, which the desk renders as a dash rather than a zero.
        /// </summary>
        public sealed record Row(DiscountGrant Grant, DiscountType Type, Student Student, GrantPreview? Preview);

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        public DiscountGrantStatus? Status { get; set; }

        public int? TypeId { get; set; }

        public string? Q { get; set; }

        /// <summary>
        /// How many grants the filter matched in the database. The page shows at most
        /// one page of them, and saying so matters here: an operator who searched a
        /// student's number and sees three rows needs to know whether that is all of
        /// them or the first slice.
        /// </summary>
        public int MatchCount { get; set; }

        public IReadOnlyList<StudentOption> StudentOptions { get; set; } = Array.Empty<StudentOption>();

        public IReadOnlyList<DiscountType> AutomaticTypes => Types.Where(t => t.EligibilityMode == DiscountEligibilityMode.Automatic && t.IsActive).ToList();
    }

    // ---- Type catalog ----

    public sealed class TypeCatalogViewModel : DiscountsPageViewModel
    {
        public sealed record Row(DiscountType Type, FeeCategory? Category, int GrantCount);

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        /// <summary>The row the grid is editing in place, if any — the ?edit= on the URL.</summary>
        public int? EditId { get; set; }
    }

    // ---- Scholarship board ----

    public sealed class ScholarshipBoardViewModel : DiscountsPageViewModel
    {
        public sealed record Row(ScholarshipProgram Program, DiscountType Type, int ApprovedCount, decimal ApprovedAmount, int PendingCount);

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        public IReadOnlyList<StudentOption> StudentOptions { get; set; } = Array.Empty<StudentOption>();

        public IReadOnlyList<DiscountType> ScholarshipTypes => Types.Where(t => t.IsActive).ToList();
    }

    // ---- Renewal queue ----

    public sealed class RenewalQueueViewModel
    {
        public IReadOnlyList<AcademicYear> Years { get; set; } = Array.Empty<AcademicYear>();

        public int? FromYearId { get; set; }

        public int? ToYearId { get; set; }

        public sealed record Row(RenewalQueueItem Item, DiscountGrant PriorGrant, DiscountType Type, Student Student);

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();
    }

    // ---- Waiver desk ----

    public sealed class WaiverDeskViewModel
    {
        public sealed record ChargeMatch(Charge Charge, FeeCategory Category, Student Student, decimal Remaining);

        public sealed record Row(Waiver Waiver, Charge Charge, Student Student);

        public string? ChargeQ { get; set; }

        public IReadOnlyList<ChargeMatch> Matches { get; set; } = Array.Empty<ChargeMatch>();

        public WaiverStatus? Status { get; set; }

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();
    }
}
