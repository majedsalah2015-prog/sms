using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Calendar;
using Sms.Application.Common.Interfaces;
using Sms.Application.Localization;
using Sms.Application.Setup;
using Sms.Domain.Calendar;
using Sms.Domain.Schools;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/04 §8.1–8.2: year calendar board (month grids, day-type
    /// painting over ranges, Hijri overlay, working-day counters per
    /// semester/term — BR-CAL-006 live) and the event manager, plus publish
    /// versions (BR-CAL-007). Weekend days come from the E-101
    /// Regional.WorkingDays setting through WorkingWeek; day types resolve
    /// through CalendarDayResolver exactly as the engine does. §8.3 amendment
    /// impact review and §8.4 portal view are deferred (need Attendance/Exam/
    /// Timetable session lists and the portal shell).
    /// </summary>
    [Route("calendar")]
    public class CalendarController : Controller
    {
        private readonly ICalendarAdmin _calendar;
        private readonly ISystemSetupAdmin _setup;
        private readonly AppDbContext _db;
        private readonly IWorkingYearContext _workingYear;
        private readonly ICurrentUser _currentUser;
        private readonly IClock _clock;

        public CalendarController(ICalendarAdmin calendar, ISystemSetupAdmin setup, AppDbContext db, IWorkingYearContext workingYear, ICurrentUser currentUser, IClock clock)
        {
            _calendar = calendar;
            _setup = setup;
            _db = db;
            _workingYear = workingYear;
            _currentUser = currentUser;
            _clock = clock;
        }

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        [HttpGet("")]
        [RequirePermission(ScreenCatalog.Modules.Calendar, ScreenCatalog.Calendar.Calendar_, ActionVerb.View)]
        public async Task<IActionResult> Index(int? year = null, bool? hijri = null, int? edit = null)
        {
            var years = await _db.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartDate).ToListAsync();
            var selected = years.FirstOrDefault(y => y.Id == (year ?? _workingYear.AcademicYearId))
                ?? years.FirstOrDefault(y => y.Status == AcademicYearStatus.Active)
                ?? years.FirstOrDefault();
            if (selected == null)
            {
                TempData["Error"] = T("Define an academic year first.", "عرّف عاماً دراسياً أولاً.");
                return RedirectToAction("Index", "AcademicYears");
            }

            var model = await BuildBoardAsync(selected, years, hijri, edit);
            return View(model);
        }

        [HttpPost("day")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Calendar, ScreenCatalog.Calendar.Calendar_, ActionVerb.Edit)]
        public async Task<IActionResult> Day(CalendarDayFormViewModel form)
        {
            try
            {
                if (form.Date == null)
                {
                    throw new InvalidOperationException(T("Pick a date.", "اختر تاريخاً."));
                }

                var end = form.EndDate ?? form.Date.Value;
                if (end < form.Date.Value)
                {
                    throw new InvalidOperationException(T("End date must be on or after the start date.", "تاريخ النهاية يجب أن يكون في أو بعد البداية."));
                }

                var painted = 0;
                for (var d = form.Date.Value.Date; d <= end.Date; d = d.AddDays(1))
                {
                    await _calendar.DefineDayAsync(form.AcademicYearId, d, form.DayType, form.Audience, form.IsProvisional);
                    painted++;
                }

                TempData["Flash"] = T($"{painted} day(s) painted as {form.DayType}.", $"تم تعيين {painted} يوم/أيام كـ {form.DayType}.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Index), new { year = form.AcademicYearId });
        }

        [HttpPost("event")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Calendar, ScreenCatalog.Calendar.Calendar_, ActionVerb.Edit)]
        public async Task<IActionResult> Event(CalendarEventFormViewModel form)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(form.NameAr) || string.IsNullOrWhiteSpace(form.NameEn))
                {
                    throw new InvalidOperationException(T("Both names are required (BR-GLB-001).", "الاسمان مطلوبان (BR-GLB-001)."));
                }

                if (form.StartDate == null)
                {
                    throw new InvalidOperationException(T("Start date is required.", "تاريخ البداية مطلوب."));
                }

                var end = form.EndDate ?? form.StartDate.Value;
                if (form.Id is int editedId)
                {
                    if (!await EventBelongsToYearAsync(editedId, form.AcademicYearId))
                    {
                        return NotFound();
                    }

                    await _calendar.UpdateEventAsync(editedId, form.NameAr.Trim(), form.NameEn.Trim(), form.Category, form.StartDate.Value, end, form.Audience, form.IsPortalVisible);
                }
                else
                {
                    await _calendar.DefineEventAsync(form.AcademicYearId, form.NameAr.Trim(), form.NameEn.Trim(), form.Category, form.StartDate.Value, end, form.Audience, form.IsPortalVisible);
                }

                if (form.MarkAsHoliday)
                {
                    for (var d = form.StartDate.Value.Date; d <= end.Date; d = d.AddDays(1))
                    {
                        await _calendar.DefineDayAsync(form.AcademicYearId, d, DayType.Holiday, form.Audience, isProvisional: form.Category == CalendarEventCategory.Religious);
                    }
                }

                TempData["Flash"] = T("Event saved.", "تم حفظ الحدث.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Index), new { year = form.AcademicYearId });
        }

        /// <summary>
        /// Cancels an event and reinstates one — BR-GLB-005's answer to "delete this". The row
        /// stays, stops painting the board and stops reaching the portal, and the T2 audit records
        /// who called it off (BR-CAL-008). BR-CAL-004 refuses it once the event has started.
        /// </summary>
        [HttpPost("event/{id:int}/active")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Calendar, ScreenCatalog.Calendar.Calendar_, ActionVerb.Deactivate)]
        public async Task<IActionResult> EventActive(int id, int academicYearId, bool isActive)
        {
            if (!await EventBelongsToYearAsync(id, academicYearId))
            {
                return NotFound();
            }

            try
            {
                await _calendar.SetEventActiveAsync(id, isActive);
                TempData["Flash"] = isActive
                    ? T("Event reinstated.", "تمت إعادة تفعيل الحدث.")
                    : T("Event cancelled — it stays on the record and leaves the board.", "أُلغي الحدث — يبقى في السجل ويختفي من اللوحة.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Index), new { year = academicYearId });
        }

        /// <summary>
        /// The tenant filter already scopes the set to this school; this asks the other half —
        /// that the id in the URL belongs to the year the form was posted from, so an id typed
        /// into the address bar cannot edit some other year's event through this screen.
        /// </summary>
        private Task<bool> EventBelongsToYearAsync(int calendarEventId, int academicYearId)
            => _db.CalendarEvents.AnyAsync(e => e.Id == calendarEventId && e.AcademicYearId == academicYearId);

        [HttpPost("publish")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Calendar, ScreenCatalog.Calendar.Calendar_, ActionVerb.Approve)]
        public async Task<IActionResult> Publish(int academicYearId)
        {
            try
            {
                var v = await _calendar.PublishAsync(academicYearId, _currentUser.UserId);
                TempData["Flash"] = T($"Calendar published as version {v.VersionNumber}.", $"تم نشر التقويم بالإصدار {v.VersionNumber}.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Index), new { year = academicYearId });
        }

        private async Task<CalendarBoardViewModel> BuildBoardAsync(AcademicYear year, IReadOnlyList<AcademicYear> years, bool? hijriToggle, int? editEventId)
        {
            var workingDaysSetting = await _setup.GetSettingAsync(SettingKeys.WorkingDays, year.Id) ?? "Sunday,Monday,Tuesday,Wednesday,Thursday";
            var weekend = new HashSet<DayOfWeek>(WorkingWeek.WeekendDays(workingDaysSetting));
            var firstDay = Enum.TryParse<DayOfWeek>(await _setup.GetSettingAsync(SettingKeys.FirstDayOfWeek), true, out var fd) ? fd : DayOfWeek.Sunday;
            var hijriDefault = bool.TryParse(await _setup.GetSettingAsync(SettingKeys.HijriDisplay), out var h) && h;
            var hijri = hijriToggle ?? hijriDefault;

            var days = await _db.CalendarDays.AsNoTracking().Where(d => d.AcademicYearId == year.Id).ToListAsync();
            var overrides = days.ToDictionary(d => d.Date.Date, d => d.DayType);
            var overrideRows = days.ToDictionary(d => d.Date.Date);
            // Every event for the manager's list — cancelled ones included, which is the point of
            // BR-GLB-006 — and only the live ones for the grid, which paints what is still on.
            var events = await _db.CalendarEvents.AsNoTracking().Where(e => e.AcademicYearId == year.Id).OrderBy(e => e.StartDate).ToListAsync();
            var liveEvents = events.Where(e => e.IsActive).ToList();
            var versions = await _db.CalendarVersions.AsNoTracking().Where(v => v.AcademicYearId == year.Id).OrderByDescending(v => v.VersionNumber).ToListAsync();
            var semesters = await _db.Semesters.AsNoTracking().Where(s => s.AcademicYearId == year.Id).OrderBy(s => s.SequenceNumber).ToListAsync();
            var terms = await _db.Terms.AsNoTracking().Where(t => t.AcademicYearId == year.Id).OrderBy(t => t.SemesterId).ThenBy(t => t.SequenceNumber).ToListAsync();

            var weekOrder = Enumerable.Range(0, 7).Select(i => (DayOfWeek)(((int)firstDay + i) % 7)).ToList();

            DayType Resolve(DateTime d) => CalendarDayResolver.Resolve(d, weekend, overrides);
            CalendarBoardViewModel.DayCell Cell(DateTime d)
            {
                var inYear = d >= year.StartDate.Date && d <= year.EndDate.Date;
                overrideRows.TryGetValue(d.Date, out var row);
                return new CalendarBoardViewModel.DayCell(
                    d, Resolve(d), row != null, row?.IsProvisional ?? false,
                    hijri ? HijriCalendarConverter.ToHijri(d).Day.ToString(CultureInfo.InvariantCulture) : null,
                    liveEvents.Where(e => d >= e.StartDate.Date && d <= e.EndDate.Date).ToList(), inYear);
            }

            // The grid is Gregorian and its month titles say so; the overlay names the Hijri month
            // the same days fall in, so the per-day Hijri numbers have a month to belong to
            // (doc/Modules/04 §8.1, ADR-4). A Gregorian month always spans two Hijri ones or sits
            // inside one, and around Dhu al-Hijjah it spans two Hijri years as well.
            string? HijriMonthLabel(DateTime first, DateTime last)
            {
                if (!hijri)
                {
                    return null;
                }

                var from = HijriCalendarConverter.ToHijri(first);
                var to = HijriCalendarConverter.ToHijri(last);
                var fromName = CalendarLabels.HijriMonth(from.Month, IsArabic);
                var toName = CalendarLabels.HijriMonth(to.Month, IsArabic);
                var fromYear = from.Year.ToString(CultureInfo.InvariantCulture);
                var toYear = to.Year.ToString(CultureInfo.InvariantCulture);

                if (from.Year != to.Year)
                {
                    return $"{fromName} {fromYear} – {toName} {toYear}";
                }

                return from.Month == to.Month ? $"{fromName} {fromYear}" : $"{fromName} – {toName} {fromYear}";
            }

            var months = new List<CalendarBoardViewModel.MonthGrid>();
            for (var m = new DateTime(year.StartDate.Year, year.StartDate.Month, 1); m <= year.EndDate; m = m.AddMonths(1))
            {
                var first = m;
                var daysInMonth = DateTime.DaysInMonth(m.Year, m.Month);
                var weeks = new List<CalendarBoardViewModel.DayCell?[]>();
                var week = new CalendarBoardViewModel.DayCell?[7];
                var col = weekOrder.IndexOf(first.DayOfWeek);
                for (var day = 1; day <= daysInMonth; day++)
                {
                    week[col] = Cell(new DateTime(m.Year, m.Month, day));
                    col++;
                    if (col == 7)
                    {
                        weeks.Add(week);
                        week = new CalendarBoardViewModel.DayCell?[7];
                        col = 0;
                    }
                }

                if (week.Any(c => c != null))
                {
                    weeks.Add(week);
                }

                months.Add(new CalendarBoardViewModel.MonthGrid(
                    m.Year, m.Month, weeks, HijriMonthLabel(first, new DateTime(m.Year, m.Month, daysInMonth))));
            }

            var counters = new List<CalendarBoardViewModel.PeriodCount>();
            int Working(DateTime s, DateTime e) => CalendarStatistics.CountInstructionalDays(EachDay(s, e).Select(Resolve));
            counters.Add(new CalendarBoardViewModel.PeriodCount(T("Whole year", "العام كاملاً"), Working(year.StartDate, year.EndDate), (int)(year.EndDate - year.StartDate).TotalDays + 1));
            foreach (var s in semesters)
            {
                counters.Add(new CalendarBoardViewModel.PeriodCount((IsArabic ? s.NameAr : s.NameEn), Working(s.StartDate, s.EndDate), (int)(s.EndDate - s.StartDate).TotalDays + 1));
                foreach (var t in terms.Where(t => t.SemesterId == s.Id))
                {
                    counters.Add(new CalendarBoardViewModel.PeriodCount("  " + (IsArabic ? t.NameAr : t.NameEn), Working(t.StartDate, t.EndDate), (int)(t.EndDate - t.StartDate).TotalDays + 1));
                }
            }

            var lastPublish = versions.FirstOrDefault()?.PublishedAtUtc;
            var hasUnpublished = lastPublish == null
                ? days.Count > 0 || events.Count > 0
                : days.Any(d => (d.ModifiedAtUtc ?? d.CreatedAtUtc) > lastPublish) || events.Any(e => (e.ModifiedAtUtc ?? e.CreatedAtUtc) > lastPublish);

            return new CalendarBoardViewModel
            {
                Year = year,
                Years = years,
                Months = months,
                WeekOrder = weekOrder,
                Counters = counters,
                Events = events,
                EditEvent = editEventId == null ? null : events.FirstOrDefault(e => e.Id == editEventId.Value),
                TodayUtc = _clock.UtcNow.Date,
                Versions = versions,
                HijriOverlay = hijri,
                HasUnpublishedEdits = hasUnpublished,
                InstructionalDays = counters[0].WorkingDays,
                OverrideCount = days.Count,
            };
        }

        private static IEnumerable<DateTime> EachDay(DateTime start, DateTime end)
        {
            for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
            {
                yield return d;
            }
        }
    }
}
