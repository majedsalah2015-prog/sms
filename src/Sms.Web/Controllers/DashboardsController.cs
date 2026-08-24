using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Attendance;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Dashboards;
using Sms.Application.ReadModels;
using Sms.Application.Security;
using Sms.Application.Setup;
using Sms.Domain.Certificates;
using Sms.Domain.Dashboards;
using Sms.Domain.Discipline;
using Sms.Domain.Grades;
using Sms.Domain.Schools;
using Sms.Domain.Sections;
using Sms.Domain.Security;
using Sms.Domain.Setup;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/31 §8 — screens over the E-702 dashboard platform: 8.1 the
    /// dashboard shell (an executive overview with widget chrome, as-of stamps,
    /// drill links and personalization mode) and 8.2 the layout administrator
    /// (widget registry + role template editor with preview-as-role). 8.3/8.4
    /// portal homes are Module 11's <c>/portal</c> screens, already built.
    /// <para>
    /// The overview's panels are hard-wired, not registry-driven, and that is
    /// the doc's own split: widget *content* (widget → data source → drill path)
    /// is a Phase 9 deliverable, so <see cref="WidgetDefinition"/> has nothing to
    /// bind a panel to yet. What the registry does govern is real: register a
    /// widget under a panel's <c>DSH-&lt;MOD&gt;-###</c> code and that panel
    /// becomes permission-gated (BR-DSH-001), role-orderable and user-
    /// personalizable (BR-DSH-003) through the engine.
    /// </para>
    /// <para>
    /// Every heavy figure is read from S8/E-802's snapshot tables or read models,
    /// never re-derived from the hot tables (NF-P5), and each carries the
    /// <c>AsOfUtc</c> BR-DSH-002 makes mandatory. The one live figure is the
    /// certificate queue, because BR-DSH-004 says action widgets refresh live.
    /// </para>
    /// </summary>
    [Route("dashboards")]
    public class DashboardsController : Controller
    {
        /// <summary>doc §14 Q2's proposed default until a per-school setting exists (SettingKeys has no anonymity key).</summary>
        private const int DefaultAnonymityThreshold = 5;

        /// <summary>How far ahead the collection calendar panel looks; the snapshot itself holds 120 days.</summary>
        private const int CollectionHorizonDays = 14;

        private const string ParentRoleCode = "PARENT";
        private const string StudentRoleCode = "STUDENT";

        private readonly ISystemSetupAdmin _setup;
        private readonly IDashboardAdmin _dashboards;
        private readonly IDashboardQuery _widgets;
        private readonly IReadModelQuery _readModels;
        private readonly ISnapshotRefreshService _snapshots;
        private readonly IPermissionService _permissions;
        private readonly AppDbContext _db;
        private readonly IWorkingYearContext _workingYear;
        private readonly ICurrentUser _user;
        private readonly IClock _clock;

        public DashboardsController(
            ISystemSetupAdmin setup, IDashboardAdmin dashboards, IDashboardQuery widgets, IReadModelQuery readModels, ISnapshotRefreshService snapshots,
            IPermissionService permissions, AppDbContext db, IWorkingYearContext workingYear, ICurrentUser user, IClock clock)
        {
            _setup = setup;
            _dashboards = dashboards;
            _widgets = widgets;
            _readModels = readModels;
            _snapshots = snapshots;
            _permissions = permissions;
            _db = db;
            _workingYear = workingYear;
            _user = user;
            _clock = clock;
        }

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        // ================================================================== 8.1 Dashboard shell

        [HttpGet("")]
        [RequirePermission(ScreenCatalog.Modules.Dashboards, ScreenCatalog.Dashboards.Dashboard, ActionVerb.View)]
        public async Task<IActionResult> Index(int? year = null, DateTime? date = null, int? threshold = null, bool personalize = false)
        {
            var years = await _db.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartDate).ToListAsync();
            var m = new DashboardOverviewViewModel
            {
                Years = years,
                Year = years.FirstOrDefault(y => y.Id == (year ?? _workingYear.AcademicYearId))
                    ?? years.FirstOrDefault(y => y.Status == AcademicYearStatus.Active)
                    ?? years.FirstOrDefault(),
                Date = (date ?? _clock.UtcNow).Date,
                AnonymityThreshold = threshold is > 0 ? threshold.Value : DefaultAnonymityThreshold,
                PersonalizeMode = personalize,
            };

            await ResolvePanelsAsync(m);

            // Before the no-year return, deliberately: a deployment still in setup has
            // no academic year (BR-SET-003 keeps the first one shut), which is exactly
            // the state this panel exists to report on. Computing it after the return
            // rendered its card with an empty body on the only screen that needed it.
            if (m.Rendered.Any(p => p.Panel.Code == DashboardPanels.SetupCompleteness))
            {
                m.Setup = await SetupCompletenessAsync();
            }

            if (m.Year == null)
            {
                return View(m);
            }

            var yid = m.Year.Id;

            // Only the panels that will actually render are computed: a widget the user cannot see
            // (BR-DSH-001) or has switched off must not cost a query either.
            var rendered = new HashSet<string>(m.Rendered.Select(p => p.Panel.Code));

            if (rendered.Contains(DashboardPanels.Attendance))
            {
                m.Attendance = await AttendanceAsync(yid, m.Date);
            }

            if (rendered.Contains(DashboardPanels.Receivables))
            {
                m.Receivables = await ReceivablesAsync(yid);
            }

            if (rendered.Contains(DashboardPanels.Collections))
            {
                m.Collections = await CollectionsAsync(yid);
            }

            if (rendered.Contains(DashboardPanels.Certificates))
            {
                m.Certificates = await CertificatesAsync();
            }

            if (rendered.Contains(DashboardPanels.Seats))
            {
                m.Seats = await SeatsAsync(yid);
            }

            if (rendered.Contains(DashboardPanels.TeacherLoad))
            {
                m.TeacherLoad = await TeacherLoadAsync(yid);
            }

            if (rendered.Contains(DashboardPanels.Restricted))
            {
                m.Restricted = await RestrictedAsync(m.Date, m.AnonymityThreshold);
            }

            return View(m);
        }

        /// <summary>
        /// doc/Modules/01 §11. Reads the wizard's own evaluator rather than counting
        /// checklist rows here, so the dashboard's percentage and the wizard's cannot
        /// disagree — BR-DSH-002's one-computation-source rule, applied to the one
        /// figure on this screen that is about the deployment rather than the school.
        /// </summary>
        private async Task<SetupCompletenessView> SetupCompletenessAsync()
        {
            var steps = await _setup.GetChecklistAsync(HttpContext.RequestAborted);
            var mandatory = steps.Where(s => s.Step.IsMandatory).ToList();
            var school = await _db.Schools.AsNoTracking().SingleOrDefaultAsync(s => s.Id == _db.CurrentSchoolId, HttpContext.RequestAborted);

            return new SetupCompletenessView(
                SetupWizardEvaluator.CompletionPercent(steps),
                mandatory.Count,
                mandatory.Count(s => s.Status == SetupStepStatus.Completed),
                school?.SetupCompletedAtUtc != null,
                school?.SetupCompletedAtUtc,
                mandatory.Where(s => s.Status != SetupStepStatus.Completed)
                    .OrderBy(s => s.Step.Order)
                    .Select(s => new SetupPendingStepView(s.Step.Code, s.Step.TitleEn, s.Step.TitleAr))
                    .ToList());
        }

        /// <summary>
        /// BR-DSH-003's personalization mode. One POST for the whole grid: every
        /// registered panel's order, plus the ids of the ones left ticked.
        /// </summary>
        [HttpPost("personalize")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Dashboards, ScreenCatalog.Dashboards.Dashboard, ActionVerb.Edit)]
        public async Task<IActionResult> Personalize(int[] widgetIds, int[] sortOrders, int[] visibleIds, int? year, DateTime? date)
        {
            var visible = new HashSet<int>(visibleIds ?? Array.Empty<int>());
            var refused = 0;
            for (var i = 0; i < (widgetIds?.Length ?? 0); i++)
            {
                var order = sortOrders != null && i < sortOrders.Length ? sortOrders[i] : (i + 1) * 10;
                try
                {
                    await _dashboards.PersonalizeAsync(_user.UserId, widgetIds![i], order, visible.Contains(widgetIds[i]), HttpContext.RequestAborted);
                }
                catch (WidgetNotPermittedException)
                {
                    // doc §9 is server-enforced: a widget the user cannot see cannot be added to their
                    // own layout either. Counted rather than thrown so one refused row does not lose the rest.
                    refused++;
                }
            }

            TempData["Flash"] = refused == 0
                ? T("Your dashboard layout was saved.", "حُفظ ترتيب لوحتك.")
                : string.Format(T("Layout saved; {0} widget(s) refused — your roles do not grant their permission.", "حُفظ الترتيب؛ رُفض {0} من العناصر — أدوارك لا تمنح صلاحيتها."), refused);
            return RedirectToAction(nameof(Index), new { year, date = date?.ToString("yyyy-MM-dd"), personalize = true });
        }

        [HttpPost("personalize/reset")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Dashboards, ScreenCatalog.Dashboards.Dashboard, ActionVerb.Edit)]
        public async Task<IActionResult> ResetLayout(int? year, DateTime? date)
        {
            await _dashboards.ResetToDefaultAsync(_user.UserId, HttpContext.RequestAborted);
            TempData["Flash"] = T("Your dashboard is back to its role default.", "عادت لوحتك إلى الافتراضي الخاص بدورك.");
            return RedirectToAction(nameof(Index), new { year, date = date?.ToString("yyyy-MM-dd") });
        }

        /// <summary>
        /// Recomputes the three DB/04 §4 snapshots on demand. Calls
        /// <see cref="ISnapshotRefreshService"/> directly rather than IJobRunner:
        /// the runner resolves a job by its ops.JobDefinition row, and no seed
        /// contributor writes those rows, so by code it would only ever throw.
        /// </summary>
        [HttpPost("refresh")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Dashboards, ScreenCatalog.Dashboards.Dashboard, ActionVerb.Post)]
        public async Task<IActionResult> RefreshSnapshots(int? year, DateTime? date)
        {
            var day = (date ?? _clock.UtcNow).Date;
            var receivables = await _snapshots.RefreshAgedReceivablesAsync(cancellationToken: HttpContext.RequestAborted);
            var attendance = await _snapshots.RefreshDailyAttendanceSummaryAsync(day, HttpContext.RequestAborted);
            var collections = await _snapshots.RefreshCollectionCalendarAsync(cancellationToken: HttpContext.RequestAborted);

            TempData["Flash"] = string.Format(
                T("Snapshots refreshed: {0} receivables, {1} attendance, {2} collection-calendar rows.", "حُدِّثت اللقطات: {0} صف ذمم، و{1} صف حضور، و{2} صف تقويم تحصيل."),
                receivables, attendance, collections);
            return RedirectToAction(nameof(Index), new { year, date = date?.ToString("yyyy-MM-dd") });
        }

        // ================================================================== 8.2 Layout administrator — widget registry

        [HttpGet("widgets")]
        [RequirePermission(ScreenCatalog.Modules.Dashboards, ScreenCatalog.Dashboards.Widgets, ActionVerb.View)]
        public async Task<IActionResult> Widgets()
        {
            var widgets = await _db.WidgetDefinitions.AsNoTracking().OrderBy(w => w.Code).ToListAsync();
            var permissionIds = widgets.Select(w => w.RequiredPermissionId).Distinct().ToList();
            var referenced = await _db.Permissions.AsNoTracking().Where(p => permissionIds.Contains(p.Id)).ToListAsync();
            var inTemplates = (await _db.LayoutTemplateWidgets.AsNoTracking().Select(x => x.WidgetDefinitionId).ToListAsync())
                .GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
            var personalized = (await _db.UserLayouts.AsNoTracking().Select(x => x.WidgetDefinitionId).ToListAsync())
                .GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

            var m = new WidgetRegistryViewModel
            {
                Rows = widgets.Select(w => new WidgetRegistryViewModel.Row(
                    w,
                    referenced.FirstOrDefault(p => p.Id == w.RequiredPermissionId),
                    DashboardPanels.Find(w.Code),
                    inTemplates.TryGetValue(w.Id, out var t) ? t : 0,
                    personalized.TryGetValue(w.Id, out var u) ? u : 0)).ToList(),
                Permissions = await _db.Permissions.AsNoTracking()
                    .OrderBy(p => p.ModuleCode).ThenBy(p => p.ScreenCode).ThenBy(p => p.Action).ToListAsync(),
                Unregistered = DashboardPanels.All.Where(p => widgets.All(w => w.Code != p.Code)).ToList(),
            };

            return View(m);
        }

        [HttpPost("widgets")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Dashboards, ScreenCatalog.Dashboards.Widgets, ActionVerb.Create)]
        public async Task<IActionResult> DefineWidget(
            string code, string owningModuleCode, string titleAr, string titleEn, int requiredPermissionId,
            WidgetRefreshClass refreshClass, string? drillTargetCode, bool isPortalEligible)
        {
            code = code?.Trim().ToUpperInvariant() ?? string.Empty;
            if (await _db.WidgetDefinitions.AnyAsync(w => w.Code == code))
            {
                // (SchoolId, Code) is unique — refused here as a message rather than as a DbUpdateException.
                TempData["Error"] = T("A widget with that code is already registered.", "يوجد عنصر مسجَّل بهذا الرمز.");
                return RedirectToAction(nameof(Widgets));
            }

            await _dashboards.DefineWidgetAsync(
                code, owningModuleCode?.Trim() ?? string.Empty, titleAr?.Trim() ?? string.Empty, titleEn?.Trim() ?? string.Empty,
                requiredPermissionId, refreshClass, drillTargetCode?.Trim() ?? string.Empty, isPortalEligible, HttpContext.RequestAborted);
            TempData["Flash"] = T("Widget registered.", "سُجِّل العنصر.");
            return RedirectToAction(nameof(Widgets));
        }

        /// <summary>Registers one of the overview's built-in panels, so it becomes permission-gated and personalizable.</summary>
        [HttpPost("widgets/built-in")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Dashboards, ScreenCatalog.Dashboards.Widgets, ActionVerb.Configure)]
        public async Task<IActionResult> RegisterBuiltIn(string code, int requiredPermissionId)
        {
            var panel = DashboardPanels.Find(code);
            if (panel == null)
            {
                return NotFound();
            }

            if (await _db.WidgetDefinitions.AnyAsync(w => w.Code == panel.Code))
            {
                TempData["Error"] = T("That panel is already registered.", "هذه اللوحة مسجَّلة بالفعل.");
                return RedirectToAction(nameof(Widgets));
            }

            await _dashboards.DefineWidgetAsync(
                panel.Code, panel.OwningModuleCode, panel.TitleAr, panel.TitleEn, requiredPermissionId,
                panel.RefreshClass, panel.DrillTargetCode, panel.IsPortalEligible, HttpContext.RequestAborted);
            TempData["Flash"] = string.Format(T("Panel {0} registered — it is now permission-gated.", "سُجِّلت اللوحة {0} — صارت خاضعة للصلاحية."), panel.Code);
            return RedirectToAction(nameof(Widgets));
        }

        // ================================================================== 8.2 Layout administrator — role templates

        [HttpGet("layouts")]
        [RequirePermission(ScreenCatalog.Modules.Dashboards, ScreenCatalog.Dashboards.Layouts, ActionVerb.View)]
        public async Task<IActionResult> Layouts(int? roleId = null)
        {
            var roles = await _db.Roles.AsNoTracking().OrderBy(r => r.Code).ToListAsync();
            var templates = await _db.LayoutTemplates.AsNoTracking().ToListAsync();
            var templateWidgets = await _db.LayoutTemplateWidgets.AsNoTracking().ToListAsync();
            var members = (await _db.RoleAssignments.AsNoTracking().Select(a => a.RoleId).ToListAsync())
                .GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

            var m = new LayoutAdminViewModel
            {
                Roles = roles.Select(r =>
                {
                    var template = templates.FirstOrDefault(t => t.RoleId == r.Id);
                    return new LayoutAdminViewModel.RoleRow(
                        r, template,
                        template == null ? 0 : templateWidgets.Count(x => x.LayoutTemplateId == template.Id),
                        members.TryGetValue(r.Id, out var c) ? c : 0,
                        IsPortalRole(r));
                }).ToList(),
            };

            var selected = roleId == null ? null : roles.FirstOrDefault(r => r.Id == roleId);
            m.Selected = selected;
            if (selected == null)
            {
                return View(m);
            }

            var isPortalRole = IsPortalRole(selected);
            m.SelectedIsPortalRole = isPortalRole;
            var template = templates.FirstOrDefault(t => t.RoleId == selected.Id);
            m.Template = template;
            var widgets = await _db.WidgetDefinitions.AsNoTracking().OrderBy(w => w.Code).ToListAsync();

            if (template == null)
            {
                m.Addable = widgets;
                return View(m);
            }

            var templateId = template.Id;
            var rows = templateWidgets.Where(x => x.LayoutTemplateId == templateId).OrderBy(x => x.SortOrder).ToList();
            var widgetIds = rows.Select(x => x.WidgetDefinitionId).ToList();
            var used = widgets.Where(w => widgetIds.Contains(w.Id)).ToList();
            var permissionIds = used.Select(w => w.RequiredPermissionId).Distinct().ToList();
            var permissions = await _db.Permissions.AsNoTracking().Where(p => permissionIds.Contains(p.Id)).ToListAsync();

            // doc §9's save-time warning, evaluated as a standing preview instead: which of these
            // widgets would a member of this role actually see? A role's grants are its RolePermission rows.
            var grantedIds = await _db.RolePermissions.AsNoTracking()
                .Where(rp => rp.RoleId == selected.Id).Select(rp => rp.PermissionId).ToListAsync();
            var roleGrants = new HashSet<int>(grantedIds);

            m.Entries = rows.Where(x => used.Any(w => w.Id == x.WidgetDefinitionId)).Select(x =>
            {
                var widget = used.First(w => w.Id == x.WidgetDefinitionId);
                return new LayoutAdminViewModel.Entry(
                    x, widget, permissions.FirstOrDefault(p => p.Id == widget.RequiredPermissionId),
                    roleGrants.Contains(widget.RequiredPermissionId),
                    PortalWidgetGate.CanRender(widget.IsPortalEligible, isPortalRole));
            }).ToList();

            m.Addable = widgets.Where(w => !widgetIds.Contains(w.Id)).ToList();
            return View(m);
        }

        [HttpPost("layouts")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Dashboards, ScreenCatalog.Dashboards.Layouts, ActionVerb.Create)]
        public async Task<IActionResult> CreateTemplate(int roleId)
        {
            if (await _db.LayoutTemplates.AnyAsync(t => t.RoleId == roleId))
            {
                // (SchoolId, RoleId) is unique: one template per role, personalization goes on top (BR-DSH-003).
                TempData["Error"] = T("That role already has a layout template.", "لهذا الدور قالب تخطيط بالفعل.");
                return RedirectToAction(nameof(Layouts), new { roleId });
            }

            await _dashboards.DefineLayoutTemplateAsync(roleId, HttpContext.RequestAborted);
            TempData["Flash"] = T("Layout template created.", "أُنشئ قالب التخطيط.");
            return RedirectToAction(nameof(Layouts), new { roleId });
        }

        [HttpPost("layouts/{templateId:int}/widgets")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Dashboards, ScreenCatalog.Dashboards.Layouts, ActionVerb.Edit)]
        public async Task<IActionResult> AddWidget(int templateId, int widgetDefinitionId, int sortOrder, int roleId)
        {
            if (await _db.LayoutTemplateWidgets.AnyAsync(x => x.LayoutTemplateId == templateId && x.WidgetDefinitionId == widgetDefinitionId))
            {
                TempData["Error"] = T("That widget is already on this template.", "هذا العنصر موجود في القالب بالفعل.");
                return RedirectToAction(nameof(Layouts), new { roleId });
            }

            await _dashboards.AddWidgetToTemplateAsync(templateId, widgetDefinitionId, sortOrder, HttpContext.RequestAborted);
            TempData["Flash"] = T("Widget added to the template.", "أُضيف العنصر إلى القالب.");
            return RedirectToAction(nameof(Layouts), new { roleId });
        }

        /// <summary>
        /// Reorder and remove. The engine only knows how to add a row to a template
        /// (<see cref="IDashboardAdmin.AddWidgetToTemplateAsync"/>), so these two edits
        /// go straight to the rows — a plain position column and a hard remove, both
        /// captured by LayoutTemplateWidget's own T3 audit tag. Same call the
        /// admissions parent-link screen makes for the same reason.
        /// </summary>
        [HttpPost("layouts/{templateId:int}/arrange")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Dashboards, ScreenCatalog.Dashboards.Layouts, ActionVerb.Edit)]
        public async Task<IActionResult> ArrangeTemplate(int templateId, int[] entryIds, int[] sortOrders, int[] removeIds, int roleId)
        {
            var remove = new HashSet<int>(removeIds ?? Array.Empty<int>());
            var rows = await _db.LayoutTemplateWidgets.Where(x => x.LayoutTemplateId == templateId).ToListAsync();

            for (var i = 0; i < (entryIds?.Length ?? 0); i++)
            {
                var row = rows.FirstOrDefault(x => x.Id == entryIds![i]);
                if (row == null || remove.Contains(row.Id))
                {
                    continue;
                }

                if (sortOrders != null && i < sortOrders.Length)
                {
                    row.SortOrder = sortOrders[i];
                }
            }

            var removed = rows.Where(x => remove.Contains(x.Id)).ToList();
            _db.LayoutTemplateWidgets.RemoveRange(removed);
            await _db.SaveChangesAsync(HttpContext.RequestAborted);

            TempData["Flash"] = removed.Count == 0
                ? T("Template order saved.", "حُفظ ترتيب القالب.")
                : string.Format(T("Template saved; {0} widget(s) removed.", "حُفظ القالب؛ أُزيل {0} عنصر."), removed.Count);
            return RedirectToAction(nameof(Layouts), new { roleId });
        }

        // ================================================================== panel resolution (BR-DSH-001/003)

        /// <summary>
        /// Resolves each built-in panel's order and visibility through the engine's
        /// three layers: the user's own <c>UserLayout</c> row wins, else their role's
        /// <c>LayoutTemplate</c> row, else the built-in order. A panel with no
        /// registered <see cref="WidgetDefinition"/> has nothing to personalize and
        /// simply keeps the built-in position — which is what a school that never
        /// touches the registry gets, and it is a working dashboard.
        /// </summary>
        private async Task ResolvePanelsAsync(DashboardOverviewViewModel m)
        {
            var codes = DashboardPanels.All.Select(p => p.Code).ToList();
            var definitions = await _db.WidgetDefinitions.AsNoTracking().Where(w => codes.Contains(w.Code)).ToListAsync();
            var definitionIds = definitions.Select(d => d.Id).ToList();

            var mine = definitionIds.Count == 0
                ? new List<UserLayout>()
                : await _db.UserLayouts.AsNoTracking()
                    .Where(l => l.UserAccountId == _user.UserId && definitionIds.Contains(l.WidgetDefinitionId)).ToListAsync();

            var roleIds = await _db.RoleAssignments.AsNoTracking()
                .Where(a => a.UserAccountId == _user.UserId).Select(a => a.RoleId).ToListAsync();
            var template = roleIds.Count == 0
                ? null
                : await _db.LayoutTemplates.AsNoTracking().Where(t => roleIds.Contains(t.RoleId)).OrderBy(t => t.Id).FirstOrDefaultAsync();
            var templateRows = template == null
                ? new List<LayoutTemplateWidget>()
                : await _db.LayoutTemplateWidgets.AsNoTracking().Where(x => x.LayoutTemplateId == template.Id).ToListAsync();
            m.TemplateRole = template == null ? null : await _db.Roles.AsNoTracking().SingleOrDefaultAsync(r => r.Id == template.RoleId);

            var permitted = await PermittedAsync(definitions);

            var states = new List<DashboardOverviewViewModel.PanelState>();
            var fallback = 0;
            foreach (var panel in DashboardPanels.All)
            {
                fallback += 10;
                var definition = definitions.FirstOrDefault(d => d.Code == panel.Code);
                var sort = fallback;
                var visible = true;
                var source = PanelSource.BuiltIn;

                if (definition != null)
                {
                    var fromTemplate = templateRows.FirstOrDefault(x => x.WidgetDefinitionId == definition.Id);
                    if (fromTemplate != null)
                    {
                        sort = fromTemplate.SortOrder;
                        source = PanelSource.RoleTemplate;
                    }

                    var personal = mine.FirstOrDefault(x => x.WidgetDefinitionId == definition.Id);
                    if (personal != null)
                    {
                        sort = personal.SortOrder;
                        visible = personal.IsVisible;
                        source = PanelSource.Personal;
                    }
                }

                states.Add(new DashboardOverviewViewModel.PanelState(
                    panel, definition, sort, visible,
                    definition == null || permitted.Contains(definition.RequiredPermissionId), source));
            }

            m.Panels = states;
        }

        /// <summary>BR-DSH-001 deny-by-default, resolved once per distinct permission rather than once per panel.</summary>
        private async Task<HashSet<int>> PermittedAsync(IReadOnlyList<WidgetDefinition> definitions)
        {
            var granted = new HashSet<int>();
            var permissionIds = definitions.Select(d => d.RequiredPermissionId).Distinct().ToList();
            if (permissionIds.Count == 0)
            {
                return granted;
            }

            foreach (var permission in await _db.Permissions.AsNoTracking().Where(p => permissionIds.Contains(p.Id)).ToListAsync())
            {
                if (await _permissions.HasPermissionAsync(permission.ModuleCode, permission.ScreenCode, permission.Action, HttpContext.RequestAborted))
                {
                    granted.Add(permission.Id);
                }
            }

            return granted;
        }

        // ================================================================== panel data (snapshots and read models only)

        private async Task<AttendanceTodayView> AttendanceAsync(int yearId, DateTime day)
        {
            var rows = await _db.DailyAttendanceSummarySnapshots.AsNoTracking()
                .Where(s => s.Date == day && s.AcademicYearId == yearId).ToListAsync();
            var expected = await _db.Sections.AsNoTracking()
                .CountAsync(s => s.AcademicYearId == yearId && s.Status == SectionStatus.Active);

            var scheduled = rows.Sum(r => r.ScheduledCount);
            var absent = rows.Sum(r => r.AbsentCount);
            var exempted = rows.Sum(r => r.ExemptedCount);
            var late = rows.Sum(r => r.LateCount);

            var stages = await _db.Stages.AsNoTracking().OrderBy(s => s.SequenceOrder).ToListAsync();
            var stageRows = rows.GroupBy(r => r.StageId).Select(g => new AttendanceStageRow(
                stages.FirstOrDefault(s => s.Id == g.Key),
                g.Sum(r => r.ScheduledCount), g.Sum(r => r.AbsentCount), g.Sum(r => r.ExemptedCount), g.Sum(r => r.LateCount),
                // Rolled up through BR-ATD-009's own calculator, never by averaging the per-section percentages.
                AttendancePercentageCalculator.Calculate(g.Sum(r => r.ScheduledCount), g.Sum(r => r.ExemptedCount), g.Sum(r => r.AbsentCount))))
                .OrderBy(r => r.Stage?.SequenceOrder ?? int.MaxValue).ToList();

            var worstRows = rows.Where(r => r.AbsentCount > 0).OrderBy(r => r.PresentPercent).Take(6).ToList();
            var sectionIds = worstRows.Select(r => r.SectionId).ToList();
            var sections = await _db.Sections.AsNoTracking().Where(s => sectionIds.Contains(s.Id)).ToListAsync();
            var grades = await GradesByProfileAsync(worstRows.Select(r => r.GradeYearProfileId));

            var worst = worstRows.Select(r => new AttendanceSectionRow(
                sections.FirstOrDefault(s => s.Id == r.SectionId),
                grades.TryGetValue(r.GradeYearProfileId, out var g) ? g : null,
                r.ScheduledCount, r.AbsentCount, r.LateCount, r.PresentPercent)).ToList();

            return new AttendanceTodayView(
                rows.Count == 0 ? (DateTime?)null : rows.Max(r => r.AsOfUtc),
                rows.Count, expected, scheduled, absent, exempted, late,
                AttendancePercentageCalculator.Calculate(scheduled, exempted, absent),
                stageRows, worst);
        }

        private async Task<ReceivablesView> ReceivablesAsync(int yearId)
        {
            var rows = await _db.AgedReceivablesSnapshots.AsNoTracking().Where(r => r.AcademicYearId == yearId).ToListAsync();
            var grades = await GradesByProfileAsync(rows.Where(r => r.GradeYearProfileId != null).Select(r => r.GradeYearProfileId!.Value));

            var byGrade = rows.Where(r => r.GradeYearProfileId != null)
                .GroupBy(r => r.GradeYearProfileId!.Value)
                .Select(g => new ReceivablesGradeRow(
                    grades.TryGetValue(g.Key, out var grade) ? grade : null,
                    g.Sum(r => r.Total), g.Sum(r => r.Over90)))
                .OrderByDescending(r => r.Total).Take(8).ToList();

            // The snapshot is the fast path and the honest one (it carries an as-of). Before the first
            // refresh there is no as-of to show and no rows to sum, so the engine's live school-wide
            // position is read once instead — showing a bare zero would read as "nobody owes anything".
            decimal? live = rows.Count == 0
                ? await _widgets.GetSchoolReceivablesTotalAsync(HttpContext.RequestAborted)
                : (decimal?)null;

            return new ReceivablesView(
                rows.Count == 0 ? (DateTime?)null : rows.Max(r => r.AsOfUtc),
                rows.Sum(r => r.Total), rows.Sum(r => r.Current), rows.Sum(r => r.Days1To30),
                rows.Sum(r => r.Days31To60), rows.Sum(r => r.Days61To90), rows.Sum(r => r.Over90),
                rows.Select(r => r.PayerId).Distinct().Count(), rows.Select(r => r.StudentId).Distinct().Count(),
                byGrade, live);
        }

        private async Task<CollectionsView> CollectionsAsync(int yearId)
        {
            var rows = await _db.CollectionCalendarSnapshots.AsNoTracking().Where(r => r.AcademicYearId == yearId).ToListAsync();
            var today = _clock.UtcNow.Date;
            var horizon = today.AddDays(CollectionHorizonDays);

            var overdue = rows.Where(r => r.DueDate < today).ToList();
            var soon = rows.Where(r => r.DueDate >= today && r.DueDate <= horizon).OrderBy(r => r.DueDate).ToList();

            return new CollectionsView(
                rows.Count == 0 ? (DateTime?)null : rows.Max(r => r.AsOfUtc),
                overdue.Sum(r => r.OutstandingAmount), overdue.Sum(r => r.OverdueCount),
                soon.Sum(r => r.OutstandingAmount), soon.Sum(r => r.InstallmentCount),
                CollectionHorizonDays,
                soon.Select(r => new CollectionDayRow(r.DueDate, r.InstallmentCount, r.ScheduledAmount, r.PaidAmount, r.OutstandingAmount, r.OverdueCount)).ToList());
        }

        private async Task<CertificateQueueView> CertificatesAsync()
        {
            // BR-DSH-004: an action widget is live. The count comes from the engine so the tile and the
            // WF-09 queue can never disagree; the list below repeats the engine's own predicate.
            var pending = await _widgets.GetPendingCertificateRequestsCountAsync(HttpContext.RequestAborted);
            var oldest = await _db.CertificateRequests.AsNoTracking()
                .Where(r => r.Status == CertificateRequestStatus.Requested || r.Status == CertificateRequestStatus.Approved)
                .OrderBy(r => r.RequestedAtUtc).Take(6).ToListAsync();

            var typeIds = oldest.Select(r => r.CertificateTypeId).Distinct().ToList();
            var types = await _db.CertificateTypes.IgnoreQueryFilters().AsNoTracking().Where(t => typeIds.Contains(t.Id)).ToListAsync();
            var studentIds = oldest.Select(r => r.StudentId).Distinct().ToList();
            var students = await _db.Students.IgnoreQueryFilters().AsNoTracking().Where(s => studentIds.Contains(s.Id)).ToListAsync();

            var now = _clock.UtcNow.Date;
            return new CertificateQueueView(pending, oldest.Select(r => new CertificateQueueRow(
                r,
                types.FirstOrDefault(t => t.Id == r.CertificateTypeId),
                students.FirstOrDefault(s => s.Id == r.StudentId),
                Math.Max(0, (now - r.RequestedAtUtc.Date).Days))).ToList());
        }

        private async Task<SeatsView> SeatsAsync(int yearId)
        {
            var rows = await _readModels.GetSeatUtilizationAsync(yearId, HttpContext.RequestAborted);
            var grades = await GradesByProfileAsync(rows.Select(r => r.GradeYearProfileId));

            var tight = rows.Where(r => r.FreeSeats <= 0 || r.PipelineApplications > r.FreeSeats)
                .OrderBy(r => r.FreeSeats)
                .Select(r => new SeatsGradeRow(
                    grades.TryGetValue(r.GradeYearProfileId, out var g) ? g : null,
                    r.PlannedSeats, r.SectionCapacity, r.Enrolled, r.PipelineApplications, r.FreeSeats))
                .Take(8).ToList();

            return new SeatsView(
                rows.Sum(r => r.PlannedSeats), rows.Sum(r => r.SectionCapacity), rows.Sum(r => r.Enrolled),
                rows.Sum(r => r.PipelineApplications), rows.Sum(r => r.FreeSeats), tight);
        }

        private async Task<TeacherLoadView> TeacherLoadAsync(int yearId)
        {
            var rows = await _readModels.GetTeacherLoadsAsync(yearId, HttpContext.RequestAborted);
            var worstRows = rows.Where(r => r.IsOverloaded).OrderByDescending(r => r.CurrentWeeklyPeriods - r.MaxWeeklyPeriods).Take(6).ToList();
            var employeeIds = worstRows.Select(r => r.EmployeeId).Distinct().ToList();
            var employees = await _db.Employees.AsNoTracking().Where(e => employeeIds.Contains(e.Id)).ToListAsync();

            return new TeacherLoadView(
                rows.Count, rows.Count(r => r.IsOverloaded),
                worstRows.Select(r => new TeacherLoadRowView(
                    employees.FirstOrDefault(e => e.Id == r.EmployeeId), r.CurrentWeeklyPeriods, r.MaxWeeklyPeriods)).ToList());
        }

        /// <summary>
        /// BR-DSH-007: the two restricted-category counts a principal legitimately
        /// needs school-wide (medical, discipline), masked below the threshold so a
        /// count of one cannot name the child it is about.
        /// </summary>
        private async Task<RestrictedView> RestrictedAsync(DateTime day, int threshold)
        {
            var visits = await _db.ClinicVisits.AsNoTracking()
                .CountAsync(v => v.ArrivedAtUtc >= day && v.ArrivedAtUtc < day.AddDays(1));
            var cases = await _db.DisciplineCases.AsNoTracking().CountAsync(c => c.Status != CaseStatus.Closed);

            return new RestrictedView(
                AnonymityThresholdGuard.Mask(visits, threshold),
                AnonymityThresholdGuard.Mask(cases, threshold),
                threshold);
        }

        // ================================================================== helpers

        /// <summary>
        /// Grade-year profile id → grade level. Every read model and snapshot keys on the
        /// profile (the grade *as run in this year*), never on the grade itself, so the
        /// hop is unavoidable — and GradeLevel.Name is a LocalizedName, not two columns.
        /// </summary>
        private async Task<Dictionary<int, GradeLevel>> GradesByProfileAsync(IEnumerable<int> profileIds)
        {
            var ids = profileIds.Distinct().ToList();
            if (ids.Count == 0)
            {
                return new Dictionary<int, GradeLevel>();
            }

            var profiles = await _db.GradeYearProfiles.AsNoTracking()
                .Where(p => ids.Contains(p.Id)).Select(p => new { p.Id, p.GradeLevelId }).ToListAsync();
            var gradeIds = profiles.Select(p => p.GradeLevelId).Distinct().ToList();
            var grades = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking().Where(g => gradeIds.Contains(g.Id)).ToListAsync();

            var map = new Dictionary<int, GradeLevel>();
            foreach (var profile in profiles)
            {
                var grade = grades.FirstOrDefault(g => g.Id == profile.GradeLevelId);
                if (grade != null)
                {
                    map[profile.Id] = grade;
                }
            }

            return map;
        }

        /// <summary>BR-DSH-006's portal side, keyed on the two seeded portal role templates (doc 06 §4.3).</summary>
        private static bool IsPortalRole(Role role) =>
            string.Equals(role.Code, ParentRoleCode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role.Code, StudentRoleCode, StringComparison.OrdinalIgnoreCase);
    }
}
