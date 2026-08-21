using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Attendance;
using Sms.Application.Audit;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Attendance;
using Sms.Domain.Calendar;
using Sms.Domain.Grades;
using Sms.Domain.Schools;
using Sms.Domain.Sections;
using Sms.Domain.Students;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/14 §8 — E-301 screens over IAttendanceAdmin: 8.1 Section
    /// capture sheet (default-all-present, one-tap taxonomy, single POST),
    /// 8.2 Gate console (late arrivals, early release verified against the
    /// real BR-PAR-008 authorized-pickup list, leave-pass lifecycle),
    /// 8.3 Attendance monitor (capture completeness by section, today's
    /// absences, BR-ATD-008 consecutive-absence alerts, day closure),
    /// 8.4 Justification review queue (incl. the "paper at the counter"
    /// submission path), 8.5 Correction screen (WF-14, mandatory reason)
    /// plus doc §10's correction register read from the audit trail,
    /// 8.6 Analytics (section/student trends, chronic list, day-of-week
    /// pattern) — all through BR-ATD-009's single AttendancePercentageCalculator.
    ///
    /// Deferred, and why: **period mode entirely** (BR-ATD-001 — the engine
    /// is daily-only because AttendancePeriod needs M15 timetable sessions,
    /// so there is no per-session grid to render); 8.7 portal excuse
    /// submission and leave-pass request (E-304 already ships the read
    /// half — child attendance summary + last 30 days — but IParentPortalQuery
    /// has no write surface and IAttendanceAdmin.SubmitJustificationAsync
    /// takes no attachment, so BR-ATD-005's medical-document requirement
    /// cannot be honoured from the parent side yet); notification dispatch
    /// for every doc §12 event (BR-ATD-008 escalation is evaluated and
    /// shown on the monitor, never sent — no EscalationCase entity and no
    /// wiring to E-007); the warning-letter workflow and ministry truancy
    /// formats (need the country pack's threshold values, doc open
    /// question #4); late-to-absence conversion (LateToAbsenceConverter
    /// exists but doc open question #2 recommends shipping it disabled, and
    /// no per-stage threshold setting exists to enable it from).
    /// </summary>
    [Route("attendance")]
    public class AttendanceController : Controller
    {
        /// <summary>BR-ATD-008's "consecutive absences >= N" default until a per-school setting exists.</summary>
        private const int DefaultConsecutiveThreshold = 3;

        /// <summary>BR-ATD-005's submission window default (doc: 3 working days).</summary>
        private const int DefaultJustificationWindowDays = 3;

        private readonly IAttendanceAdmin _attendance;
        private readonly AppDbContext _db;
        private readonly IAuditContext _audit;
        private readonly IWorkingYearContext _workingYear;
        private readonly ICurrentUser _user;
        private readonly IClock _clock;

        public AttendanceController(
            IAttendanceAdmin attendance, AppDbContext db, IAuditContext audit,
            IWorkingYearContext workingYear, ICurrentUser user, IClock clock)
        {
            _attendance = attendance;
            _db = db;
            _audit = audit;
            _workingYear = workingYear;
            _user = user;
            _clock = clock;
        }

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        // ================================================================== 8.3 Attendance monitor

        [HttpGet("")]
        [RequirePermission(ScreenCatalog.Modules.Attendance, ScreenCatalog.Attendance.Capture, ActionVerb.View)]
        public async Task<IActionResult> Index(int? year = null, DateTime? date = null, int? threshold = null)
        {
            var m = new AttendanceMonitorViewModel { ConsecutiveThreshold = threshold ?? DefaultConsecutiveThreshold };
            await FillPageAsync(m, year, date);
            if (m.Year == null) return View(m);
            var yid = m.Year.Id;
            var day = m.Date;

            var sections = await SectionsOfYearAsync(yid);
            var grades = await GradesAsync();
            var profiles = await ProfilesAsync(yid);

            var memberships = await _db.SectionMemberships.AsNoTracking()
                .Where(x => x.AcademicYearId == yid && x.EffectiveFromUtc <= day && (x.EffectiveToUtc == null || x.EffectiveToUtc > day))
                .ToListAsync();
            var activeEnrollments = await _db.Enrollments.AsNoTracking()
                .Where(e => e.AcademicYearId == yid && e.Status == EnrollmentStatus.Active)
                .Select(e => e.Id).ToListAsync();
            var activeSet = activeEnrollments.ToHashSet();
            memberships = memberships.Where(x => activeSet.Contains(x.EnrollmentId)).ToList();

            var rows = await _db.AttendanceDays.AsNoTracking()
                .Where(a => a.AcademicYearId == yid && a.Date == day).ToListAsync();

            m.CountsByStatus = rows.GroupBy(r => r.Status).ToDictionary(g => g.Key, g => g.Count());
            m.LockedRows = rows.Count(r => r.IsLocked);
            m.CapturedTotal = rows.Count;
            m.ExpectedTotal = memberships.Count;

            m.Sections = sections.Select(s =>
            {
                var expected = memberships.Count(x => x.SectionId == s.Id);
                var captured = rows.Where(r => r.SectionId == s.Id).ToList();
                return new AttendanceMonitorViewModel.SectionRow(
                    s, GradeOf(s.GradeYearProfileId, profiles, grades), expected, captured.Count,
                    captured.Count(r => IsAbsence(r.Status)), captured.Count(r => r.Status == AttendanceStatus.Late),
                    captured.Any(r => r.IsLocked));
            }).OrderBy(r => r.Grade?.SequenceOrder).ThenBy(r => r.Section.NameEn).ToList();

            // Today's exceptions — everything that is not a plain Present.
            var flagged = rows.Where(r => r.Status != AttendanceStatus.Present).ToList();
            var students = await StudentsByEnrollmentAsync(flagged.Select(r => r.EnrollmentId).ToList());
            var justifications = await _db.Justifications.AsNoTracking()
                .Where(j => flagged.Select(f => f.Id).Contains(j.AttendanceDayId)).ToListAsync();

            m.Absences = flagged
                .Where(r => students.ContainsKey(r.EnrollmentId))
                .Select(r => new AttendanceMonitorViewModel.AbsenceRow(
                    r, students[r.EnrollmentId],
                    sections.FirstOrDefault(s => s.Id == r.SectionId),
                    GradeOf(sections.FirstOrDefault(s => s.Id == r.SectionId)?.GradeYearProfileId, profiles, grades),
                    justifications.Where(j => j.AttendanceDayId == r.Id)
                        .OrderByDescending(j => j.Id).Select(j => (JustificationReviewState?)j.ReviewState).FirstOrDefault()))
                .OrderBy(r => r.Section?.NameEn).ThenBy(r => r.Student.StudentNo).ToList();

            m.Alerts = await BuildAlertsAsync(yid, day, m.ConsecutiveThreshold, sections, profiles, grades);

            m.PendingJustifications = await _db.Justifications.AsNoTracking()
                .CountAsync(j => j.ReviewState == JustificationReviewState.Submitted);
            m.OpenLeavePasses = await _db.LeavePasses.AsNoTracking()
                .CountAsync(l => l.Status == LeavePassStatus.Requested || l.Status == LeavePassStatus.Approved || l.Status == LeavePassStatus.Released);

            return View(m);
        }

        /// <summary>BR-ATD-007: day-end closure locks every row captured for the date.</summary>
        [HttpPost("close")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Attendance, ScreenCatalog.Attendance.Capture, ActionVerb.Approve)]
        public async Task<IActionResult> CloseDay(DateTime date, int? year)
        {
            try
            {
                var locked = await _attendance.CloseDayAsync(date);
                TempData["Flash"] = locked == 0
                    ? T("Nothing to close — every captured row for this day was already locked.", "لا شيء لإقفاله — كل السجلات المرصودة لهذا اليوم مقفلة سلفاً.")
                    : string.Format(T("Day closed — {0} record(s) locked. Later changes need a correction reason (WF-14).", "أُقفل اليوم — {0} سجل مقفل. أي تعديل لاحق يحتاج سبب تصحيح (WF-14)."), locked);
            }
            catch (Exception ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Index), new { year, date = date.ToString("yyyy-MM-dd") });
        }

        // ================================================================== 8.1 Section capture sheet

        [HttpGet("capture")]
        [RequirePermission(ScreenCatalog.Modules.Attendance, ScreenCatalog.Attendance.Capture, ActionVerb.View)]
        public async Task<IActionResult> Capture(int? year = null, DateTime? date = null, int? section = null)
        {
            var m = new AttendanceCaptureViewModel { SectionId = section };
            await FillPageAsync(m, year, date);
            if (m.Year == null) return View(m);
            var yid = m.Year.Id;
            var day = m.Date;

            var sections = await SectionsOfYearAsync(yid);
            var grades = await GradesAsync();
            var profiles = await ProfilesAsync(yid);
            m.AllSections = sections
                .Select(s => new AttendanceCaptureViewModel.SectionOption(s, GradeOf(s.GradeYearProfileId, profiles, grades)))
                .OrderBy(s => s.Grade?.SequenceOrder).ThenBy(s => s.Section.NameEn).ToList();

            m.Section = sections.FirstOrDefault(s => s.Id == section);
            if (m.Section == null) return View(m);
            m.Grade = GradeOf(m.Section.GradeYearProfileId, profiles, grades);

            var roster = await RosterAsync(m.Section.Id, yid, day);
            var enrollmentIds = roster.Keys.ToList();
            var existing = await _db.AttendanceDays.AsNoTracking()
                .Where(a => a.Date == day && enrollmentIds.Contains(a.EnrollmentId)).ToListAsync();
            var openPasses = await _db.LeavePasses.AsNoTracking()
                .Where(l => enrollmentIds.Contains(l.EnrollmentId)
                    && (l.Status == LeavePassStatus.Approved || l.Status == LeavePassStatus.Released))
                .Select(l => l.EnrollmentId).ToListAsync();

            m.Rows = roster
                .Select(kv => new AttendanceCaptureViewModel.Row(
                    kv.Key, kv.Value, existing.FirstOrDefault(a => a.EnrollmentId == kv.Key), openPasses.Contains(kv.Key)))
                .OrderBy(r => AttendanceLabels.StudentName(r.Student, IsArabic)).ToList();
            m.CapturedCount = m.Rows.Count(r => r.Existing != null);
            m.AnyLocked = m.Rows.Any(r => r.Existing?.IsLocked == true);
            return View(m);
        }

        /// <summary>
        /// Single POST for the whole roster (the doc's offline-tolerance and
        /// ≤ 2-minute targets). New rows go through CaptureAsync; an
        /// already-captured, still-unlocked row that changed goes through
        /// CorrectAsync — which needs an audit reason even before closure,
        /// because AttendanceDay.Status is [RequiresAuditReason] on a T1
        /// entity and any edit is an EF Modified transition. The sheet
        /// supplies a fixed pre-closure reason rather than nagging the
        /// teacher for one; locked rows are refused outright and routed to
        /// the correction screen (BR-ATD-007).
        /// </summary>
        [HttpPost("capture")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Attendance, ScreenCatalog.Attendance.Capture, ActionVerb.Edit)]
        public async Task<IActionResult> SaveCapture(int sectionId, DateTime date, int? year)
        {
            var redirect = new { year, date = date.ToString("yyyy-MM-dd"), section = sectionId };
            try
            {
                var yid = year ?? _workingYear.AcademicYearId;
                var day = date.Date;
                var calendarDay = await CalendarDayTypeAsync(yid, day);
                if (calendarDay != null && !IsWorking(calendarDay.Value))
                {
                    throw new InvalidOperationException(string.Format(
                        T("{0} is not a working day for this year's calendar — attendance is only captured on working days (BR-ATD-003).",
                          "{0} ليس يوماً دراسياً في تقويم هذا العام — لا يُرصد الحضور إلا في أيام الدراسة (BR-ATD-003)."),
                        AttendanceLabels.CalendarDayType(calendarDay.Value, IsArabic)));
                }

                var roster = await RosterAsync(sectionId, yid, day);
                var existing = await _db.AttendanceDays.AsNoTracking()
                    .Where(a => a.Date == day && roster.Keys.Contains(a.EnrollmentId)).ToListAsync();

                int created = 0, changed = 0, lockedSkipped = 0;
                foreach (var enrollmentId in roster.Keys)
                {
                    var raw = Request.Form[$"status_{enrollmentId}"].ToString();
                    if (string.IsNullOrWhiteSpace(raw) || !Enum.TryParse<AttendanceStatus>(raw, out var status)) continue;

                    var row = existing.FirstOrDefault(a => a.EnrollmentId == enrollmentId);
                    if (row == null)
                    {
                        _audit.Reason = null;
                        await _attendance.CaptureAsync(enrollmentId, day, status, _user.UserId);
                        created++;
                    }
                    else if (row.Status != status)
                    {
                        if (row.IsLocked) { lockedSkipped++; continue; }
                        _audit.Reason = T("Capture sheet edit before day closure", "تعديل من كشف الرصد قبل إقفال اليوم");
                        await _attendance.CorrectAsync(row.Id, status);
                        changed++;
                    }

                    // Per-row SaveChanges on one context: keep the tracker from growing across the roster.
                    _db.ChangeTracker.Clear();
                }

                _audit.Reason = null;
                var parts = new List<string>();
                if (created > 0) parts.Add(string.Format(T("{0} captured", "{0} رصد جديد"), created));
                if (changed > 0) parts.Add(string.Format(T("{0} changed", "{0} تعديل"), changed));
                if (lockedSkipped > 0) parts.Add(string.Format(T("{0} locked and skipped", "{0} مقفل وتم تخطيه"), lockedSkipped));
                TempData[lockedSkipped > 0 ? "Error" : "Flash"] = parts.Count == 0
                    ? T("Nothing changed.", "لا تغيير.")
                    : string.Join(" · ", parts) + (lockedSkipped > 0
                        ? " — " + T("locked rows must go through the correction screen.", "الصفوف المقفلة تُعدَّل من شاشة التصحيح.")
                        : string.Empty);
            }
            catch (Exception ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Capture), redirect);
        }

        // ================================================================== 8.2 Gate console

        [HttpGet("gate")]
        [RequirePermission(ScreenCatalog.Modules.Attendance, ScreenCatalog.Attendance.Gate, ActionVerb.View)]
        public async Task<IActionResult> Gate(int? year = null, DateTime? date = null, string? q = null, int? enrollment = null)
        {
            var m = new AttendanceGateViewModel { Query = q };
            await FillPageAsync(m, year, date);
            if (m.Year == null) return View(m);
            var yid = m.Year.Id;
            var day = m.Date;

            var sections = await SectionsOfYearAsync(yid);
            var grades = await GradesAsync();
            var profiles = await ProfilesAsync(yid);

            if (!string.IsNullOrWhiteSpace(q) || enrollment != null)
            {
                m.Hits = await SearchAsync(yid, day, q, enrollment, sections, profiles, grades);
                m.Selected = enrollment != null
                    ? m.Hits.FirstOrDefault(h => h.EnrollmentId == enrollment)
                    : m.Hits.Count == 1 ? m.Hits[0] : null;
            }

            if (m.Selected != null) m.PickupList = await PickupListAsync(m.Selected.Student.Id);

            var todaysEvents = await _db.GateEvents.AsNoTracking()
                .Where(e => e.EventTimeUtc >= day && e.EventTimeUtc < day.AddDays(1))
                .OrderByDescending(e => e.EventTimeUtc).Take(50).ToListAsync();
            var passes = await _db.LeavePasses.AsNoTracking()
                .Where(l => l.RequestedAtUtc >= day && l.RequestedAtUtc < day.AddDays(1))
                .OrderByDescending(l => l.Id).Take(50).ToListAsync();

            var ids = todaysEvents.Select(e => e.EnrollmentId).Concat(passes.Select(p => p.EnrollmentId)).Distinct().ToList();
            var students = await StudentsByEnrollmentAsync(ids);
            var sectionByEnrollment = await SectionByEnrollmentAsync(ids, day);

            m.TodaysEvents = todaysEvents.Select(e => new AttendanceGateViewModel.EventRow(
                e, students.TryGetValue(e.EnrollmentId, out var s) ? s : null,
                sectionByEnrollment.TryGetValue(e.EnrollmentId, out var sid) ? sections.FirstOrDefault(x => x.Id == sid) : null)).ToList();
            m.Passes = passes.Select(p => new AttendanceGateViewModel.PassRow(
                p, students.TryGetValue(p.EnrollmentId, out var s) ? s : null,
                sectionByEnrollment.TryGetValue(p.EnrollmentId, out var sid) ? sections.FirstOrDefault(x => x.Id == sid) : null)).ToList();
            return View(m);
        }

        /// <summary>
        /// BR-ATD-004 late arrival. The GateEvent is the reception log; the
        /// domain deliberately does not let it flip the day's status by
        /// itself, so "also mark the day Late" is an explicit, opt-in
        /// screen-level composition rather than a hidden side effect.
        /// </summary>
        [HttpPost("gate/late")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Attendance, ScreenCatalog.Attendance.Gate, ActionVerb.Create)]
        public async Task<IActionResult> RecordLate(int enrollmentId, DateTime date, string? time, bool markDay, int? year)
        {
            try
            {
                var at = CombineTime(date, time);
                await _attendance.RecordGateEventAsync(enrollmentId, GateEventType.Late, at);
                var note = string.Empty;
                if (markDay)
                {
                    var day = date.Date;
                    var row = await _db.AttendanceDays.AsNoTracking()
                        .FirstOrDefaultAsync(a => a.EnrollmentId == enrollmentId && a.Date == day);
                    if (row == null)
                    {
                        _audit.Reason = null;
                        await _attendance.CaptureAsync(enrollmentId, day, AttendanceStatus.Late, _user.UserId);
                        note = " " + T("Day marked Late.", "وسُجّل اليوم متأخراً.");
                    }
                    else if (row.Status != AttendanceStatus.Late)
                    {
                        _audit.Reason = string.Format(T("Gate late arrival logged at {0}", "وصول متأخر مُسجَّل عند البوابة {0}"), at.ToString("HH:mm"));
                        await _attendance.CorrectAsync(row.Id, AttendanceStatus.Late);
                        _audit.Reason = null;
                        note = " " + T("Day changed to Late.", "وغُيّر اليوم إلى متأخر.");
                    }
                }
                TempData["Flash"] = T("Late arrival logged.", "سُجّل الوصول المتأخر.") + note;
            }
            catch (Exception ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Gate), new { year, date = date.ToString("yyyy-MM-dd"), enrollment = enrollmentId });
        }

        /// <summary>
        /// BR-ATD-004 early release. A name outside the authorized-pickup
        /// list is only accepted as an explicit override with a reason —
        /// enforced here at the screen, because GateEvent's
        /// [RequiresAuditReason] on IsAuthorizedPickupOverride only fires on
        /// Modified, never on the insert this path performs.
        /// </summary>
        [HttpPost("gate/release")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Attendance, ScreenCatalog.Attendance.Gate, ActionVerb.Create)]
        public async Task<IActionResult> RecordRelease(
            int enrollmentId, DateTime date, string? time, string? pickupPersonName, bool isOverride, string? reason, bool markDay, int? year)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(pickupPersonName))
                    throw new InvalidOperationException(T("Name the person collecting the student.", "أدخل اسم من سيستلم الطالب."));
                if (isOverride && string.IsNullOrWhiteSpace(reason))
                    throw new InvalidOperationException(T("Releasing to someone outside the authorized list requires a reason (BR-ATD-004).", "التسليم لشخص خارج قائمة المصرّح لهم يتطلب سبباً (BR-ATD-004)."));

                var at = CombineTime(date, time);
                _audit.Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
                await _attendance.RecordGateEventAsync(
                    enrollmentId, GateEventType.EarlyLeaveRelease, at, pickupPersonName.Trim(), isOverride, _user.UserId);

                var note = string.Empty;
                if (markDay)
                {
                    var day = date.Date;
                    var row = await _db.AttendanceDays.AsNoTracking()
                        .FirstOrDefaultAsync(a => a.EnrollmentId == enrollmentId && a.Date == day);
                    if (row == null)
                    {
                        _audit.Reason = null;
                        await _attendance.CaptureAsync(enrollmentId, day, AttendanceStatus.EarlyLeave, _user.UserId);
                        note = " " + T("Day marked Early leave.", "وسُجّل اليوم خروجاً مبكراً.");
                    }
                    else if (row.Status != AttendanceStatus.EarlyLeave)
                    {
                        _audit.Reason = string.Format(T("Gate early release at {0}", "خروج مبكر من البوابة {0}"), at.ToString("HH:mm"));
                        await _attendance.CorrectAsync(row.Id, AttendanceStatus.EarlyLeave);
                        note = " " + T("Day changed to Early leave.", "وغُيّر اليوم إلى خروج مبكر.");
                    }
                }
                _audit.Reason = null;
                TempData["Flash"] = (isOverride
                    ? T("Released with an authorization override — logged with your reason.", "تم التسليم مع تجاوز التصريح — سُجّل مع السبب.")
                    : T("Release logged.", "سُجّل الخروج.")) + note;
            }
            catch (Exception ex) { _audit.Reason = null; TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Gate), new { year, date = date.ToString("yyyy-MM-dd"), enrollment = enrollmentId });
        }

        /// <summary>BR-ATD-006: in-day short leave — distinct from an early leave, it always expects a return.</summary>
        [HttpPost("gate/pass")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Attendance, ScreenCatalog.Attendance.Gate, ActionVerb.Create)]
        public async Task<IActionResult> RequestPass(int enrollmentId, string reason, DateTime date, int? year)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reason))
                    throw new InvalidOperationException(T("A leave pass needs a reason.", "الاستئذان يحتاج سبباً."));
                await _attendance.RequestLeavePassAsync(enrollmentId, reason.Trim(), _clock.UtcNow);
                TempData["Flash"] = T("Leave pass requested — a supervisor approves it before release (P2).", "تم طلب الاستئذان — يعتمده المشرف قبل الخروج (P2).");
            }
            catch (Exception ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Gate), new { year, date = date.ToString("yyyy-MM-dd"), enrollment = enrollmentId });
        }

        [HttpPost("gate/pass/{id:int}")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Attendance, ScreenCatalog.Attendance.Gate, ActionVerb.Edit)]
        public async Task<IActionResult> ChangePass(int id, LeavePassStatus target, DateTime date, int? year)
        {
            try
            {
                await _attendance.ChangeLeavePassStatusAsync(id, target, _clock.UtcNow);
                TempData["Flash"] = string.Format(T("Leave pass → {0}.", "الاستئذان ← {0}."), AttendanceLabels.LeavePass(target, IsArabic));
            }
            catch (Exception ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Gate), new { year, date = date.ToString("yyyy-MM-dd") });
        }

        // ================================================================== 8.4 Justification review queue

        [HttpGet("justifications")]
        [RequirePermission(ScreenCatalog.Modules.Attendance, ScreenCatalog.Attendance.Justifications, ActionVerb.View)]
        public async Task<IActionResult> Justifications(int? year = null, DateTime? date = null, JustificationReviewState? state = null)
        {
            var m = new JustificationQueueViewModel { State = state, WindowDays = DefaultJustificationWindowDays };
            await FillPageAsync(m, year, date);
            if (m.Year == null) return View(m);
            var yid = m.Year.Id;

            var sections = await SectionsOfYearAsync(yid);
            var all = await _db.Justifications.AsNoTracking().OrderByDescending(j => j.Id).ToListAsync();
            m.CountsByState = all.GroupBy(j => j.ReviewState).ToDictionary(g => g.Key, g => g.Count());

            var shown = (state == null ? all.Where(j => j.ReviewState == JustificationReviewState.Submitted) : all.Where(j => j.ReviewState == state))
                .Take(200).ToList();
            var dayIds = shown.Select(j => j.AttendanceDayId).Distinct().ToList();
            var days = await _db.AttendanceDays.AsNoTracking().Where(a => dayIds.Contains(a.Id) && a.AcademicYearId == yid).ToListAsync();
            var students = await StudentsByEnrollmentAsync(days.Select(d => d.EnrollmentId).ToList());

            m.Rows = shown
                .Select(j => (j, day: days.FirstOrDefault(d => d.Id == j.AttendanceDayId)))
                .Where(x => x.day != null)
                .Select(x =>
                {
                    var lag = (int)Math.Round((x.j.SubmittedAtUtc.Date - x.day!.Date).TotalDays);
                    return new JustificationQueueViewModel.Row(
                        x.j, x.day!, students.TryGetValue(x.day!.EnrollmentId, out var s) ? s : null,
                        sections.FirstOrDefault(sec => sec.Id == x.day!.SectionId),
                        lag, lag > m.WindowDays);
                }).ToList();

            // Counter-submission picker: absences in this year with no justification on file.
            var justifiedDayIds = all.Select(j => j.AttendanceDayId).ToHashSet();
            var openAbsences = await _db.AttendanceDays.AsNoTracking()
                .Where(a => a.AcademicYearId == yid
                    && (a.Status == AttendanceStatus.AbsentUnexcused || a.Status == AttendanceStatus.Late || a.Status == AttendanceStatus.EarlyLeave))
                .OrderByDescending(a => a.Date).Take(300).ToListAsync();
            openAbsences = openAbsences.Where(a => !justifiedDayIds.Contains(a.Id)).Take(100).ToList();
            var absenceStudents = await StudentsByEnrollmentAsync(openAbsences.Select(a => a.EnrollmentId).ToList());
            m.OpenAbsences = openAbsences
                .Where(a => absenceStudents.ContainsKey(a.EnrollmentId))
                .Select(a => new JustificationQueueViewModel.AbsenceOption(a.Id, absenceStudents[a.EnrollmentId], a.Date, a.Status))
                .ToList();
            return View(m);
        }

        /// <summary>BR-ATD-005's counter path ("paper at the counter") — the portal upload half is deferred with §8.7.</summary>
        [HttpPost("justifications/submit")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Attendance, ScreenCatalog.Attendance.Justifications, ActionVerb.Submit)]
        public async Task<IActionResult> SubmitJustification(int attendanceDayId, JustificationType type, int? year)
        {
            try
            {
                await _attendance.SubmitJustificationAsync(attendanceDayId, type, _clock.UtcNow);
                TempData["Flash"] = T("Excuse recorded — it now waits in the review queue.", "سُجّل العذر — وهو الآن في قائمة المراجعة.");
            }
            catch (Exception ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Justifications), new { year });
        }

        /// <summary>BR-ATD-005: accepting flips the referenced day to Excused/Medical (the engine does it, T2-audited).</summary>
        [HttpPost("justifications/{id:int}/review")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Attendance, ScreenCatalog.Attendance.Justifications, ActionVerb.Approve)]
        public async Task<IActionResult> ReviewJustification(int id, bool accept, string? reason, JustificationReviewState? state, int? year)
        {
            try
            {
                if (!accept && string.IsNullOrWhiteSpace(reason))
                    throw new InvalidOperationException(T("A rejection needs a reason — the absence stays unexcused (BR-ATD-005).", "الرفض يحتاج سبباً — يبقى الغياب بدون عذر (BR-ATD-005)."));

                // The engine flips AttendanceDay.Status on accept: T1 + [RequiresAuditReason], so carry the decision as the reason.
                _audit.Reason = accept
                    ? T("Justification accepted", "قُبل العذر")
                    : null;
                await _attendance.ReviewJustificationAsync(id, accept, _user.UserId, _clock.UtcNow, reason?.Trim());
                _audit.Reason = null;
                TempData["Flash"] = accept
                    ? T("Accepted — the absence is now excused.", "قُبل — أصبح الغياب بعذر.")
                    : T("Rejected — the absence stays unexcused.", "رُفض — يبقى الغياب بدون عذر.");
            }
            catch (Exception ex) { _audit.Reason = null; TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Justifications), new { year, state });
        }

        // ================================================================== 8.5 Correction screen (WF-14)

        [HttpGet("corrections")]
        [RequirePermission(ScreenCatalog.Modules.Attendance, ScreenCatalog.Attendance.Corrections, ActionVerb.View)]
        public async Task<IActionResult> Corrections(
            int? year = null, DateTime? from = null, DateTime? to = null, int? section = null, string? q = null, bool lockedOnly = true)
        {
            var m = new AttendanceCorrectionsViewModel { SectionId = section, Query = q, LockedOnly = lockedOnly };
            await FillPageAsync(m, year, null);
            if (m.Year == null) return View(m);
            var yid = m.Year.Id;

            m.To = (to ?? _clock.UtcNow).Date;
            m.From = (from ?? m.To.AddDays(-14)).Date;
            if (m.From > m.To) (m.From, m.To) = (m.To, m.From);

            var sections = await SectionsOfYearAsync(yid);
            var grades = await GradesAsync();
            var profiles = await ProfilesAsync(yid);
            m.AllSections = sections
                .Select(s => new AttendanceCorrectionsViewModel.SectionOption(s, GradeOf(s.GradeYearProfileId, profiles, grades)))
                .OrderBy(s => s.Grade?.SequenceOrder).ThenBy(s => s.Section.NameEn).ToList();

            var query = _db.AttendanceDays.AsNoTracking()
                .Where(a => a.AcademicYearId == yid && a.Date >= m.From && a.Date <= m.To);
            if (lockedOnly) query = query.Where(a => a.IsLocked);
            if (section != null) query = query.Where(a => a.SectionId == section);
            var rows = await query.OrderByDescending(a => a.Date).Take(400).ToListAsync();

            var students = await StudentsByEnrollmentAsync(rows.Select(r => r.EnrollmentId).ToList());
            var filtered = rows.Where(r => students.ContainsKey(r.EnrollmentId)).ToList();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var needle = q.Trim();
                filtered = filtered.Where(r =>
                {
                    var s = students[r.EnrollmentId];
                    return s.StudentNo.Contains(needle, StringComparison.OrdinalIgnoreCase)
                        || AttendanceLabels.StudentName(s, false).Contains(needle, StringComparison.OrdinalIgnoreCase)
                        || AttendanceLabels.StudentName(s, true).Contains(needle, StringComparison.OrdinalIgnoreCase);
                }).ToList();
            }

            m.Rows = filtered.Select(r =>
            {
                var sec = sections.FirstOrDefault(s => s.Id == r.SectionId);
                return new AttendanceCorrectionsViewModel.Row(r, students[r.EnrollmentId], sec, GradeOf(sec?.GradeYearProfileId, profiles, grades));
            }).Take(200).ToList();

            m.Register = await BuildRegisterAsync(yid, sections);
            return View(m);
        }

        /// <summary>BR-ATD-007's post-closure correction: the reason is mandatory and the T1 pipeline enforces it independently.</summary>
        [HttpPost("corrections/{id:int}")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Attendance, ScreenCatalog.Attendance.Corrections, ActionVerb.Approve)]
        public async Task<IActionResult> Correct(int id, AttendanceStatus status, string? reason, int? year, DateTime? from, DateTime? to, int? section)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reason))
                    throw new InvalidOperationException(T("A correction needs a reason (WF-14, BR-ATD-007).", "التصحيح يحتاج سبباً (WF-14، BR-ATD-007)."));
                _audit.Reason = reason.Trim();
                await _attendance.CorrectAsync(id, status);
                _audit.Reason = null;
                TempData["Flash"] = string.Format(T("Corrected to {0} — the change is in the register below.", "صُحّح إلى {0} — التغيير مُدرج في السجل أدناه."), AttendanceLabels.Status(status, IsArabic));
            }
            catch (Exception ex) { _audit.Reason = null; TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Corrections), new
            {
                year,
                from = from?.ToString("yyyy-MM-dd"),
                to = to?.ToString("yyyy-MM-dd"),
                section,
            });
        }

        // ================================================================== 8.6 Analytics

        [HttpGet("analytics")]
        [RequirePermission(ScreenCatalog.Modules.Attendance, ScreenCatalog.Attendance.Analytics, ActionVerb.View)]
        public async Task<IActionResult> Analytics(
            int? year = null, int? term = null, DateTime? from = null, DateTime? to = null, int? section = null, decimal? below = null)
        {
            var m = new AttendanceAnalyticsViewModel { TermId = term, SectionId = section, ChronicBelowPercent = below ?? 90m };
            await FillPageAsync(m, year, null);
            if (m.Year == null) return View(m);
            var yid = m.Year.Id;

            m.Terms = await _db.Terms.AsNoTracking().Where(t => t.AcademicYearId == yid).OrderBy(t => t.SequenceNumber).ToListAsync();
            var pickedTerm = m.Terms.FirstOrDefault(t => t.Id == term);
            m.To = (to ?? pickedTerm?.EndDate ?? _clock.UtcNow).Date;
            m.From = (from ?? pickedTerm?.StartDate ?? m.To.AddDays(-30)).Date;
            if (m.From > m.To) (m.From, m.To) = (m.To, m.From);

            var sections = await SectionsOfYearAsync(yid);
            var grades = await GradesAsync();
            var profiles = await ProfilesAsync(yid);
            m.AllSections = sections
                .Select(s => new AttendanceCorrectionsViewModel.SectionOption(s, GradeOf(s.GradeYearProfileId, profiles, grades)))
                .OrderBy(s => s.Grade?.SequenceOrder).ThenBy(s => s.Section.NameEn).ToList();

            var query = _db.AttendanceDays.AsNoTracking()
                .Where(a => a.AcademicYearId == yid && a.Date >= m.From && a.Date <= m.To);
            if (section != null) query = query.Where(a => a.SectionId == section);
            var rows = await query.ToListAsync();
            m.TotalRecords = rows.Count;

            m.OverallPercent = Percent(rows);
            m.SectionStats = rows.GroupBy(r => r.SectionId).Select(g =>
            {
                var sec = sections.FirstOrDefault(s => s.Id == g.Key);
                if (sec == null) return null;
                var list = g.ToList();
                return new AttendanceAnalyticsViewModel.SectionStat(
                    sec, GradeOf(sec.GradeYearProfileId, profiles, grades), list.Count,
                    list.Count(r => IsAbsence(r.Status)), list.Count(r => r.Status == AttendanceStatus.AbsentUnexcused),
                    list.Count(r => r.Status == AttendanceStatus.Late), list.Count(r => r.Status == AttendanceStatus.Exempted),
                    Percent(list));
            }).Where(x => x != null).Select(x => x!)
              .OrderBy(x => x.Percent).ToList();

            m.Weekdays = rows.GroupBy(r => r.Date.DayOfWeek)
                .Select(g => new AttendanceAnalyticsViewModel.WeekdayStat(
                    g.Key, g.Count(), g.Count(r => IsAbsence(r.Status)), g.Count(r => r.Status == AttendanceStatus.Late)))
                .OrderBy(w => (int)w.Day).ToList();

            var byEnrollment = rows.GroupBy(r => r.EnrollmentId).ToList();
            var students = await StudentsByEnrollmentAsync(byEnrollment.Select(g => g.Key).ToList());
            var stats = byEnrollment
                .Where(g => students.ContainsKey(g.Key))
                .Select(g =>
                {
                    var list = g.ToList();
                    var sec = sections.FirstOrDefault(s => s.Id == list[0].SectionId);
                    return new AttendanceAnalyticsViewModel.StudentStat(
                        students[g.Key], sec, GradeOf(sec?.GradeYearProfileId, profiles, grades), list.Count,
                        list.Count(r => IsAbsence(r.Status)), list.Count(r => r.Status == AttendanceStatus.AbsentUnexcused),
                        list.Count(r => r.Status == AttendanceStatus.Late), Percent(list));
                }).ToList();

            m.Chronic = stats.Where(s => s.Percent < m.ChronicBelowPercent).OrderBy(s => s.Percent).Take(50).ToList();
            m.LateLeaders = stats.Where(s => s.Late > 0).OrderByDescending(s => s.Late).Take(20).ToList();
            return View(m);
        }

        // ================================================================== helpers

        /// <summary>BR-ATD-009: every consumer on this screen set goes through the one calculator.</summary>
        private static decimal Percent(IReadOnlyList<AttendanceDay> rows)
        {
            var exempt = rows.Count(r => r.Status == AttendanceStatus.Exempted);
            var absent = rows.Count(r => IsAbsence(r.Status));
            return Math.Round(AttendancePercentageCalculator.Calculate(rows.Count, exempt, absent), 1);
        }

        private static bool IsAbsence(AttendanceStatus s) =>
            s == AttendanceStatus.AbsentExcused || s == AttendanceStatus.AbsentUnexcused || s == AttendanceStatus.MedicalLeave;

        private static bool IsWorking(DayType d) =>
            d == DayType.Working || d == DayType.Partial || d == DayType.ExamPeriodWorking;

        private DateTime CombineTime(DateTime date, string? time)
        {
            if (!string.IsNullOrWhiteSpace(time) && TimeSpan.TryParse(time, CultureInfo.InvariantCulture, out var parsed))
            {
                return date.Date.Add(parsed);
            }

            return date.Date == _clock.UtcNow.Date ? _clock.UtcNow : date.Date.AddHours(8);
        }

        private async Task FillPageAsync(AttendancePageViewModel m, int? yearId, DateTime? date)
        {
            var years = await _db.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartDate).ToListAsync();
            m.Years = years;
            m.Year = years.FirstOrDefault(y => y.Id == (yearId ?? _workingYear.AcademicYearId))
                ?? years.FirstOrDefault(y => y.Status == AcademicYearStatus.Active)
                ?? years.FirstOrDefault();
            if (m.Year == null) return;

            m.Date = (date ?? _clock.UtcNow).Date;
            m.DateInYear = m.Date >= m.Year.StartDate.Date && m.Date <= m.Year.EndDate.Date;
            m.DayType = await CalendarDayTypeAsync(m.Year.Id, m.Date);
        }

        /// <summary>BR-ATD-003 via E-103's materialized calendar; StaffOnly days are not student-working days.</summary>
        private async Task<DayType?> CalendarDayTypeAsync(int yearId, DateTime date)
        {
            var day = await _db.CalendarDays.AsNoTracking()
                .Where(d => d.AcademicYearId == yearId && d.Date == date.Date
                    && (d.Audience == CalendarAudience.All || d.Audience == CalendarAudience.StudentsOnly))
                .OrderBy(d => d.Id).FirstOrDefaultAsync();
            return day?.DayType;
        }

        private Task<List<Section>> SectionsOfYearAsync(int yearId) =>
            _db.Sections.AsNoTracking().Where(s => s.AcademicYearId == yearId && s.Status == SectionStatus.Active)
                .OrderBy(s => s.NameEn).ToListAsync();

        private Task<List<GradeLevel>> GradesAsync() =>
            _db.GradeLevels.IgnoreQueryFilters().AsNoTracking().Where(g => g.SchoolId == _db.CurrentSchoolId).ToListAsync();

        private Task<List<GradeYearProfile>> ProfilesAsync(int yearId) =>
            _db.GradeYearProfiles.IgnoreQueryFilters().AsNoTracking()
                .Where(p => p.AcademicYearId == yearId && p.SchoolId == _db.CurrentSchoolId).ToListAsync();

        private static GradeLevel? GradeOf(int? profileId, IReadOnlyList<GradeYearProfile> profiles, IReadOnlyList<GradeLevel> grades)
        {
            if (profileId == null) return null;
            var p = profiles.FirstOrDefault(x => x.Id == profileId);
            return p == null ? null : grades.FirstOrDefault(g => g.Id == p.GradeLevelId);
        }

        /// <summary>BR-ATD-003/BR-SCN-005: the section roster is membership-as-of-date, not today's membership.</summary>
        private async Task<Dictionary<int, Student>> RosterAsync(int sectionId, int yearId, DateTime date)
        {
            var day = date.Date;
            var enrollmentIds = await _db.SectionMemberships.AsNoTracking()
                .Where(x => x.SectionId == sectionId && x.AcademicYearId == yearId
                    && x.EffectiveFromUtc <= day && (x.EffectiveToUtc == null || x.EffectiveToUtc > day))
                .Select(x => x.EnrollmentId).ToListAsync();
            var active = await _db.Enrollments.AsNoTracking()
                .Where(e => enrollmentIds.Contains(e.Id) && e.Status == EnrollmentStatus.Active).ToListAsync();
            var students = await _db.Students.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.SchoolId == _db.CurrentSchoolId && active.Select(e => e.StudentId).Contains(s.Id)).ToListAsync();
            return active
                .Select(e => (e.Id, Student: students.FirstOrDefault(s => s.Id == e.StudentId)))
                .Where(x => x.Student != null)
                .ToDictionary(x => x.Id, x => x.Student!);
        }

        private async Task<Dictionary<int, Student>> StudentsByEnrollmentAsync(IReadOnlyList<int> enrollmentIds)
        {
            if (enrollmentIds.Count == 0) return new Dictionary<int, Student>();
            var ids = enrollmentIds.Distinct().ToList();
            var enrollments = await _db.Enrollments.AsNoTracking().Where(e => ids.Contains(e.Id)).ToListAsync();
            var students = await _db.Students.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.SchoolId == _db.CurrentSchoolId && enrollments.Select(e => e.StudentId).Contains(s.Id)).ToListAsync();
            var map = new Dictionary<int, Student>();
            foreach (var e in enrollments)
            {
                var s = students.FirstOrDefault(x => x.Id == e.StudentId);
                if (s != null) map[e.Id] = s;
            }

            return map;
        }

        private async Task<Dictionary<int, int>> SectionByEnrollmentAsync(IReadOnlyList<int> enrollmentIds, DateTime date)
        {
            if (enrollmentIds.Count == 0) return new Dictionary<int, int>();
            var day = date.Date;
            var ids = enrollmentIds.Distinct().ToList();
            var memberships = await _db.SectionMemberships.AsNoTracking()
                .Where(x => ids.Contains(x.EnrollmentId) && x.EffectiveFromUtc <= day && (x.EffectiveToUtc == null || x.EffectiveToUtc > day))
                .ToListAsync();
            return memberships.GroupBy(x => x.EnrollmentId).ToDictionary(g => g.Key, g => g.First().SectionId);
        }

        private async Task<List<AttendanceGateViewModel.StudentHit>> SearchAsync(
            int yearId, DateTime date, string? q, int? enrollmentId,
            IReadOnlyList<Section> sections, IReadOnlyList<GradeYearProfile> profiles, IReadOnlyList<GradeLevel> grades)
        {
            var enrollments = await _db.Enrollments.AsNoTracking()
                .Where(e => e.AcademicYearId == yearId && e.Status == EnrollmentStatus.Active).ToListAsync();
            if (enrollmentId != null) enrollments = enrollments.Where(e => e.Id == enrollmentId).ToList();

            var students = await _db.Students.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.SchoolId == _db.CurrentSchoolId && enrollments.Select(e => e.StudentId).Contains(s.Id)).ToListAsync();
            if (enrollmentId == null && !string.IsNullOrWhiteSpace(q))
            {
                var needle = q.Trim();
                students = students.Where(s =>
                    s.StudentNo.Contains(needle, StringComparison.OrdinalIgnoreCase)
                    || AttendanceLabels.StudentName(s, false).Contains(needle, StringComparison.OrdinalIgnoreCase)
                    || AttendanceLabels.StudentName(s, true).Contains(needle, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            var studentIds = students.Select(s => s.Id).ToHashSet();
            var matched = enrollments.Where(e => studentIds.Contains(e.StudentId)).Take(25).ToList();
            var ids = matched.Select(e => e.Id).ToList();
            var day = date.Date;
            var sectionByEnrollment = await SectionByEnrollmentAsync(ids, day);
            var todays = await _db.AttendanceDays.AsNoTracking().Where(a => a.Date == day && ids.Contains(a.EnrollmentId)).ToListAsync();

            return matched.Select(e =>
            {
                var sec = sectionByEnrollment.TryGetValue(e.Id, out var sid) ? sections.FirstOrDefault(s => s.Id == sid) : null;
                return new AttendanceGateViewModel.StudentHit(
                    e.Id, students.First(s => s.Id == e.StudentId), sec, GradeOf(sec?.GradeYearProfileId, profiles, grades),
                    todays.FirstOrDefault(a => a.EnrollmentId == e.Id));
            }).OrderBy(h => h.Student.StudentNo).ToList();
        }

        /// <summary>BR-ATD-004/BR-PAR-008: guardians and emergency contacts explicitly flagged as pickup-authorized.</summary>
        private async Task<List<AttendanceGateViewModel.PickupOption>> PickupListAsync(int studentId)
        {
            var now = _clock.UtcNow;
            var links = await _db.StudentGuardianLinks.AsNoTracking()
                .Where(l => l.StudentId == studentId && l.IsPickupAuthorized
                    && l.EffectiveFromUtc <= now && (l.EffectiveToUtc == null || l.EffectiveToUtc > now))
                .ToListAsync();
            var parents = await _db.Parents.IgnoreQueryFilters().AsNoTracking()
                .Where(p => p.SchoolId == _db.CurrentSchoolId && links.Select(l => l.ParentId).Contains(p.Id)).ToListAsync();
            var contacts = await _db.EmergencyContacts.AsNoTracking()
                .Where(c => c.StudentId == studentId && c.IsPickupAuthorized).ToListAsync();

            var list = parents
                .Select(p => new AttendanceGateViewModel.PickupOption(p.NameAr, p.NameEn, "guardian", p.PrimaryMobile))
                .ToList();
            list.AddRange(contacts.Select(c => new AttendanceGateViewModel.PickupOption(c.NameAr, c.NameEn, "emergency", c.Phone)));
            return list;
        }

        private async Task<List<AttendanceMonitorViewModel.AlertRow>> BuildAlertsAsync(
            int yearId, DateTime asOf, int threshold,
            IReadOnlyList<Section> sections, IReadOnlyList<GradeYearProfile> profiles, IReadOnlyList<GradeLevel> grades)
        {
            // A streak can only be as long as the window we read, so read a little past the threshold.
            var window = asOf.AddDays(-Math.Max(threshold * 4, 20));
            var recent = await _db.AttendanceDays.AsNoTracking()
                .Where(a => a.AcademicYearId == yearId && a.Date > window && a.Date <= asOf).ToListAsync();
            if (recent.Count == 0) return new List<AttendanceMonitorViewModel.AlertRow>();

            var candidates = new List<(int EnrollmentId, int SectionId, int Streak)>();
            foreach (var g in recent.GroupBy(a => a.EnrollmentId))
            {
                var ordered = g.OrderBy(a => a.Date).Select(a => a.Status == AttendanceStatus.AbsentUnexcused).ToList();
                if (!ConsecutiveAbsenceEscalationEvaluator.ShouldEscalate(ordered, threshold)) continue;
                candidates.Add((g.Key, g.OrderByDescending(a => a.Date).First().SectionId,
                    ConsecutiveAbsenceEscalationEvaluator.LongestUnexcusedStreak(ordered)));
            }

            var students = await StudentsByEnrollmentAsync(candidates.Select(c => c.EnrollmentId).ToList());
            return candidates
                .Where(c => students.ContainsKey(c.EnrollmentId))
                .Select(c => new AttendanceMonitorViewModel.AlertRow(
                    students[c.EnrollmentId], sections.FirstOrDefault(s => s.Id == c.SectionId), c.Streak, c.EnrollmentId))
                .OrderByDescending(a => a.Streak).Take(50).ToList();
        }

        /// <summary>doc §10's correction register: the AttendanceDay.Status field diffs the generic AuditCaptor already writes.</summary>
        private async Task<List<AttendanceCorrectionsViewModel.RegisterRow>> BuildRegisterAsync(int yearId, IReadOnlyList<Section> sections)
        {
            var entries = await _db.AuditEntries.AsNoTracking()
                .Where(e => e.EntityType == nameof(AttendanceDay) && e.FieldName == nameof(AttendanceDay.Status)
                    && e.SchoolId == _db.CurrentSchoolId)
                .OrderByDescending(e => e.Id).Take(50).ToListAsync();
            if (entries.Count == 0) return new List<AttendanceCorrectionsViewModel.RegisterRow>();

            var dayIds = entries.Where(e => e.EntityId != null).Select(e => (int)e.EntityId!.Value).Distinct().ToList();
            var days = await _db.AttendanceDays.AsNoTracking()
                .Where(a => dayIds.Contains(a.Id) && a.AcademicYearId == yearId).ToListAsync();
            var students = await StudentsByEnrollmentAsync(days.Select(d => d.EnrollmentId).ToList());

            return entries.Select(e =>
            {
                var day = e.EntityId == null ? null : days.FirstOrDefault(d => d.Id == (int)e.EntityId.Value);
                var student = day != null && students.TryGetValue(day.EnrollmentId, out var s) ? s : null;
                return new AttendanceCorrectionsViewModel.RegisterRow(
                    e, student, day?.Date, day == null ? null : sections.FirstOrDefault(x => x.Id == day.SectionId));
            }).ToList();
        }
    }
}
