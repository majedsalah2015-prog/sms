using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Application.Payments;
using Sms.Application.ReadModels;
using Sms.Application.Statements;
using Sms.Domain.Fees;
using Sms.Domain.Grades;
using Sms.Domain.Parents;
using Sms.Domain.Payments;
using Sms.Domain.Schools;
using Sms.Domain.Students;

namespace Sms.Web.Models
{
    // ---------------------------------------------------------------- Finance (doc/Modules/19 + 21 §8, E-303 screens)

    /// <summary>Bilingual labels for the finance enums (kept here — shared Labels.cs belongs to the parallel session).</summary>
    public static class FinanceLabels
    {
        public static string ChargeSource(ChargeSourceType s, bool ar) => s switch
        {
            ChargeSourceType.Registration => ar ? "تسجيل" : "Registration",
            ChargeSourceType.ReRegistration => ar ? "إعادة تسجيل" : "Re-registration",
            ChargeSourceType.ServiceAssignment => ar ? "خدمة" : "Service",
            ChargeSourceType.Manual => ar ? "يدوي" : "Manual",
            ChargeSourceType.OpeningBalance => ar ? "رصيد افتتاحي" : "Opening balance",
            _ => s.ToString(),
        };

        public static string ChargeStatus(ChargeStatus s, bool ar) => s switch
        {
            Domain.Fees.ChargeStatus.Posted => ar ? "مرحّلة" : "Posted",
            Domain.Fees.ChargeStatus.Void => ar ? "ملغاة" : "Void",
            _ => s.ToString(),
        };

        public static string LineStatus(FeeStructureLineStatus s, bool ar) => s switch
        {
            FeeStructureLineStatus.Draft => ar ? "مسودة" : "Draft",
            FeeStructureLineStatus.Approved => ar ? "معتمد" : "Approved",
            _ => s.ToString(),
        };

        public static string Method(PaymentMethod m, bool ar) => m switch
        {
            PaymentMethod.Cash => ar ? "نقداً" : "Cash",
            PaymentMethod.Card => ar ? "بطاقة" : "Card",
            PaymentMethod.BankTransfer => ar ? "تحويل بنكي" : "Bank transfer",
            PaymentMethod.Cheque => ar ? "شيك" : "Cheque",
            PaymentMethod.Pdc => ar ? "شيك آجل" : "PDC",
            _ => m.ToString(),
        };

        public static string PdcStatus(PdcStatus s, bool ar) => s switch
        {
            Domain.Payments.PdcStatus.Lodged => ar ? "مودَع" : "Lodged",
            Domain.Payments.PdcStatus.Due => ar ? "مستحق" : "Due",
            Domain.Payments.PdcStatus.Deposited => ar ? "أُودع بالبنك" : "Deposited",
            Domain.Payments.PdcStatus.Cleared => ar ? "مُحصَّل" : "Cleared",
            Domain.Payments.PdcStatus.Bounced => ar ? "مرتجع" : "Bounced",
            Domain.Payments.PdcStatus.Replaced => ar ? "مستبدَل" : "Replaced",
            Domain.Payments.PdcStatus.Settled => ar ? "مسوّى" : "Settled",
            _ => s.ToString(),
        };

        public static string RefundStatus(RefundVoucherStatus s, bool ar) => s switch
        {
            RefundVoucherStatus.Requested => ar ? "مطلوب" : "Requested",
            RefundVoucherStatus.Approved => ar ? "معتمد" : "Approved",
            RefundVoucherStatus.Paid => ar ? "مدفوع" : "Paid",
            RefundVoucherStatus.Rejected => ar ? "مرفوض" : "Rejected",
            _ => s.ToString(),
        };

