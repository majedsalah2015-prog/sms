using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Application.Reports;
using Sms.Application.Security;
using Sms.Domain.Grades;
using Sms.Domain.Reports;
using Sms.Domain.Security;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/30 §8 — screens over <see cref="IReportAdmin"/>, whose engine
    /// has been complete since E-701: 8.1 Report center (the registry, filtered
    /// and search-able, with what each report will let *you* do), 8.2 Report
    /// runner (parameter bar, inline vs. queued, per-report history), 8.3
    /// Subscription manager, 8.4 Execution log.
    /// <para>
    /// This module is the reporting <em>platform</em>, not the reports: the 150+
    /// catalog reports are the Phase 9 deliverable and no definition ships with
    /// the product, so the report center starts empty and is filled by the
    /// register form. Nothing here renders report <em>content</em> — a run
    /// records who asked for what, under which permission, at what size; there
    /// is no query behind a definition to execute yet (T-5, doc §14).
    /// </para>
    /// <para>
    /// Four pure gates decide what these screens may offer, and each is
    /// evaluated here before the form is drawn rather than only caught on
    /// submit: <see cref="ExportPermissionGate"/> (BR-RPT-003) disables the
    /// export option, <see cref="RestrictedDeliveryGate"/> (BR-RPT-003) removes
    /// Email from the delivery picker, <see cref="RequiredParameterEvaluator"/>
    /// (doc §9) marks and pre-validates the mandatory parameters, and
    /// <see cref="HeavyReportQueueEvaluator"/> (BR-RPT-005) tells the operator
    /// the run will queue before they start it. The engine re-checks all four —
    /// a disabled control is a courtesy, not a boundary.
    /// </para>
    /// </summary>
    [Route("reports")]
    public class ReportsController : Controller
    {
        /// <summary>
        /// BR-RPT-005's "config threshold". It is a constant rather than a
        /// system setting because <c>SettingKeys</c> (Sms.Application) registers
        /// no reporting key and inventing one belongs to the engine's epic, not
        /// to its screens. Passed explicitly on every run so the number the
        /// runner shows and the number the engine applies cannot drift — the
        /// engine's own default happens to be the same 5000.
        /// </summary>
        public const int HeavyRowThreshold = 5000;

        private const int HistoryPageSize = 300;

        private readonly IReportAdmin _reports;
        private readonly AppDbContext _db;
        private readonly IPermissionService _permissions;
        private readonly IUserAccountDirectory _users;
        private readonly IWorkingYearContext _workingYear;
        private readonly ICurrentUser _user;

        public ReportsController(
            IReportAdmin reports, AppDbContext db, IPermissionService permissions,
            IUserAccountDirectory users, IWorkingYearContext workingYear, ICurrentUser user)
        {
            _reports = reports;
            _db = db;
            _permissions = permissions;
            _users = users;
            _workingYear = workingYear;
            _user = user;
        }

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        private CancellationToken Ct => HttpContext.RequestAborted;

        // ================================================================== 8.1 Report center

        [HttpGet("")]
        [RequirePermission(ScreenCatalog.Modules.Reports, ScreenCatalog.Reports.Catalog, ActionVerb.View)]
        public async Task<IActionResult> Index(string? q = null, string? module = null, ReportSensitivity? sensitivity = null)
        {
            var definitions = await _db.ReportDefinitions.AsNoTracking().OrderBy(d => d.Code).ToListAsync(Ct);

            var m = new ReportCenterViewModel
            {
                Q = q,
                ModuleCode = module,
                Sensitivity = sensitivity,
                UsedModuleCodes = definitions.Select(d => d.OwningModuleCode).Distinct().OrderBy(c => c, StringComparer.Ordinal).ToList(),
                Permissions = await _db.Permissions.AsNoTracking()
                    .OrderBy(p => p.ModuleCode).ThenBy(p => p.ScreenCode).ThenBy(p => p.Action).ToListAsync(Ct),
            };

            var filtered = definitions.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(module))
            {
                filtered = filtered.Where(d => string.Equals(d.OwningModuleCode, module, StringComparison.OrdinalIgnoreCase));
            }

            if (sensitivity != null)
            {
                filtered = filtered.Where(d => d.Sensitivity == sensitivity);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                // In memory, like every other search in this codebase: an OrdinalIgnoreCase Contains does
                // not translate to SQL, and the registry is a few hundred rows at its Phase 9 maximum.
                var term = q.Trim();
                filtered = filtered.Where(d =>
                    d.Code.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || d.TitleEn.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || d.TitleAr.Contains(term));
            }

            var rows = filtered.ToList();
            var permissionsById = m.Permissions.ToDictionary(p => p.Id);

            var stats = await _db.ReportExecutions.AsNoTracking()
                .GroupBy(e => e.ReportDefinitionId)
                .Select(g => new { DefinitionId = g.Key, Count = g.Count(), LastId = g.Max(e => e.Id) })
                .ToListAsync(Ct);
            var lastRunIds = stats.Select(s => s.LastId).ToList();
            var lastRuns = await _db.ReportExecutions.AsNoTracking().Where(e => lastRunIds.Contains(e.Id)).ToListAsync(Ct);

            var subscriptionCounts = await _db.ReportSubscriptions.AsNoTracking()
                .Where(s => s.IsActive)
                .GroupBy(s => s.ReportDefinitionId)
                .Select(g => new { DefinitionId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.DefinitionId, x => x.Count, Ct);

            var built = new List<ReportCenterViewModel.Row>();
            foreach (var definition in rows)
            {
                permissionsById.TryGetValue(definition.PermissionId, out var permission);
                var stat = stats.FirstOrDefault(s => s.DefinitionId == definition.Id);
                built.Add(new ReportCenterViewModel.Row(
                    definition,
                    await AccessAsync(definition, permission),
                    RequiredParameterEvaluator.ParseRequiredKeys(definition.RequiredParameterKeysCsv),
                    stat?.Count ?? 0,
                    stat == null ? null : lastRuns.FirstOrDefault(e => e.Id == stat.LastId),
                    subscriptionCounts.TryGetValue(definition.Id, out var subs) ? subs : 0));
            }

            m.Rows = built;
            return View(m);
        }

        [HttpPost("definitions")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Reports, ScreenCatalog.Reports.Catalog, ActionVerb.Configure)]
        public async Task<IActionResult> CreateDefinition(
            string code, string owningModuleCode, string titleAr, string titleEn,
            OutputFormat[] formats, ReportSensitivity sensitivity, int permissionId,
            string[]? requiredStandardKeys, string? requiredOtherKeys)
        {
            code = code?.Trim() ?? string.Empty;
            try
            {
                if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(titleAr) || string.IsNullOrWhiteSpace(titleEn))
                {
                    throw new InvalidOperationException(T("A code and both titles are required.", "الرمز والعنوانان مطلوبة."));
                }

                if (formats == null || formats.Length == 0)
                {
                    throw new InvalidOperationException(T("Pick at least one output format (BR-RPT-001).", "اختر صيغة إخراج واحدة على الأقل (BR-RPT-001)."));
                }

                if (!await _db.Permissions.AnyAsync(p => p.Id == permissionId, Ct))
                {
                    throw new InvalidOperationException(T("Pick the permission that gates this report.", "اختر الصلاحية التي تحكم هذا التقرير."));
                }

                // The unique (SchoolId, Code) index would otherwise surface as a raw DbUpdateException;
                // the catalog code is operator-typed, so a clash is an ordinary mistake, not a fault.
                if (await _db.ReportDefinitions.IgnoreQueryFilters()
                        .AnyAsync(d => d.SchoolId == _db.CurrentSchoolId && d.Code == code, Ct))
                {
                    throw new InvalidOperationException(T($"Report code {code} is already registered.", $"الرمز {code} مسجَّل مسبقاً."));
                }

                var required = (requiredStandardKeys ?? Array.Empty<string>())
                    .Concat((requiredOtherKeys ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    .Select(k => k.Trim())
                    .Where(k => k.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var definition = await _reports.DefineReportAsync(
                    code,
                    owningModuleCode?.Trim() ?? string.Empty,
                    titleAr.Trim(),
                    titleEn.Trim(),
                    formats.Aggregate((a, b) => a | b),
                    sensitivity,
                    permissionId,
                    required.Count == 0 ? null : string.Join(",", required),
                    Ct);

                TempData["Flash"] = T($"Report {definition.Code} registered.", $"سُجِّل التقرير {definition.Code}.");
                return RedirectToAction(nameof(Run), new { id = definition.Id });
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
                return RedirectToAction(nameof(Index));
            }
        }

        // ================================================================== 8.2 Report runner

        [HttpGet("{id:int}/run")]
        [RequirePermission(ScreenCatalog.Modules.Reports, ScreenCatalog.Reports.Catalog, ActionVerb.View)]
        public async Task<IActionResult> Run(int id, int estimatedRowCount = 0)
        {
            var definition = await _db.ReportDefinitions.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id, Ct);
            if (definition == null)
            {
                return NotFound();
            }

            var permission = await _db.Permissions.AsNoTracking().SingleOrDefaultAsync(p => p.Id == definition.PermissionId, Ct);
            var required = RequiredParameterEvaluator.ParseRequiredKeys(definition.RequiredParameterKeysCsv);
            var requiredSet = new HashSet<string>(required, StringComparer.OrdinalIgnoreCase);

            var yearId = _workingYear.AcademicYearId;
            var grades = await _db.GradeLevels.AsNoTracking().OrderBy(g => g.SequenceOrder).ToListAsync(Ct);
            var sections = await _db.Sections.AsNoTracking()
                .Where(s => s.AcademicYearId == yearId).OrderBy(s => s.NameEn).ToListAsync(Ct);
            var profiles = await _db.GradeYearProfiles.AsNoTracking().Where(p => p.AcademicYearId == yearId).ToListAsync(Ct);
            var gradesById = grades.ToDictionary(g => g.Id);

            var m = new ReportRunViewModel
            {
                Definition = definition,
                Access = await AccessAsync(definition, permission),
                RequiredKeys = required,
                Years = await _db.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartDate).ToListAsync(Ct),
                Grades = grades,
                Sections = sections,
                SectionGradeNames = sections.ToDictionary(s => s.Id, s => GradeNameFor(s.GradeYearProfileId, profiles, gradesById)),
                HeavyRowThreshold = HeavyRowThreshold,
                EstimatedRowCount = estimatedRowCount,
                WorkingYearId = yearId,
                WouldQueue = HeavyReportQueueEvaluator.ShouldQueue(estimatedRowCount, HeavyRowThreshold),

                // A required key the definition invented has no bilingual name anywhere — the key IS the
                // label until Phase 9 gives parameters a typed, titled definition of their own.
                Fields = StandardReportParameters.Fields
                    .Select(f => f with { IsRequired = requiredSet.Contains(f.Key) })
                    .Concat(required
                        .Where(k => !StandardReportParameters.IsStandard(k))
                        .Select(k => new ReportParameterField(k, k, k, ReportParameterKind.Text, true)))
                    .ToList(),
            };

            var history = await _db.ReportExecutions.AsNoTracking()
                .Where(e => e.ReportDefinitionId == id)
                .OrderByDescending(e => e.Id).Take(50).ToListAsync(Ct);
            var names = await UserNamesAsync(history.Select(e => e.ExecutedByUserId));
            m.History = history
                .Select(e => new ReportRunViewModel.HistoryRow(e, NameOf(names, e.ExecutedByUserId)))
                .ToList();

            return View(m);
        }

        [HttpPost("{id:int}/run")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Reports, ScreenCatalog.Reports.Catalog, ActionVerb.Export)]
        public async Task<IActionResult> Execute(
            int id, string[]? paramKeys, string[]? paramValues,
            OutputFormat format, bool isExport, int estimatedRowCount)
        {
            var definition = await _db.ReportDefinitions.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id, Ct);
            if (definition == null)
            {
                return NotFound();
            }

            var permission = await _db.Permissions.AsNoTracking().SingleOrDefaultAsync(p => p.Id == definition.PermissionId, Ct);
            var access = await AccessAsync(definition, permission);
            var parameters = Pairs(paramKeys, paramValues);
            var suppliedKeys = parameters.Select(p => p.Key).ToList();

            try
            {
                if (access.PermissionMissing)
                {
                    throw new InvalidOperationException(T(
                        "This report names a permission that no longer exists; it cannot run until the registry entry is repaired.",
                        "يشير هذا التقرير إلى صلاحية لم تعد موجودة؛ لا يمكن تشغيله حتى يُصحَّح تسجيله."));
                }

                if (!access.HoldsViewPermission)
                {
                    throw new InvalidOperationException(T(
                        "You do not hold the View permission this report is gated on (BR-RPT-002).",
                        "لا تملك صلاحية العرض التي يُحكم بها هذا التقرير (BR-RPT-002)."));
                }

                if (!definition.SupportedFormats.HasFlag(format))
                {
                    throw new InvalidOperationException(T("That output format is not one this report supports.", "صيغة الإخراج هذه غير مدعومة في هذا التقرير."));
                }

                // Both of the next two rules are the engine's, re-stated here only to say them in the
                // operator's language and name the offending keys; ReportAdmin refuses either way.
                if (isExport && !access.ExportAllowed)
                {
                    throw new InvalidOperationException(T(
                        $"A {ReportLabels.Sensitivity(definition.Sensitivity, false).ToLowerInvariant()} report needs the Export permission for file output; you may still view it on screen (BR-RPT-003).",
                        $"إخراج ملف من تقرير مصنّف «{ReportLabels.Sensitivity(definition.Sensitivity, true)}» يتطلب صلاحية التصدير؛ ويبقى عرضه على الشاشة متاحاً (BR-RPT-003)."));
                }

                var missing = RequiredParameterEvaluator.FindMissing(
                    RequiredParameterEvaluator.ParseRequiredKeys(definition.RequiredParameterKeysCsv), suppliedKeys);
                if (missing.Count > 0)
                {
                    throw new InvalidOperationException(T(
                        $"Required parameter(s) missing: {string.Join(", ", missing)}.",
                        $"معاملات مطلوبة ناقصة: {string.Join("، ", missing)}."));
                }

                var execution = await _reports.RunReportAsync(
                    id, _user.UserId, ReportParameters.ToJson(parameters), suppliedKeys,
                    format, isExport, estimatedRowCount, HeavyRowThreshold, Ct);

                TempData["Flash"] = execution.Status == ReportExecutionStatus.Queued
                    ? T($"Run #{execution.Id} queued — {estimatedRowCount:N0} rows is at or above the {HeavyRowThreshold:N0}-row threshold. You will be notified when it is ready (BR-RPT-005).",
                        $"أُدرج التشغيل رقم {execution.Id} في الطابور — الحجم المقدَّر {estimatedRowCount:N0} صف يبلغ حدّ {HeavyRowThreshold:N0} صف أو يتجاوزه. وسيصلك إشعار عند الجاهزية (BR-RPT-005).")
                    : T($"Run #{execution.Id} completed inline.", $"اكتمل التشغيل رقم {execution.Id} مباشرةً.");
            }
            catch (InvalidOperationException ex)
            {
                // Catches the engine's own ReportPermissionDenied / MissingRequiredParameters /
                // ReportExportNotAllowed too — they all derive from InvalidOperationException, and their
                // messages are the last word when a screen's pre-check and the engine disagree.
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Run), new { id, estimatedRowCount });
        }

        [HttpPost("executions/{id:int}/complete")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Reports, ScreenCatalog.Reports.Executions, ActionVerb.Edit)]
        public async Task<IActionResult> CompleteRun(int id, int rowCount, int durationMs, string? returnUrl)
        {
            try
            {
                var execution = await _reports.CompleteQueuedRunAsync(id, Math.Max(0, rowCount), Math.Max(0, durationMs), Ct);
                TempData["Flash"] = T(
                    $"Queued run #{execution.Id} marked ready: {rowCount:N0} rows in {durationMs:N0} ms.",
                    $"وُسم التشغيل رقم {execution.Id} بالجاهزية: {rowCount:N0} صف خلال {durationMs:N0} مللي ثانية.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return Back(returnUrl, nameof(Log));
        }

        // ================================================================== 8.3 Subscription manager

        [HttpGet("subscriptions")]
        [RequirePermission(ScreenCatalog.Modules.Reports, ScreenCatalog.Reports.Subscriptions, ActionVerb.View)]
        public async Task<IActionResult> Subscriptions(int? reportId = null, bool showCancelled = false)
        {
            var query = _db.ReportSubscriptions.AsNoTracking().AsQueryable();
            if (!showCancelled)
            {
                query = query.Where(s => s.IsActive);
            }

            var subscriptions = await query.OrderByDescending(s => s.Id).Take(HistoryPageSize).ToListAsync(Ct);

            // Definitions are soft-active filtered; a subscription to a retired report must still name it.
            var definitions = await _db.ReportDefinitions.IgnoreQueryFilters().AsNoTracking()
                .Where(d => d.SchoolId == _db.CurrentSchoolId).OrderBy(d => d.Code).ToListAsync(Ct);
            var permissionsById = await PermissionsForAsync(definitions.Select(d => d.PermissionId));
            var names = await UserNamesAsync(subscriptions.Select(s => s.SubscriberUserId));
            var grants = await ViewGrantsAsync(subscriptions.Select(s => s.SubscriberUserId));

            var m = new ReportSubscriptionsViewModel
            {
                ShowCancelled = showCancelled,
                Definitions = definitions.Where(d => d.IsActive).ToList(),
                Rows = subscriptions.Select(s =>
                {
                    var definition = definitions.FirstOrDefault(d => d.Id == s.ReportDefinitionId);
                    Permission? permission = null;
                    if (definition != null)
                    {
                        permissionsById.TryGetValue(definition.PermissionId, out permission);
                    }

                    // BR-RPT-006's second half — "and at each send (revocation-safe)" — has no dispatch job
                    // to enforce it (E-011). Until it exists, the oversight list is where a subscription
                    // whose recipient has since lost the permission becomes visible.
                    var stillAuthorized = permission != null
                        && grants.Contains((s.SubscriberUserId, permission.ModuleCode, permission.ScreenCode));

                    return new ReportSubscriptionsViewModel.Row(s, definition, NameOf(names, s.SubscriberUserId), stillAuthorized);
                }).ToList(),
            };

            if (reportId is int selectedId)
            {
                m.Selected = definitions.FirstOrDefault(d => d.Id == selectedId && d.IsActive);
            }

            if (m.Selected != null)
            {
                permissionsById.TryGetValue(m.Selected.PermissionId, out var selectedPermission);
                m.SelectedAccess = await AccessAsync(m.Selected, selectedPermission);
                m.SelectedRequiredKeys = RequiredParameterEvaluator.ParseRequiredKeys(m.Selected.RequiredParameterKeysCsv);

                var candidates = await _users.ListAsync(activeOnly: true, Ct);
                var authorizedIds = selectedPermission == null
                    ? new HashSet<int>()
                    : await AuthorizedUserIdsAsync(selectedPermission);
                m.AuthorizedSubscribers = candidates.Where(u => authorizedIds.Contains(u.Id)).ToList();
                m.UnauthorizedCount = candidates.Count - m.AuthorizedSubscribers.Count;
            }

            return View(m);
        }

        [HttpPost("subscriptions")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Reports, ScreenCatalog.Reports.Subscriptions, ActionVerb.Create)]
        public async Task<IActionResult> Subscribe(
            int reportDefinitionId, int subscriberUserId, SubscriptionFrequency frequency,
            OutputFormat format, DeliveryChannel deliveryChannel, string[]? paramKeys, string[]? paramValues)
        {
            var definition = await _db.ReportDefinitions.AsNoTracking().SingleOrDefaultAsync(d => d.Id == reportDefinitionId, Ct);
            if (definition == null)
            {
                return NotFound();
            }

            try
            {
                if (!definition.SupportedFormats.HasFlag(format))
                {
                    throw new InvalidOperationException(T("That output format is not one this report supports.", "صيغة الإخراج هذه غير مدعومة في هذا التقرير."));
                }

                if (!RestrictedDeliveryGate.IsChannelAllowed(definition.Sensitivity, deliveryChannel))
                {
                    throw new InvalidOperationException(T(
                        "A restricted report is delivered to the portal only — it never schedules to email (BR-RPT-003).",
                        "التقرير المقيَّد يُسلَّم عبر البوابة فقط — ولا يُجدوَل إلى البريد الإلكتروني إطلاقاً (BR-RPT-003)."));
                }

                if (await _db.ReportSubscriptions.AnyAsync(
                        s => s.ReportDefinitionId == reportDefinitionId && s.SubscriberUserId == subscriberUserId && s.IsActive, Ct))
                {
                    throw new InvalidOperationException(T(
                        "That user already has an active subscription to this report.",
                        "لدى هذا المستخدم اشتراك فعّال في هذا التقرير بالفعل."));
                }

                await _reports.SubscribeAsync(
                    reportDefinitionId, subscriberUserId, frequency,
                    ReportParameters.ToJson(Pairs(paramKeys, paramValues)), format, deliveryChannel, Ct);
                TempData["Flash"] = T("Subscription saved.", "حُفظ الاشتراك.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }
            catch (DbUpdateException)
            {
                // The unique filtered index is the real guard against a double-submit racing the check above.
                TempData["Error"] = T("That user already has an active subscription to this report.", "لدى هذا المستخدم اشتراك فعّال في هذا التقرير بالفعل.");
            }

            return RedirectToAction(nameof(Subscriptions), new { reportId = reportDefinitionId });
        }

        [HttpPost("subscriptions/{id:int}/cancel")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Reports, ScreenCatalog.Reports.Subscriptions, ActionVerb.Deactivate)]
        public async Task<IActionResult> CancelSubscription(int id)
        {
            if (!await _db.ReportSubscriptions.AnyAsync(s => s.Id == id, Ct))
            {
                return NotFound();
            }

            await _reports.CancelSubscriptionAsync(id, Ct);
            TempData["Flash"] = T("Subscription cancelled.", "أُلغي الاشتراك.");
            return RedirectToAction(nameof(Subscriptions), new { showCancelled = true });
        }

        // ================================================================== 8.4 Execution log

        [HttpGet("log")]
        [RequirePermission(ScreenCatalog.Modules.Reports, ScreenCatalog.Reports.Executions, ActionVerb.View)]
        public async Task<IActionResult> Log(
            int? reportId = null, ReportExecutionStatus? status = null, bool exportsOnly = false,
            int? userId = null, DateTime? from = null, DateTime? to = null)
        {
            var query = _db.ReportExecutions.AsNoTracking().AsQueryable();
            if (reportId != null)
            {
                query = query.Where(e => e.ReportDefinitionId == reportId);
            }

            if (status != null)
            {
                query = query.Where(e => e.Status == status);
            }

            if (exportsOnly)
            {
                query = query.Where(e => e.WasExport);
            }

            if (userId != null)
            {
                query = query.Where(e => e.ExecutedByUserId == userId);
            }

            if (from != null)
            {
                query = query.Where(e => e.ExecutedAtUtc >= from.Value.Date);
            }

            if (to != null)
            {
                // Inclusive of the whole end day: an auditor picking one date means that date, not midnight.
                var end = to.Value.Date.AddDays(1);
                query = query.Where(e => e.ExecutedAtUtc < end);
            }

            var executions = await query.OrderByDescending(e => e.Id).Take(HistoryPageSize).ToListAsync(Ct);
            var definitions = await _db.ReportDefinitions.IgnoreQueryFilters().AsNoTracking()
                .Where(d => d.SchoolId == _db.CurrentSchoolId).OrderBy(d => d.Code).ToListAsync(Ct);
            var names = await UserNamesAsync(executions.Select(e => e.ExecutedByUserId));

            var m = new ReportLogViewModel
            {
                ReportDefinitionId = reportId,
                Status = status,
                ExportsOnly = exportsOnly,
                UserId = userId,
                From = from,
                To = to,
                Definitions = definitions,
                Users = await _users.ListAsync(activeOnly: false, Ct),
                Rows = executions.Select(e => new ReportLogViewModel.Row(
                    e, definitions.FirstOrDefault(d => d.Id == e.ReportDefinitionId), NameOf(names, e.ExecutedByUserId))).ToList(),
            };

            return View(m);
        }

        // ================================================================== helpers

        /// <summary>
        /// The four gates' verdicts for one definition and the signed-in user.
        /// Resolved once per definition so a screen never asks the same question
        /// twice and never answers it differently from the engine.
        /// </summary>
        private async Task<ReportAccess> AccessAsync(ReportDefinition definition, Permission? permission)
        {
            // The referenced permission's own Action is deliberately ignored, as the engine ignores it:
            // gating reads View and Export off the row's (Module, Screen) pair (ReportDefinition's remark).
            var holdsView = permission != null
                && await _permissions.HasPermissionAsync(permission.ModuleCode, permission.ScreenCode, ActionVerb.View, Ct);
            var holdsExport = permission != null
                && await _permissions.HasPermissionAsync(permission.ModuleCode, permission.ScreenCode, ActionVerb.Export, Ct);

            return new ReportAccess(
                permission,
                holdsView,
                holdsExport,
                ExportPermissionGate.CanExport(definition.Sensitivity, holdsExport),
                RestrictedDeliveryGate.IsChannelAllowed(definition.Sensitivity, DeliveryChannel.Email));
        }

        private async Task<Dictionary<int, Permission>> PermissionsForAsync(IEnumerable<int> permissionIds)
        {
            var ids = permissionIds.Distinct().ToList();
            if (ids.Count == 0)
            {
                return new Dictionary<int, Permission>();
            }

            return await _db.Permissions.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, Ct);
        }

        /// <summary>Users holding View on this (Module, Screen) pair — the only people BR-RPT-006 lets subscribe.</summary>
        private async Task<HashSet<int>> AuthorizedUserIdsAsync(Permission permission)
        {
            var ids = await _db.RoleAssignments
                .Where(a => a.Role!.Permissions.Any(rp =>
                    rp.Permission!.ModuleCode == permission.ModuleCode
                    && rp.Permission!.ScreenCode == permission.ScreenCode
                    && rp.Permission!.Action == ActionVerb.View))
                .Select(a => a.UserAccountId)
                .Distinct()
                .ToListAsync(Ct);
            return new HashSet<int>(ids);
        }

        /// <summary>
        /// Every (user, module, screen) View grant these users hold, in one
        /// query — the subscription list needs the answer per row, and asking
        /// per row would be one round trip per subscription. Duplicates from
        /// overlapping roles collapse into the set rather than into a SQL
        /// DISTINCT, which keeps the translation a plain join.
        /// </summary>
        private async Task<HashSet<(int UserId, string ModuleCode, string ScreenCode)>> ViewGrantsAsync(IEnumerable<int> userIds)
        {
            var ids = userIds.Distinct().ToList();
            var set = new HashSet<(int, string, string)>();
            if (ids.Count == 0)
            {
                return set;
            }

            var rows = await _db.RoleAssignments
                .Where(a => ids.Contains(a.UserAccountId))
                .SelectMany(a => a.Role!.Permissions
                    .Where(rp => rp.Permission!.Action == ActionVerb.View)
                    .Select(rp => new { a.UserAccountId, rp.Permission!.ModuleCode, rp.Permission!.ScreenCode }))
                .ToListAsync(Ct);

            foreach (var row in rows)
            {
                set.Add((row.UserAccountId, row.ModuleCode, row.ScreenCode));
            }

            return set;
        }

        private async Task<IReadOnlyDictionary<int, UserAccountInfo>> UserNamesAsync(IEnumerable<int> userIds)
            => await _users.GetByIdsAsync(userIds.Distinct().ToList(), Ct);

        private static string NameOf(IReadOnlyDictionary<int, UserAccountInfo> names, int userId)
            => names.TryGetValue(userId, out var info) ? info.DisplayName ?? info.UserName : $"#{userId}";

        private static string GradeNameFor(int gradeYearProfileId, IReadOnlyList<GradeYearProfile> profiles, IReadOnlyDictionary<int, GradeLevel> gradesById)
        {
            // A section names a GradeYearProfile, not a grade — the profile is the grade as run in this
            // year — so the picker's label hops through it rather than assuming the section carries one.
            var profile = profiles.FirstOrDefault(p => p.Id == gradeYearProfileId);
            if (profile == null || !gradesById.TryGetValue(profile.GradeLevelId, out var grade))
            {
                return string.Empty;
            }

            return IsArabic ? grade.Name.NameAr : grade.Name.NameEn;
        }

        /// <summary>Blank values are dropped, so the keys this screen calls "supplied" are exactly the keys the engine will (doc §9).</summary>
        private static IReadOnlyList<KeyValuePair<string, string?>> Pairs(string[]? keys, string[]? values)
        {
            var pairs = new List<KeyValuePair<string, string?>>();
            for (var i = 0; i < (keys?.Length ?? 0); i++)
            {
                var value = values != null && i < values.Length ? values[i] : null;
                if (!string.IsNullOrWhiteSpace(keys![i]) && !string.IsNullOrWhiteSpace(value))
                {
                    pairs.Add(new KeyValuePair<string, string?>(keys[i].Trim(), value!.Trim()));
                }
            }

            return pairs;
        }

        private IActionResult Back(string? returnUrl, string fallbackAction)
            => !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(fallbackAction);
    }
}
