using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Classrooms;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Classrooms;
using Sms.Domain.Schools;
using Sms.Domain.Timetable;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/08 §8.1–8.4: room catalog as a building/floor tree with
    /// capacity columns and feature tags, room detail (features, maintenance/
    /// reserved windows, bookings, sections using it), maintenance console
    /// and booking request. §8.2's session overlay, §8.3's impacted-session
    /// count and §8.5 utilization heatmap need Timetable (M15) sessions —
    /// deferred to those screens.
    /// </summary>
    [Route("rooms")]
    public class RoomsController : Controller
    {
        private readonly IRoomAdmin _rooms;
        private readonly AppDbContext _db;
        private readonly IWorkingYearContext _workingYear;
        private readonly ICurrentUser _currentUser;
        private readonly IClock _clock;

        public RoomsController(IRoomAdmin rooms, AppDbContext db, IWorkingYearContext workingYear, ICurrentUser currentUser, IClock clock)
        {
            _rooms = rooms;
            _db = db;
            _workingYear = workingYear;
            _currentUser = currentUser;
            _clock = clock;
        }

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        [HttpGet("")]
        [RequirePermission(ScreenCatalog.Modules.Classrooms, ScreenCatalog.Classrooms.Rooms, ActionVerb.View)]
        public async Task<IActionResult> Index()
        {
            var buildings = await _db.Buildings.AsNoTracking().OrderBy(b => b.Name.NameEn).ToListAsync();
            var floors = await _db.Floors.AsNoTracking().OrderBy(f => f.SequenceOrder).ToListAsync();
            var rooms = await _db.Rooms.AsNoTracking().OrderBy(r => r.Code).ToListAsync();
            var features = await _db.RoomFeatures.AsNoTracking().ToListAsync();
            var (types, featureNames) = await LookupsAsync();
            var today = _clock.UtcNow.Date;
            var unavailable = await _db.RoomAvailabilityExceptions.AsNoTracking().Where(x => x.StartDate <= today && x.EndDate >= today).Select(x => x.RoomId).Distinct().ToListAsync();
            var sectionsUsing = await _db.Sections.AsNoTracking().Where(s => s.DefaultClassroomId != null && s.Status == Sms.Domain.Sections.SectionStatus.Active).GroupBy(s => s.DefaultClassroomId!.Value).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);

            RoomCatalogViewModel.RoomRow Row(Room r) => new(r,
                types.FirstOrDefault(t => t.Id == r.RoomTypeLookupId) is { } t ? (IsArabic ? t.Ar : t.En) : "?",
                features.Where(f => f.RoomId == r.Id).Select(f => featureNames.FirstOrDefault(x => x.Id == f.FeatureLookupId) is { } fn ? (IsArabic ? fn.Ar : fn.En) : "?").ToList(),
                unavailable.Contains(r.Id), sectionsUsing.TryGetValue(r.Id, out var n) ? n : null);

            return View(new RoomCatalogViewModel
            {
                Buildings = buildings.Select(b => new RoomCatalogViewModel.BuildingNode(b, floors.Where(f => f.BuildingId == b.Id).Select(f => new RoomCatalogViewModel.FloorNode(f, rooms.Where(r => r.FloorId == f.Id).Select(Row).ToList())).ToList())).ToList(),
                RoomTypes = types,
                Floors = floors.Select(f => { var b = buildings.First(x => x.Id == f.BuildingId); return (f.Id, b.Name.NameAr, b.Name.NameEn, f.Name.NameAr, f.Name.NameEn); }).ToList(),
                TotalRooms = rooms.Count,
                TotalSeats = rooms.Sum(r => r.StandardCapacity),
                FloorOrder = floors.Count + 1,
            });
        }

        [HttpPost("building")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Classrooms, ScreenCatalog.Classrooms.Buildings, ActionVerb.Create)]
        public async Task<IActionResult> DefineBuilding(RoomCatalogViewModel form)
        {
            try { Require(form.BuildingNameAr, "Name (Arabic)"); Require(form.BuildingNameEn, "Name (English)"); await _rooms.DefineBuildingAsync(form.BuildingNameAr!, form.BuildingNameEn!); TempData["Flash"] = T("Building created.", "تم إنشاء المبنى."); }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("floor")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Classrooms, ScreenCatalog.Classrooms.Buildings, ActionVerb.Create)]
        public async Task<IActionResult> DefineFloor(RoomCatalogViewModel form)
        {
            try
            {
                if (form.FloorBuildingId == null) throw new InvalidOperationException(T("Choose a building.", "اختر مبنى."));
                Require(form.FloorNameAr, "Name (Arabic)"); Require(form.FloorNameEn, "Name (English)");
                await _rooms.DefineFloorAsync(form.FloorBuildingId.Value, form.FloorNameAr!, form.FloorNameEn!, form.FloorOrder ?? 1);
                TempData["Flash"] = T("Floor created.", "تم إنشاء الطابق.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("room")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Classrooms, ScreenCatalog.Classrooms.Rooms, ActionVerb.Create)]
        public async Task<IActionResult> DefineRoom(RoomCatalogViewModel form)
        {
            try
            {
                if (form.RoomFloorId == null || form.RoomTypeId == null) throw new InvalidOperationException(T("Choose a floor and a room type.", "اختر طابقاً ونوع القاعة."));
                Require(form.RoomCode, "Code"); Require(form.RoomNameAr, "Name (Arabic)"); Require(form.RoomNameEn, "Name (English)");
                var r = await _rooms.DefineRoomAsync(form.RoomFloorId.Value, form.RoomCode!.Trim().ToUpperInvariant(), form.RoomNameAr!, form.RoomNameEn!, form.RoomTypeId.Value, form.StandardCapacity ?? 30, form.ExamCapacity ?? 20, form.WingTag);
                TempData["Flash"] = T("Room created.", "تم إنشاء القاعة.");
                return RedirectToAction(nameof(Details), new { id = r.Id });
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Index));
        }

        // --- Edit / delete (soft: deactivate) for building / floor / room ---------

        [HttpGet("building/{id:int}/edit")]
        [RequirePermission(ScreenCatalog.Modules.Classrooms, ScreenCatalog.Classrooms.Buildings, ActionVerb.Edit)]
        public async Task<IActionResult> EditBuilding(int id)
        {
            var b = await _db.Buildings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (b == null) return NotFound();
            return View("Edit", new RoomEditViewModel { Id = id, Kind = "building", NameAr = b.Name.NameAr, NameEn = b.Name.NameEn, ChildCount = await _db.Floors.CountAsync(f => f.BuildingId == id) });
        }

        [HttpPost("building/{id:int}/edit")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Classrooms, ScreenCatalog.Classrooms.Buildings, ActionVerb.Edit)]
        public async Task<IActionResult> EditBuilding(int id, RoomEditViewModel form)
        {
            form.Id = id; form.Kind = "building";
            try
            {
                Require(form.NameAr, T("Name (Arabic)", "الاسم (عربي)")); Require(form.NameEn, T("Name (English)", "الاسم (إنجليزي)"));
                await _rooms.UpdateBuildingAsync(id, form.NameAr!.Trim(), form.NameEn!.Trim());
                TempData["Flash"] = T("Building updated.", "تم تحديث المبنى.");
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex) { ModelState.AddModelError(string.Empty, UserMessage.For(ex, IsArabic)); return View("Edit", form); }
        }

        [HttpPost("building/{id:int}/delete")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Classrooms, ScreenCatalog.Classrooms.Buildings, ActionVerb.Deactivate)]
        public async Task<IActionResult> DeleteBuilding(int id)
        {
            try { await _rooms.DeactivateBuildingAsync(id); TempData["Flash"] = T("Building removed (deactivated).", "تم حذف المبنى (إلغاء تفعيل)."); }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("floor/{id:int}/edit")]
        [RequirePermission(ScreenCatalog.Modules.Classrooms, ScreenCatalog.Classrooms.Buildings, ActionVerb.Edit)]
        public async Task<IActionResult> EditFloor(int id)
        {
            var f = await _db.Floors.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (f == null) return NotFound();
            return View("Edit", await FillEditListsAsync(new RoomEditViewModel { Id = id, Kind = "floor", NameAr = f.Name.NameAr, NameEn = f.Name.NameEn, BuildingId = f.BuildingId, Order = f.SequenceOrder, ChildCount = await _db.Rooms.CountAsync(r => r.FloorId == id) }));
        }

        [HttpPost("floor/{id:int}/edit")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Classrooms, ScreenCatalog.Classrooms.Buildings, ActionVerb.Edit)]
        public async Task<IActionResult> EditFloor(int id, RoomEditViewModel form)
        {
            form.Id = id; form.Kind = "floor";
            try
            {
                if (form.BuildingId == null) throw new InvalidOperationException(T("Choose a building.", "اختر مبنى."));
                Require(form.NameAr, T("Name (Arabic)", "الاسم (عربي)")); Require(form.NameEn, T("Name (English)", "الاسم (إنجليزي)"));
                await _rooms.UpdateFloorAsync(id, form.BuildingId.Value, form.NameAr!.Trim(), form.NameEn!.Trim(), form.Order ?? 1);
                TempData["Flash"] = T("Floor updated.", "تم تحديث الطابق.");
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex) { ModelState.AddModelError(string.Empty, UserMessage.For(ex, IsArabic)); return View("Edit", await FillEditListsAsync(form)); }
        }

        [HttpPost("floor/{id:int}/delete")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Classrooms, ScreenCatalog.Classrooms.Buildings, ActionVerb.Deactivate)]
        public async Task<IActionResult> DeleteFloor(int id)
        {
            try { await _rooms.DeactivateFloorAsync(id); TempData["Flash"] = T("Floor removed (deactivated).", "تم حذف الطابق (إلغاء تفعيل)."); }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("{id:int}/edit")]
        [RequirePermission(ScreenCatalog.Modules.Classrooms, ScreenCatalog.Classrooms.Rooms, ActionVerb.Edit)]
        public async Task<IActionResult> EditRoom(int id)
        {
            var r = await _db.Rooms.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (r == null) return NotFound();
            return View("Edit", await FillEditListsAsync(new RoomEditViewModel
            {
                Id = id, Kind = "room", NameAr = r.Name.NameAr, NameEn = r.Name.NameEn, FloorId = r.FloorId, Code = r.Code, RoomTypeId = r.RoomTypeLookupId,
                StandardCapacity = r.StandardCapacity, ExamCapacity = r.ExamCapacity, WingTag = r.WingTag,
                ChildCount = await _db.Sections.CountAsync(s => s.DefaultClassroomId == id && s.Status == Sms.Domain.Sections.SectionStatus.Active),
            }));
        }

        [HttpPost("{id:int}/edit")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Classrooms, ScreenCatalog.Classrooms.Rooms, ActionVerb.Edit)]
        public async Task<IActionResult> EditRoom(int id, RoomEditViewModel form)
        {
            form.Id = id; form.Kind = "room";
            try
            {
                if (form.FloorId == null || form.RoomTypeId == null) throw new InvalidOperationException(T("Choose a floor and a room type.", "اختر طابقاً ونوع القاعة."));
                Require(form.Code, T("Code", "الرمز")); Require(form.NameAr, T("Name (Arabic)", "الاسم (عربي)")); Require(form.NameEn, T("Name (English)", "الاسم (إنجليزي)"));
                await _rooms.UpdateRoomAsync(id, form.FloorId.Value, form.Code!.Trim().ToUpperInvariant(), form.NameAr!.Trim(), form.NameEn!.Trim(), form.RoomTypeId.Value, form.StandardCapacity ?? 30, form.ExamCapacity ?? 20, form.WingTag);
                TempData["Flash"] = T("Room updated.", "تم تحديث القاعة.");
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (InvalidOperationException ex) { ModelState.AddModelError(string.Empty, UserMessage.For(ex, IsArabic)); return View("Edit", await FillEditListsAsync(form)); }
        }

        [HttpPost("{id:int}/delete")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Classrooms, ScreenCatalog.Classrooms.Rooms, ActionVerb.Deactivate)]
        public async Task<IActionResult> DeleteRoom(int id)
        {
            try { await _rooms.DeactivateRoomAsync(id); TempData["Flash"] = T("Room removed (deactivated; bookings/history kept).", "تم حذف القاعة (إلغاء تفعيل مع حفظ الحجوزات والسجل)."); }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Index));
        }

        private async Task<RoomEditViewModel> FillEditListsAsync(RoomEditViewModel m)
        {
            var buildings = await _db.Buildings.AsNoTracking().OrderBy(b => b.Name.NameEn).ToListAsync();
            var floors = await _db.Floors.AsNoTracking().OrderBy(f => f.SequenceOrder).ToListAsync();
            m.Buildings = buildings;
            m.Floors = floors.Select(f => { var b = buildings.First(x => x.Id == f.BuildingId); return (f.Id, b.Name.NameAr, b.Name.NameEn, f.Name.NameAr, f.Name.NameEn); }).ToList();
            m.RoomTypes = (await LookupsAsync()).Types;
            return m;
        }

        [HttpGet("{id:int}")]
        [RequirePermission(ScreenCatalog.Modules.Classrooms, ScreenCatalog.Classrooms.Rooms, ActionVerb.View)]
        public async Task<IActionResult> Details(int id)
        {
            var room = await _db.Rooms.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id);
            if (room == null) return NotFound();
            var floor = await _db.Floors.AsNoTracking().SingleAsync(f => f.Id == room.FloorId);
            var building = await _db.Buildings.AsNoTracking().SingleAsync(b => b.Id == floor.BuildingId);
            var (types, featureNames) = await LookupsAsync();
            var have = await _db.RoomFeatures.AsNoTracking().Where(f => f.RoomId == id).Select(f => f.FeatureLookupId).ToListAsync();
            return View(new RoomDetailViewModel
            {
                Room = room, Floor = floor, Building = building,
                TypeName = types.FirstOrDefault(t => t.Id == room.RoomTypeLookupId) is { } t ? (IsArabic ? t.Ar : t.En) : "?",
                Features = featureNames.Where(f => have.Contains(f.Id)).ToList(),
                AvailableFeatures = featureNames.Where(f => !have.Contains(f.Id)).ToList(),
                Unavailability = await _db.RoomAvailabilityExceptions.AsNoTracking().Where(x => x.RoomId == id).OrderByDescending(x => x.StartDate).ToListAsync(),
                Bookings = await _db.RoomBookings.AsNoTracking().Where(b => b.RoomId == id).OrderByDescending(b => b.StartUtc).Take(50).ToListAsync(),
                SectionsUsing = await _db.Sections.AsNoTracking().Where(s => s.DefaultClassroomId == id).ToListAsync(),
                Years = await _db.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartDate).ToListAsync(),
                WorkingYearId = _workingYear.AcademicYearId,
                ImpactedSessions = await ImpactedSessionsAsync(id),
                Week = await RoomWeekAsync(id),
            });
        }

        /// <summary>
        /// doc/Modules/08 §8.3: how many timetabled sessions each maintenance window
        /// covers. Closing a room for a fortnight is a different decision when it costs
        /// forty sessions than when it costs none, and until now the screen showed the
        /// window without ever saying which.
        /// <para>
        /// Counted on <c>Session</c> rather than on the weekly placement, because a
        /// session is the thing that actually falls on a date — and it honours
        /// <c>OverrideRoomId</c>, so a class already moved out of this room for that
        /// day is not counted as impacted by closing it.
        /// </para>
        /// </summary>
        private async Task<IReadOnlyDictionary<int, int>> ImpactedSessionsAsync(int roomId)
        {
            var windows = await _db.RoomAvailabilityExceptions.AsNoTracking()
                .Where(x => x.RoomId == roomId).ToListAsync();
            if (windows.Count == 0)
            {
                return new Dictionary<int, int>();
            }

            var earliest = windows.Min(w => w.StartDate);
            var latest = windows.Max(w => w.EndDate);

            var sessions = await (
                from s in _db.Sessions.AsNoTracking()
                join p in _db.Placements.AsNoTracking() on s.PlacementId equals p.Id
                where s.Date >= earliest && s.Date <= latest
                      && (s.OverrideRoomId == roomId || (s.OverrideRoomId == null && p.RoomId == roomId))
                select s.Date).ToListAsync();

            return windows.ToDictionary(
                w => w.Id,
                w => sessions.Count(d => d >= w.StartDate && d <= w.EndDate));
        }

        /// <summary>
        /// doc/Modules/08 §8.2's "sessions from timetable overlaid read-only" — the
        /// room's own week off the published version. A room does not own its
        /// timetable, so nothing here edits: it answers "what happens in here", which
        /// is the question somebody standing in the doorway is asking.
        /// </summary>
        private async Task<IReadOnlyList<RoomWeekSlot>> RoomWeekAsync(int roomId)
        {
            var version = await PublishedVersionAsync(_workingYear.AcademicYearId);
            if (version == null)
            {
                return Array.Empty<RoomWeekSlot>();
            }

            var rows = await (
                from p in _db.Placements.AsNoTracking()
                where p.TimetableVersionId == version.Id && p.RoomId == roomId
                join slot in _db.PeriodSlots.AsNoTracking() on p.PeriodSlotId equals slot.Id
                select new { slot.DayOfWeek, slot.SequenceNumber, slot.StartTime, slot.EndTime, p.SectionId, p.CurriculumOfferingId, p.TeacherProfileId })
                .ToListAsync();

            if (rows.Count == 0)
            {
                return Array.Empty<RoomWeekSlot>();
            }

            // IgnoreQueryFilters throughout: a placement keeps naming the section,
            // subject and teacher it was made with, and a room's week must still read
            // after any of them is retired.
            var sectionIds = rows.Select(r => r.SectionId).Distinct().ToList();
            var sections = await _db.Sections.IgnoreQueryFilters().AsNoTracking()
                .Where(s => sectionIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => IsArabic ? s.NameAr : s.NameEn);

            var offeringIds = rows.Select(r => r.CurriculumOfferingId).Distinct().ToList();
            var subjects = await (
                from o in _db.CurriculumOfferings.IgnoreQueryFilters().AsNoTracking()
                where offeringIds.Contains(o.Id)
                join subject in _db.Subjects.IgnoreQueryFilters().AsNoTracking() on o.SubjectId equals subject.Id
                select new { o.Id, subject.Name })
                .ToDictionaryAsync(x => x.Id, x => IsArabic ? x.Name.NameAr : x.Name.NameEn);

            var teacherNames = await TeacherNamesAsync(rows.Select(r => r.TeacherProfileId).Distinct().ToList());

            return rows
                .OrderBy(r => r.DayOfWeek).ThenBy(r => r.SequenceNumber)
                .Select(r => new RoomWeekSlot(
                    r.DayOfWeek, r.SequenceNumber, r.StartTime, r.EndTime,
                    sections.TryGetValue(r.SectionId, out var section) ? section : null,
                    subjects.TryGetValue(r.CurriculumOfferingId, out var subject) ? subject : null,
                    teacherNames.TryGetValue(r.TeacherProfileId, out var teacher) ? teacher : null))
                .ToList();
        }

        /// <summary>
        /// doc/Modules/08 §8.5 — the heat map. Rooms down, teaching periods across,
        /// and one number for the building.
        /// </summary>
        [HttpGet("utilization")]
        [RequirePermission(ScreenCatalog.Modules.Classrooms, ScreenCatalog.Classrooms.Utilization, ActionVerb.View)]
        public async Task<IActionResult> Utilization(int? year = null)
        {
            var years = await _db.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartDate).ToListAsync();
            var selected = years.FirstOrDefault(y => y.Id == (year ?? _workingYear.AcademicYearId))
                ?? years.FirstOrDefault(y => y.Status == AcademicYearStatus.Active)
                ?? years.FirstOrDefault();

            var m = new RoomUtilizationViewModel { Years = years, YearId = selected?.Id };
            if (selected == null)
            {
                return View(m);
            }

            var version = await PublishedVersionAsync(selected.Id);
            m.PublishedVersionId = version?.Id;
            if (version == null)
            {
                // Nothing published means nothing to read. An empty grid here would
                // read as "every room is free", which is the opposite of the truth
                // while a draft timetable sits unpublished.
                return View(m);
            }

            // The week shape is per stage, not per version, so the columns are the
            // union of the year's teaching periods. A school with one shape gets one
            // week; a school running the primary and secondary days differently gets
            // both, which is the truth about its building.
            var shapeIds = await _db.TimetableShapes.AsNoTracking()
                .Where(s => s.AcademicYearId == selected.Id).Select(s => s.Id).ToListAsync();

            var slots = await _db.PeriodSlots.AsNoTracking()
                .Where(s => shapeIds.Contains(s.TimetableShapeId) && !s.IsBreak)
                .OrderBy(s => s.DayOfWeek).ThenBy(s => s.SequenceNumber)
                .ToListAsync();

            var placements = await _db.Placements.AsNoTracking()
                .Where(p => p.TimetableVersionId == version.Id && p.RoomId != null)
                .Select(p => new { RoomId = p.RoomId!.Value, p.PeriodSlotId })
                .ToListAsync();

            var rooms = await _db.Rooms.AsNoTracking().ToListAsync();
            var floors = await _db.Floors.IgnoreQueryFilters().AsNoTracking()
                .Where(f => f.SchoolId == _db.CurrentSchoolId).ToListAsync();
            var buildings = await _db.Buildings.IgnoreQueryFilters().AsNoTracking()
                .Where(b => b.SchoolId == _db.CurrentSchoolId).ToListAsync();

            var grid = RoomUtilizationCalculator.Build(
                rooms.Select(r => r.Id).ToList(),
                slots.Select(s => new RoomUtilizationCalculator.TeachingSlot(s.Id, s.DayOfWeek, s.SequenceNumber)).ToList(),
                placements.Select(p => new RoomUtilizationCalculator.RoomPlacement(p.RoomId, p.PeriodSlotId)).ToList());

            m.Columns = slots.Select(s => new RoomUtilizationViewModel.Column(s.Id, s.DayOfWeek, s.SequenceNumber, s.StartTime)).ToList();
            m.Rows = grid.Select(g =>
            {
                var room = rooms.First(r => r.Id == g.RoomId);
                var floor = floors.FirstOrDefault(f => f.Id == room.FloorId);
                var building = floor == null ? null : buildings.FirstOrDefault(b => b.Id == floor.BuildingId);
                return new RoomUtilizationViewModel.Row(
                    room,
                    building == null ? "—" : (IsArabic ? building.Name.NameAr : building.Name.NameEn),
                    floor == null ? "—" : (IsArabic ? floor.Name.NameAr : floor.Name.NameEn),
                    g.BySlot, g.PercentUsed, g.HasDoubleBooking);
            }).ToList();

            m.OverallPercent = RoomUtilizationCalculator.OverallPercent(grid);
            m.Busiest = m.Rows.OrderByDescending(r => r.PercentUsed).Take(3).ToList();
            m.Idlest = m.Rows.OrderBy(r => r.PercentUsed).Take(3).ToList();
            return View(m);
        }

        /// <summary>
        /// The one operational version (BR-TTB-002: only one Published at a time). The
        /// heat map and the room's week both read it rather than a draft — a draft is
        /// somebody's work in progress, not what the school is doing on Sunday.
        /// </summary>
        /// <summary>Teacher display names by profile id, through the employee the profile points at.</summary>
        private async Task<Dictionary<int, string>> TeacherNamesAsync(IReadOnlyCollection<int> teacherProfileIds)
        {
            if (teacherProfileIds.Count == 0)
            {
                return new Dictionary<int, string>();
            }

            // IgnoreQueryFilters: a placement keeps naming the teacher who made it, and
            // a room's week must still read after that teacher leaves the school.
            return await (
                from p in _db.TeacherProfiles.IgnoreQueryFilters().AsNoTracking()
                where p.SchoolId == _db.CurrentSchoolId && teacherProfileIds.Contains(p.Id)
                join e in _db.Employees.IgnoreQueryFilters().AsNoTracking() on p.EmployeeId equals e.Id
                select new { p.Id, e.FirstNameAr, e.FamilyNameAr, e.FirstNameEn, e.FamilyNameEn })
                .ToDictionaryAsync(
                    x => x.Id,
                    x => IsArabic ? $"{x.FirstNameAr} {x.FamilyNameAr}" : $"{x.FirstNameEn} {x.FamilyNameEn}");
        }

        private Task<TimetableVersion?> PublishedVersionAsync(int academicYearId)
            => _db.TimetableVersions.AsNoTracking()
                .Where(v => v.AcademicYearId == academicYearId && v.Status == TimetableVersionStatus.Published)
                .OrderByDescending(v => v.PublishedAtUtc)
                .FirstOrDefaultAsync()!;

        [HttpPost("{id:int}/feature")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Classrooms, ScreenCatalog.Classrooms.Rooms, ActionVerb.Edit)]
        public async Task<IActionResult> AddFeature(int id, int? featureLookupId)
        {
            try
            {
                if (featureLookupId == null) throw new InvalidOperationException(T("Choose a feature.", "اختر تجهيزاً."));
                await _rooms.AddFeatureAsync(id, featureLookupId.Value);
                TempData["Flash"] = T("Feature added.", "تمت إضافة التجهيز.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost("{id:int}/unavailable")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Classrooms, ScreenCatalog.Classrooms.Rooms, ActionVerb.Edit)]
        public async Task<IActionResult> SetUnavailable(int id, RoomAvailabilityReason reason, DateTime? startDate, DateTime? endDate, string? notes)
        {
            try
            {
                if (startDate == null || endDate == null) throw new InvalidOperationException(T("Start and end dates are required.", "تاريخا البداية والنهاية مطلوبان."));
                if (endDate < startDate) throw new InvalidOperationException(T("End date must be on or after the start date.", "النهاية في أو بعد البداية."));
                await _rooms.SetUnavailableAsync(id, reason, startDate.Value, endDate.Value, notes);
                TempData["Flash"] = T("Unavailability window recorded (BR-ROM-004).", "تم تسجيل فترة عدم التوفر (BR-ROM-004).");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost("{id:int}/booking")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Classrooms, ScreenCatalog.Classrooms.Rooms, ActionVerb.Edit)]
        public async Task<IActionResult> RequestBooking(int id, int? academicYearId, string? purpose, DateTime? start, DateTime? end)
        {
            try
            {
                Require(purpose, "Purpose");
                if (start == null || end == null || end <= start) throw new InvalidOperationException(T("A valid start/end is required.", "بداية ونهاية صحيحتان مطلوبتان."));
                var yearId = academicYearId ?? _workingYear.AcademicYearId;
                var b = await _rooms.RequestBookingAsync(id, yearId, purpose!.Trim(), DateTime.SpecifyKind(start.Value, DateTimeKind.Utc), DateTime.SpecifyKind(end.Value, DateTimeKind.Utc), _currentUser.UserId);
                TempData["Flash"] = T($"Booking {b.Status}.", $"الحجز: {b.Status}.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task<(IReadOnlyList<(int Id, string Ar, string En)> Types, IReadOnlyList<(int Id, string Ar, string En)> Features)> LookupsAsync()
        {
            var cats = await _db.LookupCategories.AsNoTracking().Where(c => c.Code == "RoomType" || c.Code == "RoomFeature").ToListAsync();
            var typeCat = cats.FirstOrDefault(c => c.Code == "RoomType");
            var featCat = cats.FirstOrDefault(c => c.Code == "RoomFeature");
            var values = await _db.LookupValues.AsNoTracking().Where(v => cats.Select(c => c.Id).Contains(v.LookupCategoryId)).OrderBy(v => v.SortOrder).ToListAsync();
            return (
                values.Where(v => typeCat != null && v.LookupCategoryId == typeCat.Id).Select(v => (v.Id, v.Name.NameAr, v.Name.NameEn)).ToList(),
                values.Where(v => featCat != null && v.LookupCategoryId == featCat.Id).Select(v => (v.Id, v.Name.NameAr, v.Name.NameEn)).ToList());
        }

        private static void Require(string? v, string f)
        {
            if (string.IsNullOrWhiteSpace(v)) throw new InvalidOperationException(T($"{f} is required.", $"الحقل {f} مطلوب."));
        }
    }
}
