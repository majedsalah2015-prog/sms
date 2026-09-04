using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Application.Common.Exceptions;
using Sms.Application.Discounts;
using Sms.Application.Workflow;
using Sms.Domain.Discounts;
using Sms.Domain.Workflow;
using Sms.Domain.Fees;
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
    /// doc/Modules/22 §8 — E-502 screens over IDiscountAdmin: 8.1 Grant desk
    /// (manual/automatic proposals, approve/reject/revoke), 8.2 Type catalog
    /// (policy, stacking, eligibility rules), 8.3 Scholarship board
    /// (programs + envelope + nominations), 8.4 Renewal queue (BR-DIS-007),
    /// 8.5 Waiver desk (BR-DIS-006, a register separate from pricing
    /// discounts).
    /// <para>
    /// <b>Manual grants run on the workflow engine (WF-04), not on a status
    /// field.</b> Proposing one raises a real request routed by BR-DIS-003's own
    /// percentage: at or under 10% the finance manager is the whole chain, above
    /// it the request moves to the principal, and it appears in both approvers'
    /// unified inbox (BR-WF-011). Approving here and approving from the inbox are
    /// the same call — the engine authorises the step (role, bound permission,
    /// data scope, no self-approval) and its final effect applies the grant
    /// through <see cref="IDiscountAdmin"/>, so BR-DIS-005 has one implementation
    /// and approved-but-not-applied cannot happen (BR-WF-009).
    /// </para>
    /// <para>
    /// Two paths deliberately keep the direct call: <b>automatic proposals</b>,
    /// because BR-DIS-002 decides an enumerated batch under one approval rather
    /// than one chain per child, and <b>scholarship nominations</b>, because
    /// BR-DIS-004 routes those to a committee (P5) which the seeded WF-04 does not
    /// model. Both still record their <c>ApprovalTier</c>. The doc's third tier —
    /// BR-DIS-003 sends anything over 25% to the Owner — has no chain either:
    /// doc 06 §4.3's seeded role list contains no Owner role, so the tier is
    /// recorded on the grant and the chain stops at the principal.
    /// </para>
    /// Deferred: family-level caps (no family entity), mid-year pro-ration,
    /// hardship-document attachment linkage (the checkbox is an operator
    /// attestation, not a real attachment link), sponsor billing links.
    /// </summary>
    [Route("discounts")]
    public class DiscountsController : Controller
    {
        private readonly IDiscountAdmin _discounts;
        private readonly IWorkflowService _workflow;
        private readonly AppDbContext _db;
        private readonly IWorkingYearContext _workingYear;
        private readonly ICurrentUser _user;

        public DiscountsController(
            IDiscountAdmin discounts, IWorkflowService workflow, AppDbContext db,
            IWorkingYearContext workingYear, ICurrentUser user)
        {
            _discounts = discounts;
            _workflow = workflow;
            _db = db;
            _workingYear = workingYear;
            _user = user;
        }

        /// <summary>How many grants one page of the desk shows; the header says how many matched.</summary>
        private const int PageSize = 500;

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        // ================================================================== 8.1 Grant desk

        [HttpGet("")]
        [RequirePermission(ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Grants, ActionVerb.View)]
        public async Task<IActionResult> Index(int? year = null, DiscountGrantStatus? status = null, int? typeId = null, string? q = null)
        {
            var m = new GrantDeskViewModel { Status = status, TypeId = typeId, Q = q };
            await FillPageAsync(m, year);
            if (m.Year == null) return View(m);
            var yid = m.Year.Id;

            var query = _db.DiscountGrants.AsNoTracking().Where(g => g.AcademicYearId == yid);
            if (status != null) query = query.Where(g => g.Status == status);
            if (typeId != null) query = query.Where(g => g.DiscountTypeId == typeId);

            // The search runs in the database, before the page is cut. It used to run
            // in memory over the newest 500 grants, which meant a school past its
            // five-hundredth grant could type a student's number and be told, quite
            // confidently, that they have none — the worst possible answer on a screen
            // where the next thing the operator does is grant a second discount.
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();

                // Every part of the name, not just the first and the family: a parent
                // asks about "أحمد محمد" and the register holds four name columns.
                var studentIds = await _db.Students.IgnoreQueryFilters().AsNoTracking()
                    .Where(s => s.SchoolId == _db.CurrentSchoolId
                        && (s.StudentNo.Contains(term)
                            || s.FirstNameAr.Contains(term) || s.FatherNameAr.Contains(term)
                            || s.GrandfatherNameAr.Contains(term) || s.FamilyNameAr.Contains(term)
                            || s.FirstNameEn.Contains(term) || s.FatherNameEn.Contains(term)
                            || s.GrandfatherNameEn.Contains(term) || s.FamilyNameEn.Contains(term)))
                    .Select(s => s.Id)
                    .ToListAsync();

                // IgnoreQueryFilters on the types too: a grant keeps pointing at its
                // type after the school retires it, and searching by that type's name
                // must still find the grants that carry it.
                var matchedTypeIds = await _db.DiscountTypes.IgnoreQueryFilters().AsNoTracking()
                    .Where(t => t.SchoolId == _db.CurrentSchoolId && (t.NameAr.Contains(term) || t.NameEn.Contains(term)))
                    .Select(t => t.Id)
                    .ToListAsync();

                query = query.Where(g => studentIds.Contains(g.StudentId) || matchedTypeIds.Contains(g.DiscountTypeId));
            }

            m.MatchCount = await query.CountAsync();
            var grants = await query.OrderByDescending(g => g.Id).Take(PageSize).ToListAsync();

            var shownStudentIds = grants.Select(g => g.StudentId).Distinct().ToList();
            var students = await _db.Students.IgnoreQueryFilters().AsNoTracking().Where(s => shownStudentIds.Contains(s.Id)).ToListAsync();

            // doc/Modules/22 §8.3: the desk owes a gross/net preview per proposed
            // discount. Without it the "applied" column reads 0.00 until somebody
            // approves, which an operator reasonably reads as "this discount is worth
            // nothing" — and a sibling grant sitting on the eldest child, whom the
            // ladder deliberately skips, looks exactly the same as a correct one.
            var previews = await _discounts.PreviewGrantsAsync(grants.Select(g => g.Id).ToList(), HttpContext.RequestAborted);

            m.Rows = grants.Select(g => new GrantDeskViewModel.Row(g,
                m.Types.FirstOrDefault(t => t.Id == g.DiscountTypeId) ?? new DiscountType { NameAr = "?", NameEn = "?" },
                students.FirstOrDefault(s => s.Id == g.StudentId) ?? new Student { StudentNo = "?" },
                previews.TryGetValue(g.Id, out var preview) ? preview : null)).ToList();
            m.StudentOptions = await EnrolledStudentOptionsAsync(yid);
            return View(m);
        }

        [HttpPost("grants/manual")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Grants, ActionVerb.Submit)]
        public async Task<IActionResult> ProposeManualGrant(int studentId, int discountTypeId, decimal basisValue, string reason, bool hasHardshipDocumentation, int? year)
        {
            try
            {
                if (basisValue <= 0) throw new InvalidOperationException(T("Enter a positive basis value.", "أدخل قيمة أساس موجبة."));
                if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException(T("A reason is required to propose a grant.", "السبب مطلوب لاقتراح المنحة."));
                var grant = await _discounts.ProposeManualGrantAsync(studentId, discountTypeId, basisValue, reason.Trim(), _user.UserId, hasHardshipDocumentation);
                await StartGrantChainAsync(grant, reason.Trim());
                TempData["Flash"] = RoutedMessage(grant.RequiredTier);
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Index), new { year });
        }

        [HttpPost("grants/automatic")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Grants, ActionVerb.Submit)]
        public async Task<IActionResult> ProposeAutomaticGrants(int discountTypeId, int? year)
        {
            try
            {
                var proposed = await _discounts.ProposeAutomaticGrantsAsync(discountTypeId, _user.UserId);
                TempData["Flash"] = proposed.Count == 0
                    ? T("No new eligible students found — everyone already has a grant of this type, or nobody qualifies.", "لا طلاب مؤهلون جدد — الجميع لديه منحة من هذا النوع مسبقاً، أو لا أحد مستحق.")
                    : T($"{proposed.Count} grant(s) proposed — review and approve below.", $"اقتُرح {proposed.Count} منحة — راجعها واعتمدها أدناه.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Index), new { year, typeId = discountTypeId, status = DiscountGrantStatus.Proposed });
        }

        [HttpPost("grants/{id:int}/approve")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Grants, ActionVerb.Approve)]
        public async Task<IActionResult> ApproveGrant(int id, string? envelopeOverrideReason, int? year)
        {
            try
            {
                var chain = await OpenGrantChainAsync(id);
                if (chain == null)
                {
                    // No chain: an automatic batch proposal (BR-DIS-002) or a scholarship
                    // nomination, neither of which WF-04 routes. The direct path also
                    // carries the envelope override, which has no field in a generic inbox.
                    await _discounts.ApproveGrantAsync(id, _user.UserId, Blank(envelopeOverrideReason));
                    TempData["Flash"] = T("Grant approved — discount document(s) issued.", "اعتُمدت المنحة — صدرت مستندات الخصم.");
                }
                else
                {
                    var result = await _workflow.ExecuteAsync(chain.Id, WorkflowActionType.Approve, cancellationToken: HttpContext.RequestAborted);
                    var to = IsArabic ? result.ToState.Name.NameAr : result.ToState.Name.NameEn;
                    TempData["Flash"] = result.ToState.IsFinal
                        ? T("Grant approved — discount document(s) issued.", "اعتُمدت المنحة — صدرت مستندات الخصم.")
                        : T($"Approved at your level — the request now waits at {to}.", $"اعتُمدت عند مستواك — والطلب الآن ينتظر عند {to}.");
                }
            }
            catch (WorkflowSelfApprovalException) { TempData["Error"] = SelfApprovalMessage; }
            catch (WorkflowActorNotAuthorizedException) { TempData["Error"] = NotTheApproverMessage; }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Index), new { year });
        }

        [HttpPost("grants/approve-batch")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Grants, ActionVerb.Approve)]
        public async Task<IActionResult> ApproveGrants(List<int>? ids, int? year)
        {
            try
            {
                if (ids == null || ids.Count == 0) throw new InvalidOperationException(T("Select at least one grant.", "اختر منحة واحدة على الأقل."));

                // BR-DIS-002's batch approval is for automatic proposals — one decision
                // over an enumerated set. A manual grant is not one of those: it is
                // running a WF-04 chain, and approving it here would apply a 40%
                // discount without the principal the routing sent it to, leave its
                // request open in their inbox forever, and make that request
                // un-completable (the effect would then refuse an already-approved
                // grant). So the selection is split and the chained ones are named.
                var chained = await _db.WorkflowInstances.AsNoTracking()
                    .Where(i => !i.IsClosed && i.EntityTypeName == DiscountWorkflow.EntityTypeName && ids.Contains((int)i.EntityId))
                    .Select(i => (int)i.EntityId)
                    .ToListAsync(HttpContext.RequestAborted);

                var direct = ids.Where(id => !chained.Contains(id)).ToList();
                if (direct.Count > 0)
                {
                    await _discounts.ApproveGrantsAsync(direct, _user.UserId);
                }

                TempData["Flash"] = chained.Count == 0
                    ? T($"{direct.Count} grant(s) approved.", $"اعتُمد {direct.Count} منحة.")
                    : T($"{direct.Count} grant(s) approved. {chained.Count} of them are running an approval chain and were left alone — decide those one at a time, so each goes to the approver its percentage routes it to (BR-DIS-003).",
                        $"اعتُمد {direct.Count} منحة. و{chained.Count} منها تسير في سلسلة اعتماد فتُركت — ابتّ فيها واحدة واحدة ليصل كلٌّ منها إلى المعتمِد الذي توجّهه إليه نسبته (BR-DIS-003).");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Index), new { year });
        }

        [HttpPost("grants/{id:int}/reject")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Grants, ActionVerb.Approve)]
        public async Task<IActionResult> RejectGrant(int id, string? reason, int? year)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException(T("A reason is required to reject a grant.", "السبب مطلوب لرفض المنحة."));

                var chain = await OpenGrantChainAsync(id);
                if (chain == null)
                {
                    await _discounts.RejectGrantAsync(id, _user.UserId, reason.Trim());
                }
                else
                {
                    // The closure effect is what sets the grant to Rejected, so the
                    // register and the request cannot end up disagreeing.
                    await _workflow.ExecuteAsync(chain.Id, WorkflowActionType.Reject, reason.Trim(), cancellationToken: HttpContext.RequestAborted);
                }

                TempData["Flash"] = T("Grant rejected.", "رُفضت المنحة.");
            }
            catch (WorkflowActorNotAuthorizedException) { TempData["Error"] = NotTheApproverMessage; }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Index), new { year });
        }

        [HttpPost("grants/{id:int}/revoke")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Grants, ActionVerb.Deactivate)]
        public async Task<IActionResult> RevokeGrant(int id, DateTime effectiveDate, string? reason, bool clawBack, int? year)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException(T("A reason is required to revoke a grant (T1).", "السبب مطلوب لإلغاء المنحة (T1)."));
                await _discounts.RevokeGrantAsync(id, effectiveDate, reason.Trim(), clawBack);
                TempData["Flash"] = clawBack
                    ? T("Grant revoked — a claw-back charge was posted for the forward fraction.", "أُلغيت المنحة — رُحّلت فاتورة استرجاع عن الجزء المتبقي من العام.")
                    : T("Grant revoked — past discount documents stand (BR-DIS-008 default: forgive the past).", "أُلغيت المنحة — مستندات الخصم السابقة تبقى كما هي (سياسة BR-DIS-008: العفو عن الماضي).");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Index), new { year });
        }

        // ================================================================== 8.2 Type catalog

        [HttpGet("types")]
        [RequirePermission(ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Types, ActionVerb.View)]
        public async Task<IActionResult> Types(int? year = null, int? edit = null)
        {
            var m = new TypeCatalogViewModel { EditId = edit };
            await FillPageAsync(m, year);
            // The rules come with the row: the edit form has to show the ladder the type was
            // saved with, and a second round-trip per row to fetch them would be a query per type.
            var all = await _db.DiscountTypes.IgnoreQueryFilters().AsNoTracking().Include(t => t.EligibilityRules)
                .Where(t => t.SchoolId == _db.CurrentSchoolId).OrderByDescending(t => t.IsActive).ThenBy(t => t.NameEn).ToListAsync();
            var grantCounts = await _db.DiscountGrants.AsNoTracking().GroupBy(g => g.DiscountTypeId).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
            m.Rows = all.Select(t => new TypeCatalogViewModel.Row(t, m.Categories.FirstOrDefault(c => c.Id == t.FeeCategoryId), grantCounts.FirstOrDefault(x => x.Key == t.Id)?.N ?? 0)).ToList();
            return View(m);
        }

        [HttpPost("types/new")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Types, ActionVerb.Create)]
        public async Task<IActionResult> CreateType(
            string nameAr, string nameEn, DiscountBasis basis, DiscountEligibilityMode eligibilityMode,
            int? feeCategoryId, DiscountComputationStage stage, decimal? capAmountPerStudent,
            bool isStackable, decimal maxCombinedPercent, DiscountRenewalMode renewalMode, bool requiresHardshipDocumentation,
            decimal? ladder2, decimal? ladder3, decimal? ladder4Plus, decimal? staffPercent)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nameAr) || string.IsNullOrWhiteSpace(nameEn)) throw new InvalidOperationException(T("Both names are required.", "الاسمان مطلوبان."));
                await _discounts.DefineTypeAsync(nameAr.Trim(), nameEn.Trim(), basis, eligibilityMode, feeCategoryId, stage, capAmountPerStudent,
                    isStackable, maxCombinedPercent <= 0 ? 100m : maxCombinedPercent, renewalMode, requiresHardshipDocumentation, LadderRules(ladder2, ladder3, ladder4Plus, staffPercent));
                TempData["Flash"] = T("Discount type added.", "أُضيف نوع الخصم.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Types));
        }

        /// <summary>
        /// doc/Modules/22 §8.2. Corrects a type in place. The catalog could only append, so a
        /// misspelled name or a stacking cap entered wrong could never be fixed — only shadowed
        /// by a second type, leaving the school's own list carrying both.
        /// </summary>
        [HttpPost("types/{id:int}")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Types, ActionVerb.Edit)]
        public async Task<IActionResult> EditType(
            int id, string nameAr, string nameEn, DiscountBasis basis, DiscountEligibilityMode eligibilityMode,
            int? feeCategoryId, DiscountComputationStage stage, decimal? capAmountPerStudent,
            bool isStackable, decimal maxCombinedPercent, DiscountRenewalMode renewalMode, bool requiresHardshipDocumentation,
            decimal? ladder2, decimal? ladder3, decimal? ladder4Plus, decimal? staffPercent)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nameAr) || string.IsNullOrWhiteSpace(nameEn)) throw new InvalidOperationException(T("Both names are required.", "الاسمان مطلوبان."));
                await _discounts.UpdateTypeAsync(id, nameAr.Trim(), nameEn.Trim(), basis, eligibilityMode, feeCategoryId, stage, capAmountPerStudent,
                    isStackable, maxCombinedPercent <= 0 ? 100m : maxCombinedPercent, renewalMode, requiresHardshipDocumentation, LadderRules(ladder2, ladder3, ladder4Plus, staffPercent));
                TempData["Flash"] = T("Discount type updated.", "حُدِّث نوع الخصم.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Types));
        }

        /// <summary>
        /// BR-GLB-005: retires a type. Grants already carrying it keep it — the type simply stops
        /// being offered to new ones, which is why this is reversible and not a delete.
        /// </summary>
        [HttpPost("types/{id:int}/deactivate")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Types, ActionVerb.Deactivate)]
        public async Task<IActionResult> DeactivateType(int id)
        {
            try
            {
                await _discounts.SetTypeActiveAsync(id, false);
                TempData["Flash"] = T("Discount type retired — existing grants keep it (BR-GLB-005: never deleted).", "أُوقف نوع الخصم — والمنح القائمة تحتفظ به (BR-GLB-005: لا حذف).");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Types));
        }

        /// <summary>doc/Modules/22 §8.2: puts a retired type back in the grant desk's picker.</summary>
        [HttpPost("types/{id:int}/activate")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Types, ActionVerb.Edit)]
        public async Task<IActionResult> ActivateType(int id)
        {
            try
            {
                await _discounts.SetTypeActiveAsync(id, true);
                TempData["Flash"] = T("Discount type reactivated.", "أُعيد تفعيل نوع الخصم.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Types));
        }

        /// <summary>
        /// BR-DIS-002's ladder, read off the form. Shared by create and edit so the two can never
        /// disagree about what "4th child and up" means.
        /// </summary>
        private static List<EligibilityRuleInput> LadderRules(decimal? ladder2, decimal? ladder3, decimal? ladder4Plus, decimal? staffPercent)
        {
            var rules = new List<EligibilityRuleInput>();
            if (ladder2 is > 0) rules.Add(new EligibilityRuleInput(EligibilityRuleKind.SiblingLadder, ladder2.Value, 2));
            if (ladder3 is > 0) rules.Add(new EligibilityRuleInput(EligibilityRuleKind.SiblingLadder, ladder3.Value, 3));
            if (ladder4Plus is > 0) rules.Add(new EligibilityRuleInput(EligibilityRuleKind.SiblingLadder, ladder4Plus.Value, 4));
            if (staffPercent is > 0) rules.Add(new EligibilityRuleInput(EligibilityRuleKind.Staff, staffPercent.Value));
            return rules;
        }

        // ================================================================== 8.3 Scholarship board

        [HttpGet("scholarships")]
        [RequirePermission(ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Scholarships, ActionVerb.View)]
        public async Task<IActionResult> Scholarships(int? year = null)
        {
            var m = new ScholarshipBoardViewModel();
            await FillPageAsync(m, year);
            if (m.Year == null) return View(m);
            var yid = m.Year.Id;

            var programs = await _db.ScholarshipPrograms.IgnoreQueryFilters().AsNoTracking().Where(p => p.SchoolId == _db.CurrentSchoolId && p.AcademicYearId == yid).OrderByDescending(p => p.IsActive).ThenBy(p => p.NameEn).ToListAsync();
            var programIds = programs.Select(p => p.Id).ToList();
            var grants = await _db.DiscountGrants.AsNoTracking().Where(g => g.ScholarshipProgramId != null && programIds.Contains(g.ScholarshipProgramId.Value)).ToListAsync();
            m.Rows = programs.Select(p =>
            {
                var pg = grants.Where(g => g.ScholarshipProgramId == p.Id).ToList();
                var approved = pg.Where(g => g.Status == DiscountGrantStatus.Approved).ToList();
                return new ScholarshipBoardViewModel.Row(p, m.Types.FirstOrDefault(t => t.Id == p.DiscountTypeId) ?? new DiscountType { NameAr = "?", NameEn = "?" },
                    approved.Count, approved.Sum(g => g.AppliedAmount), pg.Count(g => g.Status == DiscountGrantStatus.Proposed));
            }).ToList();
            m.StudentOptions = await EnrolledStudentOptionsAsync(yid);
            return View(m);
        }

        [HttpPost("scholarships/new")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Scholarships, ActionVerb.Create)]
        public async Task<IActionResult> CreateScholarshipProgram(string nameAr, string nameEn, int discountTypeId, int? maxAwards, decimal? maxTotalAmount, int? year)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nameAr) || string.IsNullOrWhiteSpace(nameEn)) throw new InvalidOperationException(T("Both names are required.", "الاسمان مطلوبان."));
                await _discounts.DefineScholarshipProgramAsync(nameAr.Trim(), nameEn.Trim(), discountTypeId, maxAwards, maxTotalAmount);
                TempData["Flash"] = T("Scholarship program defined.", "عُرّف برنامج المنحة الدراسية.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Scholarships), new { year });
        }

        [HttpPost("scholarships/nominate")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Scholarships, ActionVerb.Submit)]
        public async Task<IActionResult> Nominate(int studentId, int scholarshipProgramId, decimal basisValue, string reason, string? sponsorNote, int? year)
        {
            try
            {
                if (basisValue <= 0) throw new InvalidOperationException(T("Enter a positive basis value.", "أدخل قيمة أساس موجبة."));
                if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException(T("A reason is required to nominate a student.", "السبب مطلوب لترشيح طالب."));
                await _discounts.NominateForScholarshipAsync(studentId, scholarshipProgramId, basisValue, reason.Trim(), _user.UserId, Blank(sponsorNote));
                TempData["Flash"] = T("Nomination recorded — routed to the committee.", "سُجّل الترشيح — وُجّه إلى اللجنة.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Scholarships), new { year });
        }

        // ================================================================== 8.4 Renewal queue

        [HttpGet("renewals")]
        [RequirePermission(ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Renewals, ActionVerb.View)]
        public async Task<IActionResult> Renewals(int? fromYear = null, int? toYear = null)
        {
            var years = await _db.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartDate).ToListAsync();
            var m = new RenewalQueueViewModel { Years = years, FromYearId = fromYear, ToYearId = toYear };
            if (toYear == null) return View(m);

            var items = await _db.RenewalQueueItems.AsNoTracking().Where(i => i.NewAcademicYearId == toYear).OrderBy(i => i.Decision).ThenByDescending(i => i.Id).ToListAsync();
            var grantIds = items.Select(i => i.PriorGrantId).Distinct().ToList();
            var grants = await _db.DiscountGrants.AsNoTracking().Where(g => grantIds.Contains(g.Id)).ToListAsync();
            var typeIds = grants.Select(g => g.DiscountTypeId).Distinct().ToList();
            var types = await _db.DiscountTypes.IgnoreQueryFilters().AsNoTracking().Where(t => typeIds.Contains(t.Id)).ToListAsync();
            var studentIds = grants.Select(g => g.StudentId).Distinct().ToList();
            var students = await _db.Students.IgnoreQueryFilters().AsNoTracking().Where(s => studentIds.Contains(s.Id)).ToListAsync();

            m.Rows = items.Select(i =>
            {
                var g = grants.First(x => x.Id == i.PriorGrantId);
                return new RenewalQueueViewModel.Row(i, g, types.FirstOrDefault(t => t.Id == g.DiscountTypeId) ?? new DiscountType { NameAr = "?", NameEn = "?" },
                    students.FirstOrDefault(s => s.Id == g.StudentId) ?? new Student { StudentNo = "?" });
            }).ToList();
            return View(m);
        }

        [HttpPost("renewals/build")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Renewals, ActionVerb.Create)]
        public async Task<IActionResult> BuildRenewalQueue(int fromYear, int toYear)
        {
            try
            {
                if (fromYear == toYear) throw new InvalidOperationException(T("The closing and target years must differ.", "يجب أن يختلف العام المنتهي عن العام الهدف."));
                var items = await _discounts.BuildRenewalQueueAsync(fromYear, toYear);
                TempData["Flash"] = items.Count == 0
                    ? T("Nothing to queue — no eligible manual/scholarship grants, or everything is already queued.", "لا شيء ليُدرج — لا منح يدوية/دراسية مؤهلة، أو أُدرج كل شيء مسبقاً.")
                    : T($"{items.Count} grant(s) queued for renewal review.", $"أُدرج {items.Count} منحة لمراجعة التجديد.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Renewals), new { fromYear, toYear });
        }

        [HttpPost("renewals/{id:int}/decide")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Renewals, ActionVerb.Approve)]
        public async Task<IActionResult> DecideRenewal(int id, RenewalDecision decision, decimal? adjustedBasisValue, int? fromYear, int? toYear)
        {
            try
            {
                await _discounts.DecideRenewalAsync(id, decision, _user.UserId, decision == RenewalDecision.Adjusted ? adjustedBasisValue : null);
                TempData["Flash"] = T($"Renewal item {DiscountLabels.RenewalDecisionLabel(decision, false).ToLowerInvariant()}.", $"عولجت مادة التجديد ({DiscountLabels.RenewalDecisionLabel(decision, true)}).");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Renewals), new { fromYear, toYear });
        }

        // ================================================================== 8.5 Waiver desk

        [HttpGet("waivers")]
        [RequirePermission(ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Waivers, ActionVerb.View)]
        public async Task<IActionResult> Waivers(string? chargeQ = null, WaiverStatus? status = null)
        {
            var m = new WaiverDeskViewModel { ChargeQ = chargeQ, Status = status };

            if (!string.IsNullOrWhiteSpace(chargeQ))
            {
                var term = chargeQ.Trim();
                var studentIds = await _db.Students.IgnoreQueryFilters().AsNoTracking()
                    .Where(s => s.SchoolId == _db.CurrentSchoolId && (s.StudentNo.Contains(term) || s.FirstNameAr.Contains(term) || s.FamilyNameAr.Contains(term) || s.FirstNameEn.Contains(term) || s.FamilyNameEn.Contains(term)))
                    .Select(s => s.Id).ToListAsync();
                var charges = await _db.Charges.AsNoTracking().Where(c => c.Status == ChargeStatus.Posted && (c.ChargeNo.Contains(term) || studentIds.Contains(c.StudentId))).OrderByDescending(c => c.Id).Take(50).ToListAsync();
                var rows = await Sms.Web.Finance.FinanceQueries.RowsAsync(_db, charges, openOnly: true);
                m.Matches = rows.Select(r => new WaiverDeskViewModel.ChargeMatch(r.Charge, r.Category, r.Student, r.Remaining)).ToList();
            }

            var wq = _db.Waivers.AsNoTracking().AsQueryable();
            if (status != null) wq = wq.Where(w => w.Status == status);
            var waivers = await wq.OrderByDescending(w => w.Id).Take(300).ToListAsync();
            var wChargeIds = waivers.Select(w => w.ChargeId).Distinct().ToList();
            var wCharges = await _db.Charges.AsNoTracking().Where(c => wChargeIds.Contains(c.Id)).ToListAsync();
            var wStudentIds = wCharges.Select(c => c.StudentId).Distinct().ToList();
            var wStudents = await _db.Students.IgnoreQueryFilters().AsNoTracking().Where(s => wStudentIds.Contains(s.Id)).ToListAsync();
            m.Rows = waivers.Select(w =>
            {
                var c = wCharges.FirstOrDefault(x => x.Id == w.ChargeId) ?? new Charge { ChargeNo = "?" };
                return new WaiverDeskViewModel.Row(w, c, wStudents.FirstOrDefault(s => s.Id == c.StudentId) ?? new Student { StudentNo = "?" });
            }).ToList();
            return View(m);
        }

        [HttpPost("waivers/new")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Waivers, ActionVerb.Submit)]
        public async Task<IActionResult> ProposeWaiver(int chargeId, WaiverKind kind, decimal amount, string reason, string? chargeQ)
        {
            try
            {
                if (amount <= 0) throw new InvalidOperationException(T("Enter a positive amount.", "أدخل مبلغاً موجباً."));
                if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException(T("A reason is required to propose a waiver.", "السبب مطلوب لاقتراح إعفاء."));
                var waiver = await _discounts.ProposeWaiverAsync(chargeId, kind, amount, reason.Trim(), _user.UserId);
                TempData["Flash"] = T($"Waiver proposed — routed to {DiscountLabels.Tier(waiver.RequiredTier, false)}.", $"اقتُرح الإعفاء — وُجّه إلى {DiscountLabels.Tier(waiver.RequiredTier, true)}.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Waivers), new { chargeQ });
        }

        [HttpPost("waivers/{id:int}/decide")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Waivers, ActionVerb.Approve)]
        public async Task<IActionResult> DecideWaiver(int id, bool approve)
        {
            try
            {
                await _discounts.DecideWaiverAsync(id, approve, _user.UserId);
                TempData["Flash"] = approve
                    ? T("Waiver approved — a credit note was issued against the charge.", "اعتُمد الإعفاء — صدر إشعار دائن على الفاتورة.")
                    : T("Waiver rejected.", "رُفض الإعفاء.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Waivers));
        }

        // ================================================================== helpers

        private async Task FillPageAsync(DiscountsPageViewModel m, int? yearId)
        {
            var years = await _db.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartDate).ToListAsync();
            m.Years = years;
            m.Year = years.FirstOrDefault(y => y.Id == (yearId ?? _workingYear.AcademicYearId)) ?? years.FirstOrDefault(y => y.Status == Sms.Domain.Schools.AcademicYearStatus.Active) ?? years.FirstOrDefault();
            // The eligibility rules ride along: the grant desk names each automatic
            // type's ladder rungs before anyone evaluates it, so the operator can see
            // that "sibling" means the second child onward and not every child.
            m.Types = await _db.DiscountTypes.IgnoreQueryFilters().AsNoTracking().Include(t => t.EligibilityRules)
                .Where(t => t.SchoolId == _db.CurrentSchoolId && t.IsActive).OrderBy(t => t.NameEn).ToListAsync();
            m.Categories = await _db.FeeCategories.AsNoTracking().OrderBy(c => c.NameEn).ToListAsync();
        }

        private async Task<IReadOnlyList<StudentOption>> EnrolledStudentOptionsAsync(int yearId)
        {
            var studentIds = await _db.Enrollments.AsNoTracking().Where(e => e.AcademicYearId == yearId && e.Status == EnrollmentStatus.Active).Select(e => e.StudentId).Distinct().ToListAsync();
            var students = await _db.Students.AsNoTracking().Where(s => studentIds.Contains(s.Id)).OrderBy(s => s.StudentNo).ToListAsync();
            return students.Select(s => new StudentOption(s)).ToList();
        }

        private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        // ================================================================== WF-04 (doc 05 §5, BR-DIS-003)

        /// <summary>
        /// The four sentences and the two chain steps live in <see cref="GrantChain"/>, shared with
        /// the student fee file's own grant panel — the same request must reach the same approver
        /// whichever screen raised it (doc/Modules/22 §8.3).
        /// </summary>
        private static string RoutedMessage(ApprovalTier tier) => GrantChain.Routed(tier, IsArabic);

        private static string SelfApprovalMessage => GrantChain.SelfApproval(IsArabic);

        private static string NotTheApproverMessage => GrantChain.NotTheApprover(IsArabic);

        private async Task<WorkflowInstance?> OpenGrantChainAsync(int grantId)
            => await GrantChain.OpenAsync(_db, grantId, HttpContext.RequestAborted);

        private async Task StartGrantChainAsync(DiscountGrant grant, string reason)
        {
            var missing = await GrantChain.StartAsync(_discounts, _workflow, grant, reason, IsArabic, HttpContext.RequestAborted);
            if (missing != null)
            {
                TempData["Error"] = missing;
            }
        }
    }
}
