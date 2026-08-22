using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Cafeteria;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Health;
using Sms.Application.Security;
using Sms.Domain.Cafeteria;
using Sms.Domain.Payments;
using Sms.Domain.Security;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/27 §8.1 — the counter. Every rule this screen appears to
    /// enforce is actually enforced by <see cref="ICafeteriaAdmin.RecordSaleAsync"/>:
    /// the daily limit, the blocked categories, the allergy match, the
    /// nutrition class, the wallet balance and its overdraft, the open till
    /// session for cash. The screen shows them early so the operator is not
    /// surprised at the end of a queue, and shows them again as the engine's own
    /// refusal when it happens.
    /// <para>
    /// The basket lives in the browser and is posted once, whole. A counter is
    /// the one place in this product where a round trip per keystroke is felt by
    /// a person standing in a line, and a half-built sale is not something worth
    /// a table.
    /// </para>
    /// <para>
    /// One thing the screen decides on its own: a warn-level allergy match. The
    /// engine refuses the sale unless the operator has confirmed, so the first
    /// attempt comes back as a question and the second carries the answer.
    /// </para>
    /// </summary>
    [Route("cafeteria")]
    public class CafeteriaController : Controller
    {
        private readonly ICafeteriaAdmin _cafeteria;
        private readonly IHealthAdmin _health;
        private readonly AppDbContext _db;
        private readonly ICurrentUser _user;
        private readonly IClock _clock;

        public CafeteriaController(ICafeteriaAdmin cafeteria, IHealthAdmin health, AppDbContext db, ICurrentUser user, IClock clock)
        {
            _cafeteria = cafeteria;
            _health = health;
            _db = db;
            _user = user;
            _clock = clock;
        }

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        // ================================================================== 8.1 POS

        [HttpGet("")]
        [RequirePermission(ScreenCatalog.Modules.Cafeteria, ScreenCatalog.Cafeteria.Pos, ActionVerb.View)]
        public async Task<IActionResult> Index(int? holder = null, WalletHolderKind kind = WalletHolderKind.Student, string? q = null)
        {
            var m = new CafeteriaPosViewModel { HolderKind = kind, HolderQuery = q };

            m.Items = await _db.CafeteriaItems.AsNoTracking()
                .OrderBy(i => i.Category).ThenBy(i => i.NameEn)
                .Select(i => new CafeteriaPosViewModel.ItemCard(
                    i.Id, i.NameAr, i.NameEn, i.Category, i.Price, i.VatRate, i.NutritionClass, i.AllergenTags, i.IsStaffOnly))
                .ToListAsync(HttpContext.RequestAborted);
            m.Categories = m.Items.Select(i => i.Category).Distinct().OrderBy(c => c, StringComparer.Ordinal).ToList();

            // Today's menu, when one is published: the counter sells from it rather than the whole
            // catalogue, and an item that is not on today's menu is usually not behind the counter.
            var today = _clock.UtcNow.Date;
            var menu = await _db.Menus.AsNoTracking().Include(x => x.Lines)
                .Where(x => x.Date == today && x.IsPublished)
                .SingleOrDefaultAsync(HttpContext.RequestAborted);
            m.TodaysMenuItemIds = menu == null
                ? Array.Empty<int>()
                : menu.Lines.Select(l => l.CafeteriaItemId).ToList();

            m.OpenTill = await _db.TillSessions.AsNoTracking()
                .Where(t => t.Status == TillSessionStatus.Open && t.CashierUserId == _user.UserId)
                .OrderByDescending(t => t.Id)
                .FirstOrDefaultAsync(HttpContext.RequestAborted);

            if (holder is int holderId)
            {
                m.Holder = await LoadHolderAsync(kind, holderId);
            }

            // One row over the limit is asked for so the screen can say "there are more" without
            // counting the whole table a second time.
            var list = await HolderListAsync(kind, q, HolderListSize + 1);
            m.HolderListTruncated = list.Count > HolderListSize;
            m.HolderList = m.HolderListTruncated ? list.Take(HolderListSize).ToList() : list;

            m.RecentSales = await RecentSalesAsync();
            return View(m);
        }

        /// <summary>The panel the operator reads before ringing anything up: who this is, what the wallet holds, what today has already taken, and what they must not be sold.</summary>
        [HttpGet("holder")]
        [RequirePermission(ScreenCatalog.Modules.Cafeteria, ScreenCatalog.Cafeteria.Pos, ActionVerb.View)]
        public async Task<IActionResult> Holder(WalletHolderKind kind, int id)
        {
            var holder = await LoadHolderAsync(kind, id);
            return holder == null ? NotFound() : Json(holder);
        }

        /// <summary>How many people the pick list shows at once. Long enough to browse a queue, short enough that nobody scrolls a school.</summary>
        private const int HolderListSize = 50;

        /// <summary>
        /// The one query behind both the list the server renders and the one the filter box refreshes:
        /// students or employees of the selected kind, narrowed by number or by either name, ordered by
        /// number. An empty term is not an empty result — it is the start of the list, which is what
        /// lets an operator who does not know the number find somebody by looking.
        /// </summary>
        private async Task<List<CafeteriaPosViewModel.HolderRow>> HolderListAsync(WalletHolderKind kind, string? q, int take)
        {
            var term = q?.Trim();

            if (kind == WalletHolderKind.Student)
            {
                var students = _db.Students.AsNoTracking();
                if (!string.IsNullOrEmpty(term))
                {
                    students = students.Where(s => s.StudentNo.Contains(term) || s.FirstNameAr.Contains(term) || s.FamilyNameAr.Contains(term)
                        || s.FirstNameEn.Contains(term) || s.FamilyNameEn.Contains(term));
                }

                return await students
                    .OrderBy(s => s.StudentNo).Take(take)
                    .Select(s => new CafeteriaPosViewModel.HolderRow(
                        s.Id, s.StudentNo, s.FirstNameAr + " " + s.FamilyNameAr, s.FirstNameEn + " " + s.FamilyNameEn))
                    .ToListAsync(HttpContext.RequestAborted);
            }

            var employees = _db.Employees.AsNoTracking();
            if (!string.IsNullOrEmpty(term))
            {
                employees = employees.Where(e => e.EmployeeNo.Contains(term) || e.FirstNameAr.Contains(term) || e.FamilyNameAr.Contains(term)
                    || e.FirstNameEn.Contains(term) || e.FamilyNameEn.Contains(term));
            }

            return await employees
                .OrderBy(e => e.EmployeeNo).Take(take)
                .Select(e => new CafeteriaPosViewModel.HolderRow(
                    e.Id, e.EmployeeNo, e.FirstNameAr + " " + e.FamilyNameAr, e.FirstNameEn + " " + e.FamilyNameEn))
                .ToListAsync(HttpContext.RequestAborted);
        }

        [HttpGet("search")]
        [RequirePermission(ScreenCatalog.Modules.Cafeteria, ScreenCatalog.Cafeteria.Pos, ActionVerb.View)]
        public async Task<IActionResult> Search(string? q, WalletHolderKind kind = WalletHolderKind.Student)
        {
            var rows = await HolderListAsync(kind, q, HolderListSize + 1);
            var truncated = rows.Count > HolderListSize;
            return Json(new
            {
                truncated,
                rows = truncated ? rows.Take(HolderListSize) : rows,
            });
        }

        [HttpPost("sell")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Cafeteria, ScreenCatalog.Cafeteria.Pos, ActionVerb.Create)]
        public async Task<IActionResult> Sell(
            WalletHolderKind kind, int holderId, SaleTender tender, string? lines, int? tillSessionId, bool confirmAllergy = false)
        {
            var basket = ParseBasket(lines);
            if (basket.Count == 0)
            {
                TempData["Error"] = T("Nothing in the basket.", "السلة فارغة.");
                return RedirectToAction(nameof(Index), new { holder = holderId, kind });
            }

            try
            {
                var sale = await _cafeteria.RecordSaleAsync(
                    kind, holderId, basket, tender, _user.UserId, tillSessionId, confirmAllergy,
                    cancellationToken: HttpContext.RequestAborted);

                TempData["Flash"] = T(
                    $"Sale #{sale.Id} — {sale.Total.ToString("N2", CultureInfo.InvariantCulture)} taken.",
                    $"بيع رقم {sale.Id} — تم قبض {sale.Total.ToString("N2", CultureInfo.InvariantCulture)}.");
            }
            catch (SaleBlockedException ex)
            {
                // The engine's own words: which rule refused and why. Rewriting them here would give
                // the operator a second, vaguer account of a decision they need to act on.
                TempData["Error"] = T($"Refused: {ex.Message}", $"مرفوض: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = T($"Refused: {ex.Message}", $"مرفوض: {ex.Message}");
            }

            return RedirectToAction(nameof(Index), new { holder = holderId, kind });
        }

        [HttpPost("{id:int}/void")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Cafeteria, ScreenCatalog.Cafeteria.Pos, ActionVerb.Deactivate)]
        public async Task<IActionResult> Void(int id, string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = T("Voiding a sale needs a reason.", "إلغاء البيع يتطلب سبباً.");
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await _cafeteria.VoidSaleAsync(id, reason.Trim(), HttpContext.RequestAborted);
                TempData["Flash"] = T("Sale voided; the wallet and the stock were put back.", "أُلغي البيع، وأُعيدت قيمته إلى المحفظة والمخزون.");
            }
            catch (SaleNotVoidableException)
            {
                TempData["Error"] = T(
                    "This sale can no longer be voided — its till session is closed. Issue a refund instead.",
                    "لم يعد إلغاء هذا البيع ممكناً — أُغلقت جلسة صندوقه. أصدر استرداداً بدلاً من ذلك.");
            }

            return RedirectToAction(nameof(Index));
        }

        // ================================================================== helpers

        /// <summary>
        /// "3:2,7:1" — item id and quantity. Posted as one string rather than as parallel arrays
        /// because model binding drops an entry it cannot convert, which silently misaligns two
        /// arrays and rings up the wrong item at the wrong quantity.
        /// </summary>
        private static IReadOnlyList<BasketLine> ParseBasket(string? lines)
        {
            if (string.IsNullOrWhiteSpace(lines))
            {
                return Array.Empty<BasketLine>();
            }

            var basket = new List<BasketLine>();
            foreach (var entry in lines.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = entry.Split(':');
                if (parts.Length == 2
                    && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var itemId)
                    && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity)
                    && quantity > 0)
                {
                    basket.Add(new BasketLine(itemId, quantity));
                }
            }

            return basket;
        }

        private async Task<CafeteriaPosViewModel.HolderCard?> LoadHolderAsync(WalletHolderKind kind, int id)
        {
            string nameAr, nameEn, code;
            if (kind == WalletHolderKind.Student)
            {
                var student = await _db.Students.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, HttpContext.RequestAborted);
                if (student == null)
                {
                    return null;
                }

                (nameAr, nameEn, code) = ($"{student.FirstNameAr} {student.FamilyNameAr}", $"{student.FirstNameEn} {student.FamilyNameEn}", student.StudentNo);
            }
            else
            {
                var employee = await _db.Employees.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, HttpContext.RequestAborted);
                if (employee == null)
                {
                    return null;
                }

                (nameAr, nameEn, code) = ($"{employee.FirstNameAr} {employee.FamilyNameAr}", $"{employee.FirstNameEn} {employee.FamilyNameEn}", employee.EmployeeNo);
            }

            var wallet = await _cafeteria.EnsureWalletAsync(kind, id, cancellationToken: HttpContext.RequestAborted);
            var balance = await _cafeteria.BalanceAsync(wallet.Id, HttpContext.RequestAborted);

            var card = new CafeteriaPosViewModel.HolderCard
            {
                Id = id,
                Kind = kind,
                Code = code,
                NameAr = nameAr,
                NameEn = nameEn,
                WalletBalance = balance,
                OverdraftAllowance = wallet.OverdraftAllowance,
            };

            if (kind != WalletHolderKind.Student)
            {
                return card;
            }

            var control = await _db.SpendControls.AsNoTracking().SingleOrDefaultAsync(c => c.StudentId == id, HttpContext.RequestAborted);
            card.DailyLimit = control?.DailyLimit;
            card.BlockedCategories = control?.BlockedCategories;
            card.AllergyHardBlock = control?.AllergyHardBlock ?? false;

            var today = _clock.UtcNow.Date;
            card.SpentToday = (await _db.Sales.AsNoTracking()
                .Where(s => s.HolderKind == kind && s.HolderId == id && s.Status == SaleStatus.Posted
                    && s.AtUtc >= today && s.AtUtc < today.AddDays(1))
                .Select(s => s.Total)
                .ToListAsync(HttpContext.RequestAborted)).Sum();

            var banner = await _health.GetEmergencyBannerAsync(id, HttpContext.RequestAborted);
            card.Allergies = banner == null ? null : string.Join(", ", banner.SevereAllergies);

            return card;
        }

        private async Task<IReadOnlyList<CafeteriaPosViewModel.SaleRow>> RecentSalesAsync()
        {
            var sales = await _db.Sales.AsNoTracking()
                .OrderByDescending(s => s.Id).Take(12)
                .Select(s => new { s.Id, s.HolderKind, s.HolderId, s.Tender, s.Total, s.VatAmount, s.Status, s.AtUtc })
                .ToListAsync(HttpContext.RequestAborted);

            var studentIds = sales.Where(s => s.HolderKind == WalletHolderKind.Student).Select(s => s.HolderId).Distinct().ToList();
            var students = await _db.Students.IgnoreQueryFilters().AsNoTracking()
                .Where(s => studentIds.Contains(s.Id))
                .Select(s => new { s.Id, NameAr = s.FirstNameAr + " " + s.FamilyNameAr, NameEn = s.FirstNameEn + " " + s.FamilyNameEn })
                .ToDictionaryAsync(s => s.Id, HttpContext.RequestAborted);

            return sales.Select(s => new CafeteriaPosViewModel.SaleRow(
                s.Id,
                students.TryGetValue(s.HolderId, out var who) ? (IsArabic ? who.NameAr : who.NameEn) : $"#{s.HolderId}",
                s.Tender, s.Total, s.VatAmount, s.Status, s.AtUtc)).ToList();
        }
    }
}
