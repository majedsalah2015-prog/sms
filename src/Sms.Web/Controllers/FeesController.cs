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
    /// position (statement of account, as-of date, aging, drill to documents).
    /// Deferred: 8.6 late-fee run console (M20 policy), 8.8 portal fees
    /// (E-304 already shows posted invoices), threshold routing / chain
    /// approval on misc charges and credit notes (WF not modelled).
    /// </summary>
    [Route("fees")]
    public class FeesController : Controller
    {
        private readonly IFeeAdmin _fees;
        private readonly IStatementService _statements;
        private readonly ISystemSetupAdmin _setup;
        private readonly AppDbContext _db;
        private readonly IAuditContext _audit;
        private readonly IWorkingYearContext _workingYear;
        private readonly IClock _clock;

        public FeesController(IFeeAdmin fees, IStatementService statements, ISystemSetupAdmin setup, AppDbContext db, IAuditContext audit, IWorkingYearContext workingYear, IClock clock)
        {
            _fees = fees;
            _statements = statements;
            _setup = setup;
            _db = db;
            _audit = audit;
            _workingYear = workingYear;
            _clock = clock;
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

        [HttpPost("structure/lines/new")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Structure, ActionVerb.Create)]
        public async Task<IActionResult> CreateLine(int profileId, int categoryId, decimal amount, int? year)
        {
            try
            {
                if (amount < 0) throw new InvalidOperationException(T("Amount cannot be negative.", "لا يمكن أن يكون المبلغ سالباً."));
                await _fees.DefineStructureLineAsync(profileId, categoryId, amount);
                TempData["Flash"] = T("Draft line added — approve it before charging (BR-FEE-002).", "أُضيف سطر مسودة — اعتمده قبل الفوترة (BR-FEE-002).");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Structure), new { year });
        }

        [HttpPost("structure/lines/{id:int}/edit")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Structure, ActionVerb.Edit)]
        public async Task<IActionResult> EditLine(int id, decimal amount, string? reason, int? year)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException(T("A reason is required to change a fee amount (T1).", "تغيير مبلغ الرسم يتطلب سبباً (T1)."));
                _audit.Reason = reason.Trim();
                await _fees.UpdateStructureLineAsync(id, amount);
                TempData["Flash"] = T("Amount updated.", "حُدّث المبلغ.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Structure), new { year });
        }

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

        [HttpGet("position")]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Position, ActionVerb.View)]
        public async Task<IActionResult> Position(int? payerId = null, string? q = null, DateTime? asOf = null)
        {
            var m = new PayerPositionViewModel { Q = q, AsOf = asOf };
            m.Payers = await FinanceQueries.SearchPayersAsync(_db, q, take: 30);
            if (payerId == null && m.Payers.Count == 1 && !string.IsNullOrWhiteSpace(q)) payerId = m.Payers[0].Payer.Id;
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
            foreach (var child in m.Selected.Children) perChild.Add((child, await _fees.ComputeStudentPositionAsync(child.Id)));
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