        public static string StatementKind(StatementLineKind k, bool ar) => k switch
        {
            StatementLineKind.Charge => ar ? "فاتورة" : "Invoice",
            StatementLineKind.CreditNote => ar ? "إشعار دائن" : "Credit note",
            StatementLineKind.Discount => ar ? "خصم" : "Discount",
            StatementLineKind.Payment => ar ? "سند قبض" : "Receipt",
            StatementLineKind.Refund => ar ? "سند استرداد" : "Refund",
            _ => k.ToString(),
        };

        public static string Aging(AgingBucket b, bool ar) => b switch
        {
            AgingBucket.Current => ar ? "جاري" : "Current",
            AgingBucket.Days1To30 => ar ? "1–30 يوم" : "1–30 days",
            AgingBucket.Days31To60 => ar ? "31–60 يوم" : "31–60 days",
            AgingBucket.Days61To90 => ar ? "61–90 يوم" : "61–90 days",
            AgingBucket.Over90 => ar ? "+90 يوم" : "90+ days",
            _ => b.ToString(),
        };

        public static string StudentName(Student s, bool ar) => ar ? $"{s.FirstNameAr} {s.FatherNameAr} {s.FamilyNameAr}" : $"{s.FirstNameEn} {s.FatherNameEn} {s.FamilyNameEn}";

        public static string ParentName(Parent? p, bool ar) => p == null ? "—" : (ar ? p.NameAr : p.NameEn);
    }

    /// <summary>A payer as the screens show it: the Payer row + its parent + the children it pays for.</summary>
    public sealed record PayerCard(Payer Payer, Parent? Parent, IReadOnlyList<Student> Children)
    {
        public string Label(bool ar) => Parent == null ? $"#{Payer.Id}" : $"{FinanceLabels.ParentName(Parent, ar)} · {Parent.ParentFileNo}";
    }

    /// <summary>One posted charge with everything subtracted from it (BR-FEE-008 single math, BR-DIS-010 separated).</summary>
    public sealed record OpenChargeRow(Charge Charge, FeeCategory Category, Student Student, decimal Credited, decimal Discounted, decimal Allocated)
    {
        public decimal Remaining => Charge.GrossAmount - Credited - Discounted - Allocated;
    }

    /// <summary>Year picker + catalog every fees screen needs.</summary>
    public abstract class FinancePageViewModel
    {
        public IReadOnlyList<AcademicYear> Years { get; set; } = Array.Empty<AcademicYear>();

        public AcademicYear? Year { get; set; }

        public IReadOnlyList<FeeCategory> Categories { get; set; } = Array.Empty<FeeCategory>();
    }

    // ---- 19 §8.1 Category catalog ----

    public sealed class FeeCategoryCatalogViewModel : FinancePageViewModel
    {
        public sealed record Row(FeeCategory Category, int LineCount, int ChargeCount);

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        public decimal? DefaultVatRate { get; set; }

        public int? EditId { get; set; }
    }

    // ---- 19 §8.2 Fee structure workbench ----

    public sealed class FeeStructureViewModel : FinancePageViewModel
    {
        public sealed record ProfileRow(GradeYearProfile Profile, GradeLevel Grade, Stage Stage, int Enrolled);

        public IReadOnlyList<ProfileRow> Profiles { get; set; } = Array.Empty<ProfileRow>();

        /// <summary>Keyed by (profileId, categoryId).</summary>
        public Dictionary<(int, int), FeeStructureLine> Lines { get; set; } = new();

        public int DraftCount => Lines.Values.Count(l => l.Status == FeeStructureLineStatus.Draft);

        public int ApprovedCount => Lines.Values.Count(l => l.Status == FeeStructureLineStatus.Approved);

        public AcademicYear? PreviousYear { get; set; }
    }

    // ---- 19 §8.3 Charge explorer ----

    public sealed class ChargeExplorerViewModel : FinancePageViewModel
    {
        public sealed record Row(Charge Charge, FeeCategory Category, Student Student, Parent? Payer, decimal Remaining);

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        public string? Q { get; set; }

        public int? CategoryId { get; set; }

        public ChargeSourceType? Source { get; set; }

        public ChargeStatus? Status { get; set; }

