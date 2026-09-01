using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Installments;
using Sms.Application.Security;
using Sms.Domain.Installments;
using Sms.Domain.Security;
using Sms.Web.Models;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/20 §8.2 exception assignment, reached from the child's own file: change the
    /// template a standing plan is on and let BR-INS-003's controlled recomputation re-date the
    /// unpaid remainder (owner request, 2026-09-01).
    /// <para>
    /// Until now the student file could only choose a template for a child who had none — a plan
    /// once assigned was a dead end here, and the screen said so ("reschedule it from the
    /// instalment plans screen"). That screen cannot change a template either: its wizard takes
    /// hand-typed dates and amounts, so a school that simply wanted a family moved from nine
    /// instalments to two had to retype the two instalments and could never say which plan the
    /// family was on afterwards. This makes the template the thing that is chosen and lets the
    /// engine derive the dates, which is what a template is for.
    /// </para>
    /// <para>
    /// <b>The remainder is the only thing that moves.</b> BR-INS-003 routes a plan change through
    /// BR-INS-005, so what has been collected stays collected: a wholly unpaid instalment is
    /// superseded and kept in history, a partly paid one is trimmed to what was actually received,
    /// and the new template's percentages are applied to what is still owed. The charges the old
    /// instalments claimed are handed to the new ones — the family owes the same money on
    /// different dates, not different money.
    /// </para>
    /// <para>
    /// <b>Two permissions, deliberately.</b> BR-INS-002 gates a different template for one family
    /// on the Finance Manager (Installments/Assignment/Create) and BR-INS-005 puts the
    /// recomputation behind that role's approval (Installments/Cases/Approve). Doing both in one
    /// click is only defensible for someone who holds both, exactly as the discount half of this
    /// screen demands Submit and Approve together. A cashier who may propose a reschedule still
    /// proposes it in the wizard.
    /// </para>
    /// <para>
    /// <b>Deviation from BR-INS-005:</b> that rule writes the change as a proposal decided later
    /// by a second person. Collapsing the two into one act is the owner's request and matches the
    /// authority the rule names, but it does mean no <c>RescheduleCase</c> row exists for a change
    /// made here — the audit trail is the T1 <c>PlanAssignment</c> entry plus the
    /// <c>ScheduleRevision</c> before/after snapshot carrying the reason. The P4 half of the rule
    /// is <b>not</b> collapsed: a change that would extend the last due date beyond the allowed
    /// window or past year-end is refused here and sent to the wizard, because a one-click change
    /// carries no Principal.
    /// </para>
    /// </summary>
    public partial class FeesController
    {
        [HttpPost("students/{id:int}/plan/{planAssignmentId:int}/template")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Assignment, ActionVerb.Create)]
        public async Task<IActionResult> ChangeStudentPlanTemplate(
            [FromServices] IInstallmentAdmin installments,
            int id,
            int planAssignmentId,
            int? planTemplateId,
            string? reason,
            int? year = null)
        {
            try
            {
                if (!await CanChangePlanTemplateAsync())
                {
                    throw new InvalidOperationException(T(
                        "You may not change the plan a family is on — that reshapes a live schedule, which needs the reschedule-approval right as well (BR-INS-005).",
                        "لا تملك صلاحية تغيير خطة الأسرة — فذلك يعيد تشكيل جدول قائم، ويستلزم أيضاً صلاحية اعتماد إعادة الجدولة (BR-INS-005)."));
                }

                // The assignment id comes off a form and the route only names the student. Without
                // this the same permission would reshape any schedule in the school by editing one
                // number — it says the holder may change a plan, not that they may change this one.
                var assignment = await _db.PlanAssignments.AsNoTracking()
                    .SingleOrDefaultAsync(a => a.Id == planAssignmentId && a.StudentId == id, HttpContext.RequestAborted);
                if (assignment == null)
                {
                    throw new InvalidOperationException(T(
                        "That plan does not belong to this student.", "هذه الخطة لا تخص هذا الطالب."));
                }

                if (planTemplateId == null)
                {
                    throw new InvalidOperationException(T(
                        "Choose the template the plan should move to.", "اختر القالب الذي تنتقل إليه الخطة."));
                }

                if (string.IsNullOrWhiteSpace(reason))
                {
                    throw new InvalidOperationException(T(
                        "A reason is mandatory — moving a family onto a different plan is a per-family exception and is reported as one (BR-INS-002).",
                        "السبب إلزامي — نقل الأسرة إلى خطة أخرى استثناء خاص بها ويُدرَج في التقارير كذلك (BR-INS-002)."));
                }

                var changed = await installments.ChangePlanTemplateAsync(
                    planAssignmentId, planTemplateId.Value, reason.Trim(),
                    await FeeFileWeekendDaysAsync(assignment.AcademicYearId),
                    cancellationToken: HttpContext.RequestAborted);

                var schedule = await installments.GetScheduleAsync(changed.Id, HttpContext.RequestAborted);
                var live = schedule.Where(i => i.Status is not (InstallmentStatus.Rescheduled or InstallmentStatus.WrittenOff)).ToList();
                var template = await _db.PlanTemplates.IgnoreQueryFilters().AsNoTracking()
                    .SingleAsync(t => t.Id == changed.PlanTemplateId, HttpContext.RequestAborted);

                TempData["Flash"] = T(
                    $"Plan moved to \"{template.NameEn}\" — the unpaid remainder is now spread over {live.Count} instalment(s), the last falling {live.Max(i => i.DueDate):yyyy-MM-dd}. What was already collected is untouched.",
                    $"نُقلت الخطة إلى «{template.NameAr}» — وُزّع المتبقي غير المسدَّد على {live.Count} أقساط، آخرها في {live.Max(i => i.DueDate):yyyy-MM-dd}. ولم يُمَسّ ما سُدِّد.");
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(StudentFinanceDetail), new { id, year });
        }

        // ================================================================== the panel behind the GET

        /// <summary>
        /// BR-INS-002 + BR-INS-005 together. Checked once per request and reused by the panel
        /// builder and the POST, so what the screen offers and what the action accepts cannot
        /// disagree.
        /// </summary>
        private async Task<bool> CanChangePlanTemplateAsync()
            => await _permissions.HasPermissionAsync(
                   ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Assignment, ActionVerb.Create, HttpContext.RequestAborted)
               && await _permissions.HasPermissionAsync(
                   ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Cases, ActionVerb.Approve, HttpContext.RequestAborted);

        /// <summary>
        /// One picker per standing plan. Built from the same
        /// <see cref="IInstallmentAdmin.GetScheduleAsync"/> the change itself reads, so the
        /// remainder printed beside the picker is the figure that will actually be re-dated
        /// rather than a second opinion computed from the coverage the panels above show.
        /// </summary>
        private async Task<IReadOnlyList<PlanTemplateChangePanel>> BuildPlanChangePanelsAsync(
            IInstallmentAdmin installments, StudentFinanceDetailViewModel model)
        {
            if (model.Plans.Count == 0 || !await CanChangePlanTemplateAsync())
            {
                return Array.Empty<PlanTemplateChangePanel>();
            }

            // Approved templates of the plans' year. Read once for every plan on the file: a
            // student with tuition and transport plans would otherwise query the catalogue twice
            // for the same rows.
            var yearId = model.Plans[0].Assignment.AcademicYearId;
            var approved = await _db.PlanTemplates.AsNoTracking()
                .Include(t => t.Installments)
                .Where(t => t.AcademicYearId == yearId && t.Status == PlanTemplateStatus.Approved)
                .OrderBy(t => t.NameEn)
                .ToListAsync(HttpContext.RequestAborted);

            var panels = new List<PlanTemplateChangePanel>();
            foreach (var plan in model.Plans)
            {
                var schedule = await installments.GetScheduleAsync(plan.Assignment.Id, HttpContext.RequestAborted);
                var unpaid = schedule
                    .Where(i => i.Status is not (InstallmentStatus.Rescheduled or InstallmentStatus.WrittenOff) && i.Paid < i.Amount)
                    .ToList();

                panels.Add(new PlanTemplateChangePanel
                {
                    PlanAssignmentId = plan.Assignment.Id,
                    Current = plan.Template,
                    Remainder = unpaid.Sum(i => i.Amount - i.Paid),

                    // A template scoped to another fee category would date charges this plan does
                    // not hold; one scoped to none applies to any group (BR-INS-002).
                    Options = approved
                        .Where(t => t.Id != plan.Assignment.PlanTemplateId)
                        .Where(t => t.FeeCategoryId == null || t.FeeCategoryId == plan.Assignment.FeeCategoryId)
                        .Select(t => new PlanTemplateChangePanel.Option(t, t.Installments.Count))
                        .ToList(),
                });
            }

            return panels;
        }
    }
}
