using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.GlExport;
using Sms.Application.Payments;
using Sms.Application.Security;
using Sms.Domain.Payments;
using Sms.Domain.Security;
using Sms.Web.Models;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/21 §3 BR-PAY-002 — the school's own accounts, and the screen
    /// that maintains them.
    /// <para>
    /// A receipt used to record how a payment was made and a reference number,
    /// but never <em>where the money went</em>. A school with three bank
    /// accounts could not tell from the system which one a transfer had landed
    /// in, and a cashier taking a call from a parent asking where to send the
    /// money had nothing on screen to read out. This catalogue answers both:
    /// it is the list the cashier picks the destination from, and the list the
    /// IBAN is read from.
    /// </para>
    /// <para>
    /// In its own file because <c>PaymentsController</c> is a busy one, and
    /// because this is configuration rather than the counter's work — the two
    /// share nothing but the tenant.
    /// </para>
    /// </summary>
    public partial class PaymentsController
    {
        // ================================================================== Collection accounts

        [HttpGet("accounts")]
        [RequirePermission(ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Accounts, ActionVerb.View)]
        public async Task<IActionResult> Accounts(int? edit = null, [FromServices] IGlAccountDirectory? glAccounts = null)
        {
            var m = new CollectionAccountCatalogViewModel { EditId = edit };

            // IgnoreQueryFilters: a retired account still has to be listed, or a school cannot see
            // that it once collected into it — and cannot put it back (CLAUDE.md soft-active trap).
            var accounts = await _db.CollectionAccounts.IgnoreQueryFilters().AsNoTracking()
                .Where(a => a.SchoolId == _db.CurrentSchoolId)
                .OrderByDescending(a => a.IsActive).ThenBy(a => a.Kind).ThenBy(a => a.DisplayOrder).ThenBy(a => a.Code)
                .ToListAsync();
            var ids = accounts.Select(a => a.Id).ToList();

            // EF Core's Sqlite provider can't translate Sum() over decimal - materialize, then group in memory.
            var receipts = await _db.Receipts.AsNoTracking()
                .Where(r => r.CollectionAccountId != null && ids.Contains(r.CollectionAccountId.Value) && r.Status == ReceiptStatus.Posted)
                .Select(r => new { AccountId = r.CollectionAccountId!.Value, r.Amount })
                .ToListAsync();
            var byAccount = receipts.GroupBy(r => r.AccountId)
                .ToDictionary(g => g.Key, g => (Count: g.Count(), Total: g.Sum(x => x.Amount)));

            m.Banks = await BankPickerAsync();
            var bankNames = await BankNamesAsync();
            m.Rows = accounts.Select(a =>
            {
                var bank = a.BankLookupId != null && bankNames.TryGetValue(a.BankLookupId.Value, out var n) ? n : (a.BankName, a.BankName);
                byAccount.TryGetValue(a.Id, out var totals);
                return new CollectionAccountCatalogViewModel.Row(a, bank.Item1, bank.Item2, totals.Count, totals.Total);
            }).ToList();

            // The chart of accounts when a ledger is attached, so the GL code is picked rather than
            // typed. A ledger that cannot answer must not take this screen down with it — the
            // catalogue is a school screen and stays editable, with the free-text field instead.
            if (glAccounts != null)
            {
                try
                {
                    m.GlAccounts = await glAccounts.GetPostableAccountsAsync(HttpContext.RequestAborted);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    // Deliberately swallowed, narrowly: empty reads downstream as "no ledger attached".
                }
            }

            return View(m);
        }

        [HttpPost("accounts/new")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Accounts, ActionVerb.Create)]
        public async Task<IActionResult> CreateAccount(
            [FromServices] ICollectionAccountAdmin admin,
            string code, string nameAr, string nameEn, CollectionAccountKind kind,
            int? bankLookupId = null, string? bankName = null, string? accountNo = null, string? iban = null,
            string? glExportCode = null, int displayOrder = 0, bool isDefault = false)
        {
            try
            {
                GuardNames(code, nameAr, nameEn);
                await admin.DefineAsync(
                    code.Trim(), nameAr.Trim(), nameEn.Trim(), kind,
                    bankLookupId, Blank(bankName), Blank(accountNo), Blank(iban), Blank(glExportCode), displayOrder, isDefault);
                TempData["Flash"] = T("Account added — the cashier can collect into it now.", "أُضيف الحساب — يمكن للصندوق التحصيل فيه الآن.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Accounts));
        }

        [HttpPost("accounts/{id:int}/edit")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Accounts, ActionVerb.Edit)]
        public async Task<IActionResult> EditAccount(
            [FromServices] ICollectionAccountAdmin admin,
            int id, string code, string nameAr, string nameEn,
            int? bankLookupId = null, string? bankName = null, string? accountNo = null, string? iban = null,
            string? glExportCode = null, int displayOrder = 0, bool isDefault = false)
        {
            try
            {
                GuardNames(code, nameAr, nameEn);
                await admin.UpdateAsync(
                    id, code.Trim(), nameAr.Trim(), nameEn.Trim(),
                    bankLookupId, Blank(bankName), Blank(accountNo), Blank(iban), Blank(glExportCode), displayOrder, isDefault);
                TempData["Flash"] = T("Account updated — receipts already issued keep naming it.", "حُدّث الحساب — تبقى السندات الصادرة مرتبطة به.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Accounts));
        }

        [HttpPost("accounts/{id:int}/deactivate")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Accounts, ActionVerb.Deactivate)]
        public async Task<IActionResult> DeactivateAccount([FromServices] ICollectionAccountAdmin admin, int id)
        {
            try
            {
                await admin.DeactivateAsync(id);
                TempData["Flash"] = T("Account retired — it keeps its receipts and leaves the cashier's list.", "أُلغي تفعيل الحساب — يحتفظ بسنداته ويختفي من قائمة الصندوق.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Accounts));
        }

        /// <summary>
        /// Putting a retired account back. <c>Edit</c> rather than a verb of its
        /// own: it changes a record's state, and whoever may correct an IBAN is
        /// the same person who may decide the account is open again.
        /// </summary>
        [HttpPost("accounts/{id:int}/reactivate")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Accounts, ActionVerb.Edit)]
        public async Task<IActionResult> ReactivateAccount([FromServices] ICollectionAccountAdmin admin, int id)
        {
            try
            {
                await admin.ReactivateAsync(id);
                TempData["Flash"] = T("Account is open again.", "أُعيد تفعيل الحساب.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Accounts));
        }

        // ================================================================== shared with the cashier screen

        /// <summary>
        /// The destinations the cashier may pick, active only and flattened for
        /// the view. Every kind in one list: the screen filters it by method in
        /// the browser, so changing the method does not cost a round trip.
        /// </summary>
        internal async Task<IReadOnlyList<CollectionAccountOption>> CollectionAccountOptionsAsync()
        {
            var accounts = await _db.CollectionAccounts.AsNoTracking()
                .OrderByDescending(a => a.IsDefault).ThenBy(a => a.DisplayOrder).ThenBy(a => a.Code)
                .ToListAsync();
            if (accounts.Count == 0)
            {
                return Array.Empty<CollectionAccountOption>();
            }

            var bankNames = await BankNamesAsync();
            return accounts.Select(a =>
            {
                var bank = a.BankLookupId != null && bankNames.TryGetValue(a.BankLookupId.Value, out var n) ? n : (a.BankName, a.BankName);
                return new CollectionAccountOption(a.Id, a.Code, a.NameAr, a.NameEn, a.Kind, bank.Item1, bank.Item2, a.AccountNo, a.Iban, a.IsDefault);
            }).ToList();
        }

        /// <summary>The "Bank" catalogue as a picker — the soft-active filter applies, because a retired bank should not be offered.</summary>
        private async Task<IReadOnlyList<(int Id, string Ar, string En)>> BankPickerAsync()
        {
            var category = await _db.LookupCategories.AsNoTracking().SingleOrDefaultAsync(c => c.Code == "Bank");
            if (category == null)
            {
                return Array.Empty<(int, string, string)>();
            }

            return await _db.LookupValues.AsNoTracking()
                .Where(v => v.LookupCategoryId == category.Id)
                .OrderBy(v => v.SortOrder)
                .Select(v => new ValueTuple<int, string, string>(v.Id, v.Name.NameAr, v.Name.NameEn))
                .ToListAsync();
        }

        /// <summary>
        /// Names for banks already recorded on an account, retired ones
        /// included — the lookup, where <see cref="BankPickerAsync"/> is the
        /// picker. Reading a stored id out of the picker's list is how a page
        /// starts printing nothing the day somebody tidies the catalogue.
        /// </summary>
        private async Task<IReadOnlyDictionary<int, (string Ar, string En)>> BankNamesAsync()
        {
            var category = await _db.LookupCategories.IgnoreQueryFilters().AsNoTracking()
                .SingleOrDefaultAsync(c => c.Code == "Bank" && c.SchoolId == _db.CurrentSchoolId);
            if (category == null)
            {
                return new Dictionary<int, (string Ar, string En)>();
            }

            var values = await _db.LookupValues.IgnoreQueryFilters().AsNoTracking()
                .Where(v => v.LookupCategoryId == category.Id && v.SchoolId == _db.CurrentSchoolId)
                .Select(v => new { v.Id, v.Name.NameAr, v.Name.NameEn })
                .ToListAsync();
            return values.ToDictionary(v => v.Id, v => (v.NameAr, v.NameEn));
        }

        private void GuardNames(string code, string nameAr, string nameEn)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new InvalidOperationException(T("A code is required — it is how the account is referred to.", "الرمز مطلوب — به يُشار إلى الحساب."));
            }

            if (string.IsNullOrWhiteSpace(nameAr) || string.IsNullOrWhiteSpace(nameEn))
            {
                throw new InvalidOperationException(T("Both names are required.", "الاسمان مطلوبان."));
            }
        }

        private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
