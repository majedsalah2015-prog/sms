using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Discounts;
using Sms.Application.Security;
using Sms.Application.Workflow;
using Sms.Domain.Discounts;
using Sms.Domain.Security;
using Sms.Domain.Workflow;
using Sms.Web.Finance;
using Sms.Web.Models;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/22 §8.3 — the grant desk, reached from the child rather than from the roll.
    /// <para>
    /// The discounts panel on the student's fee file was a register: it said what stood against the
    /// child and offered no way to change it. Everything the doc calls the grant desk lived at
    /// <c>/discounts</c>, a list of every grant in the school — so a clerk sitting with a family who
    /// asked for a sibling discount had to leave the child's file, find the child again in a second
    /// search, act, and navigate back to see what it did to the balance. §8.3 describes that screen
    /// as "student/family position with gross/net preview per proposed discount", which is this
    /// page. These five actions put the desk where the position already is.
    /// </para>
    /// <para>
    /// They are the module's own operations, not new rules: the same <see cref="IDiscountAdmin"/>
    /// calls and the same WF-04 chain (<see cref="GrantChain"/>) the grant desk makes, so a grant
    /// raised here reaches the approver BR-DIS-003 routes it to, and two screens cannot end up
    /// disagreeing about whether it was decided. They carry the Discounts module's permissions
    /// rather than the Fees ones they are rendered beside — otherwise this screen would be a way to
    /// grant a discount without holding the discount right (BR-SEC-010), the mistake the fee-file
    /// basket beside it already refuses to make.
    /// </para>
    /// <para>
    /// <b>Add / edit / remove, in this product's vocabulary.</b> Adding is proposing (BR-DIS-003 —
    /// approval is a separate act, held by a separate right). Editing corrects a proposal in place
    /// and re-routes it; an approved grant is not editable, because BR-DIS-005 has already issued its
    /// discount documents and recomputed the schedule against them. Removing is not a delete — there
    /// is none in this product (BR-GLB-005): a proposal is rejected, an approved grant is revoked
    /// with an effective date and a reason (BR-DIS-008), and both stay in the register the auditor
    /// reads (BR-DIS-009).
    /// </para>
    /// </summary>
    public partial class FeesController
    {
        // ================================================================== add

        /// <summary>BR-DIS-003: propose a manual grant against this child and start its WF-04 chain.</summary>
        [HttpPost("students/{id:int}/discounts")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Grants, ActionVerb.Submit)]
        public async Task<IActionResult> GrantStudentDiscount(
            [FromServices] IDiscountAdmin discounts,
            [FromServices] IWorkflowService workflow,
            [FromServices] ICurrentUser user,
            int id, int? discountTypeId, decimal? basisValue, string? reason, bool hasHardshipDocumentation = false, int? year = null)
        {
            try
            {
                // ProposeManualGrantAsync files the grant against the working year, not the year in
                // the query string — the same reason the basket beside it refuses: a grant filed into
                // a year the operator is not looking at is worse than a refusal they can read.
                if (year != null && year != _workingYear.AcademicYearId)
                {
                    throw new InvalidOperationException(T(
                        "You are viewing a year other than the working year. Switch to the working year before granting a discount.",
                        "أنت تستعرض عاماً غير عام العمل. انتقل إلى عام العمل قبل منح خصم."));
                }

                if (discountTypeId == null)
                {
                    throw new InvalidOperationException(T("Choose a discount type.", "اختر نوع الخصم."));
                }

                if (basisValue == null || basisValue <= 0m)
                {
                    throw new InvalidOperationException(T("Enter a positive discount value.", "أدخل قيمة خصم موجبة."));
                }

                var text = RequiredGrantReason(reason);
                var grant = await discounts.ProposeManualGrantAsync(
                    id, discountTypeId.Value, basisValue.Value, text, user.UserId, hasHardshipDocumentation, HttpContext.RequestAborted);

                var missingChain = await GrantChain.StartAsync(discounts, workflow, grant, text, IsArabic, HttpContext.RequestAborted);
                if (missingChain != null)
                {
                    TempData["Error"] = missingChain;
                }
                else
                {
                    TempData["Flash"] = GrantChain.Routed(grant.RequiredTier, IsArabic);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(StudentFinanceDetail), new { id, year });
        }

        // ================================================================== edit

        /// <summary>
        /// doc/Modules/22 §8.3: correct a proposal that has not been decided — its value, its reason,
        /// its hardship attestation. The re-routing lives in the port, because the tier and the
        /// percentage it is derived from must not be computed in two places (BR-DIS-003).
        /// </summary>
        [HttpPost("students/{id:int}/discounts/{grantId:int}/edit")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Grants, ActionVerb.Submit)]
        public async Task<IActionResult> EditStudentDiscount(
            [FromServices] IDiscountAdmin discounts,
            int id, int grantId, decimal? basisValue, string? reason, bool hasHardshipDocumentation = false, int? year = null)
        {
            try
            {
                var grant = await GrantOfStudentAsync(grantId, id);
                if (basisValue == null || basisValue <= 0m)
                {
                    throw new InvalidOperationException(T("Enter a positive discount value.", "أدخل قيمة خصم موجبة."));
                }

                if (grant.Status != DiscountGrantStatus.Proposed)
                {
                    // Said here rather than left to the port's own refusal, which is worded for a desk
                    // deciding a grant and would answer an edit with "cannot be decided".
                    throw new InvalidOperationException(T(
                        "Only a discount still awaiting approval can be changed. This one has been decided, and its discount documents are already issued against the invoices — the correction is to revoke it and grant the corrected one (BR-DIS-005/008).",
                        "لا يُعدَّل إلا خصم ما زال ينتظر الاعتماد. هذا الخصم بُتّ فيه وصدرت مستندات خصمه على الفواتير — والتصحيح أن تسحبه وتمنح الخصم الصحيح بدلاً منه (BR-DIS-005/008)."));
                }

                var text = RequiredGrantReason(reason);
                await discounts.UpdateManualGrantAsync(grantId, basisValue.Value, text, hasHardshipDocumentation, HttpContext.RequestAborted);

                // Re-read: the tier the port re-derived is what the operator needs told, because
                // raising the value can move the grant to a different approver than the one it was
                // originally routed to.
                var updated = await _db.DiscountGrants.AsNoTracking().SingleAsync(g => g.Id == grantId, HttpContext.RequestAborted);
                TempData["Flash"] = IsArabic
                    ? $"عُدِّل الخصم — وهو الآن ينتظر اعتماد {DiscountLabels.Tier(updated.RequiredTier, true)}."
                    : $"Discount changed — it now waits for {DiscountLabels.Tier(updated.RequiredTier, false)} to approve it.";
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(StudentFinanceDetail), new { id, year });
        }

        // ================================================================== approve

        /// <summary>
        /// BR-DIS-005: approval is what makes a grant reduce anything, so a panel that can add one
        /// has to be able to approve it — otherwise "add a discount" leaves the balance untouched and
        /// reads as a bug. Routed through the chain when one is open, exactly as the desk does.
        /// </summary>
        [HttpPost("students/{id:int}/discounts/{grantId:int}/approve")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Grants, ActionVerb.Approve)]
        public async Task<IActionResult> ApproveStudentDiscount(
            [FromServices] IDiscountAdmin discounts,
            [FromServices] IWorkflowService workflow,
            [FromServices] ICurrentUser user,
            int id, int grantId, string? envelopeOverrideReason, int? year = null)
        {
            try
            {
                await GrantOfStudentAsync(grantId, id);
                var chain = await GrantChain.OpenAsync(_db, grantId, HttpContext.RequestAborted);
                if (chain == null)
                {
                    await discounts.ApproveGrantAsync(
                        grantId, user.UserId,
                        string.IsNullOrWhiteSpace(envelopeOverrideReason) ? null : envelopeOverrideReason.Trim(),
                        HttpContext.RequestAborted);
                    TempData["Flash"] = T("Grant approved — discount document(s) issued.", "اعتُمدت المنحة — صدرت مستندات الخصم.");
                }
                else
                {
                    var result = await workflow.ExecuteAsync(chain.Id, WorkflowActionType.Approve, cancellationToken: HttpContext.RequestAborted);
                    var to = IsArabic ? result.ToState.Name.NameAr : result.ToState.Name.NameEn;
                    TempData["Flash"] = result.ToState.IsFinal
                        ? T("Grant approved — discount document(s) issued.", "اعتُمدت المنحة — صدرت مستندات الخصم.")
                        : T($"Approved at your level — the request now waits at {to}.", $"اعتُمدت عند مستواك — والطلب الآن ينتظر عند {to}.");
                }
            }
            catch (WorkflowSelfApprovalException) { TempData["Error"] = GrantChain.SelfApproval(IsArabic); }
            catch (WorkflowActorNotAuthorizedException) { TempData["Error"] = GrantChain.NotTheApprover(IsArabic); }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(StudentFinanceDetail), new { id, year });
        }

        // ================================================================== remove — reject a proposal

        /// <summary>BR-DIS-003: refusing a proposal. The row stays in the register with its reason (BR-DIS-009).</summary>
        [HttpPost("students/{id:int}/discounts/{grantId:int}/reject")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Grants, ActionVerb.Approve)]
        public async Task<IActionResult> RejectStudentDiscount(
            [FromServices] IDiscountAdmin discounts,
            [FromServices] IWorkflowService workflow,
            [FromServices] ICurrentUser user,
            int id, int grantId, string? reason, int? year = null)
        {
            try
            {
                await GrantOfStudentAsync(grantId, id);
                if (string.IsNullOrWhiteSpace(reason))
                {
                    throw new InvalidOperationException(T("A reason is required to reject a discount.", "السبب مطلوب لرفض الخصم."));
                }

                var chain = await GrantChain.OpenAsync(_db, grantId, HttpContext.RequestAborted);
                if (chain == null)
                {
                    await discounts.RejectGrantAsync(grantId, user.UserId, reason.Trim(), HttpContext.RequestAborted);
                }
                else
                {
                    // The chain's closure effect is what sets the grant to Rejected, so the register
                    // and the open request cannot end up disagreeing.
                    await workflow.ExecuteAsync(chain.Id, WorkflowActionType.Reject, reason.Trim(), cancellationToken: HttpContext.RequestAborted);
                }

                TempData["Flash"] = T("Discount rejected.", "رُفض الخصم.");
            }
            catch (WorkflowActorNotAuthorizedException) { TempData["Error"] = GrantChain.NotTheApprover(IsArabic); }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(StudentFinanceDetail), new { id, year });
        }

        // ================================================================== remove — revoke an approved grant

        /// <summary>
        /// BR-DIS-008: withdrawing a discount that is already applied. Effective date on or after
        /// today, reason mandatory (T1), and the past is forgiven unless the operator asks for the
        /// claw-back charge.
        /// </summary>
        [HttpPost("students/{id:int}/discounts/{grantId:int}/revoke")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Grants, ActionVerb.Deactivate)]
        public async Task<IActionResult> RevokeStudentDiscount(
            [FromServices] IDiscountAdmin discounts,
            int id, int grantId, DateTime? effectiveDate, string? reason, bool clawBack = false, int? year = null)
        {
            try
            {
                await GrantOfStudentAsync(grantId, id);
                if (string.IsNullOrWhiteSpace(reason))
                {
                    throw new InvalidOperationException(T(
                        "A reason is required to revoke a discount (T1).", "السبب مطلوب لسحب الخصم (T1)."));
                }

                await discounts.RevokeGrantAsync(
                    grantId, effectiveDate ?? _clock.UtcNow.Date, reason.Trim(), clawBack, HttpContext.RequestAborted);

                TempData["Flash"] = clawBack
                    ? T("Discount revoked — a claw-back charge was posted for the forward fraction.",
                        "سُحب الخصم — رُحّلت فاتورة استرجاع عن الجزء المتبقي من العام.")
                    : T("Discount revoked — past discount documents stand (BR-DIS-008 default: forgive the past).",
                        "سُحب الخصم — مستندات الخصم السابقة تبقى كما هي (سياسة BR-DIS-008: العفو عن الماضي).");
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(StudentFinanceDetail), new { id, year });
        }

        // ================================================================== helpers

        /// <summary>
        /// The grant, proved to be this child's. Without it the grant id in the URL would be a way to
        /// decide any grant in the school from any student's page — the tenant filter answers "same
        /// school", which is a different question from "same student".
        /// </summary>
        private async Task<DiscountGrant> GrantOfStudentAsync(int grantId, int studentId)
        {
            var grant = await _db.DiscountGrants.AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == grantId && g.StudentId == studentId, HttpContext.RequestAborted);
            if (grant == null)
            {
                throw new InvalidOperationException(T(
                    "That discount does not belong to this student.", "هذا الخصم لا يخص هذا الطالب."));
            }

            return grant;
        }

        /// <summary>BR-DIS-003 makes the reason mandatory on every grant — it is the field the register is read for.</summary>
        private string RequiredGrantReason(string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new InvalidOperationException(T(
                    "A reason is mandatory on a discount grant (BR-DIS-003).", "السبب إلزامي على منح الخصم (BR-DIS-003)."));
            }

            return reason.Trim();
        }

        /// <summary>
        /// The desk's rights and its type picker, or null when the reader holds none of the three —
        /// the panel is then the register it has always been (BR-SEC-010: unauthorised surface
        /// disappears rather than rendering disabled).
        /// </summary>
        private async Task<StudentDiscountDesk?> BuildDiscountDeskAsync(StudentFinanceDetailViewModel model)
        {
            var desk = new StudentDiscountDesk
            {
                CanPropose = await _permissions.HasPermissionAsync(
                    ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Grants, ActionVerb.Submit, HttpContext.RequestAborted),
                CanDecide = await _permissions.HasPermissionAsync(
                    ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Grants, ActionVerb.Approve, HttpContext.RequestAborted),
                CanRevoke = await _permissions.HasPermissionAsync(
                    ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Grants, ActionVerb.Deactivate, HttpContext.RequestAborted),
                IsWorkingYear = model.Year != null && model.Year.Id == _workingYear.AcademicYearId,
            };

            if (!desk.CanAct)
            {
                return null;
            }

            // Manual only, and only for a child this year's roll actually holds: BR-DIS-002 grants an
            // automatic type by an eligibility run over the whole school, and handing one to a single
            // student from here would produce a grant the run does not know it made.
            if (desk.CanPropose && desk.IsWorkingYear && !model.NotEnrolled)
            {
                desk.Types = await _db.DiscountTypes.AsNoTracking()
                    .Where(t => t.EligibilityMode == DiscountEligibilityMode.Manual)
                    .OrderBy(t => t.NameEn)
                    .ToListAsync(HttpContext.RequestAborted);
            }

            return desk;
        }
    }
}
