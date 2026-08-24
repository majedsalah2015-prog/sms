using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Discipline;
using Sms.Application.Security;
using Sms.Domain.Discipline;
using Sms.Domain.Security;
using Sms.Domain.Students;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/25 §8 — the six staff screens over <see cref="IDisciplineAdmin"/>, whose engine
    /// has been complete since E-503: 8.1 code designer, 8.2 quick-record, 8.3 case board, 8.4 case
    /// file, 8.5 action tracker, 8.6 analytics.
    /// <para>
    /// Nothing here decides anything the engine does not. Due process (BR-DCP-003), the ladder
    /// proposal and the deviation reason (BR-DCP-005), the Principal gate (BR-DCP-004), the pack's
    /// suspension cap and the independent appeal reviewer (BR-DCP-006) are all enforced behind
    /// <see cref="IDisciplineAdmin"/>. The screens' one job is to show those conditions <i>before</i>
    /// somebody runs into them — the decide form says a statement is missing and a Principal is
    /// needed while the decision is still being typed, rather than refusing it afterwards — and to
    /// translate the refusal when it comes anyway.
    /// </para>
    /// <para>
    /// The permission split is the module's own separation of powers, not a convenience:
    /// <c>Incidents</c> and <c>Cases</c> are separate screens because BR-DCP-002 opens recording to
    /// every teacher while BR-DCP-003 closes deciding to almost nobody, and one screen cannot hold
    /// both grants. A teacher therefore reaches the record desk and not the board.
    /// </para>
    /// <para>
    /// <b>Deferred, and why.</b> §8.7 portal (child behaviour view, parent appeal submission,
    /// handbook) needs <c>PortalController</c> and the BR-DCP-008 policy setting that decides how
    /// much a parent sees; the engine half is ready — <c>GetParentViewAsync</c> already projects it
    /// and already drops the reporter per BR-DCP-010 — so this is a portal task, not a discipline
    /// one. Evidence attachments (🔒 doc 10) are carried as ids through every call that takes one,
    /// but no upload widget is offered: the attachment UI is being built elsewhere and inventing a
    /// second one here would be the thing to undo later. Keep-apart pairs, behaviour-contract
    /// drafting and parent meetings have engine surface and no screen yet — the contract list is
    /// read on the action tracker, but nothing here creates one.
    /// </para>
    /// </summary>
    [Route("discipline")]
    public class DisciplineController : Controller
    {
        private readonly IDisciplineAdmin _discipline;
        private readonly AppDbContext _db;
        private readonly ICurrentUser _user;
        private readonly IClock _clock;
        private readonly IWorkingYearContext _year;

        public DisciplineController(
            IDisciplineAdmin discipline, AppDbContext db, ICurrentUser user, IClock clock, IWorkingYearContext year)
        {
            _discipline = discipline;
            _db = db;
            _user = user;
            _clock = clock;
            _year = year;
        }

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        // ================================================================== 8.3 case board

        [HttpGet("")]
        [RequirePermission(ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Cases, ActionVerb.View)]
        public async Task<IActionResult> Index(int? severity = null, bool closed = false)
        {
            var cases = await _db.DisciplineCases.AsNoTracking()
                .Where(c => closed || c.Status != CaseStatus.Closed)
                .Where(c => severity == null || c.Severity == severity)
                .OrderBy(c => c.Id)
                .ToListAsync();

            var incidentIds = cases.Select(c => c.IncidentId).ToList();
            var incidents = await _db.Incidents.AsNoTracking()
                .Where(i => incidentIds.Contains(i.Id)).ToListAsync();
            var violations = await ViolationLookupAsync(incidents.Select(i => i.ViolationTypeId));
            var consequences = await ConsequenceLookupAsync(cases.Select(c => c.ProposedConsequenceTypeId).Where(id => id.HasValue).Select(id => id!.Value));
            var students = await StudentLabelsAsync(cases.Select(c => c.StudentId));

            var caseIds = cases.Select(c => c.Id).ToList();
            var pendingAppeals = (await _db.Appeals.AsNoTracking()
                .Where(a => caseIds.Contains(a.DisciplineCaseId) && a.Outcome == AppealOutcome.Pending)
                .Select(a => a.DisciplineCaseId).ToListAsync()).ToHashSet();

            var now = _clock.UtcNow;
            var cards = cases.Select(c =>
            {
                var incident = incidents.FirstOrDefault(i => i.Id == c.IncidentId);
                violations.TryGetValue(incident?.ViolationTypeId ?? 0, out var violation);
                ConsequenceType? proposed = null;
                if (c.ProposedConsequenceTypeId.HasValue)
                {
                    consequences.TryGetValue(c.ProposedConsequenceTypeId.Value, out proposed);
                }

                // Age is measured from the incident, not the case row: a case that sat unopened for a
                // week is exactly what the board exists to surface, and the case's own timestamps
                // move every time somebody touches it.
                var since = incident?.OccurredAtUtc ?? now;
                return new CaseBoardViewModel.Card(
                    c,
                    students.TryGetValue(c.StudentId, out var label) ? label : UnknownStudent(),
                    violation?.NameAr ?? "—", violation?.NameEn ?? "—",
                    incident?.IncidentNo ?? "—",
                    Math.Max(0, (int)(now.Date - since.Date).TotalDays),
                    proposed?.NameAr, proposed?.NameEn,
                    pendingAppeals.Contains(c.Id));
            }).ToList();

            return View(new CaseBoardViewModel { Cards = cards, SeverityFilter = severity, IncludeClosed = closed });
        }

        // ================================================================== 8.4 case file

        [HttpGet("cases/{id:int}")]
        [RequirePermission(ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Cases, ActionVerb.View)]
        public async Task<IActionResult> Case(int id)
        {
            var model = await BuildCaseAsync(id);
            return model == null ? NotFound() : View(model);
        }

        [HttpPost("cases/{id:int}/investigate")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Cases, ActionVerb.Edit)]
        public async Task<IActionResult> StartInvestigation(int id)
        {
            try
            {
                await _discipline.StartInvestigationAsync(id, _user.UserId, HttpContext.RequestAborted);
                TempData["Flash"] = T("Investigation opened.", "فُتح التحقيق.");
            }
            catch (Exception ex)
            {
                TempData["Error"] = Translate(ex);
            }

            return RedirectToAction(nameof(Case), new { id });
        }

        [HttpPost("cases/{id:int}/statement")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Cases, ActionVerb.Edit)]
        public async Task<IActionResult> AddStatement(int id, StatementKind kind, string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                TempData["Error"] = T("A statement needs its text.", "الإفادة تحتاج نصّها.");
                return RedirectToAction(nameof(Case), new { id });
            }

            try
            {
                await _discipline.AddStatementAsync(id, kind, text.Trim(), cancellationToken: HttpContext.RequestAborted);
                TempData["Flash"] = T("Statement recorded.", "سُجِّلت الإفادة.");
            }
            catch (Exception ex)
            {
                TempData["Error"] = Translate(ex);
            }

            return RedirectToAction(nameof(Case), new { id });
        }

        [HttpPost("cases/{id:int}/decide")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Cases, ActionVerb.Approve)]
        public async Task<IActionResult> Decide(int id, int consequenceTypeId, string? articleRef, string? deviationReason, bool principalApproves = false)
        {
            try
            {
                // The Principal is the signed-in user when the box is ticked: the engine wants a user
                // id to record against the decision, and the only person who can tick it is the one
                // holding Approve on this screen (BR-DCP-004). A separate "which principal" picker
                // would let the decision name somebody who never saw it.
                await _discipline.DecideAsync(
                    id, consequenceTypeId, articleRef ?? string.Empty, _user.UserId,
                    string.IsNullOrWhiteSpace(deviationReason) ? null : deviationReason.Trim(),
                    principalApproves ? _user.UserId : null,
                    HttpContext.RequestAborted);
                TempData["Flash"] = T("Decision recorded and guardians notified.", "سُجِّل القرار وأُبلغ أولياء الأمر.");
            }
            catch (Exception ex)
            {
                TempData["Error"] = Translate(ex);
            }

            return RedirectToAction(nameof(Case), new { id });
        }

        [HttpPost("cases/{id:int}/action")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Cases, ActionVerb.Edit)]
        public async Task<IActionResult> ApplyAction(int id, DateTime startDate, int? days)
        {
            try
            {
                await _discipline.ApplyActionAsync(id, startDate, days, HttpContext.RequestAborted);
                TempData["Flash"] = T("Action applied — the appeal window is open.", "نُفِّذ الإجراء — ومهلة التظلّم مفتوحة.");
            }
            catch (Exception ex)
            {
                TempData["Error"] = Translate(ex);
            }

            return RedirectToAction(nameof(Case), new { id });
        }

        /// <summary>
        /// BR-DCP-006's counter path. The appeal itself is the parent's act and belongs on the portal
        /// (§8.7), but a parent who walks in with a letter still has to be able to file one — the same
        /// shape as attendance's "paper at the counter". The guardian is named explicitly because the
        /// engine records who appealed, and a staff member is not that person.
        /// </summary>
        [HttpPost("cases/{id:int}/appeal")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Cases, ActionVerb.Edit)]
        public async Task<IActionResult> FileAppeal(int id, int parentId, string? grounds)
        {
            if (string.IsNullOrWhiteSpace(grounds))
            {
                TempData["Error"] = T("An appeal needs its grounds.", "التظلّم يحتاج أسبابه.");
                return RedirectToAction(nameof(Case), new { id });
            }

            try
            {
                await _discipline.FileAppealAsync(id, parentId, grounds.Trim(), HttpContext.RequestAborted);
                TempData["Flash"] = T("Appeal filed.", "قُدِّم التظلّم.");
            }
            catch (Exception ex)
            {
                TempData["Error"] = Translate(ex);
            }

            return RedirectToAction(nameof(Case), new { id });
        }

        [HttpPost("cases/{id:int}/appeal/decide")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Cases, ActionVerb.Approve)]
        public async Task<IActionResult> DecideAppeal(int id, int appealId, AppealOutcome outcome, string? note, int? modifiedConsequenceTypeId)
        {
            try
            {
                await _discipline.DecideAppealAsync(
                    appealId, _user.UserId, outcome, string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                    outcome == AppealOutcome.Modified ? modifiedConsequenceTypeId : null,
                    HttpContext.RequestAborted);
                TempData["Flash"] = T("Appeal decided.", "بُتّ في التظلّم.");
            }
            catch (Exception ex)
            {
                TempData["Error"] = Translate(ex);
            }

            return RedirectToAction(nameof(Case), new { id });
        }

        [HttpPost("cases/{id:int}/close")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Cases, ActionVerb.Approve)]
        public async Task<IActionResult> CloseCase(int id)
        {
            try
            {
                await _discipline.CloseCaseAsync(id, HttpContext.RequestAborted);
                TempData["Flash"] = T("Case closed.", "أُغلقت القضية.");
            }
            catch (Exception ex)
            {
                TempData["Error"] = Translate(ex);
            }

            return RedirectToAction(nameof(Case), new { id });
        }

        // ================================================================== 8.2 quick record

        [HttpGet("record")]
        [RequirePermission(ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Incidents, ActionVerb.View)]
        public async Task<IActionResult> Record(int? section = null)
        {
            var model = new RecordBehaviorViewModel { SectionId = section, Today = _clock.UtcNow.Date };
            var code = await ActiveCodeAsync();
            model.HasPublishedCode = code is { IsPublished: true };
            if (code != null)
            {
                model.Violations = await _db.ViolationTypes.AsNoTracking()
                    .Where(v => v.BehaviorCodeId == code.Id).OrderBy(v => v.Severity).ThenBy(v => v.ArticleRef).ToListAsync();
                model.Merits = await _db.MeritTypes.AsNoTracking()
                    .Where(m => m.BehaviorCodeId == code.Id).OrderBy(m => m.NameEn).ToListAsync();
            }

            model.Sections = await _db.Sections.AsNoTracking()
                .Where(s => s.AcademicYearId == _year.AcademicYearId)
                .OrderBy(s => s.NameEn).ToListAsync();

            if (section.HasValue)
            {
                model.Roster = await RosterAsync(section.Value);
            }

            model.Recent = await RecentAsync();
            return View(model);
        }

        [HttpPost("record/incident")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Incidents, ActionVerb.Create)]
        public async Task<IActionResult> RecordIncident(int studentId, int violationTypeId, string? narrative, DateTime? occurredAt, int? section, bool notifyParent = true)
        {
            if (string.IsNullOrWhiteSpace(narrative))
            {
                TempData["Error"] = T("Say what happened — the narrative is the record.", "اكتب ما حدث — السرد هو السجلّ نفسه.");
                return RedirectToAction(nameof(Record), new { section });
            }

            try
            {
                var incident = await _discipline.RecordIncidentAsync(
                    studentId, violationTypeId, _user.UserId, narrative.Trim(),
                    occurredAt ?? _clock.UtcNow, await CurrentTermIdAsync(), evidenceAttachmentId: null,
                    notifyParent, HttpContext.RequestAborted);

                TempData["Flash"] = incident.CaseId.HasValue
                    ? T($"Incident {incident.IncidentNo} recorded — a case was opened.", $"سُجِّلت المخالفة {incident.IncidentNo} — وفُتحت قضية.")
                    : T($"Incident {incident.IncidentNo} recorded and resolved at teacher level.", $"سُجِّلت المخالفة {incident.IncidentNo} وعُولجت على مستوى المعلم.");
            }
            catch (Exception ex)
            {
                TempData["Error"] = Translate(ex);
            }

            return RedirectToAction(nameof(Record), new { section });
        }

        [HttpPost("record/merit")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Incidents, ActionVerb.Create)]
        public async Task<IActionResult> RecordMerit(int studentId, int meritTypeId, int? points, string? note, int? section)
        {
            try
            {
                // The one-tap merit of §8.2: no points box means "what this merit is worth", which is
                // the type's own award value.
                var type = await _db.MeritTypes.AsNoTracking().FirstOrDefaultAsync(m => m.Id == meritTypeId);
                if (type == null)
                {
                    TempData["Error"] = T("That merit is not in the code.", "هذا التميّز غير موجود في اللائحة.");
                    return RedirectToAction(nameof(Record), new { section });
                }

                await _discipline.RecordMeritAsync(
                    studentId, meritTypeId, points ?? type.Points, _user.UserId,
                    await CurrentTermIdAsync(), string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                    HttpContext.RequestAborted);
                TempData["Flash"] = T("Merit recorded.", "سُجِّل التميّز.");
            }
            catch (Exception ex)
            {
                TempData["Error"] = Translate(ex);
            }

            return RedirectToAction(nameof(Record), new { section });
        }

        // ================================================================== 8.1 code designer

        [HttpGet("code")]
        [RequirePermission(ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Code, ActionVerb.View)]
        public async Task<IActionResult> Code()
        {
            var model = new BehaviorCodeViewModel();
            var yearId = _year.AcademicYearId;

            // Superseded versions are deactivated rather than removed, and incidents keep pointing at
            // the version they were judged under — so the history reads through IgnoreQueryFilters,
            // not through the soft-active filter that hides everything but the current one.
            model.Versions = await _db.BehaviorCodes.IgnoreQueryFilters().AsNoTracking()
                .Where(c => c.SchoolId == _db.CurrentSchoolId && c.AcademicYearId == yearId)
                .OrderByDescending(c => c.Version).ToListAsync();
            model.Active = model.Versions.FirstOrDefault(c => c.IsActive);

            if (model.Active != null)
            {
                var codeId = model.Active.Id;
                model.Violations = await _db.ViolationTypes.AsNoTracking()
                    .Where(v => v.BehaviorCodeId == codeId).OrderBy(v => v.Severity).ThenBy(v => v.ArticleRef).ToListAsync();
                model.Merits = await _db.MeritTypes.AsNoTracking()
                    .Where(m => m.BehaviorCodeId == codeId).OrderBy(m => m.NameEn).ToListAsync();
                model.Consequences = await _db.ConsequenceTypes.AsNoTracking()
                    .Where(c => c.BehaviorCodeId == codeId).OrderBy(c => c.SeverityRank).ToListAsync();

                var steps = await _db.LadderSteps.AsNoTracking().Where(s => s.BehaviorCodeId == codeId).ToListAsync();
                model.Ladder = steps
                    .Select(s => (s, Consequence: model.Consequences.FirstOrDefault(c => c.Id == s.ConsequenceTypeId)))
                    .Where(x => x.Consequence != null)
                    .ToDictionary(x => (x.s.Severity, x.s.RepetitionCount), x => x.Consequence!);

                var violationIds = model.Violations.Select(v => v.Id).ToList();
                model.IncidentsRecorded = await _db.Incidents.CountAsync(i => violationIds.Contains(i.ViolationTypeId));
            }

            return View(model);
        }

        /// <summary>
        /// BR-DCP-001: a code is versioned, never edited in place — incidents already judged under a
        /// version must keep reading against the text they were judged by. So this always defines a
        /// new version and retires the previous one, which is what <c>DefineBehaviorCodeAsync</c>
        /// does; the form arrives pre-filled with the current version precisely so that "change one
        /// article" is not retyping the whole code.
        /// </summary>
        [HttpPost("code")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Code, ActionVerb.Configure)]
        public async Task<IActionResult> DefineCode(
            string? nameAr, string? nameEn, int? maxSuspensionDays, int appealWindowDays,
            string[]? vArticle, string[]? vNameAr, string[]? vNameEn, int[]? vSeverity, int[]? vPoints,
            string[]? mNameAr, string[]? mNameEn, int[]? mPoints, int[]? mMax,
            ConsequenceKind[]? cKind, string[]? cNameAr, string[]? cNameEn, int[]? cRank, bool[]? cSuspension,
            int[]? ladder)
        {
            var violations = Zip(vArticle, vNameAr, vNameEn, vSeverity, vPoints);
            var merits = ZipMerits(mNameAr, mNameEn, mPoints, mMax);

            // The ladder grid names a consequence by the row it was rendered in, and blank rows are
            // dropped on the way in — so the row index the form posted is not the index of the same
            // consequence in the list the engine receives. Carrying the map is the whole reason this
            // returns a pair: without it, clearing one consequence row silently re-points every
            // ladder step below it at a different punishment.
            var (consequences, rowToInput) = ZipConsequences(cKind, cNameAr, cNameEn, cRank, cSuspension);

            if (string.IsNullOrWhiteSpace(nameAr) || string.IsNullOrWhiteSpace(nameEn))
            {
                TempData["Error"] = T("The code needs a name in both languages.", "اللائحة تحتاج اسماً بالعربية والإنجليزية.");
                return RedirectToAction(nameof(Code));
            }

            if (violations.Count == 0 || consequences.Count == 0)
            {
                TempData["Error"] = T("A code needs at least one violation and one consequence to be usable.", "اللائحة تحتاج مخالفة واحدة وإجراءً واحداً على الأقل لتكون قابلة للاستخدام.");
                return RedirectToAction(nameof(Code));
            }

            // The grid posts one value per (severity, repetition) cell in a fixed order, -1 meaning
            // "no proposal at this step". A cell pointing at a row that was left blank has nothing to
            // propose, so it is dropped — never silently slid onto its neighbour.
            var steps = new List<LadderStepInput>();
            if (ladder != null)
            {
                var cell = 0;
                for (var severity = 1; severity <= BehaviorCodeViewModel.MaxSeverity; severity++)
                {
                    for (var repetition = 1; repetition <= BehaviorCodeViewModel.MaxRepetition; repetition++, cell++)
                    {
                        if (cell >= ladder.Length)
                        {
                            continue;
                        }

                        if (rowToInput.TryGetValue(ladder[cell], out var index))
                        {
                            steps.Add(new LadderStepInput(severity, repetition, index));
                        }
                    }
                }
            }

            try
            {
                var code = await _discipline.DefineBehaviorCodeAsync(
                    nameAr.Trim(), nameEn.Trim(), violations, merits, consequences, steps,
                    maxSuspensionDays, appealWindowDays <= 0 ? 7 : appealWindowDays, HttpContext.RequestAborted);
                TempData["Flash"] = T($"Version {code.Version} defined. Publish it to show families the handbook.", $"عُرِّفت النسخة {code.Version}. انشرها ليطّلع أولياء الأمور على اللائحة.");
            }
            catch (Exception ex)
            {
                TempData["Error"] = Translate(ex);
            }

            return RedirectToAction(nameof(Code));
        }

        [HttpPost("code/{id:int}/publish")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Code, ActionVerb.Approve)]
        public async Task<IActionResult> PublishCode(int id)
        {
            try
            {
                await _discipline.PublishBehaviorCodeAsync(id, HttpContext.RequestAborted);
                TempData["Flash"] = T("Code published to families.", "نُشرت اللائحة لأولياء الأمور.");
            }
            catch (Exception ex)
            {
                TempData["Error"] = Translate(ex);
            }

            return RedirectToAction(nameof(Code));
        }

        // ================================================================== 8.5 action tracker

        [HttpGet("actions")]
        [RequirePermission(ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Actions, ActionVerb.View)]
        public async Task<IActionResult> Actions(bool completed = false)
        {
            var today = _clock.UtcNow.Date;
            var actions = await _db.ActionsApplied.AsNoTracking()
                .Where(a => completed || a.CompletedAtUtc == null)
                .OrderBy(a => a.StartDate).ToListAsync();

            var consequences = await ConsequenceLookupAsync(actions.Select(a => a.ConsequenceTypeId));
            var caseIds = actions.Select(a => a.DisciplineCaseId).Distinct().ToList();
            var cases = await _db.DisciplineCases.AsNoTracking().Where(c => caseIds.Contains(c.Id)).ToListAsync();
            var students = await StudentLabelsAsync(cases.Select(c => c.StudentId));

            var rows = actions.Select(a =>
            {
                consequences.TryGetValue(a.ConsequenceTypeId, out var consequence);
                var owner = cases.FirstOrDefault(c => c.Id == a.DisciplineCaseId);
                var ends = a.Days.HasValue ? a.StartDate.AddDays(a.Days.Value) : (DateTime?)null;
                return new ActionTrackerViewModel.Row(
                    a,
                    owner != null && students.TryGetValue(owner.StudentId, out var label) ? label : UnknownStudent(),
                    consequence ?? new ConsequenceType { NameAr = "—", NameEn = "—" },
                    a.DisciplineCaseId,
                    ends,
                    a.CompletedAtUtc == null && ends.HasValue && ends.Value.Date < today);
            }).ToList();

            var contracts = await _db.BehaviorContracts.AsNoTracking()
                .Where(c => c.EndsOn == null || c.EndsOn >= today)
                .OrderBy(c => c.EndsOn).ToListAsync();
            var contractStudents = await StudentLabelsAsync(contracts.Select(c => c.StudentId));

            return View(new ActionTrackerViewModel
            {
                Today = today,
                ShowCompleted = completed,
                Detentions = rows.Where(r => r.Consequence.Kind == ConsequenceKind.Detention || r.Consequence.Kind == ConsequenceKind.CommunityService).ToList(),
                Suspensions = rows.Where(r => r.Consequence.IsSuspensionClass).ToList(),
                Other = rows.Where(r => r.Consequence.Kind != ConsequenceKind.Detention
                                     && r.Consequence.Kind != ConsequenceKind.CommunityService
                                     && !r.Consequence.IsSuspensionClass).ToList(),
                Contracts = contracts.Select(c => new ActionTrackerViewModel.ContractRow(
                    c,
                    contractStudents.TryGetValue(c.StudentId, out var label) ? label : UnknownStudent(),
                    c.ParentSignedAtUtc != null && c.StudentAcknowledgedAtUtc != null)).ToList(),
            });
        }

        [HttpPost("actions/{id:int}/complete")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Actions, ActionVerb.Edit)]
        public async Task<IActionResult> CompleteAction(int id, bool completed = false)
        {
            try
            {
                await _discipline.CompleteActionAsync(id, HttpContext.RequestAborted);
                TempData["Flash"] = T("Marked as served.", "سُجِّل التنفيذ.");
            }
            catch (Exception ex)
            {
                TempData["Error"] = Translate(ex);
            }

            return RedirectToAction(nameof(Actions), new { completed });
        }

        // ================================================================== 8.6 analytics

        [HttpGet("analytics")]
        [RequirePermission(ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Analytics, ActionVerb.View)]
        public async Task<IActionResult> Analytics(int? repeatThreshold = null)
        {
            var yearId = _year.AcademicYearId;
            var model = new BehaviorAnalyticsViewModel { RepeatThreshold = Math.Max(2, repeatThreshold ?? 2) };

            var incidents = await _db.Incidents.AsNoTracking().Where(i => i.AcademicYearId == yearId).ToListAsync();
            var violations = await ViolationLookupAsync(incidents.Select(i => i.ViolationTypeId));

            model.Violations = violations.Values
                .OrderBy(v => v.Severity).ThenBy(v => v.ArticleRef)
                .Select(v => new BehaviorAnalyticsViewModel.ViolationRow(v.Id, v.NameAr, v.NameEn, v.Severity))
                .ToList();

            // Grade comes through the enrollment's profile, and both the profile and the grade are
            // read past the soft-active filter: a grade retired mid-year still has to name the rows
            // recorded under it, or the heatmap loses a column and the totals stop adding up.
            var gradeByStudent = await GradeByStudentAsync(yearId);
            var grades = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking()
                .Where(g => g.SchoolId == _db.CurrentSchoolId).ToListAsync();
            var usedGradeIds = gradeByStudent.Values.Distinct().ToHashSet();
            model.Grades = grades.Where(g => usedGradeIds.Contains(g.Id))
                .OrderBy(g => g.SequenceOrder)
                .Select(g => new BehaviorAnalyticsViewModel.GradeColumn(g.Id, g.Name.NameAr, g.Name.NameEn))
                .ToList();

            var cells = new List<BehaviorAnalyticsViewModel.HeatCell>();
            foreach (var group in incidents
                .Where(i => gradeByStudent.ContainsKey(i.StudentId))
                .GroupBy(i => (Grade: gradeByStudent[i.StudentId], i.ViolationTypeId)))
            {
                cells.Add(new BehaviorAnalyticsViewModel.HeatCell(group.Key.Grade, group.Key.ViolationTypeId, group.Count()));
            }

            model.Cells = cells;
            model.MaxCell = cells.Count == 0 ? 0 : cells.Max(c => c.Count);
            model.ByHour = incidents.GroupBy(i => i.OccurredAtUtc.Hour)
                .OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count());

            var offenderStudents = incidents.GroupBy(i => i.StudentId)
                .Where(g => g.Count() >= model.RepeatThreshold)
                .Select(g => new
                {
                    StudentId = g.Key,
                    Incidents = g.Count(),
                    Points = g.Sum(i => violations.TryGetValue(i.ViolationTypeId, out var v) ? v.Points : 0),
                    Highest = g.Max(i => i.Severity),
                })
                .OrderByDescending(x => x.Points).ThenByDescending(x => x.Incidents)
                .Take(25).ToList();

            var merits = await _db.Merits.AsNoTracking().Where(m => m.AcademicYearId == yearId).ToListAsync();
            var meritLeaders = merits.GroupBy(m => m.StudentId)
                .Select(g => new { StudentId = g.Key, Awards = g.Count(), Points = g.Sum(m => m.Points) })
                .OrderByDescending(x => x.Points).Take(25).ToList();

            var labels = await StudentLabelsAsync(offenderStudents.Select(o => o.StudentId).Concat(meritLeaders.Select(m => m.StudentId)));
            model.RepeatOffenders = offenderStudents.Select(o => new BehaviorAnalyticsViewModel.OffenderRow(
                labels.TryGetValue(o.StudentId, out var l) ? l : UnknownStudent(), o.Incidents, o.Points, o.Highest)).ToList();
            model.MeritLeaders = meritLeaders.Select(m => new BehaviorAnalyticsViewModel.MeritRow(
                labels.TryGetValue(m.StudentId, out var l) ? l : UnknownStudent(), m.Awards, m.Points)).ToList();

            return View(model);
        }

        // ================================================================== shared reads

        private async Task<CaseFileViewModel?> BuildCaseAsync(int id)
        {
            var disciplineCase = await _db.DisciplineCases.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
            if (disciplineCase == null)
            {
                return null;
            }

            var incident = await _db.Incidents.AsNoTracking().FirstOrDefaultAsync(i => i.Id == disciplineCase.IncidentId);
            if (incident == null)
            {
                return null;
            }

            var violation = await _db.ViolationTypes.AsNoTracking().FirstOrDefaultAsync(v => v.Id == incident.ViolationTypeId);
            if (violation == null)
            {
                return null;
            }

            // The case is judged against the version it was opened under, which may since have been
            // superseded — so both the code and its consequence list read past the soft-active filter.
            var code = await _db.BehaviorCodes.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(c => c.SchoolId == _db.CurrentSchoolId && c.Id == violation.BehaviorCodeId);
            var consequences = await _db.ConsequenceTypes.AsNoTracking()
                .Where(c => c.BehaviorCodeId == violation.BehaviorCodeId)
                .OrderBy(c => c.SeverityRank).ToListAsync();

            var statements = await _db.CaseStatements.AsNoTracking()
                .Where(s => s.DisciplineCaseId == id).OrderBy(s => s.RecordedAtUtc).ToListAsync();
            var action = await _db.ActionsApplied.AsNoTracking().FirstOrDefaultAsync(a => a.DisciplineCaseId == id);
            var appeal = await _db.Appeals.AsNoTracking().FirstOrDefaultAsync(a => a.DisciplineCaseId == id);
            var students = await StudentLabelsAsync(new[] { disciplineCase.StudentId });

            var proposed = disciplineCase.ProposedConsequenceTypeId.HasValue
                ? consequences.FirstOrDefault(c => c.Id == disciplineCase.ProposedConsequenceTypeId.Value) : null;
            var decided = disciplineCase.DecidedConsequenceTypeId.HasValue
                ? consequences.FirstOrDefault(c => c.Id == disciplineCase.DecidedConsequenceTypeId.Value) : null;

            var model = new CaseFileViewModel
            {
                Case = disciplineCase,
                Incident = incident,
                Violation = violation,
                Student = students.TryGetValue(disciplineCase.StudentId, out var label) ? label : UnknownStudent(),
                Statements = statements,
                Action = action,
                Appeal = appeal,
                Consequences = consequences,
                Proposed = proposed,
                Decided = decided,
                MaxSuspensionDays = code?.MaxSuspensionDays,
                AppealWindowDays = code?.AppealWindowDays ?? 7,
                DueProcessSatisfied = DueProcessPolicy.StatementsSatisfied(disciplineCase.Severity, statements.Select(s => s.Kind).ToList()),
                DecisionNeedsPrincipal = disciplineCase.RequiresPrincipal,
                AppealWindowClosesAtUtc = disciplineCase.DecidedAtUtc?.AddDays(code?.AppealWindowDays ?? 7),
                Guardians = await GuardiansAsync(disciplineCase.StudentId),
            };

            model.Timeline = BuildTimeline(model);
            return model;
        }

        private static IReadOnlyList<CaseFileViewModel.TimelineEvent> BuildTimeline(CaseFileViewModel m)
        {
            // BR-DCP-010: the reporter is not named. The trail is what happened, not who said it —
            // the identity is in the audit record, which is where a complaint about it belongs.
            var events = new List<CaseFileViewModel.TimelineEvent>
            {
                new(m.Incident.OccurredAtUtc,
                    "وقعت المخالفة", "Incident occurred",
                    m.Incident.Narrative, m.Incident.Narrative, "bi-exclamation-circle"),
            };

            foreach (var statement in m.Statements)
            {
                events.Add(new CaseFileViewModel.TimelineEvent(
                    statement.RecordedAtUtc,
                    DisciplineLabels.StatementKind(statement.Kind, true), DisciplineLabels.StatementKind(statement.Kind, false),
                    statement.Text, statement.Text, "bi-chat-quote"));
            }

            if (m.Case.DecidedAtUtc.HasValue)
            {
                events.Add(new CaseFileViewModel.TimelineEvent(
                    m.Case.DecidedAtUtc.Value,
                    "صدر القرار", "Decision",
                    $"{m.Decided?.NameAr} — {m.Case.DecisionArticleRef}", $"{m.Decided?.NameEn} — {m.Case.DecisionArticleRef}",
                    "bi-gavel"));
            }

            if (m.Action != null)
            {
                events.Add(new CaseFileViewModel.TimelineEvent(
                    m.Action.StartDate,
                    "نُفِّذ الإجراء", "Action applied",
                    m.Action.Days.HasValue ? $"{m.Action.Days} يوم" : null,
                    m.Action.Days.HasValue ? $"{m.Action.Days} day(s)" : null,
                    "bi-clipboard-check"));
            }

            if (m.Appeal != null)
            {
                events.Add(new CaseFileViewModel.TimelineEvent(
                    m.Appeal.FiledAtUtc, "قُدِّم تظلّم", "Appeal filed", m.Appeal.Grounds, m.Appeal.Grounds, "bi-arrow-counterclockwise"));
                if (m.Appeal.DecidedAtUtc.HasValue)
                {
                    events.Add(new CaseFileViewModel.TimelineEvent(
                        m.Appeal.DecidedAtUtc.Value,
                        DisciplineLabels.AppealOutcome(m.Appeal.Outcome, true), DisciplineLabels.AppealOutcome(m.Appeal.Outcome, false),
                        m.Appeal.DecisionNote, m.Appeal.DecisionNote, "bi-check2-circle"));
                }
            }

            if (m.Case.ClosedAtUtc.HasValue)
            {
                events.Add(new CaseFileViewModel.TimelineEvent(
                    m.Case.ClosedAtUtc.Value, "أُغلقت القضية", "Case closed", null, null, "bi-lock"));
            }

            return events.OrderBy(e => e.AtUtc).ToList();
        }

        private async Task<BehaviorCode?> ActiveCodeAsync()
        {
            var yearId = _year.AcademicYearId;
            return await _db.BehaviorCodes.AsNoTracking()
                .Where(c => c.AcademicYearId == yearId)
                .OrderByDescending(c => c.Version).FirstOrDefaultAsync();
        }

        private async Task<int?> CurrentTermIdAsync()
        {
            var today = _clock.UtcNow.Date;
            var term = await _db.Terms.AsNoTracking()
                .Where(t => t.AcademicYearId == _year.AcademicYearId && t.StartDate <= today && t.EndDate >= today)
                .OrderBy(t => t.SequenceNumber).FirstOrDefaultAsync();
            return term?.Id;
        }

        /// <summary>BR-SCN-005: the roster is the membership standing today, through the active enrollment.</summary>
        private async Task<IReadOnlyList<RecordBehaviorViewModel.RosterEntry>> RosterAsync(int sectionId)
        {
            var yearId = _year.AcademicYearId;
            var today = _clock.UtcNow.Date;
            var enrollmentIds = await _db.SectionMemberships.AsNoTracking()
                .Where(x => x.SectionId == sectionId && x.AcademicYearId == yearId
                    && x.EffectiveFromUtc <= today && (x.EffectiveToUtc == null || x.EffectiveToUtc > today))
                .Select(x => x.EnrollmentId).ToListAsync();
            var studentIds = await _db.Enrollments.AsNoTracking()
                .Where(e => enrollmentIds.Contains(e.Id) && e.Status == EnrollmentStatus.Active)
                .Select(e => e.StudentId).ToListAsync();

            var labels = await StudentLabelsAsync(studentIds);
            var incidents = await _db.Incidents.AsNoTracking()
                .Where(i => i.AcademicYearId == yearId && studentIds.Contains(i.StudentId))
                .Select(i => i.StudentId).ToListAsync();
            var merits = await _db.Merits.AsNoTracking()
                .Where(m => m.AcademicYearId == yearId && studentIds.Contains(m.StudentId))
                .Select(m => new { m.StudentId, m.Points }).ToListAsync();

            return studentIds
                .Where(labels.ContainsKey)
                .Select(id => new RecordBehaviorViewModel.RosterEntry(
                    id, labels[id],
                    incidents.Count(x => x == id),
                    merits.Where(m => m.StudentId == id).Sum(m => m.Points)))
                .OrderBy(r => r.Label.NameEn)
                .ToList();
        }

        private async Task<IReadOnlyList<RecordBehaviorViewModel.RecentEntry>> RecentAsync()
        {
            var yearId = _year.AcademicYearId;
            var incidents = await _db.Incidents.AsNoTracking()
                .Where(i => i.AcademicYearId == yearId)
                .OrderByDescending(i => i.OccurredAtUtc).Take(12).ToListAsync();
            var merits = await _db.Merits.AsNoTracking()
                .Where(m => m.AcademicYearId == yearId)
                .OrderByDescending(m => m.Id).Take(12).ToListAsync();

            var violations = await ViolationLookupAsync(incidents.Select(i => i.ViolationTypeId));
            var meritTypeIds = merits.Select(m => m.MeritTypeId).Distinct().ToList();
            var meritTypes = await _db.MeritTypes.AsNoTracking()
                .Where(m => meritTypeIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m);
            var labels = await StudentLabelsAsync(incidents.Select(i => i.StudentId).Concat(merits.Select(m => m.StudentId)));

            var rows = new List<RecordBehaviorViewModel.RecentEntry>();
            foreach (var incident in incidents)
            {
                violations.TryGetValue(incident.ViolationTypeId, out var violation);
                rows.Add(new RecordBehaviorViewModel.RecentEntry(
                    incident.OccurredAtUtc,
                    labels.TryGetValue(incident.StudentId, out var l) ? l : UnknownStudent(),
                    IsArabic ? violation?.NameAr ?? "—" : violation?.NameEn ?? "—",
                    incident.Severity, false, incident.IncidentNo, incident.CaseId));
            }

            foreach (var merit in merits)
            {
                meritTypes.TryGetValue(merit.MeritTypeId, out var type);
                rows.Add(new RecordBehaviorViewModel.RecentEntry(
                    merit.CreatedAtUtc,
                    labels.TryGetValue(merit.StudentId, out var l) ? l : UnknownStudent(),
                    IsArabic ? type?.NameAr ?? "—" : type?.NameEn ?? "—",
                    0, true, null, null));
            }

            return rows.OrderByDescending(r => r.OccurredAtUtc).Take(15).ToList();
        }

        /// <summary>Student → grade for the year, through the enrollment's profile. Both sides read past the soft-active filter (a retired grade still labels its rows).</summary>
        private async Task<Dictionary<int, int>> GradeByStudentAsync(int yearId)
        {
            var enrollments = await _db.Enrollments.AsNoTracking()
                .Where(e => e.AcademicYearId == yearId)
                .Select(e => new { e.StudentId, e.GradeYearProfileId }).ToListAsync();
            var profiles = await _db.GradeYearProfiles.IgnoreQueryFilters().AsNoTracking()
                .Where(p => p.SchoolId == _db.CurrentSchoolId)
                .Select(p => new { p.Id, p.GradeLevelId }).ToListAsync();
            var gradeByProfile = profiles.ToDictionary(p => p.Id, p => p.GradeLevelId);

            var map = new Dictionary<int, int>();
            foreach (var enrollment in enrollments)
            {
                if (gradeByProfile.TryGetValue(enrollment.GradeYearProfileId, out var gradeId))
                {
                    map[enrollment.StudentId] = gradeId;
                }
            }

            return map;
        }

        private async Task<Dictionary<int, ViolationType>> ViolationLookupAsync(IEnumerable<int> ids)
        {
            var list = ids.Distinct().ToList();
            return list.Count == 0
                ? new Dictionary<int, ViolationType>()
                : await _db.ViolationTypes.AsNoTracking().Where(v => list.Contains(v.Id)).ToDictionaryAsync(v => v.Id, v => v);
        }

        private async Task<Dictionary<int, ConsequenceType>> ConsequenceLookupAsync(IEnumerable<int> ids)
        {
            var list = ids.Distinct().ToList();
            return list.Count == 0
                ? new Dictionary<int, ConsequenceType>()
                : await _db.ConsequenceTypes.AsNoTracking().Where(c => list.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c);
        }

        /// <summary>Students read past the soft-active filter: a withdrawn student's case still has to name them.</summary>
        private async Task<Dictionary<int, StudentLabel>> StudentLabelsAsync(IEnumerable<int> ids)
        {
            var list = ids.Distinct().ToList();
            if (list.Count == 0)
            {
                return new Dictionary<int, StudentLabel>();
            }

            var rows = await _db.Students.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.SchoolId == _db.CurrentSchoolId && list.Contains(s.Id))
                .Select(s => new
                {
                    s.Id,
                    s.StudentNo,
                    NameAr = s.FirstNameAr + " " + s.FatherNameAr + " " + s.FamilyNameAr,
                    NameEn = s.FirstNameEn + " " + s.FatherNameEn + " " + s.FamilyNameEn,
                })
                .ToListAsync();
            return rows.ToDictionary(s => s.Id, s => new StudentLabel(s.StudentNo, s.NameAr, s.NameEn));
        }

        /// <summary>
        /// The student's guardians, for BR-DCP-006's counter-filed appeal. Parents read past the
        /// soft-active filter for the same reason students do: a guardian deactivated since the case
        /// opened still has to be nameable on it.
        /// </summary>
        private async Task<IReadOnlyList<CaseFileViewModel.GuardianOption>> GuardiansAsync(int studentId)
        {
            var parentIds = await _db.StudentGuardianLinks.AsNoTracking()
                .Where(l => l.StudentId == studentId).Select(l => l.ParentId).ToListAsync();
            if (parentIds.Count == 0)
            {
                return Array.Empty<CaseFileViewModel.GuardianOption>();
            }

            return await _db.Parents.IgnoreQueryFilters().AsNoTracking()
                .Where(p => p.SchoolId == _db.CurrentSchoolId && parentIds.Contains(p.Id))
                .OrderBy(p => p.NameEn)
                .Select(p => new CaseFileViewModel.GuardianOption(p.Id, p.NameAr, p.NameEn))
                .ToListAsync();
        }

        private static StudentLabel UnknownStudent() => new("—", "—", "—");

        // ================================================================== refusals

        /// <summary>
        /// BR-GLB-001: an engine refusal is English by construction, and a parent-facing module is
        /// exactly where raw English must not surface. Every exception the port documents is named
        /// here; anything else falls back to the engine's own text rather than being swallowed.
        /// </summary>
        private static string Translate(Exception ex) => ex switch
        {
            StatementsRequiredException =>
                T("A serious case cannot be decided before the student or a parent has given a statement (BR-DCP-003).",
                  "لا يُبتّ في قضية جسيمة قبل أخذ إفادة الطالب أو ولي الأمر (BR-DCP-003)."),
            DecisionArticleRequiredException =>
                T("The decision must cite an article of the behaviour code (BR-DCP-003).",
                  "يجب أن يستند القرار إلى مادة من لائحة السلوك (BR-DCP-003)."),
            DecisionDeviationReasonRequiredException =>
                T("This decision is lighter than the code proposes — say why (BR-DCP-005).",
                  "هذا القرار أخفّ ممّا تقترحه اللائحة — اذكر السبب (BR-DCP-005)."),
            PrincipalApprovalRequiredException =>
                T("This decision needs the Principal: it is harsher than the proposal, a suspension, or a gravest-level case (BR-DCP-004).",
                  "هذا القرار يحتاج اعتماد المدير: فهو أشدّ من المقترح، أو فصل، أو قضية بالغة (BR-DCP-004)."),
            SuspensionExceedsPackLimitException =>
                T("The suspension is longer than the regulation allows (BR-DCP-004).",
                  "مدّة الفصل تتجاوز ما تسمح به اللائحة النظامية (BR-DCP-004)."),
            AppealNotAllowedException =>
                T("No appeal is possible here — minor cases cannot be appealed, the window has closed, or one was already filed (BR-DCP-006).",
                  "لا تظلّم هنا — القضايا البسيطة لا يُتظلَّم عليها، أو انقضت المهلة، أو قُدِّم تظلّم من قبل (BR-DCP-006)."),
            AppealReviewerNotIndependentException =>
                T("An appeal cannot be reviewed by the person who took the decision (BR-DCP-006).",
                  "لا يراجع التظلّم من أصدر القرار نفسه (BR-DCP-006)."),
            CaseNotClosableException =>
                T("The case cannot close yet — the appeal window is open or an appeal is undecided (BR-DCP-006).",
                  "لا يمكن إغلاق القضية بعد — مهلة التظلّم مفتوحة أو هناك تظلّم لم يُبتّ فيه (BR-DCP-006)."),
            InvalidCaseStatusTransitionException =>
                T("That step is not the case's next one (BR-DCP-003).",
                  "هذه الخطوة ليست التالية في مسار القضية (BR-DCP-003)."),
            MeritPointsOutOfBoundsException =>
                T("That many points is outside what this merit may award (BR-DCP-002).",
                  "هذا العدد من النقاط خارج ما يمنحه هذا التميّز (BR-DCP-002)."),
            _ => UserMessage.For(ex, IsArabic),
        };

        // ================================================================== code-form binding

        private static List<ViolationTypeInput> Zip(string[]? article, string[]? nameAr, string[]? nameEn, int[]? severity, int[]? points)
        {
            var rows = new List<ViolationTypeInput>();
            var count = Min(article?.Length, nameAr?.Length, nameEn?.Length);
            for (var i = 0; i < count; i++)
            {
                if (string.IsNullOrWhiteSpace(article![i]) || string.IsNullOrWhiteSpace(nameAr![i]) || string.IsNullOrWhiteSpace(nameEn![i]))
                {
                    continue;
                }

                rows.Add(new ViolationTypeInput(
                    article[i].Trim(), nameAr[i].Trim(), nameEn[i].Trim(),
                    At(severity, i, 1), At(points, i, 0)));
            }

            return rows;
        }

        private static List<MeritTypeInput> ZipMerits(string[]? nameAr, string[]? nameEn, int[]? points, int[]? max)
        {
            var rows = new List<MeritTypeInput>();
            var count = Min(nameAr?.Length, nameEn?.Length);
            for (var i = 0; i < count; i++)
            {
                if (string.IsNullOrWhiteSpace(nameAr![i]) || string.IsNullOrWhiteSpace(nameEn![i]))
                {
                    continue;
                }

                var award = At(points, i, 1);
                rows.Add(new MeritTypeInput(nameAr[i].Trim(), nameEn[i].Trim(), award, Math.Max(award, At(max, i, award))));
            }

            return rows;
        }

        /// <summary>
        /// Returns the consequences the engine will receive, and the map from the form's rendered row
        /// index to each one's position in that list — which is what the ladder cells are expressed in.
        /// </summary>
        private static (List<ConsequenceTypeInput> Rows, Dictionary<int, int> RowToInput) ZipConsequences(
            ConsequenceKind[]? kind, string[]? nameAr, string[]? nameEn, int[]? rank, bool[]? suspension)
        {
            var rows = new List<ConsequenceTypeInput>();
            var map = new Dictionary<int, int>();
            var count = Min(kind?.Length, nameAr?.Length, nameEn?.Length);
            for (var i = 0; i < count; i++)
            {
                if (string.IsNullOrWhiteSpace(nameAr![i]) || string.IsNullOrWhiteSpace(nameEn![i]))
                {
                    continue;
                }

                var k = kind![i];

                // BR-DCP-004: the suspension classes are suspension-class whatever the form says, so
                // the Principal gate and the pack cap cannot be dropped by clearing a field.
                var isSuspension = k == ConsequenceKind.InSchoolSuspension
                                || k == ConsequenceKind.ExternalSuspension
                                || At(suspension, i, false);
                map[i] = rows.Count;
                rows.Add(new ConsequenceTypeInput(k, nameAr[i].Trim(), nameEn[i].Trim(), At(rank, i, i + 1), isSuspension));
            }

            return (rows, map);
        }

        private static int Min(params int?[] lengths)
        {
            var value = int.MaxValue;
            foreach (var length in lengths)
            {
                value = Math.Min(value, length ?? 0);
            }

            return value == int.MaxValue ? 0 : value;
        }

        private static TValue At<TValue>(TValue[]? array, int index, TValue fallback)
            => array != null && index < array.Length ? array[index] : fallback;
    }
}
