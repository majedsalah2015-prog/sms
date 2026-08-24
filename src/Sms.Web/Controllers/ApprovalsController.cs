using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Security;
using Sms.Application.Workflow;
using Sms.Domain.Workflow;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc 05 §7 — the workflow framework's own user-facing components, which
    /// belong to no module because an approver works one queue across all of them:
    /// the "My Approvals" inbox (BR-WF-011, P-INBOX), the submitter's request
    /// tracker, the history panel on a single instance, and a read-only view of the
    /// catalogue the school has.
    /// <para>
    /// <b>Why these actions declare no screen permission.</b> This is a personal
    /// surface, like the portal home or the password screen: it shows a person the
    /// work that has been routed to <em>them</em> and nothing else. Every row comes
    /// from <see cref="IApprovalInboxQuery"/>, which selects only instances whose
    /// current state has a transition one of the user's own roles may take; every
    /// action is authorised again by <see cref="WorkflowEngine"/> against the
    /// transition's required role, its bound permission and the record's scope
    /// (BR-WF-004), with self-approval blocked (BR-WF-003). A screen-level gate on
    /// top of that could only do harm — it would hide an inbox from somebody who has
    /// approvals waiting in it.
    /// </para>
    /// <para>
    /// <b>What is deliberately not here.</b> doc §7's fourth component — the
    /// definition designer with states, transitions, thresholds, SLAs and
    /// simulation — is not built; <see cref="Catalog"/> shows what is seeded and
    /// nothing edits it, because a school editing a live state machine needs the
    /// versioning path of BR-WF-008 behind the screen, not a form. SLA badges are
    /// absent for a plainer reason: no SLA is modelled anywhere in the schema, and
    /// doc §11 Q3 still lists the default values as unproposed. Age in days is shown
    /// instead, which is the honest part of the same idea.
    /// </para>
    /// <para>
    /// <b>What actually reaches this queue.</b> Seeding the catalogue
    /// (WF-01..WF-15) makes the definitions exist; a module reaches the queue only
    /// once it raises its request through the engine instead of setting a status.
    /// M22's manual discount grants (WF-04) are the first that do, so a discount
    /// over 10% genuinely waits here for the principal. The rest still decide on
    /// their own screens, and each migration is a change to committed behaviour that
    /// lands module by module — until then this screen says why it is empty rather
    /// than showing a blank page.
    /// </para>
    /// </summary>
    [Route("approvals")]
    public class ApprovalsController : Controller
    {
        private readonly IApprovalInboxQuery _inbox;
        private readonly IWorkflowService _workflow;
        private readonly IUserAccountDirectory _users;
        private readonly AppDbContext _db;
        private readonly ICurrentUser _user;
        private readonly IClock _clock;

        public ApprovalsController(
            IApprovalInboxQuery inbox, IWorkflowService workflow, IUserAccountDirectory users,
            AppDbContext db, ICurrentUser user, IClock clock)
        {
            _inbox = inbox;
            _workflow = workflow;
            _users = users;
            _db = db;
            _user = user;
            _clock = clock;
        }

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        private CancellationToken Ct => HttpContext.RequestAborted;

        // ------------------------------------------------------------------ the inbox

        [HttpGet("")]
        [NoPermissionRequired("A personal work queue: it lists only what this user's own roles may act on, and every action is authorised again by the workflow engine.")]
        public async Task<IActionResult> Index()
        {
            var pending = await _inbox.GetPendingForAsync(_user.UserId, Ct);
            var m = new ApprovalInboxViewModel
            {
                CatalogueCount = await _db.WorkflowDefinitions.CountAsync(d => d.IsActive, Ct),
                OpenInstanceCount = await _db.WorkflowInstances.CountAsync(i => !i.IsClosed, Ct),
            };

            if (pending.Count == 0)
            {
                return View(m);
            }

            var instanceIds = pending.Select(p => p.InstanceId).ToList();
            var instances = await _db.WorkflowInstances.AsNoTracking()
                .Where(i => instanceIds.Contains(i.Id)).ToListAsync(Ct);
            var definitionIds = instances.Select(i => i.WorkflowDefinitionId).Distinct().ToList();

            var definitions = await _db.WorkflowDefinitions.AsNoTracking()
                .Where(d => definitionIds.Contains(d.Id)).ToListAsync(Ct);
            var transitions = await _db.WorkflowTransitions.AsNoTracking()
                .Where(t => definitionIds.Contains(t.WorkflowDefinitionId)).ToListAsync(Ct);
            var states = await _db.WorkflowStates.AsNoTracking()
                .Where(s => definitionIds.Contains(s.WorkflowDefinitionId)).ToListAsync(Ct);

            var roleIds = (await _db.RoleAssignments.AsNoTracking()
                .Where(a => a.UserAccountId == _user.UserId).Select(a => a.RoleId).ToListAsync(Ct)).ToHashSet();

            var names = await NamesOfAsync(pending.Select(p => p.SubmittedByUserId));
            var now = _clock.UtcNow;

            var rows = new List<ApprovalInboxViewModel.Row>();
            foreach (var item in pending.OrderBy(p => p.PendingSinceUtc))
            {
                var instance = instances.SingleOrDefault(i => i.Id == item.InstanceId);
                if (instance == null)
                {
                    continue;
                }

                var definition = definitions.Single(d => d.Id == instance.WorkflowDefinitionId);
                var mine = transitions
                    .Where(t => t.WorkflowDefinitionId == definition.Id
                                && t.FromStateId == instance.CurrentStateId
                                && t.RequiredRoleId != null
                                && roleIds.Contains(t.RequiredRoleId.Value))
                    .ToList();

                var approve = mine.FirstOrDefault(t => t.Action == WorkflowActionType.Approve);
                rows.Add(new ApprovalInboxViewModel.Row(
                    instance.Id,
                    definition.Code,
                    IsArabic ? definition.Name.NameAr : definition.Name.NameEn,
                    EntityLabel(instance),
                    StateName(states, instance.CurrentStateId),
                    item.SubmittedByUserId is int by && names.TryGetValue(by, out var name) ? name : null,
                    item.PendingSinceUtc,
                    Math.Max(0, (int)(now.Date - item.PendingSinceUtc.Date).TotalDays),
                    approve != null,
                    mine.Any(t => t.Action == WorkflowActionType.Reject),
                    mine.Any(t => t.Action == WorkflowActionType.Return),
                    approve is { ReasonPolicy: not ReasonPolicy.Optional },
                    instance.SubmittedByUserId == _user.UserId));
            }

            m.Items = rows;
            return View(m);
        }

        /// <summary>
        /// Approve, reject or return one item. Every refusal the engine can raise is
        /// translated here — an approver must never be shown the engine's English.
        /// </summary>
        [HttpPost("{id:int}/act")]
        [ValidateAntiForgeryToken]
        [NoPermissionRequired("The workflow engine authorises the transition itself: required role, bound permission, record scope, and the self-approval block.")]
        public async Task<IActionResult> Act(int id, WorkflowActionType action, string? reason)
        {
            // The three the inbox draws, and no more. The action arrives from a form
            // field, so "the view only renders three buttons" is not a boundary: the
            // seeded catalogue leaves Submit and Cancel un-roled and un-permissioned
            // (they belong to whoever raised the record, on that record's own screen),
            // and WorkflowEngine.Authorize skips both checks when a transition names
            // neither — so an unfiltered action here would let any staff account
            // cancel a request it has nothing to do with.
            if (action != WorkflowActionType.Approve && action != WorkflowActionType.Reject && action != WorkflowActionType.Return)
            {
                TempData["Error"] = T(
                    "This queue decides requests — approve, return or reject. Submitting or cancelling one belongs on the screen that raised it.",
                    "هذا الطابور يبتّ في الطلبات — اعتماداً أو إعادةً أو رفضاً. أما تقديم الطلب أو إلغاؤه فمن شاشة الوحدة التي رفعته.");
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var result = await _workflow.ExecuteAsync(id, action, string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(), cancellationToken: Ct);
                var to = IsArabic ? result.ToState.Name.NameAr : result.ToState.Name.NameEn;
                TempData["Flash"] = string.Format(
                    CultureInfo.CurrentUICulture, T("Request #{0} moved to {1}.", "انتقل الطلب رقم {0} إلى {1}."), id, to);
            }
            catch (WorkflowSelfApprovalException)
            {
                TempData["Error"] = T(
                    "You submitted this request, so you cannot approve it (BR-WF-003).",
                    "أنت من قدّم هذا الطلب، فلا يمكنك اعتماده (BR-WF-003).");
            }
            catch (WorkflowReasonRequiredException)
            {
                TempData["Error"] = T(
                    "This action needs a reason before it can be recorded.",
                    "هذا الإجراء يحتاج سبباً قبل تسجيله.");
            }
            catch (WorkflowActorNotAuthorizedException)
            {
                TempData["Error"] = T(
                    "You are not the approver for this step, or the record is outside the scope you hold.",
                    "لست المعتمِد لهذه المرحلة، أو أن السجل خارج النطاق الذي تملكه.");
            }
            catch (WorkflowTransitionNotAllowedException)
            {
                TempData["Error"] = T(
                    "That move is not allowed from the request's current state — it may have been decided already.",
                    "هذه الحركة غير مسموحة من حالة الطلب الحالية — ربما بُتَّ فيه بالفعل.");
            }

            return RedirectToAction(nameof(Index));
        }

        // ------------------------------------------------------------------ the submitter's side

        [HttpGet("mine")]
        [NoPermissionRequired("A person's own submitted requests and where each one has reached.")]
        public async Task<IActionResult> Mine()
        {
            var instances = await _db.WorkflowInstances.AsNoTracking()
                .Where(i => i.SubmittedByUserId == _user.UserId)
                .OrderByDescending(i => i.Id)
                .Take(200)
                .ToListAsync(Ct);

            var m = new MyRequestsViewModel();
            if (instances.Count == 0)
            {
                return View(m);
            }

            var definitionIds = instances.Select(i => i.WorkflowDefinitionId).Distinct().ToList();
            var definitions = await _db.WorkflowDefinitions.AsNoTracking()
                .Where(d => definitionIds.Contains(d.Id)).ToListAsync(Ct);
            var states = await _db.WorkflowStates.AsNoTracking()
                .Where(s => definitionIds.Contains(s.WorkflowDefinitionId)).ToListAsync(Ct);

            m.Items = instances.Select(i =>
            {
                var definition = definitions.Single(d => d.Id == i.WorkflowDefinitionId);
                var state = states.SingleOrDefault(s => s.Id == i.CurrentStateId);
                return new MyRequestsViewModel.Row(
                    i.Id,
                    definition.Code,
                    IsArabic ? definition.Name.NameAr : definition.Name.NameEn,
                    EntityLabel(i),
                    state == null ? string.Empty : (IsArabic ? state.Name.NameAr : state.Name.NameEn),
                    i.IsClosed,
                    state?.IsEditableInState ?? false,
                    i.ReturnCount,
                    i.CreatedAtUtc);
            }).ToList();

            return View(m);
        }

        // ------------------------------------------------------------------ one instance's trail

        [HttpGet("{id:int}")]
        [NoPermissionRequired("The step trail of one request, shown to the people already able to see the request through the inbox or the tracker.")]
        public async Task<IActionResult> History(int id)
        {
            var instance = await _db.WorkflowInstances.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id, Ct);
            if (instance == null || !await MayReadAsync(instance))
            {
                return NotFound();
            }

            var definition = await _db.WorkflowDefinitions.AsNoTracking()
                .SingleAsync(d => d.Id == instance.WorkflowDefinitionId, Ct);
            var states = await _db.WorkflowStates.AsNoTracking()
                .Where(s => s.WorkflowDefinitionId == definition.Id).ToListAsync(Ct);
            var steps = await _db.WorkflowSteps.AsNoTracking()
                .Where(s => s.WorkflowInstanceId == id)
                .OrderBy(s => s.OccurredAtUtc).ThenBy(s => s.Id)
                .ToListAsync(Ct);

            var names = await NamesOfAsync(steps.Select(s => (int?)s.ActorUserId).Append(instance.SubmittedByUserId));

            return View(new WorkflowHistoryViewModel
            {
                InstanceId = instance.Id,
                WorkflowCode = definition.Code,
                WorkflowName = IsArabic ? definition.Name.NameAr : definition.Name.NameEn,
                EntityLabel = EntityLabel(instance),
                StateName = StateName(states, instance.CurrentStateId),
                IsClosed = instance.IsClosed,
                RoutingValue = instance.RoutingValue,
                SubmittedBy = instance.SubmittedByUserId is int by && names.TryGetValue(by, out var submitter) ? submitter : null,
                Steps = steps.Select(s => new WorkflowHistoryViewModel.Step(
                    s.OccurredAtUtc,
                    s.Action,
                    StateName(states, s.FromStateId),
                    StateName(states, s.ToStateId),
                    names.TryGetValue(s.ActorUserId, out var actor) ? actor : null,
                    s.Reason,
                    s.IsDelegated)).ToList(),
            });
        }

        // ------------------------------------------------------------------ the catalogue

        [HttpGet("catalog")]
        [NoPermissionRequired("Read-only: which workflows this school has and who approves at each level. It reads no record.")]
        public async Task<IActionResult> Catalog()
        {
            var definitions = await _db.WorkflowDefinitions.AsNoTracking()
                .OrderBy(d => d.Code).ThenBy(d => d.Version).ToListAsync(Ct);
            var m = new WorkflowCatalogViewModel();
            if (definitions.Count == 0)
            {
                return View(m);
            }

            var ids = definitions.Select(d => d.Id).ToList();
            var states = await _db.WorkflowStates.AsNoTracking().Where(s => ids.Contains(s.WorkflowDefinitionId)).ToListAsync(Ct);
            var transitions = await _db.WorkflowTransitions.AsNoTracking().Where(t => ids.Contains(t.WorkflowDefinitionId)).ToListAsync(Ct);
            var openCounts = (await _db.WorkflowInstances.AsNoTracking()
                    .Where(i => !i.IsClosed && ids.Contains(i.WorkflowDefinitionId))
                    .Select(i => i.WorkflowDefinitionId)
                    .ToListAsync(Ct))
                .GroupBy(x => x)
                .ToDictionary(g => g.Key, g => g.Count());

            // Roles are read through the filter deliberately: a level whose role the
            // school retired should read as missing here, which is exactly the state
            // the chain is in.
            var roles = await _db.Roles.AsNoTracking().ToListAsync(Ct);

            m.Items = definitions.Select(definition =>
            {
                var mine = states.Where(s => s.WorkflowDefinitionId == definition.Id).ToList();
                var levels = transitions
                    .Where(t => t.WorkflowDefinitionId == definition.Id && t.Action == WorkflowActionType.Approve)
                    .OrderBy(t => t.FromStateId).ThenBy(t => t.MinRoutingValue ?? decimal.MinValue)
                    .Select(t =>
                    {
                        var role = roles.SingleOrDefault(r => r.Id == t.RequiredRoleId);
                        return new WorkflowCatalogViewModel.LevelRow(
                            StateName(mine, t.FromStateId),
                            StateName(mine, t.ToStateId),
                            role == null ? null : (IsArabic ? role.Name.NameAr : role.Name.NameEn),
                            t.PermissionModuleCode == null ? null : $"{t.PermissionModuleCode} · {t.PermissionScreenCode} · {t.PermissionAction}",
                            Band(t));
                    })
                    .ToList();

                return new WorkflowCatalogViewModel.Row(
                    definition.Id,
                    definition.Code,
                    IsArabic ? definition.Name.NameAr : definition.Name.NameEn,
                    definition.EntityTypeName,
                    definition.Version,
                    definition.IsActive,
                    mine.Count,
                    openCounts.TryGetValue(definition.Id, out var open) ? open : 0,
                    levels);
            }).ToList();

            return View(m);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// Who may read one request's trail. The screen carries no permission of its
        /// own precisely because the inbox and the tracker already filter to the
        /// reader — but a trail opened by id has no such filter behind it, and this
        /// one shows the business key, the routing value, the submitter and every
        /// reason anyone typed. On WF-04 that is a named student, a discount
        /// percentage and a hardship reason (BR-GLB-072), reachable by walking
        /// <c>/approvals/1</c>, <c>/approvals/2</c>… without it.
        /// <para>
        /// Three ways in, matching the two screens that link here: you submitted it,
        /// you have already acted on it, or one of your roles decides a move that is
        /// open from where it now stands — the same predicate the inbox uses.
        /// </para>
        /// </summary>
        private async Task<bool> MayReadAsync(WorkflowInstance instance)
        {
            if (instance.SubmittedByUserId == _user.UserId)
            {
                return true;
            }

            if (await _db.WorkflowSteps.AsNoTracking()
                    .AnyAsync(s => s.WorkflowInstanceId == instance.Id && s.ActorUserId == _user.UserId, Ct))
            {
                return true;
            }

            var roleIds = await _db.RoleAssignments.AsNoTracking()
                .Where(a => a.UserAccountId == _user.UserId).Select(a => a.RoleId).ToListAsync(Ct);

            return roleIds.Count > 0
                && await _db.WorkflowTransitions.AsNoTracking().AnyAsync(
                    t => t.WorkflowDefinitionId == instance.WorkflowDefinitionId
                         && t.FromStateId == instance.CurrentStateId
                         && t.RequiredRoleId != null
                         && roleIds.Contains(t.RequiredRoleId.Value), Ct);
        }

        private static string Band(WorkflowTransition t)
        {
            if (t.MinRoutingValue == null && t.MaxRoutingValue == null)
            {
                return T("any value", "أي قيمة");
            }

            // Trimmed to the digits a threshold actually carries: the column's scale
            // renders 10 as "10.0000", which reads as precision nobody chose.
            static string N(decimal? value) => value?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty;

            if (t.MinRoutingValue == null)
            {
                return string.Format(CultureInfo.CurrentUICulture, T("up to {0}", "حتى {0}"), N(t.MaxRoutingValue));
            }

            return t.MaxRoutingValue == null
                ? string.Format(CultureInfo.CurrentUICulture, T("over {0}", "أكثر من {0}"), N(t.MinRoutingValue))
                : string.Format(CultureInfo.CurrentUICulture, T("over {0} up to {1}", "أكثر من {0} وحتى {1}"), N(t.MinRoutingValue), N(t.MaxRoutingValue));
        }

        /// <summary>The record a request is about, as much as the instance knows: its business key, else the entity and id.</summary>
        private static string EntityLabel(WorkflowInstance instance)
            => string.IsNullOrWhiteSpace(instance.BusinessKey)
                ? $"{instance.EntityTypeName} #{instance.EntityId.ToString(CultureInfo.InvariantCulture)}"
                : instance.BusinessKey!;

        private static string StateName(IReadOnlyList<WorkflowState> states, int stateId)
        {
            var state = states.SingleOrDefault(s => s.Id == stateId);
            return state == null ? string.Empty : (IsArabic ? state.Name.NameAr : state.Name.NameEn);
        }

        private async Task<Dictionary<int, string>> NamesOfAsync(IEnumerable<int?> userIds)
        {
            var ids = userIds.Where(id => id is int and > 0).Select(id => id!.Value).Distinct().ToList();
            if (ids.Count == 0)
            {
                return new Dictionary<int, string>();
            }

            var accounts = await _users.GetByIdsAsync(ids, Ct);
            return accounts.ToDictionary(kv => kv.Key, kv => kv.Value.DisplayName ?? kv.Value.UserName);
        }
    }
}
