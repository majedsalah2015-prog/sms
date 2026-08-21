using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.GlExport;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/19 §8 "GL export" — the screen E-503 deferred, and with it
    /// gap G-16: the engine, the mapping table and the ledger port were all
    /// built and none of them were reachable from the running application, so
    /// no operator could define a mapping or generate a batch at all.
    /// <para>
    /// Three screens: the batch register with its period form, one batch's
    /// journal with the CSV behind it, and the mapping table. Generation is the
    /// only verb that matters — when a ledger is attached,
    /// <see cref="IGlExportService.GenerateAsync"/> posts as part of generating,
    /// so there is deliberately no separate "post" button that could leave a
    /// batch stranded between the two states.
    /// </para>
    /// <para>
    /// Every refusal reachable from here is configuration an operator can fix —
    /// an unmapped key, a period already covered, a closed period in the ledger
    /// — so each is caught and answered in the reader's language, naming the
    /// thing to fix rather than reporting that something went wrong.
    /// </para>
    /// </summary>
    [Route("gl-export")]
    public class GlExportController : Controller
    {
        private readonly IGlExportService _export;
        private readonly AppDbContext _db;
        private readonly ICurrentUser _user;

        /// <summary>Null when this deployment has no attached ledger — the O3 fallback, where batches are still generated, balanced and downloaded as CSV.</summary>
        private readonly IGlPostingPort? _posting;

        public GlExportController(IGlExportService export, AppDbContext db, ICurrentUser user, IGlPostingPort? posting = null)
        {
            _export = export;
            _db = db;
            _user = user;
            _posting = posting;
        }

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        // ================================================================== Batch register

        [HttpGet("")]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.GlExport, ActionVerb.View)]
        public async Task<IActionResult> Index()
        {
            var m = new GlExportIndexViewModel { LedgerAttached = _posting != null };

            m.Batches = await _db.GlExportBatches.AsNoTracking()
                .OrderByDescending(b => b.Id)
                .Select(b => new GlExportIndexViewModel.BatchRow(
                    b.Id, b.BatchNo, b.PeriodFromUtc, b.PeriodToUtc, b.TotalDebit, b.TotalCredit,
                    b.SourceDocumentCount, b.Status, b.PostedJournalNo, b.GeneratedAtUtc))
                .ToListAsync(HttpContext.RequestAborted);

            m.MappedKeyCount = await _db.GlAccountMappings.CountAsync(HttpContext.RequestAborted);
            m.UnmappedKeyCount = (await UnmappedKeysAsync()).Count;

            // The month after the last covered one is the answer nine times out of ten, so the form
            // opens on it rather than on today.
            var lastTo = m.Batches.Count == 0 ? (DateTime?)null : m.Batches.Max(b => b.PeriodToUtc);
            var start = lastTo?.Date.AddDays(1) ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-1);
            m.PeriodFrom = start;
            m.PeriodTo = new DateTime(start.Year, start.Month, DateTime.DaysInMonth(start.Year, start.Month));

            return View(m);
        }

        [HttpPost("generate")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.GlExport, ActionVerb.Post)]
        public async Task<IActionResult> Generate(DateTime periodFrom, DateTime periodTo)
        {
            if (periodTo.Date < periodFrom.Date)
            {
                TempData["Error"] = T("The period ends before it starts.", "تاريخ نهاية الفترة قبل بدايتها.");
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // Inclusive of the closing day: an operator picking 1-30 September means the whole of
                // the 30th, and a receipt taken at 14:00 that day belongs to September.
                var batch = await _export.GenerateAsync(
                    periodFrom.Date, periodTo.Date.AddDays(1).AddTicks(-1), _user.UserId, HttpContext.RequestAborted);

                TempData["Flash"] = batch.PostedJournalNo == null
                    ? T($"Batch {batch.BatchNo} generated.", $"تم توليد الدفعة {batch.BatchNo}.")
                    : T($"Batch {batch.BatchNo} generated and posted to the ledger as {batch.PostedJournalNo}.",
                        $"تم توليد الدفعة {batch.BatchNo} وترحيلها إلى الأستاذ العام بالقيد {batch.PostedJournalNo}.");
                return RedirectToAction(nameof(Details), new { id = batch.Id });
            }
            catch (GlMappingMissingException ex)
            {
                var keys = string.Join(IsArabic ? "، " : ", ", ex.MissingKeys);
                TempData["Error"] = T(
                    $"These journal keys have no account yet: {keys}. Map them, then generate again.",
                    $"هذه المفاتيح بلا حساب بعد: {keys}. اربطها ثم أعد التوليد.");
                return RedirectToAction(nameof(Mappings));
            }
            catch (GlPeriodOverlapException ex)
            {
                TempData["Error"] = T(
                    ex.Message,
                    "تتداخل الفترة مع دفعة قائمة — يجب إلغاؤها أولاً حتى لا تصل المستندات نفسها إلى الأستاذ العام مرتين.");
            }
            catch (GlPostingRejectedException ex)
            {
                TempData["Error"] = T(
                    $"The ledger refused the batch [{ex.ErrorCode}]: {ex.Message}",
                    $"رفض الأستاذ العام الدفعة [{ex.ErrorCode}]: {ex.Message}");
            }

            return RedirectToAction(nameof(Index));
        }

        // ================================================================== One batch

        [HttpGet("{id:int}")]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.GlExport, ActionVerb.View)]
        public async Task<IActionResult> Details(int id)
        {
            var batch = await _db.GlExportBatches.AsNoTracking()
                .Include(b => b.Lines)
                .SingleOrDefaultAsync(b => b.Id == id, HttpContext.RequestAborted);
            if (batch == null)
            {
                return NotFound();
            }

            var m = new GlExportBatchViewModel
            {
                Batch = batch,
                Lines = batch.Lines.OrderBy(l => l.SequenceNumber).ToList(),
                LedgerAttached = _posting != null,
            };

            var codes = m.Lines.Select(l => l.AccountCode).Distinct().ToList();
            m.AccountNames = await _db.GlAccountMappings.AsNoTracking()
                .Where(x => codes.Contains(x.AccountCode))
                .GroupBy(x => x.AccountCode)
                .Select(g => new { g.Key, Ar = g.First().AccountNameAr, En = g.First().AccountNameEn })
                .ToDictionaryAsync(x => x.Key, x => IsArabic ? x.Ar : x.En, HttpContext.RequestAborted);

            return View(m);
        }

        [HttpGet("{id:int}/csv")]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.GlExport, ActionVerb.Export)]
        public async Task<IActionResult> Csv(int id)
        {
            var batch = await _db.GlExportBatches.AsNoTracking().SingleOrDefaultAsync(b => b.Id == id, HttpContext.RequestAborted);
            if (batch == null)
            {
                return NotFound();
            }

            var csv = await _export.RenderCsvAsync(id, HttpContext.RequestAborted);

            // UTF-8 with a BOM: without it Excel reads the Arabic account names as mojibake, which is
            // the most common way a correct export looks broken to the person who opens it. The hash
            // on the batch is over the CSV text, so the preamble is added here and not in the writer.
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
            return File(bytes, "text/csv", $"{batch.BatchNo}.csv");
        }

        [HttpPost("{id:int}/void")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.GlExport, ActionVerb.Deactivate)]
        public async Task<IActionResult> Void(int id, string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = T("Voiding a batch needs a reason.", "إلغاء الدفعة يتطلب سبباً.");
                return RedirectToAction(nameof(Details), new { id });
            }

            try
            {
                await _export.VoidAsync(id, reason.Trim(), HttpContext.RequestAborted);
                TempData["Flash"] = T("Batch voided; its period can be generated again.", "أُلغيت الدفعة، ويمكن توليد فترتها من جديد.");
            }
            catch (GlBatchNotGeneratedException)
            {
                TempData["Error"] = T("This batch is already voided.", "هذه الدفعة ملغاة أصلاً.");
            }
            catch (GlPostingRejectedException ex)
            {
                TempData["Error"] = T(
                    $"The ledger refused the reversing entry [{ex.ErrorCode}]: {ex.Message}",
                    $"رفض الأستاذ العام قيد العكس [{ex.ErrorCode}]: {ex.Message}");
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // ================================================================== Mapping table

        [HttpGet("mappings")]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.GlMapping, ActionVerb.View)]
        public async Task<IActionResult> Mappings()
        {
            var m = new GlMappingsViewModel { LedgerAttached = _posting != null };
            m.Rows = await _db.GlAccountMappings.AsNoTracking().OrderBy(x => x.Key).ToListAsync(HttpContext.RequestAborted);
            m.UnmappedKeys = await UnmappedKeysAsync();
            return View(m);
        }

        [HttpPost("mappings")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.GlMapping, ActionVerb.Configure)]
        public async Task<IActionResult> SaveMapping(string? key, string? accountCode, string? accountNameAr, string? accountNameEn)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(accountCode))
            {
                TempData["Error"] = T("A mapping needs both a key and an account code.", "الربط يتطلب مفتاحاً ورمز حساب معاً.");
                return RedirectToAction(nameof(Mappings));
            }

            await _export.DefineMappingAsync(
                key.Trim(), accountCode.Trim(),
                accountNameAr?.Trim() ?? string.Empty, accountNameEn?.Trim() ?? string.Empty,
                HttpContext.RequestAborted);

            TempData["Flash"] = T($"'{key}' now posts to {accountCode}.", $"أصبح «{key}» يُرحَّل إلى {accountCode}.");
            return RedirectToAction(nameof(Mappings));
        }

        /// <summary>
        /// Which of the keys a batch could need have no account yet. The fixed set, one cash key per
        /// payment method, and one revenue key per fee category — deactivated categories included,
        /// because their posted charges keep the same key forever (gap G-14).
        /// </summary>
        private async Task<IReadOnlyList<string>> UnmappedKeysAsync()
        {
            var keys = new List<string>
            {
                GlAccountKeys.Receivables, GlAccountKeys.VatOutput, GlAccountKeys.Discounts,
                GlAccountKeys.AdvancesReceived, GlAccountKeys.WalletLiability,
                GlAccountKeys.CafeteriaRevenue, GlAccountKeys.StoreRevenue,
            };

            foreach (Sms.Domain.Payments.PaymentMethod method in Enum.GetValues(typeof(Sms.Domain.Payments.PaymentMethod)))
            {
                keys.Add(GlAccountKeys.Cash(method.ToString()));
            }

            var categories = await _db.FeeCategories.IgnoreQueryFilters().AsNoTracking()
                .Where(c => c.SchoolId == _db.CurrentSchoolId)
                .Select(c => new { c.Id, c.GlExportCode })
                .ToListAsync(HttpContext.RequestAborted);
            keys.AddRange(categories.Select(c => GlAccountKeys.Revenue(c.Id, c.GlExportCode)));

            var mapped = new HashSet<string>(
                await _db.GlAccountMappings.AsNoTracking().Select(x => x.Key).ToListAsync(HttpContext.RequestAborted),
                StringComparer.OrdinalIgnoreCase);

            return keys.Distinct(StringComparer.OrdinalIgnoreCase).Where(k => !mapped.Contains(k)).ToList();
        }
    }
}
