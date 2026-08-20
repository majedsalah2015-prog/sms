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
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;

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
        public async Task<IActionResult> DefineBuilding(RoomCatalogViewModel form)
        {
            try { Require(form.BuildingNameAr, "Name (Arabic)"); Require(form.BuildingNameEn, "Name (English)"); await _rooms.DefineBuildingAsync(form.BuildingNameAr!, form.BuildingNameEn!); TempData["Flash"] = T("Building created.", "تم إنشاء المبنى."); }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("floor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DefineFloor(RoomCatalogViewModel form)
        {
            try
            {
                if (form.FloorBuildingId == null) throw new InvalidOperationException(T("Choose a building.", "اختر مبنى."));
                Require(form.FloorNameAr, "Name (Arabic)"); Require(form.FloorNameEn, "Name (English)");
                await _rooms.DefineFloorAsync(form.FloorBuildingId.Value, form.FloorNameAr!, form.FloorNameEn!, form.FloorOrder ?? 1);
                TempData["Flash"] = T("Floor created.", "تم إنشاء الطابق.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("room")]
        [ValidateAntiForgeryToken]
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
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Index));
        }

        // --- Edit / delete (soft: deactivate) for building / floor / room ---------

        [HttpGet("building/{id:int}/edit")]
        public async Task<IActionResult> EditBuilding(int id)
        {
            var b = await _db.Buildings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (b == null) return NotFound();
            return View("Edit", new RoomEditViewModel { Id = id, Kind = "building", NameAr = b.Name.NameAr, NameEn = b.Name.NameEn, ChildCount = await _db.Floors.CountAsync(f => f.BuildingId == id) });
        }

        [HttpPost("building/{id:int}/edit")]
        [ValidateAntiForgeryToken]
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
            catch (InvalidOperationException ex) { ModelState.AddModelError(string.Empty, ex.Message); return View("Edit", form); }
        }

        [HttpPost("building/{id:int}/delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBuilding(int id)
        {
            try { await _rooms.DeactivateBuildingAsync(id); TempData["Flash"] = T("Building removed (deactivated).", "تم حذف المبنى (إلغاء تفعيل)."); }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("floor/{id:int}/edit")]
        public async Task<IActionResult> EditFloor(int id)
        {
            var f = await _db.Floors.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (f == null) return NotFound();
            return View("Edit", await FillEditListsAsync(new RoomEditViewModel { Id = id, Kind = "floor", NameAr = f.Name.NameAr, NameEn = f.Name.NameEn, BuildingId = f.BuildingId, Order = f.SequenceOrder, ChildCount = await _db.Rooms.CountAsync(r => r.FloorId == id) }));
        }

        [HttpPost("floor/{id:int}/edit")]
        [ValidateAntiForgeryToken]
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
            catch (InvalidOperationException ex) { ModelState.AddModelError(string.Empty, ex.Message); return View("Edit", await FillEditListsAsync(form)); }
        }

        [HttpPost("floor/{id:int}/delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFloor(int id)
        {
            try { await _rooms.DeactivateFloorAsync(id); TempData["Flash"] = T("Floor removed (deactivated).", "تم حذف الطابق (إلغاء تفعيل)."); }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("{id:int}/edit")]
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
            catch (InvalidOperationException ex) { ModelState.AddModelError(string.Empty, ex.Message); return View("Edit", await FillEditListsAsync(form)); }
        }

        [HttpPost("{id:int}/delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRoom(int id)
        {
            try { await _rooms.DeactivateRoomAsync(id); TempData["Flash"] = T("Room removed (deactivated; bookings/history kept).", "تم حذف القاعة (إلغاء تفعيل مع حفظ الحجوزات والسجل)."); }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
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
            });
        }

        [HttpPost("{id:int}/feature")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddFeature(int id, int? featureLookupId)
        {
            try
            {
                if (featureLookupId == null) throw new InvalidOperationException(T("Choose a feature.", "اختر تجهيزاً."));
                await _rooms.AddFeatureAsync(id, featureLookupId.Value);
                TempData["Flash"] = T("Feature added.", "تمت إضافة التجهيز.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost("{id:int}/unavailable")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetUnavailable(int id, RoomAvailabilityReason reason, DateTime? startDate, DateTime? endDate, string? notes)
        {
            try
            {
                if (startDate == null || endDate == null) throw new InvalidOperationException(T("Start and end dates are required.", "تاريخا البداية والنهاية مطلوبان."));
                if (endDate < startDate) throw new InvalidOperationException(T("End date must be on or after the start date.", "النهاية في أو بعد البداية."));
                await _rooms.SetUnavailableAsync(id, reason, startDate.Value, endDate.Value, notes);
                TempData["Flash"] = T("Unavailability window recorded (BR-ROM-004).", "تم تسجيل فترة عدم التوفر (BR-ROM-004).");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost("{id:int}/booking")]
        [ValidateAntiForgeryToken]
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
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
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
