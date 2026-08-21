using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Common.Interfaces;
using Sms.Application.Payments;
using Sms.Domain.Fees;
using Sms.Domain.Payments;
using Sms.Domain.Students;
using Sms.Infrastructure.Persistence;
using Sms.Web.Finance;
using Sms.Web.Models;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/21 §8 — E-303 screens over IPaymentAdmin: 8.1 Cashier
    /// (payer search → position → capture → auto-allocation preview → receipt),
    /// 8.2 Till session console (open/float, live totals by method, close
    /// with count + variance reason), 8.3 PDC registry (lifecycle board,
    /// due-this-week, bounce handling), 8.4 Refund desk (position check,
    /// voucher chain), 8.5 Allocation explorer. Deferred: manual allocation
    /// override / reversal (engine allocates oldest-first only), 8.6 day
    /// close &amp; bank reconciliation, 8.7 portal pay-now (BR-PAY-007 dormant),
    /// receipt void.
    /// </summary>
    [Route("payments")]
    public class PaymentsController : Controller
    {
        private readonly IPaymentAdmin _payments;
        private readonly AppDbContext _db;
        private readonly IAuditContext _audit;
        private readonly ICurrentUser _user;
        private readonly IClock _clock;

        public PaymentsController(IPaymentAdmin payments, AppDbContext db, IAuditContext audit, ICurrentUser user, IClock clock)
        {
            _payments = payments;
            _db = db;
            _audit = audit;
            _user = user;
            _clock = clock;
        }

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        // ================================================================== 8.1 Cashier

        [HttpGet("")]
        [RequirePermission(ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Cashier, ActionVerb.View)]
        public async Task<IActionResult> Index(string? q = null, int? payerId = null, decimal? amount = null)
        {
            var m = new CashierViewModel { Q = q, PreviewAmount = amount };
            m.OpenSessions = await _db.TillSessions.AsNoTracking().Where(s => s.Status == TillSessionStatus.Open).OrderBy(s => s.OpenedAtUtc).ToListAsync();
            m.MySession = m.OpenSessions.FirstOrDefault(s => s.CashierUserId == _user.UserId);
            if (!string.IsNullOrWhiteSpace(q))
            {
                m.Matches = await FinanceQueries.SearchPayersAsync(_db, q, take: 20);
                if (payerId == null && m.Matches.Count == 1) payerId = m.Matches[0].Payer.Id;
            }
            if (payerId != null)
            {
                m.Selected = await FinanceQueries.CardAsync(_db, payerId.Value);
                if (m.Selected == null) return NotFound();
                m.OpenCharges = await FinanceQueries.ChargeRowsAsync(_db, payerId: payerId);
                m.AdvanceBalance = await FinanceQueries.AdvanceBalanceAsync(_db, payerId.Value);
                var preview = amount ?? m.TotalDue;
                if (preview > 0)
                {
                    var (lines, leftover) = FinanceQueries.PreviewAllocation(preview, m.OpenCharges);
                    m.Preview = lines; m.PreviewLeftover = leftover; m.PreviewAmount = preview;
                }
            }
            var recent = await _db.Receipts.AsNoTracking().OrderByDescending(r => r.Id).Take(8).ToListAsync();
            var cards = await FinanceQueries.CardsAsync(_db, await _db.Payers.AsNoTracking().Where(p => recent.Select(r => r.PayerId).Contains(p.Id)).ToListAsync(), includeChildren: false);
            m.RecentReceipts = recent.Select(r => (r, cards.FirstOrDefault(c => c.Payer.Id == r.PayerId)?.Parent)).ToList();
            return View(m);
        }

        [HttpPost("receipts/new")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Cashier, ActionVerb.Create)]
        public async Task<IActionResult> Capture(int payerId, decimal amount, PaymentMethod method, string? methodRefNo, int? tillSessionId, string? q)
        {
            try
            {
                if (amount <= 0) throw new InvalidOperationException(T("Enter a positive amount.", "أدخل مبلغاً موجباً."));
                if (method == PaymentMethod.Pdc) throw new InvalidOperationException(T("Post-dated cheques are lodged in the PDC registry; the receipt is issued on clearance.", "الشيكات الآجلة تُسجَّل في سجل الشيكات؛ يصدر السند عند التحصيل."));
                if (method != PaymentMethod.Cash && string.IsNullOrWhiteSpace(methodRefNo)) throw new InvalidOperationException(T("A reference (card slip / transfer / cheque no.) is required for non-cash methods.", "المرجع (قسيمة البطاقة / التحويل / رقم الشيك) مطلوب لغير النقد."));
                if (method == PaymentMethod.Cash && tillSessionId == null) throw new InvalidOperationException(T("Cash needs an open till session — open one in the Till console.", "النقد يتطلب جلسة صندوق مفتوحة — افتح واحدة من وحدة الصندوق."));
                var receipt = await _payments.CaptureReceiptAsync(payerId, method, amount, tillSessionId, string.IsNullOrWhiteSpace(methodRefNo) ? null : methodRefNo.Trim());
                TempData["Flash"] = T($"Receipt {receipt.ReceiptNo} issued for {receipt.Amount:N2} and allocated oldest-first (BR-PAY-003).", $"صدر السند {receipt.ReceiptNo} بمبلغ {receipt.Amount:N2} وخُصّص للأقدم أولاً (BR-PAY-003).");
                return RedirectToAction(nameof(Receipt), new { id = receipt.Id });
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Index), new { q, payerId, amount });
        }

        [HttpGet("receipts/{id:int}")]
        [RequirePermission(ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Cashier, ActionVerb.View)]
        public async Task<IActionResult> Receipt(int id, bool print = false)
        {
            var receipt = await _db.Receipts.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id);
            if (receipt == null) return NotFound();
            var card = await FinanceQueries.CardAsync(_db, receipt.PayerId);
            if (card == null) return NotFound();
            var school = await _db.Schools.AsNoTracking().SingleOrDefaultAsync(s => s.Id == _db.CurrentSchoolId);
            var m = new ReceiptViewModel
            {
                Receipt = receipt, Payer = card, IsPrint = print,
                Allocations = await AllocationLinesAsync(new[] { id }),
                Session = receipt.TillSessionId == null ? null : await _db.TillSessions.AsNoTracking().SingleOrDefaultAsync(s => s.Id == receipt.TillSessionId),
                SchoolNameAr = school?.NameAr ?? "", SchoolNameEn = school?.NameEn ?? "",
            };
            m.PositionAfter = (await FinanceQueries.ChargeRowsAsync(_db, payerId: receipt.PayerId)).Sum(r => r.Remaining) - await FinanceQueries.AdvanceBalanceAsync(_db, receipt.PayerId);
            return View(m);
        }

        // ================================================================== 8.2 Till session console

        [HttpGet("till")]
        [RequirePermission(ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Till, ActionVerb.View)]
        public async Task<IActionResult> Till(int? close = null)
        {
            var sessions = await _db.TillSessions.AsNoTracking().OrderByDescending(s => s.OpenedAtUtc).Take(60).ToListAsync();
            var ids = sessions.Select(s => s.Id).ToList();
            var receipts = await _db.Receipts.AsNoTracking().Where(r => r.TillSessionId != null && ids.Contains(r.TillSessionId.Value) && r.Status == ReceiptStatus.Posted).Select(r => new { r.TillSessionId, r.Method, r.Amount }).ToListAsync();
            TillConsoleViewModel.SessionRow Row(TillSession s)
            {
                var mine = receipts.Where(r => r.TillSessionId == s.Id).ToList();
                return new TillConsoleViewModel.SessionRow(s, mine.Count, mine.Sum(r => r.Amount), mine.GroupBy(r => r.Method).ToDictionary(g => g.Key, g => g.Sum(r => r.Amount)));
            }
            var rows = sessions.Select(Row).ToList();
            var m = new TillConsoleViewModel
            {
                Open = rows.Where(r => r.Session.Status == TillSessionStatus.Open).ToList(),
                Closed = rows.Where(r => r.Session.Status == TillSessionStatus.Closed).ToList(),
                CurrentUserId = _user.UserId,
                Closing = close == null ? null : rows.FirstOrDefault(r => r.Session.Id == close && r.Session.Status == TillSessionStatus.Open),
            };
            return View(m);
        }

        [HttpPost("till/open")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Till, ActionVerb.Create)]
        public async Task<IActionResult> OpenTill(string tillCode, decimal floatAmount = 0m)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(tillCode)) throw new InvalidOperationException(T("Till code is required.", "رمز الصندوق مطلوب."));
                if (floatAmount < 0) throw new InvalidOperationException(T("Float cannot be negative.", "لا يمكن أن تكون العهدة سالبة."));
                if (await _db.TillSessions.AnyAsync(s => s.Status == TillSessionStatus.Open && s.CashierUserId == _user.UserId)) throw new InvalidOperationException(T("You already have an open session — close it first.", "لديك جلسة مفتوحة — أغلقها أولاً."));
                if (await _db.TillSessions.AnyAsync(s => s.Status == TillSessionStatus.Open && s.TillCode == tillCode.Trim())) throw new InvalidOperationException(T("That till already has an open session.", "هذا الصندوق له جلسة مفتوحة."));
                var session = await _payments.OpenTillSessionAsync(_user.UserId, tillCode.Trim(), floatAmount);
                TempData["Flash"] = T($"Session opened on till {session.TillCode} with float {session.FloatAmount:N2}.", $"فُتحت الجلسة على الصندوق {session.TillCode} بعهدة {session.FloatAmount:N2}.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Till));
        }

        [HttpPost("till/{id:int}/close")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Till, ActionVerb.Post)]
        public async Task<IActionResult> CloseTill(int id, decimal countedTotal, string? varianceReason)
        {
            try
            {
                var system = (await _db.Receipts.AsNoTracking().Where(r => r.TillSessionId == id && r.Status == ReceiptStatus.Posted).Select(r => r.Amount).ToListAsync()).Sum();
                if (countedTotal != system && string.IsNullOrWhiteSpace(varianceReason)) throw new InvalidOperationException(T($"Counted {countedTotal:N2} ≠ system {system:N2} — a variance reason is required.", $"المعدود {countedTotal:N2} ≠ النظام {system:N2} — سبب الفرق مطلوب."));
                await _payments.CloseTillSessionAsync(id, countedTotal, string.IsNullOrWhiteSpace(varianceReason) ? null : varianceReason.Trim());
                TempData["Flash"] = countedTotal == system ? T("Session closed — no variance.", "أُغلقت الجلسة — لا فرق.") : T($"Session closed with variance {countedTotal - system:+#,0.00;-#,0.00}.", $"أُغلقت الجلسة بفرق {countedTotal - system:+#,0.00;-#,0.00}.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Till));
        }

        // ================================================================== 8.3 PDC registry

        [HttpGet("pdc")]
        [RequirePermission(ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Pdc, ActionVerb.View)]
        public async Task<IActionResult> Pdc(PdcStatus? status = null)
        {
            var all = await _db.Pdcs.AsNoTracking().OrderBy(p => p.ChequeDate).ToListAsync();
            var cards = await FinanceQueries.CardsAsync(_db, await _db.Payers.AsNoTracking().Where(p => all.Select(x => x.PayerId).Distinct().Contains(p.Id)).ToListAsync(), includeChildren: false);
            var today = _clock.UtcNow.Date;
            var m = new PdcRegistryViewModel
            {
                Filter = status, Today = today,
                Counts = all.GroupBy(p => p.Status).ToDictionary(g => g.Key, g => g.Count()),
                Rows = all.Where(p => status == null || p.Status == status).Select(p => new PdcRegistryViewModel.Row(p, cards.FirstOrDefault(c => c.Payer.Id == p.PayerId)?.Parent,
                    Enum.GetValues<PdcStatus>().Where(t => PdcStatusTransitions.CanTransition(p.Status, t)).ToList(),
                    DueThisWeek: p.ChequeDate.Date >= today && p.ChequeDate.Date <= today.AddDays(7) && p.Status is PdcStatus.Lodged or PdcStatus.Due,
                    Overdue: p.ChequeDate.Date < today && p.Status is PdcStatus.Lodged or PdcStatus.Due)).ToList(),
                Payers = await FinanceQueries.SearchPayersAsync(_db, null, take: 200, includeChildren: false),
            };
            return View(m);
        }

        [HttpPost("pdc/new")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Pdc, ActionVerb.Create)]
        public async Task<IActionResult> LodgePdc(int payerId, string bankName, string chequeNo, DateTime? chequeDate, decimal amount)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(bankName) || string.IsNullOrWhiteSpace(chequeNo)) throw new InvalidOperationException(T("Bank and cheque number are required.", "البنك ورقم الشيك مطلوبان."));
                if (chequeDate == null) throw new InvalidOperationException(T("Cheque date is required.", "تاريخ الشيك مطلوب."));
                if (amount <= 0) throw new InvalidOperationException(T("Enter a positive amount.", "أدخل مبلغاً موجباً."));
                var pdc = await _payments.LodgePdcAsync(payerId, bankName.Trim(), chequeNo.Trim(), chequeDate.Value.Date, amount);
                TempData["Flash"] = T($"Cheque {pdc.ChequeNo} lodged for {pdc.Amount:N2}, due {pdc.ChequeDate:yyyy-MM-dd}.", $"سُجّل الشيك {pdc.ChequeNo} بمبلغ {pdc.Amount:N2}، يستحق {pdc.ChequeDate:yyyy-MM-dd}.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Pdc));
        }

        [HttpPost("pdc/{id:int}/status")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Pdc, ActionVerb.Edit)]
        public async Task<IActionResult> PdcStatusChange(int id, PdcStatus target, PdcStatus? filter)
        {
            try
            {
                await _payments.ChangePdcStatusAsync(id, target, _clock.UtcNow);
                TempData["Flash"] = target switch
                {
                    PdcStatus.Cleared => T("Cheque cleared — a receipt was issued and allocated.", "حُصّل الشيك — صدر سند وخُصّص."),
                    PdcStatus.Bounced => T("Cheque bounced — covered installments are un-covered; collect a replacement or settle (BR-INS-009).", "ارتجع الشيك — رُفعت تغطيته عن الأقساط؛ حصّل بديلاً أو سوِّ (BR-INS-009)."),
                    _ => T($"Cheque is now {FinanceLabels.PdcStatus(target, false)}.", $"أصبح الشيك بحالة {FinanceLabels.PdcStatus(target, true)}."),
                };
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Pdc), new { status = filter });
        }

        // ================================================================== 8.4 Refund desk

        [HttpGet("refunds")]
        [RequirePermission(ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Refunds, ActionVerb.View)]
        public async Task<IActionResult> Refunds(int? payerId = null, RefundVoucherStatus? status = null)
        {
            var all = await _db.RefundVouchers.AsNoTracking().OrderByDescending(v => v.Id).Take(200).ToListAsync();
            var cards = await FinanceQueries.CardsAsync(_db, await _db.Payers.AsNoTracking().Where(p => all.Select(x => x.PayerId).Distinct().Contains(p.Id)).ToListAsync(), includeChildren: false);
            var m = new RefundDeskViewModel
            {
                Filter = status,
                Rows = all.Where(v => status == null || v.Status == status).Select(v => new RefundDeskViewModel.Row(v, cards.FirstOrDefault(c => c.Payer.Id == v.PayerId)?.Parent,
                    Enum.GetValues<RefundVoucherStatus>().Where(t => RefundVoucherStatusTransitions.CanTransition(v.Status, t)).ToList())).ToList(),
                Payers = await FinanceQueries.SearchPayersAsync(_db, null, take: 200, includeChildren: false),
            };
            if (payerId != null)
            {
                m.Selected = await FinanceQueries.CardAsync(_db, payerId.Value);
                if (m.Selected == null) return NotFound();
                m.AdvanceBalance = await FinanceQueries.AdvanceBalanceAsync(_db, payerId.Value);
                m.Committed = await FinanceQueries.CommittedRefundsAsync(_db, payerId.Value);
            }
            return View(m);
        }

        [HttpPost("refunds/new")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Refunds, ActionVerb.Submit)]
        public async Task<IActionResult> RequestRefund(int payerId, decimal amount, PaymentMethod method, string? reason)
        {
            try
            {
                if (amount <= 0) throw new InvalidOperationException(T("Enter a positive amount.", "أدخل مبلغاً موجباً."));
                if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException(T("A reason is mandatory on a refund voucher.", "السبب إلزامي على سند الاسترداد."));
                _audit.Reason = reason.Trim();
                var v = await _payments.RequestRefundAsync(payerId, amount, method, reason.Trim());
                TempData["Flash"] = T($"Refund voucher {v.VoucherNo} requested for {v.Amount:N2} — awaiting approval.", $"طُلب سند الاسترداد {v.VoucherNo} بمبلغ {v.Amount:N2} — بانتظار الاعتماد.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Refunds), new { payerId });
        }

        [HttpPost("refunds/{id:int}/status")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Refunds, ActionVerb.Approve)]
        public async Task<IActionResult> RefundStatusChange(int id, RefundVoucherStatus target, int? payerId, RefundVoucherStatus? filter)
        {
            try
            {
                await _payments.ChangeRefundVoucherStatusAsync(id, target);
                TempData["Flash"] = T($"Voucher is now {FinanceLabels.RefundStatus(target, false)}.", $"أصبح السند بحالة {FinanceLabels.RefundStatus(target, true)}.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Refunds), new { payerId, status = filter });
        }

        // ================================================================== 8.5 Allocation explorer

        [HttpGet("allocations")]
        [RequirePermission(ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Allocations, ActionVerb.View)]
        public async Task<IActionResult> Allocations(int? payerId = null, string? q = null)
        {
            var m = new AllocationExplorerViewModel { Payers = await FinanceQueries.SearchPayersAsync(_db, q, take: 30) };
            if (payerId == null && m.Payers.Count == 1 && !string.IsNullOrWhiteSpace(q)) payerId = m.Payers[0].Payer.Id;
            if (payerId == null) return View(m);
            m.Selected = await FinanceQueries.CardAsync(_db, payerId.Value);
            if (m.Selected == null) return NotFound();
            var receipts = await _db.Receipts.AsNoTracking().Where(r => r.PayerId == payerId).OrderByDescending(r => r.IssuedAtUtc).ToListAsync();
            var lines = await AllocationLinesAsync(receipts.Select(r => r.Id).ToList());
            m.Receipts = receipts.Select(r => new AllocationExplorerViewModel.ReceiptRow(r, lines.Where(l => l.Allocation.ReceiptId == r.Id).ToList())).ToList();
            return View(m);
        }

        // ================================================================== helpers

        private async Task<IReadOnlyList<(PaymentAllocation Allocation, Charge Charge, FeeCategory Category, Student Student)>> AllocationLinesAsync(IReadOnlyCollection<int> receiptIds)
        {
            var allocations = await _db.PaymentAllocations.AsNoTracking().Where(a => receiptIds.Contains(a.ReceiptId)).ToListAsync();
            var cids = allocations.Select(a => a.ChargeId).Distinct().ToList();
            var charges = await _db.Charges.AsNoTracking().Where(c => cids.Contains(c.Id)).ToListAsync();
            var catIds = charges.Select(c => c.FeeCategoryId).Distinct().ToList();
            var cats = await _db.FeeCategories.IgnoreQueryFilters().AsNoTracking().Where(c => catIds.Contains(c.Id)).ToListAsync();
            var sids = charges.Select(c => c.StudentId).Distinct().ToList();
            var students = await _db.Students.IgnoreQueryFilters().AsNoTracking().Where(s => sids.Contains(s.Id)).ToListAsync();
            return allocations.Select(a =>
            {
                var c = charges.First(x => x.Id == a.ChargeId);
                return (a, c, cats.FirstOrDefault(x => x.Id == c.FeeCategoryId) ?? new FeeCategory { NameAr = "?", NameEn = "?" }, students.FirstOrDefault(s => s.Id == c.StudentId) ?? new Student { StudentNo = "?" });
            }).OrderBy(x => x.c.PostedAtUtc).ToList();
        }
    }
}
