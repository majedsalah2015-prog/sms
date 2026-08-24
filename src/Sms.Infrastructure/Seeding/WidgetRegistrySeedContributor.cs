using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Dashboards;
using Sms.Application.Security;
using Sms.Application.Seeding;
using Sms.Domain.Dashboards;
using Sms.Domain.Security;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Seeding
{
    /// <summary>
    /// Registers the dashboard panels that already compute something into
    /// <c>core.WidgetDefinition</c>, and gives four seeded roles a default layout
    /// over them (<c>docs/Reports/Dashboard-Specifications.md</c>, BR-DSH-001/003).
    /// <para>
    /// <b>Why registering matters.</b> The overview's panels are hard-wired, and an
    /// unregistered panel renders for everyone — the registry is opt-in by design.
    /// Registering a panel under its <c>DSH-&lt;MOD&gt;-###</c> code is what turns it
    /// into a widget: permission-gated (BR-DSH-001, a panel disappears for a user who
    /// cannot open the screen behind it), orderable per role through
    /// <c>LayoutTemplate</c>, and personalizable per user through <c>UserLayout</c>
    /// (BR-DSH-003).
    /// </para>
    /// <para>
    /// <b>Scope: 6 of the specification's 78, and deliberately so.</b> A widget with
    /// no computation behind it is worse than no widget — it advertises a number the
    /// dashboard cannot produce. Only the eight built-in panels compute anything
    /// today, and only six of them have a catalogued screen whose permission can
    /// gate them. The two that stay unregistered:
    /// <list type="bullet">
    /// <item><c>DSH-CRT-001</c> (certificate requests) — module 18 has no screens, so
    /// there is no certificate permission to gate it with.</item>
    /// <item><c>DSH-DIS-001</c> (restricted medical and discipline counts) — same, for
    /// module 24; binding the most sensitive panel on the dashboard to a stand-in
    /// permission is the one mistake worth avoiding most.</item>
    /// </list>
    /// Both keep working as built-in panels meanwhile, which is the documented
    /// fallback, and both should be registered here the moment their module's screens
    /// land. The remaining 72 widgets in the specification are Phase 9 content work
    /// waiting on <see cref="IDashboardQuery"/> to grow computations for them.
    /// </para>
    /// <para>
    /// <b>The layouts.</b> Four personas out of the specification's seven get a
    /// template, for the same reason: a persona whose seeded grants do not include a
    /// single registered widget would get an empty template. Teacher is the notable
    /// absence — the teacher persona's dashboard <em>is</em> the "My classes"
    /// workspace (M13 §8.6), which does not exist yet, and the seeded TEACHER role
    /// holds <c>Attendance/Capture</c> rather than <c>Attendance/Analytics</c>, so the
    /// attendance widget would be hidden from them anyway. Order follows the
    /// specification's own per-persona lists, keeping only the widgets that exist.
    /// </para>
    /// <para>
    /// <b>Defaults, not policy</b> — the rule the seeded role grants already follow. A
    /// role that has any template at all has been curated by the school, so re-running
    /// leaves it exactly as it is: a widget the school dropped from a template stays
    /// dropped, and a reordering survives.
    /// </para>
    /// </summary>
    public class WidgetRegistrySeedContributor : ISeedContributor
    {
        private readonly AppDbContext _db;
        private readonly IDashboardAdmin _dashboards;

        public WidgetRegistrySeedContributor(AppDbContext db, IDashboardAdmin dashboards)
        {
            _db = db;
            _dashboards = dashboards;
        }

        public string Name => "Dashboard widget registry and role layouts (docs/Reports/Dashboard-Specifications.md)";

        // With the report catalogue (35): both bind to the permissions seeded at 21,
        // and the widgets drill into the reports registered just before them.
        public int Order => 36;

        private sealed record Widget(
            string Code, string Module, string Screen, string TitleEn, string TitleAr,
            WidgetRefreshClass Refresh, string DrillTarget);

        /// <summary>
        /// Codes are <c>DashboardPanels</c>'s, matched exactly — a definition whose code
        /// does not match a built-in panel governs nothing. Titles are the panels' own,
        /// so the registry screen and the dashboard name the same thing.
        /// </summary>
        private static readonly Widget[] Widgets =
        {
            new("DSH-ATT-001", ScreenCatalog.Modules.Attendance, ScreenCatalog.Attendance.Analytics,
                "Attendance today", "حضور اليوم", WidgetRefreshClass.Cached15Min, "RPT-ATD-001"),

            new("DSH-FEE-001", ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Position,
                "Receivables outstanding", "الذمم المستحقة", WidgetRefreshClass.Daily, "RPT-FEE-004"),

            new("DSH-INS-001", ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Schedule,
                "Collection calendar", "تقويم التحصيل", WidgetRefreshClass.Daily, "RPT-INS-001"),

            new("DSH-GRD-001", ScreenCatalog.Modules.Grades, ScreenCatalog.Grades.Grades_,
                "Seats and pipeline", "المقاعد وخط القبول", WidgetRefreshClass.Daily, "GRD/Grades"),

            new("DSH-TCH-001", ScreenCatalog.Modules.Teachers, ScreenCatalog.Teachers.Load,
                "Teacher load", "نصاب المعلمين", WidgetRefreshClass.Daily, "TCH/Load"),

            // doc/Modules/01 §11 — the Sys Admin figure. Gated on the wizard's own
            // permission: whoever cannot open setup has no business being told how
            // much of it is left.
            new("DSH-SET-001", ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Wizard,
                "Setup completeness", "اكتمال الإعداد", WidgetRefreshClass.Live, "SET/Wizard"),
        };

        /// <summary>
        /// The panel codes this contributor registers. Public because the coupling is
        /// the whole mechanism: a definition whose code does not match a built-in
        /// panel governs nothing, and only <c>Sms.Web</c> knows what the panels are —
        /// so the check lives in <c>Sms.Web.Tests</c> and reads this list.
        /// </summary>
        public static readonly IReadOnlyList<string> RegisteredPanelCodes =
            Array.AsReadOnly(Array.ConvertAll(Widgets, w => w.Code));

        /// <summary>
        /// docs/Reports/Dashboard-Specifications.md §1–4, reduced to the registered
        /// widgets and keeping the document's ordering. Sort orders step by 10 so a
        /// school can slot a widget between two without renumbering the row.
        /// </summary>
        private static readonly (string RoleCode, string[] WidgetCodes)[] Layouts =
        {
            // §1 Principal: attendance, then enrollment, then the money, then the staff.
            ("PRINCIPAL", new[] { "DSH-ATT-001", "DSH-GRD-001", "DSH-FEE-001", "DSH-INS-001", "DSH-TCH-001" }),

            // §2 Vice Principal: coverage and conduct — attendance first, load second.
            ("VICE_PRINCIPAL", new[] { "DSH-ATT-001", "DSH-TCH-001" }),

            // §3 Registrar: the pipeline. Their seeded grants do not include attendance.
            ("REGISTRAR", new[] { "DSH-GRD-001" }),

            // §4 Finance manager: what is owed, then when it lands.
            ("FINANCE_MANAGER", new[] { "DSH-FEE-001", "DSH-INS-001" }),

            // Not one of the specification's seven personas — doc/Modules/01 §11 asks
            // for a Sys Admin dashboard of its own, and its first figure is how much
            // of the setup is left. Nobody else needs it on their morning screen.
            ("SYSADMIN", new[] { "DSH-SET-001" }),
        };

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            if (!await _db.Schools.AnyAsync(cancellationToken))
            {
                return;
            }

            var byCode = await SeedWidgetsAsync(cancellationToken);
            await SeedLayoutsAsync(byCode, cancellationToken);
        }

        private async Task<Dictionary<string, int>> SeedWidgetsAsync(CancellationToken cancellationToken)
        {
            // IgnoreQueryFilters so a widget the school switched off is still seen as
            // present — re-running must not bring it back.
            var existing = await _db.WidgetDefinitions.IgnoreQueryFilters().AsNoTracking()
                .Where(w => w.SchoolId == _db.CurrentSchoolId)
                .Select(w => new { w.Id, w.Code })
                .ToListAsync(cancellationToken);
            var byCode = existing.ToDictionary(w => w.Code, w => w.Id, StringComparer.OrdinalIgnoreCase);

            var permissions = await _db.Permissions.AsNoTracking()
                .Where(p => p.Action == ActionVerb.View)
                .Select(p => new { p.Id, p.ModuleCode, p.ScreenCode })
                .ToListAsync(cancellationToken);
            var gates = permissions.ToDictionary(
                p => p.ModuleCode + "/" + p.ScreenCode, p => p.Id, StringComparer.OrdinalIgnoreCase);

            foreach (var widget in Widgets)
            {
                if (byCode.ContainsKey(widget.Code))
                {
                    continue;
                }

                if (!gates.TryGetValue(widget.Module + "/" + widget.Screen, out var permissionId))
                {
                    continue;
                }

                // Staff dashboard only: BR-DSH-006 keeps every one of these off the
                // portal, which serves published data through its own home instead.
                var defined = await _dashboards.DefineWidgetAsync(
                    widget.Code, widget.Module, widget.TitleAr, widget.TitleEn, permissionId,
                    widget.Refresh, widget.DrillTarget, isPortalEligible: false, cancellationToken);

                byCode[widget.Code] = defined.Id;
                _db.ChangeTracker.Clear();
            }

            return byCode;
        }

        private async Task SeedLayoutsAsync(IReadOnlyDictionary<string, int> widgetIds, CancellationToken cancellationToken)
        {
            foreach (var (roleCode, widgetCodes) in Layouts)
            {
                var role = await _db.Roles.AsNoTracking()
                    .SingleOrDefaultAsync(r => r.Code == roleCode, cancellationToken);
                if (role == null)
                {
                    continue;
                }

                // Defaults, not policy — the same rule the role grants follow. A role
                // that already has a template has been curated by the school, and a
                // widget it dropped stays dropped; only first provisioning fills one in.
                if (await _db.LayoutTemplates.AsNoTracking().AnyAsync(t => t.RoleId == role.Id, cancellationToken))
                {
                    continue;
                }

                var template = await _dashboards.DefineLayoutTemplateAsync(role.Id, cancellationToken);
                _db.ChangeTracker.Clear();

                var sortOrder = 0;
                foreach (var code in widgetCodes)
                {
                    if (!widgetIds.TryGetValue(code, out var widgetId))
                    {
                        continue;
                    }

                    sortOrder += 10;
                    await _dashboards.AddWidgetToTemplateAsync(template.Id, widgetId, sortOrder, cancellationToken);
                    _db.ChangeTracker.Clear();
                }
            }
        }
    }
}