        public bool OpenOnly { get; set; }

        public decimal TotalGross { get; set; }

        public decimal TotalRemaining { get; set; }

        public int PayerCount { get; set; }
    }

    // ---- 19 §8.4 Misc charge entry ----

    public sealed class MiscChargeViewModel : FinancePageViewModel
    {
        public sealed record StudentOption(Student Student, GradeLevel? Grade, GradeYearProfile? Profile);

        public sealed record PayerOption(Parent Parent, bool IsFinanciallyResponsible, Payer? Payer);

        public IReadOnlyList<StudentOption> Students { get; set; } = Array.Empty<StudentOption>();

        public Student? Selected { get; set; }

        public GradeYearProfile? SelectedProfile { get; set; }

        public IReadOnlyList<PayerOption> Payers { get; set; } = Array.Empty<PayerOption>();

        /// <summary>Approved structure lines for the selected student's grade-year (the "post from structure" path).</summary>
        public IReadOnlyList<(FeeStructureLine Line, FeeCategory Category, bool AlreadyCharged)> StructureLines { get; set; } = Array.Empty<(FeeStructureLine, FeeCategory, bool)>();
    }

    // ---- 19 §8.3 document view + §8.5 credit note flow ----

    public sealed class ChargeDocumentViewModel
    {
        public Charge Charge { get; set; } = null!;

        public FeeCategory Category { get; set; } = null!;

        public Student Student { get; set; } = null!;

        public Parent? Payer { get; set; }

        public AcademicYear Year { get; set; } = null!;

        public GradeLevel? Grade { get; set; }

        public string SchoolNameAr { get; set; } = "";

        public string SchoolNameEn { get; set; } = "";

        public string? SchoolAddress { get; set; }

        public string? VatRegistrationNumber { get; set; }

        public string? ZatcaQrPayload { get; set; }

        public IReadOnlyList<CreditNote> CreditNotes { get; set; } = Array.Empty<CreditNote>();

        public IReadOnlyList<(PaymentAllocation Allocation, Receipt Receipt)> Allocations { get; set; } = Array.Empty<(PaymentAllocation, Receipt)>();

        public decimal Discounted { get; set; }

        public decimal Credited => CreditNotes.Sum(n => n.Amount);

        public decimal Allocated => Allocations.Sum(a => a.Allocation.AllocatedAmount);

        public decimal Remaining => Charge.GrossAmount - Credited - Discounted - Allocated;

        public bool IsPrint { get; set; }
    }

    // ---- 19 §8.7 Student/payer position ----

    public sealed class PayerPositionViewModel
    {
        public IReadOnlyList<PayerCard> Payers { get; set; } = Array.Empty<PayerCard>();

        public PayerCard? Selected { get; set; }

        public string? Q { get; set; }

        public DateTime? AsOf { get; set; }

        public PayerStatement? Statement { get; set; }

        public IReadOnlyList<OpenChargeRow> AllCharges { get; set; } = Array.Empty<OpenChargeRow>();

        public IReadOnlyList<OpenChargeRow> OpenCharges { get; set; } = Array.Empty<OpenChargeRow>();

        public Dictionary<AgingBucket, decimal> Aging { get; set; } = new();

        public decimal AdvanceBalance { get; set; }

        public IReadOnlyList<(Student Student, decimal Position)> PerChild { get; set; } = Array.Empty<(Student, decimal)>();
    }

    // ---- 21 §8.1 Cashier ----

    public sealed class CashierViewModel
    {
        public IReadOnlyList<PayerCard> Matches { get; set; } = Array.Empty<PayerCard>();

        public string? Q { get; set; }

        public PayerCard? Selected { get; set; }

        public IReadOnlyList<OpenChargeRow> OpenCharges { get; set; } = Array.Empty<OpenChargeRow>();

        public decimal TotalDue => OpenCharges.Sum(c => c.Remaining);

        public decimal AdvanceBalance { get; set; }

