using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Application.GlExport;
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

        /// <summary>
        /// The ledger's five account classifications, for the chart-of-accounts
        /// picker. <c>Unspecified</c> is rendered as an em dash rather than the
        /// word: the ledger declining to classify an account is not a fact worth
        /// putting a label on, and the account is offered either way.
        /// </summary>
        public static string AccountNature(GlAccountNature n, bool ar) => n switch
        {
            GlAccountNature.Asset => ar ? "أصول" : "Asset",
            GlAccountNature.Liability => ar ? "التزامات" : "Liability",
            GlAccountNature.Equity => ar ? "حقوق ملكية" : "Equity",
            GlAccountNature.Revenue => ar ? "إيرادات" : "Revenue",
            GlAccountNature.Expense => ar ? "مصروفات" : "Expense",
            _ => "—",
        };

        public static string StudentName(Student s, bool ar) => ar ? $"{s.FirstNameAr} {s.FatherNameAr} {s.FamilyNameAr}" : $"{s.FirstNameEn} {s.FatherNameEn} {s.FamilyNameEn}";

        public static string ParentName(Parent? p, bool ar) => p == null ? "—" : (ar ? p.NameAr : p.NameEn);
    }

    /// <summary>
    /// A child as it appears on a payer's card: the student, what this payer is to them, and
    /// whether the school bills this payer for them. BR-PAR-005 assigns financial responsibility
    /// per child, so the answer differs sibling to sibling and a bare name cannot carry it.
    /// An empty relationship means no live guardian link — a former guardian the old charges
    /// still name (see <see cref="PayerResponsibilityEvaluator"/>).
    /// </summary>
    public sealed record PayerChild(Student Student, string RelationshipAr, string RelationshipEn, bool IsFinanciallyResponsible)
    {
        public string Relationship(bool ar) => ar ? RelationshipAr : RelationshipEn;

        public bool IsCurrentGuardian => !string.IsNullOrWhiteSpace(RelationshipAr) || !string.IsNullOrWhiteSpace(RelationshipEn);
    }

    /// <summary>A payer as the screens show it: the Payer row + its parent + the children it pays for.</summary>
    public sealed record PayerCard(Payer Payer, Parent? Parent, IReadOnlyList<PayerChild> Children)
    {
        public string Label(bool ar) => Parent == null ? $"#{Payer.Id}" : $"{FinanceLabels.ParentName(Parent, ar)} · {Parent.ParentFileNo}";

        /// <summary>The children as plain students, for the screens that only name them.</summary>
        public IReadOnlyList<Student> Students => Children.Select(c => c.Student).ToList();

        /// <summary>
        /// BR-FEE-004: the school bills this payer for none of the children on the card. Set from
        /// <see cref="PayerResponsibilityEvaluator.IsResponsibleForNothing"/> when the card is built
        /// with its children; left false on the list screens that carry no children and take no money.
        /// </summary>
        public bool IsResponsibleForNothing { get; init; }

        /// <summary>True when responsibility is split across this card — BR-PAR-005's divorced-parents case.</summary>
        public bool HasSplitResponsibility => Children.Any(c => c.IsFinanciallyResponsible) && Children.Any(c => !c.IsFinanciallyResponsible);
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

        /// <summary>
        /// The attached ledger's postable accounts, so the GL export code is
        /// chosen from the real chart rather than typed from memory
        /// (docs/Integration/00 — the free-text account code is named there as the
        /// interface's one remaining gap). Empty when no ledger is attached, which
        /// is a supported way to run: the field stays free text.
        /// </summary>
        public IReadOnlyList<GlAccountOption> GlAccounts { get; set; } = Array.Empty<GlAccountOption>();

        /// <summary>True when a ledger answered — the screen may offer a chart to pick from.</summary>
        public bool HasLedger => GlAccounts.Count > 0;

        /// <summary>
        /// The chart entry a stored code names, or <c>null</c> when the chart does
        /// not have it. Null with <see cref="HasLedger"/> true is worth showing the
        /// operator: it is either a code kept for an accountant's own ledger, or the
        /// transposed digit this picker exists to prevent.
        /// </summary>
        public GlAccountOption? FindAccount(string? code) =>
            string.IsNullOrWhiteSpace(code) ? null : GlAccounts.FirstOrDefault(a => a.Code == code.Trim());
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

    /// <summary>
    /// A guardian this school has never billed. There is no <c>Payer</c> row behind them — one is
    /// created by the first charge (BR-FEE-004) — so they carry no statement, no aging and no
    /// balance. They are listed anyway because the alternative is what this screen used to do:
    /// answer "no matching payers" for a guardian who is plainly on file, which reads as the
    /// person not existing rather than as their account not having been opened.
    /// </summary>
    public sealed record UnbilledGuardian(Parent Parent, IReadOnlyList<Student> Children);

    public sealed class PayerPositionViewModel
    {
        public IReadOnlyList<PayerCard> Payers { get; set; } = Array.Empty<PayerCard>();

        public PayerCard? Selected { get; set; }

        /// <summary>Guardians matching the search who have never been billed — offered under the payers, not mixed into them.</summary>
        public IReadOnlyList<UnbilledGuardian> Unbilled { get; set; } = Array.Empty<UnbilledGuardian>();

        /// <summary>The unbilled guardian being read, when the reader picked one of them instead of a payer.</summary>
        public UnbilledGuardian? SelectedUnbilled { get; set; }

        /// <summary>How many guardians on file have never been billed at all — the number that explains a short payer list.</summary>
        public int UnbilledTotal { get; set; }

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

        /// <summary>
        /// BR-STU-008: a photograph is shown "per permission", and the cashier's screen is not the
        /// place that grants it. False renders the children as names alone rather than as broken
        /// image frames — the photo endpoint answers <c>NotFound</c> to a user without
        /// <c>STU/File/View</c>, which is correct and would look like a fault here.
        /// </summary>
        public bool CanSeeStudentPhotos { get; set; }

        /// <summary>
        /// The school's own accounts a payment may be collected into, every
        /// kind in one list (BR-PAY-002). The screen narrows it to the chosen
        /// method's kind in the browser, and shows the selected account's IBAN
        /// beside it — a parent asking where to send a transfer is asking for
        /// that number, and the cashier had nowhere to read it from.
        /// </summary>
        public IReadOnlyList<CollectionAccountOption> CollectionAccounts { get; set; } = Array.Empty<CollectionAccountOption>();
    }

    public sealed class ReceiptViewModel
    {
        public Receipt Receipt { get; set; } = null!;

        public PayerCard Payer { get; set; } = null!;

        public IReadOnlyList<(PaymentAllocation Allocation, Charge Charge, FeeCategory Category, Student Student)> Allocations { get; set; } = Array.Empty<(PaymentAllocation, Charge, FeeCategory, Student)>();

        public decimal Allocated => Allocations.Sum(a => a.Allocation.AllocatedAmount);

        public decimal Unallocated => Receipt.Amount - Allocated;

        public TillSession? Session { get; set; }

        /// <summary>Where the money went — null on a receipt issued before the catalogue existed, and read past the soft-active filter so a retired account still prints.</summary>
        public CollectionAccount? CollectionAccount { get; set; }

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

        /// <summary>
        /// The drawer the cashier will be given when they press Open — shown rather than asked for,
        /// because a till code is the system's own key and never a cashier's decision (BR-PAY-001).
        /// </summary>
        public string NextTillCode { get; set; } = string.Empty;

        /// <summary>True once this cashier has held a till before, so the screen can say "your till" rather than "a new till".</summary>
        public bool NextTillIsReturning { get; set; }
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
