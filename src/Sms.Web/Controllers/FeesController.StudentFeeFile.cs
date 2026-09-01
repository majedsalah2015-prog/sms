using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Fees;
using Sms.Application.Security;
using Sms.Application.Setup;
using Sms.Domain.Discounts;
using Sms.Domain.Fees;
using Sms.Domain.Installments;
using Sms.Domain.Security;
using Sms.Domain.Students;
using Sms.Web.Models;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/19 §8.7 turned into the screen a fee clerk actually works from: the items,
    /// the installment template and the discount picked on the student's own file and approved
    /// as one act, with edit and remove on anything already billed (owner request, 2026-08-31).
    /// <para>
    /// It gathers three screens the specification lists apart — §8.4 misc charge entry,
    /// doc/Modules/20 §8.2 exception assignment, doc/Modules/22 §8.3 grant desk — onto the one
    /// page whose subject is the child. Those screens stay where they are and keep their own
    /// bulk and cross-family work; this is the single-student path, which is the one the counter
    /// uses and the one that previously required three screens and a remembered student number.
    /// </para>
    /// <para>
    /// <b>Rights are checked per part, not per page.</b> Committing needs Fees/Charges/Post;
    /// the template picker additionally needs Installments/Assignment/Create and the discount
    /// picker Discounts/Grants/Submit + Approve. Without that, this screen would be a way to
    /// approve a discount without holding the discount permission — BR-SEC-010 hides what the
    /// user may not do rather than refusing it after they have filled the form in.
    /// </para>
    /// <para>
    /// <b>Deviation from the owner's wording:</b> "delete an item" is a credit note here, not a
    /// deletion. BR-GLB-005 leaves no delete verb in this product and a posted invoice is
    /// immutable (BR-GLB-062) — the item stops being owed and the document stays readable with
    /// the reason on it. Voiding was the alternative and is deliberately not used: it erases
    /// billed history and breaks BR-FEE-005's invoice hash chain.
    /// </para>
    /// </summary>
    public partial class FeesController
    {
        // ================================================================== the basket

        [HttpPost("students/{id:int}/commit")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Charges, ActionVerb.Post)]
        public async Task<IActionResult> CommitStudentFeeFile(
            [FromServices] IStudentFeeFileService feeFile,
            int id,
            int parentId,
            int? year = null,
            int[]? items = null,
            int? extraCategoryId = null,
            decimal? extraAmount = null,
            string? extraReason = null,
            int? planTemplateId = null,
            int? discountTypeId = null,
            decimal? discountValue = null,
            string? discountReason = null,
            bool discountHasDocumentation = false)
        {
            try
            {
                // The engines behind the basket read the working year, not the year in the query
                // string. Committing from a year the clerk is only browsing would post into a
                // different one from the one on screen, so it is refused rather than redirected.
                if (year != null && year != _workingYear.AcademicYearId)
                {
                    throw new InvalidOperationException(T(
                        "You are viewing a year other than the working year. Switch to the working year before approving this student's finances.",
                        "أنت تستعرض عاماً غير عام العمل. انتقل إلى عام العمل قبل اعتماد مالية الطالب."));
                }

                var chosen = (items ?? Array.Empty<int>()).ToList();
                var extra = await BuildExtraItemAsync(extraCategoryId, extraAmount, extraReason);
                var plan = await GuardedPlanTemplateAsync(planTemplateId);
                var discount = await GuardedDiscountAsync(discountTypeId, discountValue, discountReason, discountHasDocumentation);

                var result = await feeFile.CommitAsync(
                    new StudentFeeFileCommit(id, parentId, chosen, extra, plan, discount, await FeeFileWeekendDaysAsync()),
                    HttpContext.RequestAborted);

                TempData["Flash"] = CommitSummary(result);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(StudentFinanceDetail), new { id, year });
        }

        // ================================================================== edit / remove a billed item

        [HttpPost("students/{id:int}/items/{chargeId:int}/adjust")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Charges, ActionVerb.Deactivate)]
        public async Task<IActionResult> AdjustStudentFeeItem(
            [FromServices] IStudentFeeFileService feeFile, int id, int chargeId, decimal? newAmount, string? reason, int? year = null)
        {
            try
            {
                if (newAmount == null || newAmount < 0m)
                {
                    throw new InvalidOperationException(T("Enter the new amount for this item.", "أدخل المبلغ الجديد لهذا البند."));
                }

                if (string.IsNullOrWhiteSpace(reason))
                {
                    throw new InvalidOperationException(T(
                        "A reason is mandatory — the item changes by a credit note that has to say why (BR-GLB-062).",
                        "السبب إلزامي — يتغيّر البند بإشعار دائن يجب أن يذكر السبب (BR-GLB-062)."));
                }

                await EnsureChargeBelongsToStudentAsync(chargeId, id);
                var note = await feeFile.AdjustItemAsync(chargeId, newAmount.Value, reason.Trim(), HttpContext.RequestAborted);
                TempData["Flash"] = T(
                    $"Item reduced to {newAmount.Value:N2} — credit note {note.CreditNoteNo} for {note.Amount:N2}.",
                    $"خُفّض البند إلى {newAmount.Value:N2} — إشعار دائن {note.CreditNoteNo} بمبلغ {note.Amount:N2}.");
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(StudentFinanceDetail), new { id, year });
        }

        [HttpPost("students/{id:int}/items/{chargeId:int}/remove")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Charges, ActionVerb.Deactivate)]
        public async Task<IActionResult> RemoveStudentFeeItem(
            [FromServices] IStudentFeeFileService feeFile, int id, int chargeId, string? reason, int? year = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reason))
                {
                    throw new InvalidOperationException(T(
                        "A reason is mandatory — the item is relieved by a credit note that stays on the record (BR-GLB-005).",
                        "السبب إلزامي — يُعفى البند بإشعار دائن يبقى في السجل (BR-GLB-005)."));
                }

                await EnsureChargeBelongsToStudentAsync(chargeId, id);
                var note = await feeFile.RemoveItemAsync(chargeId, reason.Trim(), HttpContext.RequestAborted);
                TempData["Flash"] = T(
                    $"Item removed — credit note {note.CreditNoteNo} for {note.Amount:N2}. The invoice stays on file with the reason.",
                    $"حُذف البند — إشعار دائن {note.CreditNoteNo} بمبلغ {note.Amount:N2}. تبقى الفاتورة في الملف مع السبب.");
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(StudentFinanceDetail), new { id, year });
        }

        // ================================================================== the panel behind the GET

        /// <summary>
        /// Builds the basket for <see cref="StudentFinanceDetail"/>. Returns null when the reader
        /// may not post charges: an unusable panel is worse than no panel, because it advertises
        /// a gesture the person will be refused (BR-SEC-010).
        /// </summary>
        private async Task<StudentFeeFilePanel?> BuildFeeFilePanelAsync(StudentFinanceDetailViewModel model, int studentId)
        {
            var canCommit = await _permissions.HasPermissionAsync(
                ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Charges, ActionVerb.Post, HttpContext.RequestAborted);
            var canAdjust = await _permissions.HasPermissionAsync(
                ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Charges, ActionVerb.Deactivate, HttpContext.RequestAborted);
            if (!canCommit && !canAdjust)
            {
                return null;
            }

            var workingYearId = _workingYear.AcademicYearId;
            var panel = new StudentFeeFilePanel
            {
                WorkingYearId = workingYearId,
                CanCommit = canCommit,
                CanAdjustItems = canAdjust,
                CanAssignPlan = await _permissions.HasPermissionAsync(
                    ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Assignment, ActionVerb.Create, HttpContext.RequestAborted),
                CanGrantDiscount = await _permissions.HasPermissionAsync(
                        ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Grants, ActionVerb.Submit, HttpContext.RequestAborted)
                    && await _permissions.HasPermissionAsync(
                        ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Grants, ActionVerb.Approve, HttpContext.RequestAborted),
                Payers = model.Guardians,
                HasPlan = model.Plans.Count > 0,
                HasDiscount = model.Discounts.Any(d => d.Grant.Status != DiscountGrantStatus.Rejected),
                AllCategories = model.Categories,
            };

            panel.Blocker =
                model.Year != null && model.Year.Id != workingYearId ? FeeFileBlocker.NotWorkingYear
                : model.NotEnrolled ? FeeFileBlocker.NotEnrolled
                : model.Guardians.Count == 0 ? FeeFileBlocker.NoGuardian
                : FeeFileBlocker.None;

            if (panel.Blocker != FeeFileBlocker.None)
            {
                return panel;
            }

            // The offerable items are the price list the detail screen has already read, minus
            // what it has already billed — the same StructureRow that answers "not billed" two
            // panels above, so the checklist and the comparison table can never disagree.
            panel.Items = model.Structure
                .Where(r => !r.IsCharged)
                .Select(r => new StudentFeeFilePanel.ItemOption(r.Category, r.Expected))
                .ToList();

            if (panel.CanAssignPlan && !panel.HasPlan)
            {
                // Include the splits: the picker names each template by how many installments it
                // makes, and without them every option reads "0 installments".
                panel.Templates = await _db.PlanTemplates.AsNoTracking()
                    .Include(t => t.Installments)
                    .Where(t => t.AcademicYearId == workingYearId && t.Status == PlanTemplateStatus.Approved)
                    .OrderBy(t => t.NameEn)
                    .ToListAsync(HttpContext.RequestAborted);
            }

            if (panel.CanGrantDiscount && !panel.HasDiscount)
            {
                // Manual only: an automatic type is granted by its own eligibility run over the
                // whole roll (BR-DIS-002), and handing one to a single child from here would
                // produce a grant the run does not know it made.
                panel.DiscountTypes = await _db.DiscountTypes.AsNoTracking()
                    .Where(t => t.EligibilityMode == DiscountEligibilityMode.Manual)
                    .OrderBy(t => t.NameEn)
                    .ToListAsync(HttpContext.RequestAborted);
            }

            return panel;
        }

        // ================================================================== helpers

        /// <summary>
        /// doc/Modules/19 §8.4 on this screen: a service the grade's price list does not carry.
        /// BR-FEE-003 makes the reason mandatory, because a manual amount is the one figure in
        /// the system that cannot be re-derived from anything else.
        /// </summary>
        private async Task<ManualFeeItem?> BuildExtraItemAsync(int? categoryId, decimal? amount, string? reason)
        {
            if (categoryId == null && amount == null && string.IsNullOrWhiteSpace(reason))
            {
                return null;
            }

            if (categoryId == null)
            {
                throw new InvalidOperationException(T("Choose a fee category for the extra item.", "اختر فئة رسوم للبند الإضافي."));
            }

            if (amount == null || amount <= 0m)
            {
                throw new InvalidOperationException(T("Enter a positive amount for the extra item.", "أدخل مبلغاً موجباً للبند الإضافي."));
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new InvalidOperationException(T(
                    "A reason is mandatory for an item priced outside the fee structure (BR-FEE-003).",
                    "السبب إلزامي للبند المسعَّر خارج هيكل الرسوم (BR-FEE-003)."));
            }

            if (!await _db.FeeCategories.AsNoTracking().AnyAsync(c => c.Id == categoryId, HttpContext.RequestAborted))
            {
                throw new InvalidOperationException(T("That fee category no longer exists.", "لم تعد فئة الرسوم هذه موجودة."));
            }

            return new ManualFeeItem(categoryId.Value, amount.Value, reason.Trim());
        }

        /// <summary>
        /// A template id arriving from a user who does not hold Installments/Assignment/Create is
        /// a hand-built form, not a click: the picker is not rendered for them. Refused rather
        /// than honoured, so this screen cannot become a way around the other module's permission.
        /// </summary>
        private async Task<int?> GuardedPlanTemplateAsync(int? planTemplateId)
        {
            if (planTemplateId == null)
            {
                return null;
            }

            var allowed = await _permissions.HasPermissionAsync(
                ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Assignment, ActionVerb.Create, HttpContext.RequestAborted);
            if (!allowed)
            {
                throw new InvalidOperationException(T(
                    "You may not assign an installment plan.", "لا تملك صلاحية إسناد خطة تقسيط."));
            }

            return planTemplateId;
        }

        /// <summary>Same guard for the discount half — this screen approves the grant outright, so it demands Submit and Approve both.</summary>
        private async Task<DiscountRequest?> GuardedDiscountAsync(int? discountTypeId, decimal? basisValue, string? reason, bool hasDocumentation)
        {
            if (discountTypeId == null && basisValue == null && string.IsNullOrWhiteSpace(reason))
            {
                return null;
            }

            var allowed = await _permissions.HasPermissionAsync(
                    ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Grants, ActionVerb.Submit, HttpContext.RequestAborted)
                && await _permissions.HasPermissionAsync(
                    ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Grants, ActionVerb.Approve, HttpContext.RequestAborted);
            if (!allowed)
            {
                throw new InvalidOperationException(T(
                    "You may not approve a discount grant.", "لا تملك صلاحية اعتماد منح الخصم."));
            }

            if (discountTypeId == null)
            {
                throw new InvalidOperationException(T("Choose a discount type.", "اختر نوع الخصم."));
            }

            if (basisValue == null || basisValue <= 0m)
            {
                throw new InvalidOperationException(T("Enter a positive discount value.", "أدخل قيمة خصم موجبة."));
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new InvalidOperationException(T(
                    "A reason is mandatory on a discount grant (BR-DIS-003).", "السبب إلزامي على منح الخصم (BR-DIS-003)."));
            }

            return new DiscountRequest(discountTypeId.Value, basisValue.Value, reason.Trim(), hasDocumentation);
        }

        /// <summary>
        /// The charge id comes off a form, and nothing else in the route ties it to the student
        /// whose file is open. Without this check a clerk could credit any invoice in the school
        /// by editing one number — the permission says they may issue credit notes, not that they
        /// may issue this one from this page.
        /// </summary>
        private async Task EnsureChargeBelongsToStudentAsync(int chargeId, int studentId)
        {
            var belongs = await _db.Charges.AsNoTracking().AnyAsync(c => c.Id == chargeId && c.StudentId == studentId, HttpContext.RequestAborted);
            if (!belongs)
            {
                throw new InvalidOperationException(T(
                    "That item does not belong to this student.", "هذا البند لا يخص هذا الطالب."));
            }
        }

        /// <summary>
        /// Read from the same <c>Regional.WorkingDays</c> setting the calendar board and the
        /// assignment console use, through the same helper — a second source for the working week
        /// would let a due date land on a day the calendar calls a weekend.
        /// <para>
        /// <paramref name="yearId"/> defaults to the working year, which is the only year the
        /// basket can commit into. A plan being reshaped is read in its own year instead: the
        /// setting is per year, and shifting a 2026 due date by the 2027 working week would put it
        /// on a day that year calls a weekend.
        /// </para>
        /// </summary>
        private async Task<ISet<DayOfWeek>> FeeFileWeekendDaysAsync(int? yearId = null)
        {
            var working = await _setup.GetSettingAsync(SettingKeys.WorkingDays, yearId ?? _workingYear.AcademicYearId)
                ?? "Sunday,Monday,Tuesday,Wednesday,Thursday";
            return new HashSet<DayOfWeek>(WorkingWeek.WeekendDays(working));
        }

        /// <summary>Reports what the commit actually did. "Saved" is not an answer when money moved.</summary>
        private string CommitSummary(StudentFeeFileCommitResult result)
        {
            var parts = new List<string>();
            if (result.ItemCount > 0)
            {
                parts.Add(T($"{result.ItemCount} item(s) billed, {result.PostedGross:N2} gross",
                    $"فُوتِرت {result.ItemCount} بنود بإجمالي {result.PostedGross:N2}"));
            }

            if (result.PlanAssignmentId != null)
            {
                parts.Add(T($"schedule of {result.InstallmentCount} installment(s) generated",
                    $"وُلّد جدول من {result.InstallmentCount} أقساط"));
            }

            if (result.DiscountGrantId != null)
            {
                parts.Add(T($"discount approved, {result.DiscountApplied:N2} applied",
                    $"اعتُمد الخصم وطُبّق منه {result.DiscountApplied:N2}"));
            }

            return T("Student finances approved — ", "اعتُمدت مالية الطالب — ") + string.Join(T("; ", "؛ "), parts) + ".";
        }
    }
}