        public decimal? PreviewAmount { get; set; }

        public IReadOnlyList<(OpenChargeRow Row, decimal Amount)> Preview { get; set; } = Array.Empty<(OpenChargeRow, decimal)>();

        public decimal PreviewLeftover { get; set; }

        public IReadOnlyList<TillSession> OpenSessions { get; set; } = Array.Empty<TillSession>();

        public TillSession? MySession { get; set; }

        public IReadOnlyList<(Receipt Receipt, Parent? Payer)> RecentReceipts { get; set; } = Array.Empty<(Receipt, Parent?)>();
    }

    public sealed class ReceiptViewModel
    {
        public Receipt Receipt { get; set; } = null!;

        public PayerCard Payer { get; set; } = null!;

        public IReadOnlyList<(PaymentAllocation Allocation, Charge Charge, FeeCategory Category, Student Student)> Allocations { get; set; } = Array.Empty<(PaymentAllocation, Charge, FeeCategory, Student)>();

        public decimal Allocated => Allocations.Sum(a => a.Allocation.AllocatedAmount);

        public decimal Unallocated => Receipt.Amount - Allocated;

        public TillSession? Session { get; set; }

        public string SchoolNameAr { get; set; } = "";

        public string SchoolNameEn { get; set; } = "";

        public decimal PositionAfter { get; set; }

        public bool IsPrint { get; set; }
    }

    // ---- 21 §8.2 Till session console ----

    public sealed class TillConsoleViewModel
    {
        public sealed record SessionRow(TillSession Session, int ReceiptCount, decimal Total, Dictionary<PaymentMethod, decimal> ByMethod);

        public IReadOnlyList<SessionRow> Open { get; set; } = Array.Empty<SessionRow>();

        public IReadOnlyList<SessionRow> Closed { get; set; } = Array.Empty<SessionRow>();

        public int CurrentUserId { get; set; }

        public SessionRow? Closing { get; set; }
    }

    // ---- 21 §8.3 PDC registry ----

    public sealed class PdcRegistryViewModel
    {
        public sealed record Row(Pdc Pdc, Parent? Payer, IReadOnlyList<PdcStatus> Next, bool DueThisWeek, bool Overdue);

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        public PdcStatus? Filter { get; set; }

        public Dictionary<PdcStatus, int> Counts { get; set; } = new();

        public IReadOnlyList<PayerCard> Payers { get; set; } = Array.Empty<PayerCard>();

        public DateTime Today { get; set; }
    }

    // ---- 21 §8.4 Refund desk ----

    public sealed class RefundDeskViewModel
    {
        public sealed record Row(RefundVoucher Voucher, Parent? Payer, IReadOnlyList<RefundVoucherStatus> Next);

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        public IReadOnlyList<PayerCard> Payers { get; set; } = Array.Empty<PayerCard>();

        public PayerCard? Selected { get; set; }

        public decimal AdvanceBalance { get; set; }

        public decimal Committed { get; set; }

        public decimal Refundable => AdvanceBalance - Committed;

        public RefundVoucherStatus? Filter { get; set; }
    }

    // ---- 21 §8.5 Allocation explorer ----

    public sealed class AllocationExplorerViewModel
    {
        public sealed record ReceiptRow(Receipt Receipt, IReadOnlyList<(PaymentAllocation Allocation, Charge Charge, FeeCategory Category, Student Student)> Allocations)
        {
            public decimal Allocated => Allocations.Sum(a => a.Allocation.AllocatedAmount);
        }

        public IReadOnlyList<PayerCard> Payers { get; set; } = Array.Empty<PayerCard>();

        public PayerCard? Selected { get; set; }

        public IReadOnlyList<ReceiptRow> Receipts { get; set; } = Array.Empty<ReceiptRow>();

        public decimal TotalReceived => Receipts.Sum(r => r.Receipt.Amount);

        public decimal TotalAllocated => Receipts.Sum(r => r.Allocated);
    }
}
