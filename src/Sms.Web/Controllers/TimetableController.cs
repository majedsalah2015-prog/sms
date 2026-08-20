using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Calendar;
using Sms.Application.Common.Interfaces;
using Sms.Application.Setup;
using Sms.Application.Timetable;
using Sms.Domain.Calendar;
using Sms.Domain.Classrooms;
using Sms.Domain.Employees;
using Sms.Domain.Schools;
using Sms.Domain.Sections;
using Sms.Domain.Teachers;
using Sms.Domain.Timetable;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;
using Sms.Web.Timetable;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/15 §8 — E-401 (Timetable) screens over the S4 engine:
    /// 8.1 shape designer, 8.2 builder (section-week grid + teacher/room
    /// pivots, completeness meters, quality score, copy-section), 8.3
    /// conflict &amp; validation board, 8.4 publication console (checklist,
    /// diff vs operational version, WF-12 validate/publish), 8.5 daily cover
    /// console (absent teacher → affected sessions → free+qualified
    /// suggestions, one-click assign, printable summary), 8.6 session conflict
    /// queue (calendar amendments / room maintenance after publication), 8.7
    /// personal views (teacher week, printable bilingual section timetable,
    /// room schedule; the portal child view lives in PortalController).
    /// Assisted-manual by design (no solver, doc §13). Deferred: drag-drop
    /// (form-based placement instead), the absent-teacher feed from Module 12
    /// staff attendance (BR-EMP-005 not built — the console takes a manual
    /// pick), WF-12's VP approval step (Validate→Publish is direct, same
    /// status-table substitution as every other workflow here), amendment
    /// impact panel (BR-TTB-009 P2 chain), notifications (doc §12),
    /// teacher-availability hard constraints (Module 13 deferral).
    /// </summary>
    [Route("timetable")]
    public class TimetableController : Controller
    {
        private readonly ITimetableAdmin _timetable;
        private readonly ISystemSetupAdmin _setup;
        private readonly AppDbContext _db;
        private readonly IWorkingYearContext _workingYear;
        private readonly ICurrentUser _user;
        private readonly IClock _clock;

        public TimetableController(ITimetableAdmin timetable, ISystemSetupAdmin setup, AppDbContext db, IWorkingYearContext workingYear, ICurrentUser user, IClock clock)
        {
            _timetable = timetable;
            _setup = setup;
            _db = db;
            _workingYear = workingYear;
            _user = user;
            _clock = clock;
        }

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        private DateTime Today => _clock.UtcNow.Date;

        // ================================================================== 8.1 Shape designer

        [HttpGet("shape")]
        public async Task<IActionResult> Shape(int? year = null, int? stage = null)
        {
            var m = new ShapeDesignerViewModel();
            await FillYearAsync(m, year, null);
            if (m.Year == null) return View(m);

            var stages = await _db.Stages.IgnoreQueryFilters().AsNoTracking().Where(s => s.SchoolId == _db.CurrentSchoolId && s.IsActive).OrderBy(s => s.SequenceOrder).ToListAsync();
            var shapes = await _db.TimetableShapes.AsNoTracking().Where(s => s.AcademicYearId == m.Year.Id).ToListAsync();
            var slotCounts = await _db.PeriodSlots.AsNoTracking().Where(s => shapes.Select(x => x.Id).Contains(s.TimetableShapeId)).GroupBy(s => s.TimetableShapeId).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
            m.Stages = stages.Select(s => { var sh = shapes.FirstOrDefault(x => x.StageId == s.Id); return new ShapeDesignerViewModel.StageOption(s, sh, sh == null ? 0 : slotCounts.FirstOrDefault(c => c.Key == sh.Id)?.N ?? 0); }).ToList();
            m.Stage = m.Stages.FirstOrDefault(s => s.Stage.Id == stage) ?? m.Stages.FirstOrDefault();

            var (_, firstDay, workingDays) = await TimetableQueries.WeekConfigAsync(_setup, m.Year.Id);
            m.WorkingDays = workingDays;
            if (m.Stage?.Shape != null)
            {
                m.Slots = await _db.PeriodSlots.AsNoTracking().Where(s => s.TimetableShapeId == m.Stage.Shape.Id).OrderBy(s => s.DayOfWeek).ThenBy(s => s.SequenceNumber).ToListAsync();
                var slotIds = m.Slots.Select(s => s.Id).ToList();
                m.SlotsInUse = (await _db.Placements.AsNoTracking().Where(p => slotIds.Contains(p.PeriodSlotId)).Select(p => p.PeriodSlotId).Distinct().ToListAsync()).ToHashSet();
            }

            m.Days = TimetableQueries.OrderDays(workingDays.Concat(m.Slots.Select(s => s.DayOfWeek)), firstDay);
            return View(m);
        }

        [HttpPost("shape/define")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DefineShape(int year, int stageId)
        {
            try
            {
                if (await _db.TimetableShapes.AnyAsync(s => s.AcademicYearId == year && s.StageId == stageId)) throw new InvalidOperationException(T("This stage already has a shape for the year.", "لهذه المرحلة شكل جدول لهذا العام مسبقاً."));
                await _timetable.DefineShapeAsync(stageId, year);
                TempData["Flash"] = T("Shape created — add the day's period slots (BR-TTB-001).", "أُنشئ شكل الجدول — أضف حصص اليوم (BR-TTB-001).");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Shape), new { year, stage = stageId });
        }

        [HttpPost("shape/{shapeId:int}/slots")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSlot(int shapeId, int year, int stage, DayOfWeek? day, int? sequence, TimeSpan? start, TimeSpan? end, bool isBreak, bool allWorkingDays)
        {
            try
            {
                if (sequence == null || sequence <= 0 || start == null || end == null || end <= start) throw new InvalidOperationException(T("Sequence must be positive and end after start.", "يجب أن يكون الترتيب موجباً ووقت النهاية بعد البداية."));
                var (_, _, workingDays) = await TimetableQueries.WeekConfigAsync(_setup, year);
                var days = allWorkingDays ? workingDays : day == null ? throw new InvalidOperationException(T("Choose a day.", "اختر يوماً.")) : new[] { day.Value };
                var existing = await _db.PeriodSlots.AsNoTracking().Where(s => s.TimetableShapeId == shapeId).ToListAsync();
                var added = 0; var skipped = 0;
                foreach (var d in days)
                {
                    if (existing.Any(s => s.DayOfWeek == d && s.SequenceNumber == sequence)) { skipped++; continue; }
                    if (existing.Any(s => s.DayOfWeek == d && s.StartTime < end && start < s.EndTime)) { skipped++; continue; }
                    await _timetable.AddPeriodSlotAsync(shapeId, d, sequence.Value, start.Value, end.Value, isBreak);
                    added++;
                }
                TempData["Flash"] = T($"{added} slot(s) added; {skipped} skipped (sequence taken or time overlap).", $"أُضيفت {added} حصة؛ تُخطيت {skipped} (الترتيب مستخدم أو تداخل وقت).");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Shape), new { year, stage });
        }

        [HttpPost("shape/slots/{slotId:int}/remove")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveSlot(int slotId, int year, int stage)
        {
            try { await _timetable.RemovePeriodSlotAsync(slotId); TempData["Flash"] = T("Slot removed.", "حُذفت الحصة."); }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Shape), new { year, stage });
        }

        /// <summary>Visual day template: copy one day's slots onto another day (a short Friday is then just "copy Sunday, remove the last two").</summary>
        [HttpPost("shape/{shapeId:int}/copy-day")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CopyDay(int shapeId, int year, int stage, DayOfWeek fromDay, DayOfWeek toDay)
        {
            try
            {
                if (fromDay == toDay) throw new InvalidOperationException(T("Pick two different days.", "اختر يومين مختلفين."));
                var slots = await _db.PeriodSlots.AsNoTracking().Where(s => s.TimetableShapeId == shapeId).ToListAsync();
                var src = slots.Where(s => s.DayOfWeek == fromDay).ToList();
                if (src.Count == 0) throw new InvalidOperationException(T("Source day has no slots.", "اليوم المصدر بلا حصص."));
                var added = 0;
                foreach (var s in src.Where(s => !slots.Any(x => x.DayOfWeek == toDay && x.SequenceNumber == s.SequenceNumber)))
                {
                    await _timetable.AddPeriodSlotAsync(shapeId, toDay, s.SequenceNumber, s.StartTime, s.EndTime, s.IsBreak);
                    added++;
                }
                TempData["Flash"] = T($"Copied {added} slot(s) to {TimetableLabels.Day(toDay, false)}.", $"نُسخت {added} حصة إلى {TimetableLabels.Day(toDay, true)}.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Shape), new { year, stage });
        }

        // ================================================================== 8.2 Timetable builder

        [HttpGet("")]
        public async Task<IActionResult> Builder(int? year = null, int? version = null, int? section = null, string? mode = null, int? teacher = null, int? room = null)
        {
            var m = new BuilderViewModel { Mode = mode is "teacher" or "room" ? mode : "section" };
            await FillYearAsync(m, year, version);
            if (m.Year == null) return View(m);
            var (_, firstDay, _) = await TimetableQueries.WeekConfigAsync(_setup, m.Year.Id);

            var placements = m.Version == null ? new List<Placement>() : await _db.Placements.AsNoTracking().Where(p => p.TimetableVersionId == m.Version.Id).ToListAsync();
            var r = await TimetableQueries.ResolveAsync(_db, m.Year.Id, placements);

            m.Sections = r.Sections.Values.Where(s => r.SectionGrade.ContainsKey(s.Id)).Select(s => new BuilderViewModel.SectionOption(s, r.SectionGrade[s.Id].Grade, r.SectionGrade[s.Id].StageId)).OrderBy(s => s.Grade.SequenceOrder).ThenBy(s => s.Section.NameEn).ToList();
            m.Teachers = r.Teachers.Values.Select(t => new BuilderViewModel.TeacherOption(t.Profile, t.Employee)).OrderBy(t => IsArabic ? t.Employee.FirstNameAr : t.Employee.FirstNameEn).ToList();
            m.Rooms = r.Rooms.Values.Where(x => x.IsActive).OrderBy(x => x.Code).ToList();
            m.Section = m.Sections.FirstOrDefault(s => s.Section.Id == section) ?? m.Sections.FirstOrDefault();
            m.Teacher = m.Teachers.FirstOrDefault(t => t.Profile.Id == teacher) ?? m.Teachers.FirstOrDefault();
            m.Room = m.Rooms.FirstOrDefault(x => x.Id == room) ?? m.Rooms.FirstOrDefault();

            // Grid frame + cells per mode
            IEnumerable<PlacementCell> shown;
            IEnumerable<PeriodSlot> frameSlots;
            if (m.Mode == "teacher")
            {
                shown = m.Teacher == null ? Array.Empty<PlacementCell>() : r.Cells.Where(c => c.Profile.Id == m.Teacher.Profile.Id);
                frameSlots = r.Slots.Values; m.HasShape = r.Slots.Count > 0;
            }
            else if (m.Mode == "room")
            {
                shown = m.Room == null ? Array.Empty<PlacementCell>() : r.Cells.Where(c => c.Room?.Id == m.Room.Id);
                frameSlots = r.Slots.Values; m.HasShape = r.Slots.Count > 0;
            }
            else
            {
                shown = m.Section == null ? Array.Empty<PlacementCell>() : r.Cells.Where(c => c.Section.Id == m.Section.Section.Id);
                var shape = m.Section == null ? null : r.ShapesByStage.GetValueOrDefault(m.Section.StageId);
                frameSlots = shape == null ? Array.Empty<PeriodSlot>() : r.Slots.Values.Where(s => s.TimetableShapeId == shape.Id);
                m.HasShape = shape != null;
            }
            var (days, seqs, slots) = TimetableQueries.Frame(frameSlots, firstDay);
            m.Days = days; m.Sequences = seqs; m.Slots = slots; m.Cells = TimetableQueries.GroupCells(shown);

            // Completeness: the selected section's current offerings vs placed, with the teachers holding an assignment (BR-TCH-002 → picker options)
            var currentOfferings = r.Offerings.Values.Where(o => o.EffectiveToUtc == null).ToList();
            var assignments = await _db.TeacherAssignments.AsNoTracking().Where(a => a.AcademicYearId == m.Year.Id && a.EffectiveToUtc == null).ToListAsync();
            if (m.Section != null)
            {
                var sec = m.Section.Section;
                m.Offerings = currentOfferings.Where(o => o.GradeYearProfileId == sec.GradeYearProfileId && r.Subjects.ContainsKey(o.SubjectId))
                    .Select(o => new BuilderViewModel.OfferingRow(o, r.Subjects[o.SubjectId], o.WeeklyPeriods, placements.Count(p => p.SectionId == sec.Id && p.CurriculumOfferingId == o.Id),
                        assignments.Where(a => a.CurriculumOfferingId == o.Id && a.SectionId == sec.Id && r.Teachers.ContainsKey(a.TeacherProfileId)).OrderBy(a => a.Role)
                            .Select(a => new BuilderViewModel.TeacherOption(r.Teachers[a.TeacherProfileId].Profile, r.Teachers[a.TeacherProfileId].Employee)).ToList()))
                    .OrderBy(o => o.Subject.Code).ToList();
                m.CopySources = m.Sections.Where(s => s.Section.GradeYearProfileId == sec.GradeYearProfileId && s.Section.Id != sec.Id && placements.Any(p => p.SectionId == s.Section.Id)).ToList();
            }
            m.Meters = m.Sections.Select(s => new BuilderViewModel.SectionMeter(s, currentOfferings.Where(o => o.GradeYearProfileId == s.Section.GradeYearProfileId).Sum(o => o.WeeklyPeriods), placements.Count(p => p.SectionId == s.Section.Id))).ToList();

            // Quality + hard conflicts over the whole version
            var q = Quality(r);
            m.QualityScore = q.Score; m.WarningCount = q.Warnings.Count;
            m.HardConflictCount = HardConflicts(r.Cells).Count;
            return View(m);
        }

        [HttpPost("versions/define")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DefineVersion(int year, int? termId, string? returnTo)
        {
            TimetableVersion? v = null;
            try { v = await _timetable.DefineVersionAsync(year, termId); TempData["Flash"] = T($"Draft version v{v.Id} created.", $"أُنشئت المسودة v{v.Id}."); }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(returnTo == "publish" ? nameof(Publish) : nameof(Builder), new { year, version = v?.Id });
        }

        [HttpPost("place")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Place(int year, int version, int section, int slot, string? pick, int? room)
        {
            try
            {
                // pick = "{offeringId}-{teacherProfileId}" — one combined picker per cell, options are only (offering, assigned teacher) pairs (BR-TCH-002 by construction)
                var parts = (pick ?? "").Split('-');
                if (parts.Length != 2 || !int.TryParse(parts[0], out var offering) || !int.TryParse(parts[1], out var teacher)) throw new InvalidOperationException(T("Choose an offering and its assigned teacher.", "اختر المادة ومعلمها المُسنَد."));
                await _timetable.PlaceAsync(version, section, slot, offering, teacher, room);
                TempData["Flash"] = T("Placed.", "وُضعت الحصة.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Builder), new { year, version, section });
        }

        [HttpPost("placements/{id:int}/remove")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemovePlacement(int id, int year, int version, int? section, string? mode, int? teacher, int? room)
        {
            try { await _timetable.RemovePlacementAsync(id); TempData["Flash"] = T("Placement removed.", "أُزيلت الحصة."); }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Builder), new { year, version, section, mode, teacher, room });
        }

        /// <summary>Copy-section tool: replays another section's week onto this one, re-pointing each placement at the teacher assigned to the same offering in the target section (BR-TCH-002); conflicts/unassigned are skipped and counted.</summary>
        [HttpPost("copy-section")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CopySection(int year, int version, int section, int? fromSection)
        {
            try
            {
                if (fromSection == null) throw new InvalidOperationException(T("Choose a source section.", "اختر الشعبة المصدر."));
                var src = await _db.Placements.AsNoTracking().Where(p => p.TimetableVersionId == version && p.SectionId == fromSection).ToListAsync();
                var assignments = await _db.TeacherAssignments.AsNoTracking().Where(a => a.AcademicYearId == year && a.SectionId == section && a.EffectiveToUtc == null).OrderBy(a => a.Role).ToListAsync();
                var copied = 0; var skipped = 0;
                foreach (var p in src)
                {
                    var t = assignments.FirstOrDefault(a => a.CurriculumOfferingId == p.CurriculumOfferingId);
                    if (t == null) { skipped++; continue; }
                    try { await _timetable.PlaceAsync(version, section, p.PeriodSlotId, p.CurriculumOfferingId, t.TeacherProfileId, null); copied++; }
                    catch (InvalidOperationException) { skipped++; }
                }
                TempData["Flash"] = T($"Copied {copied} placement(s); {skipped} skipped (no assigned teacher, or a teacher/section conflict). Rooms are not copied.", $"نُسخت {copied} حصة؛ تُخطيت {skipped} (لا معلم مُسنَد أو تعارض معلم/شعبة). لا تُنسخ القاعات.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Builder), new { year, version, section });
        }

        // ================================================================== 8.3 Conflict & validation board

        [HttpGet("validation")]
        public async Task<IActionResult> Validation(int? year = null, int? version = null)
        {
            var m = new ValidationBoardViewModel();
            await FillYearAsync(m, year, version);
            if (m.Year == null || m.Version == null) return View(m);

            var placements = await _db.Placements.AsNoTracking().Where(p => p.TimetableVersionId == m.Version.Id).ToListAsync();
            var r = await TimetableQueries.ResolveAsync(_db, m.Year.Id, placements);
            var assignments = await _db.TeacherAssignments.AsNoTracking().Where(a => a.AcademicYearId == m.Year.Id && a.EffectiveToUtc == null).ToListAsync();
            var timetabledSections = placements.Select(p => p.SectionId).Distinct().Where(r.Sections.ContainsKey).Select(id => r.Sections[id]).ToList();

            m.Completeness = timetabledSections.SelectMany(s => r.Offerings.Values.Where(o => o.GradeYearProfileId == s.GradeYearProfileId && o.EffectiveToUtc == null && r.Subjects.ContainsKey(o.SubjectId))
                    .Select(o => new ValidationBoardViewModel.CompletenessRow(s, r.Subjects[o.SubjectId], o, o.WeeklyPeriods, placements.Count(p => p.SectionId == s.Id && p.CurriculumOfferingId == o.Id),
                        assignments.Where(a => a.CurriculumOfferingId == o.Id && a.SectionId == s.Id && r.Teachers.ContainsKey(a.TeacherProfileId)).Select(a => r.Teachers[a.TeacherProfileId].Employee).ToList())))
                .OrderBy(c => c.Placed == c.Required ? 1 : 0).ThenBy(c => c.Section.NameEn).ThenBy(c => c.Subject.Code).ToList();
            m.SectionsWithoutShape = r.Sections.Values.Where(s => r.SectionGrade.TryGetValue(s.Id, out var g) && !r.ShapesByStage.ContainsKey(g.StageId)).OrderBy(s => s.NameEn).ToList();
            m.HardConflicts = HardConflicts(r.Cells);

            var q = Quality(r);
            m.QualityScore = q.Score;
            m.Warnings = q.Warnings.Select(w => new ValidationBoardViewModel.SoftWarning(w,
                w.SectionId == null ? null : r.Sections.GetValueOrDefault(w.SectionId.Value),
                w.TeacherProfileId == null ? null : r.Teachers.GetValueOrDefault(w.TeacherProfileId.Value).Employee,
                w.CurriculumOfferingId == null || !r.Offerings.ContainsKey(w.CurriculumOfferingId.Value) ? null : r.Subjects.GetValueOrDefault(r.Offerings[w.CurriculumOfferingId.Value].SubjectId))).ToList();
            return View(m);
        }

        [HttpPost("versions/{id:int}/validate")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ValidateVersion(int id, int year, string? returnTo)
        {
            try { await _timetable.ValidateVersionAsync(id); TempData["Flash"] = T("Version validated — zero hard-constraint violations, every placed section complete (BR-TTB-002/003). Editing is now locked; reopen to change.", "تم التحقق من الإصدار — لا مخالفات صارمة وكل شعبة موضوعة مكتملة (BR-TTB-002/003). التحرير مقفل الآن؛ أعد الفتح للتعديل."); }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(returnTo == "publish" ? nameof(Publish) : nameof(Validation), new { year, version = id });
        }

        [HttpPost("versions/{id:int}/reopen")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReopenVersion(int id, int year, string? returnTo)
        {
            try { await _timetable.ReopenVersionAsync(id); TempData["Flash"] = T("Version reopened for editing.", "أُعيد فتح الإصدار للتحرير."); }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(returnTo == "publish" ? nameof(Publish) : returnTo == "builder" ? nameof(Builder) : nameof(Validation), new { year, version = id });
        }

        // ================================================================== 8.4 Publication console

        [HttpGet("publish")]
        public async Task<IActionResult> Publish(int? year = null, int? version = null)
        {
            var m = new PublishConsoleViewModel();
            await FillYearAsync(m, year, version);
            if (m.Year == null) return View(m);
            m.YearTerms = await _db.Terms.AsNoTracking().Where(t => t.AcademicYearId == m.Year.Id).OrderBy(t => t.StartDate).ToListAsync();
            var vids = m.Versions.Select(v => v.Id).ToList();
            var pCounts = await _db.Placements.AsNoTracking().Where(p => vids.Contains(p.TimetableVersionId)).GroupBy(p => p.TimetableVersionId).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
            var sCounts = await (from s in _db.Sessions.AsNoTracking() join p in _db.Placements.AsNoTracking() on s.PlacementId equals p.Id where vids.Contains(p.TimetableVersionId) group s by p.TimetableVersionId into g select new { g.Key, N = g.Count() }).ToListAsync();
            m.Rows = m.Versions.Select(v => new PublishConsoleViewModel.VersionRow(v, v.TermId == null ? null : m.Terms.GetValueOrDefault(v.TermId.Value), pCounts.FirstOrDefault(c => c.Key == v.Id)?.N ?? 0, sCounts.FirstOrDefault(c => c.Key == v.Id)?.N ?? 0)).ToList();
            m.CurrentPublished = await TimetableQueries.CurrentPublishedAsync(_db, m.Year.Id);
            var (weekend, _, _) = await TimetableQueries.WeekConfigAsync(_setup, m.Year.Id);
            m.WeekendDays = weekend.OrderBy(d => d).ToList();
            if (m.Version == null) return View(m);

            var term = m.Version.TermId == null ? null : m.Terms.GetValueOrDefault(m.Version.TermId.Value);
            var scopeStart = term?.StartDate ?? m.Year.StartDate; var scopeEnd = term?.EndDate ?? m.Year.EndDate;
            m.RangeStart = Today > scopeStart && Today <= scopeEnd ? Today : scopeStart; m.RangeEnd = scopeEnd;

            var placements = await _db.Placements.AsNoTracking().Where(p => p.TimetableVersionId == m.Version.Id).ToListAsync();
            var r = await TimetableQueries.ResolveAsync(_db, m.Year.Id, placements);
            var timetabledSections = placements.Select(p => p.SectionId).Distinct().Where(r.Sections.ContainsKey).Select(id => r.Sections[id]).ToList();
            var shortfalls = timetabledSections.SelectMany(s => r.Offerings.Values.Where(o => o.GradeYearProfileId == s.GradeYearProfileId && o.EffectiveToUtc == null)
                .Select(o => o.WeeklyPeriods - placements.Count(p => p.SectionId == s.Id && p.CurriculumOfferingId == o.Id))).Count(x => x != 0);
            var hard = HardConflicts(r.Cells).Count;
            var q = Quality(r);
            var stagesPlaced = timetabledSections.Select(s => r.SectionGrade.GetValueOrDefault(s.Id).StageId).Distinct().ToList();
            var allSections = r.Sections.Count;
            m.Checklist = new[]
            {
                new PublishConsoleViewModel.ChecklistItem(T("Shape defined for every timetabled stage (BR-TTB-001)", "شكل الجدول معرَّف لكل مرحلة موضوعة (BR-TTB-001)"), stagesPlaced.All(r.ShapesByStage.ContainsKey) && placements.Count > 0),
                new PublishConsoleViewModel.ChecklistItem(T("Placements exist", "توجد حصص موضوعة"), placements.Count > 0, T($"{placements.Count} placement(s), {timetabledSections.Count}/{allSections} sections", $"{placements.Count} حصة، {timetabledSections.Count}/{allSections} شعبة")),
                new PublishConsoleViewModel.ChecklistItem(T("Completeness — every offering of every timetabled section fully placed (BR-TTB-003)", "الاكتمال — كل مادة لكل شعبة موضوعة مكتملة (BR-TTB-003)"), shortfalls == 0 && placements.Count > 0, shortfalls == 0 ? null : T($"{shortfalls} offering/section row(s) short or over", $"{shortfalls} صف مادة/شعبة ناقص أو زائد")),
                new PublishConsoleViewModel.ChecklistItem(T("Zero hard-constraint violations (BR-TTB-004)", "لا مخالفات صارمة (BR-TTB-004)"), hard == 0, hard == 0 ? null : T($"{hard} double-booking(s)", $"{hard} حجز مزدوج")),
                new PublishConsoleViewModel.ChecklistItem(T("Soft constraints acknowledged (BR-TTB-005)", "الإقرار بالقيود المرنة (BR-TTB-005)"), q.Warnings.Count == 0, T($"quality {q.Score}/100, {q.Warnings.Count} warning(s) — acknowledge at publish", $"الجودة {q.Score}/100، {q.Warnings.Count} تنبيه — أقرّ عند النشر")),
                new PublishConsoleViewModel.ChecklistItem(T("Version Validated (WF-12)", "الإصدار مُتحقَّق (WF-12)"), m.Version.Status != TimetableVersionStatus.Draft),
            };

            if (m.CurrentPublished != null && m.CurrentPublished.Id != m.Version.Id)
            {
                var before = await _db.Placements.AsNoTracking().Where(p => p.TimetableVersionId == m.CurrentPublished.Id).ToListAsync();
                var rb = await TimetableQueries.ResolveAsync(_db, m.Year.Id, before);
                var bDict = rb.Cells.ToDictionary(c => (c.Section.Id, c.Slot.Id));
                var aDict = r.Cells.ToDictionary(c => (c.Section.Id, c.Slot.Id));
                var diff = new List<PublishConsoleViewModel.DiffRow>();
                foreach (var key in bDict.Keys.Union(aDict.Keys))
                {
                    bDict.TryGetValue(key, out var b); aDict.TryGetValue(key, out var a);
                    var sec = (a ?? b)!.Section; var slot = (a ?? b)!.Slot;
                    if (a == null) diff.Add(new PublishConsoleViewModel.DiffRow("removed", sec, slot, b, null));
                    else if (b == null) diff.Add(new PublishConsoleViewModel.DiffRow("added", sec, slot, null, a));
                    else if (a.Offering.Id != b.Offering.Id || a.Profile.Id != b.Profile.Id || a.Room?.Id != b.Room?.Id) diff.Add(new PublishConsoleViewModel.DiffRow("changed", sec, slot, b, a));
                }
                m.Diff = diff.OrderBy(d => d.Section.NameEn).ThenBy(d => d.Slot.DayOfWeek).ThenBy(d => d.Slot.SequenceNumber).ToList();
            }
            return View(m);
        }

        [HttpPost("versions/{id:int}/publish")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PublishVersion(int id, int year, DateTime? rangeStart, DateTime? rangeEnd, bool acknowledgeSoft)
        {
            try
            {
                if (rangeStart == null || rangeEnd == null || rangeEnd < rangeStart) throw new InvalidOperationException(T("Give a valid session-generation range.", "حدد نطاقاً صالحاً لتوليد الحصص."));
                if (!acknowledgeSoft) throw new InvalidOperationException(T("Acknowledge the soft-constraint warnings to publish (BR-TTB-005).", "أقرّ بتنبيهات القيود المرنة للنشر (BR-TTB-005)."));
                var (weekend, _, _) = await TimetableQueries.WeekConfigAsync(_setup, year);
                await _timetable.PublishAsync(id, _user.UserId, DateTime.SpecifyKind(rangeStart.Value, DateTimeKind.Utc), DateTime.SpecifyKind(rangeEnd.Value, DateTimeKind.Utc), weekend);
                var n = await (from s in _db.Sessions join p in _db.Placements on s.PlacementId equals p.Id where p.TimetableVersionId == id select s.Id).CountAsync();
                TempData["Flash"] = T($"Published — {n} dated sessions generated on working days (BR-TTB-006). Portal and personal views now read this version.", $"تم النشر — وُلِّدت {n} حصة مؤرخة في أيام العمل (BR-TTB-006). البوابة والعروض الشخصية تقرأ هذا الإصدار الآن.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Publish), new { year, version = id });
        }

        // ================================================================== 8.5 Daily cover console

        [HttpGet("cover")]
        public async Task<IActionResult> Cover(DateTime? date = null, int? teacher = null, bool print = false)
        {
            var d = (date ?? Today).Date;
            var m = new CoverConsoleViewModel { Date = d, Print = print };
            var yearId = _workingYear.AcademicYearId;
            var (weekend, _, _) = await TimetableQueries.WeekConfigAsync(_setup, yearId);
            var overrides = await _db.CalendarDays.AsNoTracking().Where(x => x.AcademicYearId == yearId && x.Date == d).ToDictionaryAsync(x => x.Date.Date, x => x.DayType);
            m.IsWorkingDay = CalendarDayResolver.Resolve(d, weekend, overrides) == DayType.Working;

            var sessions = await _db.Sessions.AsNoTracking().Where(s => s.Date == d).ToListAsync();
            var pids = sessions.Select(s => s.PlacementId).Distinct().ToList();
            var placements = await _db.Placements.AsNoTracking().Where(p => pids.Contains(p.Id)).ToListAsync();
            var r = await TimetableQueries.ResolveAsync(_db, yearId, placements);
            var subs = await _db.Substitutions.AsNoTracking().Where(s => sessions.Select(x => x.Id).Contains(s.SessionId)).ToListAsync();
            m.Rooms = r.Rooms.Values.Where(x => x.IsActive).OrderBy(x => x.Code).ToList();
            m.TotalSessions = sessions.Count;

            var rows = sessions.Select(s => (Session: s, Cell: r.Cell(s.PlacementId))).Where(x => x.Cell != null).Select(x => (x.Session, Cell: x.Cell!)).ToList();
            m.Teachers = rows.GroupBy(x => x.Cell.Profile.Id).Select(g => new CoverConsoleViewModel.TeacherOption(g.First().Cell.Profile, g.First().Cell.Teacher, g.Count(), g.Count(x => x.Session.Status == SessionStatus.Held)))
                .OrderBy(t => IsArabic ? t.Employee.FirstNameAr : t.Employee.FirstNameEn).ToList();
            m.AbsentTeacher = m.Teachers.FirstOrDefault(t => t.Profile.Id == teacher);

            CoverConsoleViewModel.SessionRow Row((Session Session, PlacementCell Cell) x, IReadOnlyList<CoverConsoleViewModel.Candidate> candidates)
            {
                var sub = subs.Where(s => s.SessionId == x.Session.Id).OrderByDescending(s => s.AssignedAtUtc).FirstOrDefault();
                var subEmp = sub == null ? null : r.Teachers.GetValueOrDefault(sub.SubstituteTeacherProfileId).Employee;
                var room = x.Session.OverrideRoomId != null ? r.Rooms.GetValueOrDefault(x.Session.OverrideRoomId.Value) : x.Cell.Room;
                return new CoverConsoleViewModel.SessionRow(x.Session, x.Cell, room, sub, subEmp, candidates);
            }

            if (m.AbsentTeacher != null)
            {
                // Substitute suggestions: free at that time (no non-cancelled session of their own whose slot overlaps, not already covering then) and
                // qualified by the same proxy the engine uses (holds an assignment for the offering anywhere) — supervise-only candidates listed after.
                var assignedOfferings = await _db.TeacherAssignments.AsNoTracking().Where(a => a.AcademicYearId == yearId).Select(a => new { a.TeacherProfileId, a.CurriculumOfferingId }).Distinct().ToListAsync();
                var activeProfiles = r.Teachers.Values.Where(t => t.Employee.Status == EmployeeStatus.Active).ToList();
                m.Affected = rows.Where(x => x.Cell.Profile.Id == m.AbsentTeacher.Profile.Id).OrderBy(x => x.Cell.Slot.StartTime).Select(x =>
                {
                    bool Busy(int profileId) =>
                        rows.Any(o => o.Cell.Profile.Id == profileId && o.Session.Status != SessionStatus.Cancelled && o.Session.Status != SessionStatus.Substituted && Overlaps(o.Cell.Slot, x.Cell.Slot))
                        || subs.Any(s => s.SubstituteTeacherProfileId == profileId && rows.Any(o => o.Session.Id == s.SessionId && Overlaps(o.Cell.Slot, x.Cell.Slot)));
                    var candidates = activeProfiles.Where(t => t.Profile.Id != m.AbsentTeacher.Profile.Id && !Busy(t.Profile.Id))
                        .Select(t => new CoverConsoleViewModel.Candidate(t.Profile, t.Employee, assignedOfferings.Any(a => a.TeacherProfileId == t.Profile.Id && a.CurriculumOfferingId == x.Cell.Offering.Id)))
                        .OrderByDescending(c => c.Qualified).ThenBy(c => IsArabic ? c.Employee.FirstNameAr : c.Employee.FirstNameEn).ToList();
                    return Row(x, candidates);
                }).ToList();
            }

            m.Summary = rows.Where(x => x.Session.Status != SessionStatus.Held).OrderBy(x => x.Cell.Slot.StartTime).ThenBy(x => x.Cell.Section.NameEn).Select(x => Row(x, Array.Empty<CoverConsoleViewModel.Candidate>())).ToList();
            return View(m);
        }

        [HttpPost("sessions/{id:int}/substitute")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Substitute(int id, DateTime date, int teacher, int? substituteProfileId, string? reason, bool superviseOnly, bool notCounted)
        {
            try
            {
                if (substituteProfileId == null) throw new InvalidOperationException(T("Choose a substitute.", "اختر معلماً بديلاً."));
                if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException(T("A reason is required (BR-TTB-010).", "السبب مطلوب (BR-TTB-010)."));
                await _timetable.AssignSubstituteAsync(id, substituteProfileId.Value, reason.Trim(), superviseOnly, !notCounted);
                TempData["Flash"] = T("Substitute assigned for this dated session only (BR-TTB-007)." + (superviseOnly ? " Supervise-only — flagged, not marked qualified teaching." : ""), "عُيِّن البديل لهذه الحصة المؤرخة فقط (BR-TTB-007)." + (superviseOnly ? " إشراف فقط — مُعلَّم، لا يُعد تدريساً مؤهلاً." : ""));
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Cover), new { date = date.ToString("yyyy-MM-dd"), teacher });
        }

        [HttpPost("sessions/{id:int}/room")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeRoom(int id, int? roomId, string? reason, string? returnTo, DateTime? date, int? teacher, int? days)
        {
            try
            {
                if (roomId == null) throw new InvalidOperationException(T("Choose a room.", "اختر قاعة."));
                if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException(T("A reason is required (BR-TTB-008).", "السبب مطلوب (BR-TTB-008)."));
                await _timetable.ChangeSessionRoomAsync(id, roomId.Value, reason.Trim());
                TempData["Flash"] = T("Room changed for this session (BR-TTB-008) — visible on every view immediately.", "غُيِّرت القاعة لهذه الحصة (BR-TTB-008) — تظهر في كل العروض فوراً.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return returnTo == "conflicts" ? RedirectToAction(nameof(Conflicts), new { from = date?.ToString("yyyy-MM-dd"), days }) : RedirectToAction(nameof(Cover), new { date = date?.ToString("yyyy-MM-dd"), teacher });
        }

        [HttpPost("sessions/{id:int}/cancel")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelSession(int id, string? reason, string? returnTo, DateTime? date, int? teacher, int? days)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException(T("A reason is required (BR-TTB-010).", "السبب مطلوب (BR-TTB-010)."));
                await _timetable.CancelSessionAsync(id, reason.Trim());
                TempData["Flash"] = T("Session cancelled (logged with reason).", "أُلغيت الحصة (سُجِّلت مع السبب).");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return returnTo == "conflicts" ? RedirectToAction(nameof(Conflicts), new { from = date?.ToString("yyyy-MM-dd"), days }) : RedirectToAction(nameof(Cover), new { date = date?.ToString("yyyy-MM-dd"), teacher });
        }

        // ================================================================== 8.6 Session conflict queue

        [HttpGet("conflicts")]
        public async Task<IActionResult> Conflicts(DateTime? from = null, int days = 30)
        {
            var start = (from ?? Today).Date; var end = start.AddDays(Math.Clamp(days, 1, 120));
            var m = new ConflictQueueViewModel { From = start, Days = Math.Clamp(days, 1, 120) };
            var yearId = _workingYear.AcademicYearId;
            var (weekend, _, _) = await TimetableQueries.WeekConfigAsync(_setup, yearId);
            var overrides = await _db.CalendarDays.AsNoTracking().Where(x => x.AcademicYearId == yearId && x.Date >= start && x.Date < end).ToDictionaryAsync(x => x.Date.Date, x => x.DayType);
            var sessions = await _db.Sessions.AsNoTracking().Where(s => s.Date >= start && s.Date < end && s.Status != SessionStatus.Cancelled).ToListAsync();
            var pids = sessions.Select(s => s.PlacementId).Distinct().ToList();
            var placements = await _db.Placements.AsNoTracking().Where(p => pids.Contains(p.Id)).ToListAsync();
            var r = await TimetableQueries.ResolveAsync(_db, yearId, placements);
            var roomExceptions = await _db.RoomAvailabilityExceptions.AsNoTracking().Where(x => x.StartDate < end && x.EndDate >= start).ToListAsync();
            m.Rooms = r.Rooms.Values.Where(x => x.IsActive).OrderBy(x => x.Code).ToList();

            var rows = new List<ConflictQueueViewModel.Row>();
            foreach (var s in sessions)
            {
                var cell = r.Cell(s.PlacementId); if (cell == null) continue;
                var dayType = CalendarDayResolver.Resolve(s.Date, weekend, overrides);
                if (dayType != DayType.Working)
                {
                    rows.Add(new ConflictQueueViewModel.Row(s, cell, "calendar", T($"Day is now {dayType} (calendar amendment after publication, BR-CAL-004/BR-TTB-006)", $"اليوم أصبح {dayType} (تعديل تقويم بعد النشر، BR-CAL-004/BR-TTB-006)")));
                    continue;
                }
                var roomId = s.OverrideRoomId ?? cell.Placement.RoomId;
                var ex = roomId == null ? null : roomExceptions.FirstOrDefault(x => x.RoomId == roomId && x.StartDate.Date <= s.Date.Date && x.EndDate.Date >= s.Date.Date);
                if (ex != null)
                {
                    var room = r.Rooms.GetValueOrDefault(roomId!.Value);
                    rows.Add(new ConflictQueueViewModel.Row(s, cell, "room", T($"Room {room?.Code} unavailable: {ex.Reason} (BR-ROM-004/BR-TTB-006)", $"القاعة {room?.Code} غير متاحة: {ex.Reason} (BR-ROM-004/BR-TTB-006)")));
                }
            }
            m.Rows = rows.OrderBy(x => x.Session.Date).ThenBy(x => x.Cell.Slot.StartTime).ToList();
            return View(m);
        }

        // ================================================================== 8.7 Personal views

        [HttpGet("teachers/{id:int}")]
        public async Task<IActionResult> Teacher(int id, int? year = null, int? version = null, bool print = false)
        {
            var m = await TimetableQueries.PersonalAsync(_db, _setup, "teacher", id, year ?? _workingYear.AcademicYearId, Today, version, IsArabic);
            m.Print = print; m.BackId = id;
            return View("Personal", m);
        }

        [HttpGet("sections/{id:int}")]
        public async Task<IActionResult> Section(int id, int? year = null, int? version = null, bool print = false)
        {
            var m = await TimetableQueries.PersonalAsync(_db, _setup, "section", id, year ?? _workingYear.AcademicYearId, Today, version, IsArabic);
            m.Print = print; m.BackId = id;
            return View("Personal", m);
        }

        [HttpGet("rooms/{id:int}")]
        public async Task<IActionResult> Room(int id, int? year = null, int? version = null, bool print = false)
        {
            var m = await TimetableQueries.PersonalAsync(_db, _setup, "room", id, year ?? _workingYear.AcademicYearId, Today, version, IsArabic);
            m.Print = print; m.BackId = id;
            return View("Personal", m);
        }

        // ================================================================== helpers

        /// <summary>BR-TTB-005 soft-constraint pass over a resolved version; break/assembly slots are passed so they never count as teacher idle gaps.</summary>
        private static TimetableQualityEvaluator.Result Quality(TimetableQueries.Resolved r) =>
            TimetableQualityEvaluator.Evaluate(
                r.Cells.Select(c => new TimetableQualityEvaluator.PlacedPeriod(c.Placement.Id, c.Section.Id, c.Offering.Id, c.Profile.Id, c.Slot.DayOfWeek, c.Slot.SequenceNumber)),
                r.Slots.Values.Where(s => s.IsBreak).Select(s => (s.DayOfWeek, s.SequenceNumber)).ToHashSet());

        private static bool Overlaps(PeriodSlot a, PeriodSlot b) => a.DayOfWeek == b.DayOfWeek && a.StartTime < b.EndTime && b.StartTime < a.EndTime;

        /// <summary>BR-TTB-004 double-bookings as they stand (the engine blocks new ones; this catches anything that slipped in through data changes, e.g. a room merged later).</summary>
        private IReadOnlyList<ValidationBoardViewModel.HardConflict> HardConflicts(IReadOnlyList<PlacementCell> cells)
        {
            var list = new List<ValidationBoardViewModel.HardConflict>();
            foreach (var g in cells.GroupBy(c => (c.Slot.Id, c.Profile.Id)).Where(g => g.Count() > 1)) list.Add(new ValidationBoardViewModel.HardConflict("teacher", g.First().Slot, g.ToList()));
            foreach (var g in cells.GroupBy(c => (c.Slot.Id, c.Section.Id)).Where(g => g.Count() > 1)) list.Add(new ValidationBoardViewModel.HardConflict("section", g.First().Slot, g.ToList()));
            foreach (var g in cells.Where(c => c.Room != null).GroupBy(c => (c.Slot.Id, c.Room!.Id)).Where(g => g.Count() > 1)) list.Add(new ValidationBoardViewModel.HardConflict("room", g.First().Slot, g.ToList()));
            return list;
        }

        private async Task FillYearAsync(TimetableYearViewModel m, int? yearId, int? versionId)
        {
            m.Years = await _db.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartDate).ToListAsync();
            m.Year = m.Years.FirstOrDefault(y => y.Id == (yearId ?? _workingYear.AcademicYearId)) ?? m.Years.FirstOrDefault(y => y.Status == AcademicYearStatus.Active) ?? m.Years.FirstOrDefault();
            if (m.Year == null) return;
            m.Versions = await _db.TimetableVersions.AsNoTracking().Where(v => v.AcademicYearId == m.Year.Id).OrderByDescending(v => v.Id).ToListAsync();
            m.Terms = await _db.Terms.AsNoTracking().Where(t => t.AcademicYearId == m.Year.Id).ToDictionaryAsync(t => t.Id);
            // default: the requested version, else the newest Draft/Validated (the one being worked on), else the operational one
            m.Version = m.Versions.FirstOrDefault(v => v.Id == versionId)
                ?? m.Versions.FirstOrDefault(v => v.Status != TimetableVersionStatus.Published)
                ?? m.Versions.OrderByDescending(v => v.PublishedAtUtc).FirstOrDefault();
        }
    }
}
