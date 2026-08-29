using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Common.Interfaces;
using Sms.Application.Fees;
using Sms.Application.GlExport;
using Sms.Application.ReadModels;
using Sms.Application.Setup;
using Sms.Application.Statements;
using Sms.Domain.Fees;
using Sms.Domain.Schools;
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
    /// doc/Modules/19 §8 — E-303 screens over IFeeAdmin: 8.1 Category
    /// catalog &amp; VAT mapping, 8.2 Fee structure workbench (grade-year grid,
    /// copy-from-last-year with % uplift, approval), 8.3 Charge explorer +
    /// bilingual tax-invoice document view (ZATCA Phase-1 QR payload),
    /// 8.4 Misc charge entry, 8.5 Credit note flow, 8.7 Student/payer
    /// position — read payer-first here (statement of account, as-of date,
    /// aging, drill to documents) and student-first in
    /// <c>FeesController.StudentFinance.cs</c>.
    /// Deferred: 8.6 late-fee run console (M20 policy), 8.8 portal fees
    /// (E-304 already shows posted invoices), threshold routing / chain
    /// approval on misc charges and credit notes (WF not modelled).
    /// </summary>
    [Route("fees")]
    public partial class FeesController : Controller
    {
        private readonly IFeeAdmin _fees;
        private readonly IStatementService _statements;
        private readonly ISystemSetupAdmin _setup;
        private readonly AppDbContext _db;
        private readonly IAuditContext _audit;
        private readonly IWorkingYearContext _workingYear;
        private readonly IClock _clock;
        private readonly IPermissionService _permissions;
        private readonly IGlAccountDirectory? _glAccounts;

        /// <summary>
        /// <paramref name="glAccounts"/> is optional on purpose, exactly as
        /// <c>IGlPostingPort</c> is on <c>GlExportController</c>: it is registered
        /// only by the ERP bridge, and a school running this system standalone
        /// still configures GL export codes by hand against an accountant's own
        /// ledger. Absent, the category screen offers no chart and keeps the
        /// free-text field it always had.
        /// </summary>
        public FeesController(IFeeAdmin fees, IStatementService statements, ISystemSetupAdmin setup, AppDbContext db, IAuditContext audit, IWorkingYearContext workingYear, IClock clock, IPermissionService permissions, IGlAccountDirectory? glAccounts = null)
        {
            _fees = fees;
            _statements = statements;
            _setup = setup;
            _db = db;
            _audit = audit;
            _workingYear = workingYear;
            _clock = clock;
            _permissions = permissions;
            _glAccounts = glAccounts;
        }

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        // ================================================================== 8.3 Charge explorer

        [HttpGet("")]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Charges, ActionVerb.View)]
        public async Task<IActionResult> Index(int? year = null, string? q = null, int? category = null, ChargeSourceType? source = null, ChargeStatus? status = null, bool open = false)
        {
            var m = new ChargeExplorerViewModel { Q = q, CategoryId = category, Source = source, Status = status, OpenOnly = open };
            await FillPageAsync(m, year);
            if (m.Year == null) return View(m);
            var yid = m.Year.Id;

            var query = _db.Charges.AsNoTracking().Where(c => c.AcademicYearId == yid);
            if (category != null) query = query.Where(c => c.FeeCategoryId == category);
            if (source != null) query = query.Where(c => c.SourceType == source);
            if (status != null) query = query.Where(c => c.Status == status);
            var charges = await query.OrderByDescending(c => c.Id).Take(500).ToListAsync();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                var studentIds = await _db.Students.IgnoreQueryFilters().AsNoTracking()
                    .Where(s => s.SchoolId == _db.CurrentSchoolId && (s.StudentNo.Contains(term) || s.FirstNameAr.Contains(term) || s.FamilyNameAr.Contains(term) || s.FirstNameEn.Contains(term) || s.FamilyNameEn.Contains(term)))
                    .Select(s => s.Id).ToListAsync();
                var parentIds = await _db.Parents.IgnoreQueryFilters().AsNoTracking().Where(p => p.NameAr.Contains(term) || p.NameEn.Contains(term) || p.ParentFileNo.Contains(term) || p.PrimaryMobile.Contains(term)).Select(p => p.Id).ToListAsync();
                var payerIds = await _db.Payers.AsNoTracking().Where(p => p.ParentId != null && parentIds.Contains(p.ParentId.Value)).Select(p => p.Id).ToListAsync();
                charges = charges.Where(c => c.ChargeNo.Contains(term, StringComparison.OrdinalIgnoreCase) || studentIds.Contains(c.StudentId) || payerIds.Contains(c.PayerId)).ToList();
            }

            var rows = await FinanceQueries.RowsAsync(_db, charges, openOnly: false);
            var cards = await FinanceQueries.CardsAsync(_db, await _db.Payers.AsNoTracking().Where(p => charges.Select(c => c.PayerId).Distinct().Contains(p.Id)).ToListAsync(), includeChildren: false);
            var list = rows.Select(r => new ChargeExplorerViewModel.Row(r.Charge, r.Category, r.Student, cards.FirstOrDefault(c => c.Payer.Id == r.Charge.PayerId)?.Parent, r.Remaining)).ToList();
            if (open) list = list.Where(r => r.Charge.Status == ChargeStatus.Posted && r.Remaining > 0).ToList();
            m.Rows = list;
            m.TotalGross = list.Where(r => r.Charge.Status == ChargeStatus.Posted).Sum(r => r.Charge.GrossAmount);
            m.TotalRemaining = list.Where(r => r.Charge.Status == ChargeStatus.Posted).Sum(r => r.Remaining);
            m.PayerCount = list.Select(r => r.Charge.PayerId).Distinct().Count();
            return View(m);
        }

        // ================================================================== 8.1 Category catalog

        [HttpGet("categories")]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Categories, ActionVerb.View)]
        public async Task<IActionResult> Categories(int? edit = null)
        {
            var m = new FeeCategoryCatalogViewModel { EditId = edit };
            await FillPageAsync(m, null);
            var all = await _db.FeeCategories.IgnoreQueryFilters().AsNoTracking().Where(c => c.SchoolId == _db.CurrentSchoolId).OrderByDescending(c => c.IsActive).ThenBy(c => c.NameEn).ToListAsync();
            var lineCounts = await _db.FeeStructureLines.AsNoTracking().GroupBy(l => l.FeeCategoryId).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
            var chargeCounts = await _db.Charges.AsNoTracking().GroupBy(c => c.FeeCategoryId).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
            m.Rows = all.Select(c => new FeeCategoryCatalogViewModel.Row(c, lineCounts.FirstOrDefault(x => x.Key == c.Id)?.N ?? 0, chargeCounts.FirstOrDefault(x => x.Key == c.Id)?.N ?? 0)).ToList();
            var vat = await _setup.GetSettingAsync(SettingKeys.VatRate);
            m.DefaultVatRate = decimal.TryParse(vat, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;

            // The chart of accounts, when a ledger is attached, so the GL export code is picked from
            // the accounts that actually exist. A failure here is not this screen's failure: the
            // catalogue must still open and stay editable if the ledger is down, so an empty list —
            // which reads downstream as "no ledger" — is the right answer rather than a 500.
            if (_glAccounts != null)
            {
                try
                {
                    m.GlAccounts = await _glAccounts.GetPostableAccountsAsync(HttpContext.RequestAborted);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    // Deliberately swallowed, and narrowly: the catalogue is a school screen that
                    // must keep opening when the ledger cannot answer. m.GlAccounts stays empty,
                    // which the view reads as "no ledger" and renders as the free-text field.
                }
            }

            return View(m);
        }

        [HttpPost("categories/new")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Categories, ActionVerb.Create)]
        public async Task<IActionResult> CreateCategory(string nameAr, string nameEn, string? vatRate, bool isMandatory = false, bool isRefundable = false, bool isServiceLinked = false, string? glExportCode = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nameAr) || string.IsNullOrWhiteSpace(nameEn)) throw new InvalidOperationException(T("Both names are required.", "الاسمان مطلوبان."));
                await _fees.DefineCategoryAsync(nameAr.Trim(), nameEn.Trim(), ParseRate(vatRate), isMandatory, isRefundable, isServiceLinked, Blank(glExportCode));
                TempData["Flash"] = T("Category added.", "أُضيفت الفئة.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Categories));
        }

        [HttpPost("categories/{id:int}/edit")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Categories, ActionVerb.Edit)]
        public async Task<IActionResult> EditCategory(int id, string nameAr, string nameEn, string? vatRate, bool isMandatory = false, bool isRefundable = false, bool isServiceLinked = false, string? glExportCode = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nameAr) || string.IsNullOrWhiteSpace(nameEn)) throw new InvalidOperationException(T("Both names are required.", "الاسمان مطلوبان."));
                await _fees.UpdateCategoryAsync(id, nameAr.Trim(), nameEn.Trim(), ParseRate(vatRate), isMandatory, isRefundable, isServiceLinked, Blank(glExportCode));
                TempData["Flash"] = T("Category updated — existing charges keep their VAT snapshot (BR-GLB-061).", "حُدّثت الفئة — تحتفظ الفواتير السابقة بنسبة الضريبة المسجّلة عليها (BR-GLB-061).");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Categories));
        }

        [HttpPost("categories/{id:int}/deactivate")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Categories, ActionVerb.Deactivate)]
        public async Task<IActionResult> DeactivateCategory(int id)
        {
            try
            {
                await _fees.DeactivateCategoryAsync(id);
                TempData["Flash"] = T("Category deactivated.", "أُلغي تفعيل الفئة.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Categories));
        }

        // ================================================================== 8.2 Fee structure workbench

        [HttpGet("structure")]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Structure, ActionVerb.View)]
        public async Task<IActionResult> Structure(int? year = null)
        {
            var m = new FeeStructureViewModel();
            await FillPageAsync(m, year);
            if (m.Year == null) return View(m);
            var yid = m.Year.Id;

            var profiles = await _db.GradeYearProfiles.IgnoreQueryFilters().AsNoTracking().Where(p => p.AcademicYearId == yid && p.SchoolId == _db.CurrentSchoolId && p.IsActive).ToListAsync();
            var grades = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking().Where(g => g.SchoolId == _db.CurrentSchoolId).ToListAsync();
            var stages = await _db.Stages.IgnoreQueryFilters().AsNoTracking().Where(s => s.SchoolId == _db.CurrentSchoolId).ToListAsync();
            var enrolled = await _db.Enrollments.AsNoTracking().Where(e => e.AcademicYearId == yid && e.Status == EnrollmentStatus.Active).GroupBy(e => e.GradeYearProfileId).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
            m.Profiles = profiles.Select(p =>
            {
                var g = grades.FirstOrDefault(x => x.Id == p.GradeLevelId) ?? new Sms.Domain.Grades.GradeLevel();
                return new FeeStructureViewModel.ProfileRow(p, g, stages.FirstOrDefault(s => s.Id == g.StageId) ?? new Sms.Domain.Grades.Stage(), enrolled.FirstOrDefault(x => x.Key == p.Id)?.N ?? 0);
            }).OrderBy(r => r.Stage.SequenceOrder).ThenBy(r => r.Grade.SequenceOrder).ToList();
            var pids = profiles.Select(p => p.Id).ToList();
            m.Lines = (await _db.FeeStructureLines.AsNoTracking().Where(l => pids.Contains(l.GradeYearProfileId)).ToListAsync()).ToDictionary(l => (l.GradeYearProfileId, l.FeeCategoryId));
            m.PreviousYear = m.Years.Where(y => y.StartDate < m.Year.StartDate).OrderByDescending(y => y.StartDate).FirstOrDefault();
            return View(m);
        }

        /// <summary>
        /// doc/Modules/19 §8.2. The grid was a post per cell, so pricing one grade across ten
        /// categories was ten submits and ten page reloads, and a row abandoned halfway stayed
        /// halfway. This saves the whole grade row in one act: a cell that was empty and now
        /// carries an amount becomes a Draft line, a Draft line whose figure changed is updated
        /// against that cell's own reason (T1 on <c>FeeStructureLine.Amount</c>), a cell left
        /// blank asks for nothing, and Approved or Withdrawn cells are passed over — BR-FEE-002
        /// makes an approved price immutable and its only exit is Withdraw, with a reason
        /// (BR-GLB-005).
        /// <para>
        /// Guarded on Edit, with Create tested at runtime for the cells that would add a line:
        /// one button is not one act, and a user who may revise prices but not introduce new
        /// ones keeps that boundary here as much as in a per-cell grid. Whatever could not be
        /// saved is counted back to the user rather than dropped in silence.
        /// </para>
        /// </summary>
        [HttpPost("structure/rows/{profileId:int}/save")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Structure, ActionVerb.Edit)]
        public async Task<IActionResult> SaveRow(int profileId, int? year)
        {
            try
            {
                if (!await RowBelongsToSchoolAsync(profileId)) return NotFound();

                var categories = await _db.FeeCategories.AsNoTracking().ToListAsync();
                var lines = await _db.FeeStructureLines.AsNoTracking().Where(l => l.GradeYearProfileId == profileId).ToListAsync();
                var canCreate = await _permissions.HasPermissionAsync(
                    ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Structure, ActionVerb.Create, HttpContext.RequestAborted);

                int added = 0, updated = 0, blocked = 0;
                var rejected = new List<string>();
                var unexplained = new List<string>();

                foreach (var c in categories)
                {
                    var raw = Request.Form[$"amount_{c.Id}"].ToString().Trim();
                    if (raw.Length == 0) continue;

                    var name = IsArabic ? c.NameAr : c.NameEn;

                    // Invariant on purpose: the cell is a number input, whose value the browser
                    // posts in that format whatever language the page is being read in.
                    if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount < 0)
                    {
                        rejected.Add(name);
                        continue;
                    }

                    var line = lines.FirstOrDefault(l => l.FeeCategoryId == c.Id);
                    if (line == null)
                    {
                        if (!canCreate) { blocked++; continue; }
                        _audit.Reason = null;
                        await _fees.DefineStructureLineAsync(profileId, c.Id, amount);
                        added++;
                        continue;
                    }

                    if (line.Status != FeeStructureLineStatus.Draft || line.Amount == amount) continue;

                    var reason = Request.Form[$"reason_{c.Id}"].ToString().Trim();
                    if (reason.Length == 0) { unexplained.Add(name); continue; }
                    _audit.Reason = reason;
                    await _fees.UpdateStructureLineAsync(line.Id, amount);
                    updated++;
                }

                TempData["Flash"] = added + updated == 0
                    ? T("Nothing changed in this row.", "لا تغيير في هذا الصف.")
                    : T($"Row saved — {added} added, {updated} updated. A new line is Draft; approve it before charging (BR-FEE-002).", $"حُفظ الصف — أُضيف {added} وحُدّث {updated}. السطر الجديد مسودة؛ اعتمده قبل الفوترة (BR-FEE-002).");

                var sep = T(", ", "، ");
                var refusals = new List<string>();
                if (rejected.Count > 0)
                {
                    refusals.Add(T($"{rejected.Count} cell(s) not saved — an amount must be a number of zero or more: {string.Join(sep, rejected)}.", $"لم تُحفظ {rejected.Count} خلية — المبلغ رقم أكبر من أو يساوي صفراً: {string.Join(sep, rejected)}."));
                }

                if (unexplained.Count > 0)
                {
                    refusals.Add(T($"A reason is required to change a fee amount (T1), so {unexplained.Count} cell(s) were left as they were: {string.Join(sep, unexplained)}.", $"تغيير مبلغ الرسم يتطلب سبباً (T1)، فبقيت {unexplained.Count} خلية كما هي: {string.Join(sep, unexplained)}."));
                }

                if (blocked > 0)
                {
                    refusals.Add(T($"{blocked} new price(s) were not added — you may revise this grade's prices but not introduce new ones.", $"لم تُضف {blocked} خلية جديدة — لديك صلاحية تعديل أسعار هذا الصف دون إنشاء أسعار جديدة."));
                }

                if (refusals.Count > 0) TempData["Error"] = string.Join(" · ", refusals);
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Structure), new { year });
        }

        /// <summary>
        /// doc/Modules/19 §8.2 — empties a grade's row in one act, which is what a row copied
        /// from the wrong year or priced against the wrong template needs. Draft lines only: an
        /// approved price is not scratch data but a figure the school has committed to
        /// (BR-FEE-002), and it leaves the price list only by withdrawal with a reason on the
        /// record (BR-GLB-005). Anything kept for that reason is reported rather than silently
        /// skipped — a row that looks unchanged is exactly how a clearing gets believed to have
        /// happened when it did not.
        /// </summary>
        [HttpPost("structure/rows/{profileId:int}/reset")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Structure, ActionVerb.Deactivate)]
        public async Task<IActionResult> ResetRow(int profileId, int? year)
        {
            try
            {
                if (!await RowBelongsToSchoolAsync(profileId)) return NotFound();

                var lines = await _db.FeeStructureLines.AsNoTracking().Where(l => l.GradeYearProfileId == profileId).ToListAsync();
                var kept = lines.Count(l => l.Status != FeeStructureLineStatus.Draft);
                var cleared = 0;
                foreach (var id in lines.Where(l => l.Status == FeeStructureLineStatus.Draft).Select(l => l.Id).ToList())
                {
                    await _fees.DeleteStructureLineAsync(id);
                    cleared++;
                }

                TempData["Flash"] = cleared == 0
                    ? T("Nothing to clear — this row has no draft lines.", "لا شيء للتصفير — لا مسودات في هذا الصف.")
                    : T($"{cleared} draft line(s) cleared.", $"صُفِّر {cleared} سطر مسودة.");

                if (kept > 0)
                {
                    TempData["Error"] = T($"{kept} approved or withdrawn price(s) stay — an approved price leaves the list only by withdrawal, with a reason (BR-GLB-005).", $"بقي {kept} سعر معتمد أو مسحوب — لا يخرج السعر المعتمد من القائمة إلا بالسحب مع ذكر السبب (BR-GLB-005).");
                }
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Structure), new { year });
        }

        /// <summary>
        /// The row a POST names is a route value, so it is checked against the tenant before
        /// anything is written. GradeYearProfile is year-scoped as well as school-scoped, which
        /// is why the filters come off and the school is stated explicitly.
        /// </summary>
        private async Task<bool> RowBelongsToSchoolAsync(int profileId) =>
            await _db.GradeYearProfiles.IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(p => p.Id == profileId && p.SchoolId == _db.CurrentSchoolId);

        [HttpPost("structure/lines/{id:int}/approve")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Structure, ActionVerb.Approve)]
        public async Task<IActionResult> ApproveLine(int id, int? year)
        {
            try
            {
                await _fees.ApproveStructureLineAsync(id);
                TempData["Flash"] = T("Line approved — it is now immutable and chargeable.", "اعتُمد السطر — أصبح ثابتاً وقابلاً للفوترة.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Structure), new { year });
        }

        [HttpPost("structure/lines/{id:int}/delete")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Structure, ActionVerb.Deactivate)]
        public async Task<IActionResult> DeleteLine(int id, int? year)
        {
            try
            {
                await _fees.DeleteStructureLineAsync(id);
                TempData["Flash"] = T("Draft line removed.", "حُذف سطر المسودة.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Structure), new { year });
        }

        /// <summary>
        /// The exit an approved price did not have. BR-FEE-002 makes the amount
        /// immutable and the delete path is draft-only, so before this a line approved
        /// against the wrong grade stayed in the price list permanently. Withdrawing
        /// leaves the row and its figure readable (BR-GLB-005) and stops it billing.
        /// </summary>
        [HttpPost("structure/{id:int}/withdraw")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Structure, ActionVerb.Deactivate)]
        public async Task<IActionResult> WithdrawLine(int id, string? reason, int? year)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reason))
                {
                    throw new InvalidOperationException(T("A reason is required to withdraw an approved price.", "السبب مطلوب لسحب سعر معتمد."));
                }

                await _fees.WithdrawStructureLineAsync(id, reason.Trim());
                TempData["Flash"] = T("Price withdrawn — it stays on the record and stops being charged.", "سُحب السعر — يبقى في السجل ويتوقف عن التحميل.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Structure), new { year });
        }

        [HttpPost("structure/approve-all")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Structure, ActionVerb.Approve)]
        public async Task<IActionResult> ApproveAll(int year)
        {
            var n = 0;
            try
            {
                var drafts = await _db.FeeStructureLines.AsNoTracking().Where(l => l.AcademicYearId == year && l.Status == FeeStructureLineStatus.Draft).Select(l => l.Id).ToListAsync();
                foreach (var id in drafts) { await _fees.ApproveStructureLineAsync(id); n++; }
                TempData["Flash"] = T($"{n} line(s) approved.", $"اعتُمد {n} سطر/أسطر.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Structure), new { year });
        }

        [HttpPost("structure/copy")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Structure, ActionVerb.Create)]
        public async Task<IActionResult> CopyStructure(int sourceYear, int year, decimal uplift = 0m)
        {
            try
            {
                if (sourceYear == year) throw new InvalidOperationException(T("Source and target years must differ.", "يجب أن يختلف العام المصدر عن الهدف."));
                var n = await _fees.CopyStructureAsync(sourceYear, year, uplift);
                TempData["Flash"] = n == 0
                    ? T("Nothing copied — no approved source lines, or every grade × category already has a line.", "لم يُنسخ شيء — لا أسطر معتمدة في المصدر أو كل الأزواج موجودة.")
                    : T($"{n} draft line(s) created at {(uplift >= 0 ? "+" : "")}{uplift:0.##}% — review and approve.", $"أُنشئ {n} سطر مسودة بنسبة {(uplift >= 0 ? "+" : "")}{uplift:0.##}% — راجع واعتمد.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Structure), new { year });
        }

        // ================================================================== 8.4 Misc charge entry (+ post from structure)

        [HttpGet("charges/new")]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Charges, ActionVerb.Create)]
        public async Task<IActionResult> NewCharge(int? studentId = null, int? year = null)
        {
            var m = await BuildNewChargeAsync(studentId, year);
            return View(m);
        }

        [HttpPost("charges/new")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Charges, ActionVerb.Post)]
        public async Task<IActionResult> PostCharge(int studentId, int parentId, string mode, int? categoryId, decimal? amount, string? reason, int? year, ChargeSourceType? sourceType = null)
        {
            try
            {
                if (categoryId == null) throw new InvalidOperationException(T("Choose a fee category.", "اختر فئة رسوم."));
                var payer = await _fees.EnsurePayerForParentAsync(parentId);
                Charge charge;
                if (mode == "structure")
                {
                    var enrollment = await _db.Enrollments.AsNoTracking().Where(e => e.StudentId == studentId && e.Status == EnrollmentStatus.Active).OrderByDescending(e => e.Id).FirstOrDefaultAsync()
                        ?? throw new InvalidOperationException(T("The student has no active enrollment to charge against.", "لا قيد نشط للطالب لفوترته."));
                    if (await _db.Charges.AnyAsync(c => c.StudentId == studentId && c.FeeCategoryId == categoryId && c.AcademicYearId == enrollment.AcademicYearId && c.Status == ChargeStatus.Posted && c.SourceType != ChargeSourceType.Manual))
                        throw new InvalidOperationException(T("This category is already charged for the student this year.", "هذه الفئة مفوترة للطالب هذا العام مسبقاً."));
                    var src = sourceType is ChargeSourceType.Registration or ChargeSourceType.ReRegistration or ChargeSourceType.ServiceAssignment ? sourceType.Value : ChargeSourceType.Registration;
                    charge = await _fees.PostChargeAsync(studentId, payer.Id, enrollment.GradeYearProfileId, categoryId.Value, src);
                }
                else
                {
                    if (amount == null || amount <= 0) throw new InvalidOperationException(T("Enter a positive amount.", "أدخل مبلغاً موجباً."));
                    if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException(T("A reason is mandatory for a manual charge (BR-FEE-003).", "السبب إلزامي للرسم اليدوي (BR-FEE-003)."));
                    _audit.Reason = reason.Trim();
                    charge = await _fees.PostManualChargeAsync(studentId, payer.Id, categoryId.Value, amount.Value);
                }
                TempData["Flash"] = T($"Charge {charge.ChargeNo} posted — gross {charge.GrossAmount:N2} (VAT {charge.VatAmount:N2}).", $"رُحّلت الفاتورة {charge.ChargeNo} — الإجمالي {charge.GrossAmount:N2} (الضريبة {charge.VatAmount:N2}).");
                return RedirectToAction(nameof(Charge), new { id = charge.Id });
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(NewCharge), new { studentId, year });
        }

        // ================================================================== 8.3 document view + 8.5 credit note flow

        [HttpGet("charges/{id:int}")]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Charges, ActionVerb.View)]
        public async Task<IActionResult> Charge(int id, bool print = false)
        {
            var charge = await _db.Charges.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id);
            if (charge == null) return NotFound();
            var school = await _db.Schools.AsNoTracking().SingleOrDefaultAsync(s => s.Id == _db.CurrentSchoolId);
            var vatNo = await _setup.GetSettingAsync(SettingKeys.VatRegistrationNumber);
            var payer = await _db.Payers.AsNoTracking().SingleOrDefaultAsync(p => p.Id == charge.PayerId);
            var enrollment = await _db.Enrollments.AsNoTracking().Where(e => e.StudentId == charge.StudentId && e.AcademicYearId == charge.AcademicYearId).OrderByDescending(e => e.Id).FirstOrDefaultAsync();
            var profile = enrollment == null ? null : await _db.GradeYearProfiles.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(p => p.Id == enrollment.GradeYearProfileId);
            var allocations = await _db.PaymentAllocations.AsNoTracking().Where(a => a.ChargeId == id).ToListAsync();
            var rids = allocations.Select(a => a.ReceiptId).ToList();
            var receipts = await _db.Receipts.AsNoTracking().Where(r => rids.Contains(r.Id)).ToListAsync();
            var m = new ChargeDocumentViewModel
            {
                Charge = charge,
                Category = await _db.FeeCategories.IgnoreQueryFilters().AsNoTracking().SingleAsync(c => c.Id == charge.FeeCategoryId),
                Student = await _db.Students.IgnoreQueryFilters().AsNoTracking().SingleAsync(s => s.Id == charge.StudentId),
                Payer = payer?.ParentId == null ? null : await _db.Parents.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(p => p.Id == payer.ParentId),
                Year = await _db.AcademicYears.AsNoTracking().SingleAsync(y => y.Id == charge.AcademicYearId),
                Grade = profile == null ? null : await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(g => g.Id == profile.GradeLevelId),
                SchoolNameAr = school?.NameAr ?? "", SchoolNameEn = school?.NameEn ?? "", SchoolAddress = school == null ? null : string.Join(", ", new[] { school.AddressLine, school.City }.Where(x => !string.IsNullOrWhiteSpace(x))),
                VatRegistrationNumber = vatNo,
                CreditNotes = await _db.CreditNotes.AsNoTracking().Where(n => n.ChargeId == id).OrderBy(n => n.IssuedAtUtc).ToListAsync(),
                Allocations = allocations.Select(a => (a, receipts.First(r => r.Id == a.ReceiptId))).OrderBy(x => x.Item2.IssuedAtUtc).ToList(),
                Discounted = (await _db.DiscountDocuments.AsNoTracking().Where(d => d.ChargeId == id).Select(d => d.Amount).ToListAsync()).Sum(),
                IsPrint = print,
            };
            if (charge.VatRateSnapshot != null && !string.IsNullOrWhiteSpace(vatNo))
            {
                m.ZatcaQrPayload = ZatcaQrCodeBuilder.BuildBase64Payload(school?.NameAr ?? school?.NameEn ?? "", vatNo!, charge.PostedAtUtc, charge.GrossAmount, charge.VatAmount);
            }
            return View(m);
        }

        [HttpPost("charges/{id:int}/void")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Charges, ActionVerb.Deactivate)]
        public async Task<IActionResult> VoidCharge(int id, string? reason, int? year)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException(T("A reason is mandatory to void a charge (T1).", "السبب إلزامي لحذف الفاتورة (T1)."));
                _audit.Reason = reason.Trim();
                var no = await _db.Charges.AsNoTracking().Where(c => c.Id == id).Select(c => c.ChargeNo).FirstOrDefaultAsync();
                await _fees.VoidChargeAsync(id);
                TempData["Flash"] = T($"Charge {no} voided — it no longer counts toward any balance.", $"حُذفت الفاتورة {no} (ملغاة) — لم تعد تُحتسب في أي رصيد.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Index), new { year });
        }

        [HttpPost("charges/{id:int}/credit-note")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Charges, ActionVerb.Deactivate)]
        public async Task<IActionResult> IssueCreditNote(int id, decimal amount, string? reason)
        {
            try
            {
                if (amount <= 0) throw new InvalidOperationException(T("Enter a positive amount.", "أدخل مبلغاً موجباً."));
                if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException(T("A reason is mandatory on a credit note.", "السبب إلزامي على الإشعار الدائن."));
                _audit.Reason = reason.Trim();
                var note = await _fees.IssueCreditNoteAsync(id, amount, reason.Trim());
                TempData["Flash"] = T($"Credit note {note.CreditNoteNo} issued for {note.Amount:N2}.", $"صدر الإشعار الدائن {note.CreditNoteNo} بمبلغ {note.Amount:N2}.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Charge), new { id });
        }

        // ================================================================== 8.7 Student/payer position

        /// <summary>
        /// <para>
        /// <paramref name="parentId"/> exists because a <c>Payer</c> row is created by the first
        /// charge (BR-FEE-004), not by being a guardian — so a school that has registered six
        /// hundred families and billed two had a statement screen that could find two of them and
        /// answered "no matching payers" for the rest. A guardian on file who has never been billed
        /// is a legitimate answer to this search, and their statement is legitimately empty; saying
        /// so is the difference between an account not yet opened and a person who does not exist.
        /// </para>
        /// </summary>
        [HttpGet("position")]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Position, ActionVerb.View)]
        public async Task<IActionResult> Position(int? payerId = null, string? q = null, DateTime? asOf = null, int? parentId = null)
        {
            var m = new PayerPositionViewModel { Q = q, AsOf = asOf };
            m.Payers = await FinanceQueries.SearchPayersAsync(_db, q, take: 30);
            (m.Unbilled, m.UnbilledTotal) = await FinanceQueries.SearchUnbilledGuardiansAsync(_db, q, take: 30);

            if (parentId != null)
            {
                m.SelectedUnbilled = await FinanceQueries.UnbilledGuardianAsync(_db, parentId.Value);
                // Null means the guardian has since been billed — a charge posted between the search
                // and the click. Fall through to their real payer rather than showing an empty
                // statement that is now a lie.
                if (m.SelectedUnbilled != null) return View(m);
                payerId ??= await _db.Payers.AsNoTracking().Where(p => p.ParentId == parentId).Select(p => p.Id).FirstOrDefaultAsync();
                if (payerId == 0) return NotFound();
            }

            if (payerId == null && m.Payers.Count == 1 && m.Unbilled.Count == 0 && !string.IsNullOrWhiteSpace(q)) payerId = m.Payers[0].Payer.Id;
            if (payerId == null && m.Payers.Count == 0 && m.Unbilled.Count == 1 && !string.IsNullOrWhiteSpace(q))
            {
                m.SelectedUnbilled = m.Unbilled[0];
                return View(m);
            }
            if (payerId == null) return View(m);
            m.Selected = await FinanceQueries.CardAsync(_db, payerId.Value);
            if (m.Selected == null) return NotFound();

            var asOfUtc = asOf?.Date.AddDays(1).AddTicks(-1);
            m.Statement = await _statements.BuildAsync(payerId.Value, asOfUtc);
            m.AllCharges = await FinanceQueries.ChargeRowsAsync(_db, payerId: payerId, openOnly: false);
            m.OpenCharges = m.AllCharges.Where(r => r.Remaining > 0).ToList();
            m.AdvanceBalance = await FinanceQueries.AdvanceBalanceAsync(_db, payerId.Value);
            var now = _clock.UtcNow;
            m.Aging = m.OpenCharges.GroupBy(r => ReceivablesAgingBucketer.Bucket(r.Charge.PostedAtUtc, now)).ToDictionary(g => g.Key, g => g.Sum(r => r.Remaining));
            var perChild = new List<(Student, decimal)>();
            foreach (var child in m.Selected.Students) perChild.Add((child, await _fees.ComputeStudentPositionAsync(child.Id)));
            m.PerChild = perChild;
            return View(m);
        }

        // ================================================================== helpers

        private async Task<MiscChargeViewModel> BuildNewChargeAsync(int? studentId, int? year)
        {
            var m = new MiscChargeViewModel();
            await FillPageAsync(m, year);
            if (m.Year == null) return m;
            var yid = m.Year.Id;
            var enrollments = await _db.Enrollments.AsNoTracking().Where(e => e.AcademicYearId == yid && e.Status == EnrollmentStatus.Active).ToListAsync();
            var sids = enrollments.Select(e => e.StudentId).Distinct().ToList();
            var students = await _db.Students.AsNoTracking().Where(s => sids.Contains(s.Id)).ToListAsync();
            var profiles = await _db.GradeYearProfiles.IgnoreQueryFilters().AsNoTracking().Where(p => p.AcademicYearId == yid).ToListAsync();
            var grades = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking().Where(g => g.SchoolId == _db.CurrentSchoolId).ToListAsync();
            m.Students = students.Select(s =>
            {
                var e = enrollments.First(x => x.StudentId == s.Id);
                var p = profiles.FirstOrDefault(x => x.Id == e.GradeYearProfileId);
                return new MiscChargeViewModel.StudentOption(s, p == null ? null : grades.FirstOrDefault(g => g.Id == p.GradeLevelId), p);
            }).OrderBy(o => o.Grade?.SequenceOrder).ThenBy(o => o.Student.StudentNo).ToList();
            if (studentId == null) return m;

            var sel = m.Students.FirstOrDefault(o => o.Student.Id == studentId);
            if (sel == null) return m;
            m.Selected = sel.Student;
            m.SelectedProfile = sel.Profile;
            var links = await _db.StudentGuardianLinks.AsNoTracking().Where(l => l.StudentId == studentId && l.EffectiveToUtc == null).ToListAsync();
            var pids = links.Select(l => l.ParentId).ToList();
            var parents = await _db.Parents.IgnoreQueryFilters().AsNoTracking().Where(p => pids.Contains(p.Id)).ToListAsync();
            var payers = await _db.Payers.AsNoTracking().Where(p => p.ParentId != null && pids.Contains(p.ParentId.Value)).ToListAsync();
            m.Payers = links.Select(l => new MiscChargeViewModel.PayerOption(parents.First(p => p.Id == l.ParentId), l.IsFinanciallyResponsible, payers.FirstOrDefault(p => p.ParentId == l.ParentId)))
                .OrderByDescending(p => p.IsFinanciallyResponsible).ThenBy(p => p.Parent.NameEn).ToList();
            if (sel.Profile != null)
            {
                var lines = await _db.FeeStructureLines.AsNoTracking().Where(l => l.GradeYearProfileId == sel.Profile.Id && l.Status == FeeStructureLineStatus.Approved).ToListAsync();
                var charged = await _db.Charges.AsNoTracking().Where(c => c.StudentId == studentId && c.AcademicYearId == yid && c.Status == ChargeStatus.Posted && c.SourceType != ChargeSourceType.Manual).Select(c => c.FeeCategoryId).ToListAsync();
                m.StructureLines = lines.Select(l => (l, m.Categories.FirstOrDefault(c => c.Id == l.FeeCategoryId) ?? new FeeCategory { NameAr = "?", NameEn = "?" }, charged.Contains(l.FeeCategoryId))).OrderBy(x => x.Item2.NameEn).ToList();
            }
            return m;
        }

        private async Task FillPageAsync(FinancePageViewModel m, int? yearId)
        {
            var years = await _db.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartDate).ToListAsync();
            m.Years = years;
            m.Year = years.FirstOrDefault(y => y.Id == (yearId ?? _workingYear.AcademicYearId)) ?? years.FirstOrDefault(y => y.Status == AcademicYearStatus.Active) ?? years.FirstOrDefault();
            m.Categories = await _db.FeeCategories.AsNoTracking().OrderBy(c => c.NameEn).ToListAsync();
        }

        private static decimal? ParseRate(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            if (!decimal.TryParse(raw.Replace("%", "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var v)) throw new InvalidOperationException("VAT rate must be numeric (e.g. 15 or 0.15).");
            if (v > 1m) v /= 100m; // accept "15" as 15 %
            if (v < 0m || v > 1m) throw new InvalidOperationException("VAT rate must be between 0 and 100 %.");
            return v;
        }

        private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}
