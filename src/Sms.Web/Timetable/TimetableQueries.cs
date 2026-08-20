using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Setup;
using Sms.Domain.Classrooms;
using Sms.Domain.Employees;
using Sms.Domain.Grades;
using Sms.Domain.Sections;
using Sms.Domain.Subjects;
using Sms.Domain.Teachers;
using Sms.Domain.Timetable;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;

namespace Sms.Web.Timetable
{
    /// <summary>
    /// Read-side helpers shared by TimetableController (staff screens) and
    /// the portal's child timetable: resolve placements into renderable
    /// cells, week configuration (weekend days / first day from E-101's
    /// Regional settings), and the personal week grids (doc/Modules/15 §8.7).
    /// Pure queries — nothing here saves.
    /// </summary>
    public static class TimetableQueries
    {
        public sealed class Resolved
        {
            public IReadOnlyList<PlacementCell> Cells { get; init; } = Array.Empty<PlacementCell>();

            /// <summary>Every slot of every shape in the year (pivots span stages).</summary>
            public IReadOnlyDictionary<int, PeriodSlot> Slots { get; init; } = new Dictionary<int, PeriodSlot>();

            public IReadOnlyDictionary<int, TimetableShape> ShapesByStage { get; init; } = new Dictionary<int, TimetableShape>();

            public IReadOnlyDictionary<int, Section> Sections { get; init; } = new Dictionary<int, Section>();

            /// <summary>sectionId → (grade, stageId) via GradeYearProfile → GradeLevel.</summary>
            public IReadOnlyDictionary<int, (GradeLevel Grade, int StageId)> SectionGrade { get; init; } = new Dictionary<int, (GradeLevel, int)>();

            public IReadOnlyDictionary<int, CurriculumOffering> Offerings { get; init; } = new Dictionary<int, CurriculumOffering>();

            public IReadOnlyDictionary<int, Subject> Subjects { get; init; } = new Dictionary<int, Subject>();

            public IReadOnlyDictionary<int, (TeacherProfile Profile, Employee Employee)> Teachers { get; init; } = new Dictionary<int, (TeacherProfile, Employee)>();

            public IReadOnlyDictionary<int, Room> Rooms { get; init; } = new Dictionary<int, Room>();

            public PlacementCell? Cell(int placementId) => Cells.FirstOrDefault(c => c.Placement.Id == placementId);
        }

        /// <summary>Loads the year's reference data once and joins the given placements into cells (placements whose references are gone are skipped, not faulted).</summary>
        public static async Task<Resolved> ResolveAsync(AppDbContext db, int yearId, IReadOnlyList<Placement> placements)
        {
            var shapes = await db.TimetableShapes.AsNoTracking().Where(s => s.AcademicYearId == yearId).ToListAsync();
            var shapeIds = shapes.Select(s => s.Id).ToList();
            var slots = await db.PeriodSlots.AsNoTracking().Where(s => shapeIds.Contains(s.TimetableShapeId)).ToListAsync();
            var sections = await db.Sections.AsNoTracking().Where(s => s.AcademicYearId == yearId).ToListAsync();
            var profiles = await db.GradeYearProfiles.IgnoreQueryFilters().AsNoTracking().Where(p => p.AcademicYearId == yearId && p.SchoolId == db.CurrentSchoolId).ToListAsync();
            var grades = await db.GradeLevels.IgnoreQueryFilters().AsNoTracking().Where(g => g.SchoolId == db.CurrentSchoolId).ToListAsync();
            var offerings = await db.CurriculumOfferings.AsNoTracking().Where(o => o.AcademicYearId == yearId).ToListAsync();
            var subjectIds = offerings.Select(o => o.SubjectId).Distinct().ToList();
            var subjects = await db.Subjects.IgnoreQueryFilters().AsNoTracking().Where(s => subjectIds.Contains(s.Id)).ToListAsync();
            var tprofiles = await db.TeacherProfiles.AsNoTracking().ToListAsync();
            var empIds = tprofiles.Select(p => p.EmployeeId).ToList();
            var employees = await db.Employees.AsNoTracking().Where(e => empIds.Contains(e.Id)).ToListAsync();
            var rooms = await db.Rooms.IgnoreQueryFilters().AsNoTracking().Where(r => r.SchoolId == db.CurrentSchoolId).ToListAsync();

            var sectionDict = sections.ToDictionary(s => s.Id);
            var sectionGrade = new Dictionary<int, (GradeLevel, int)>();
            foreach (var s in sections)
            {
                var p = profiles.FirstOrDefault(x => x.Id == s.GradeYearProfileId);
                var g = p == null ? null : grades.FirstOrDefault(x => x.Id == p.GradeLevelId);
                if (g != null) sectionGrade[s.Id] = (g, g.StageId);
            }

            var teachers = tprofiles
                .Select(p => (Profile: p, Employee: employees.FirstOrDefault(e => e.Id == p.EmployeeId)))
                .Where(x => x.Employee != null)
                .ToDictionary(x => x.Profile.Id, x => (Profile: x.Profile, Employee: x.Employee!));
            var slotDict = slots.ToDictionary(s => s.Id);
            var offDict = offerings.ToDictionary(o => o.Id);
            var subjDict = subjects.ToDictionary(s => s.Id);
            var roomDict = rooms.ToDictionary(r => r.Id);

            var cells = new List<PlacementCell>();
            foreach (var p in placements)
            {
                if (!slotDict.TryGetValue(p.PeriodSlotId, out var slot) || !sectionDict.TryGetValue(p.SectionId, out var section)
                    || !offDict.TryGetValue(p.CurriculumOfferingId, out var off) || !subjDict.TryGetValue(off.SubjectId, out var subj)
                    || !teachers.TryGetValue(p.TeacherProfileId, out var t))
                {
                    continue;
                }

                cells.Add(new PlacementCell(p, slot, section, subj, off, t.Employee, t.Profile, p.RoomId == null ? null : roomDict.GetValueOrDefault(p.RoomId.Value)));
            }

            return new Resolved
            {
                Cells = cells, Slots = slotDict, ShapesByStage = shapes.GroupBy(s => s.StageId).ToDictionary(g => g.Key, g => g.First()),
                Sections = sectionDict, SectionGrade = sectionGrade, Offerings = offDict, Subjects = subjDict, Teachers = teachers, Rooms = roomDict,
            };
        }

        public static async Task<(HashSet<DayOfWeek> Weekend, DayOfWeek FirstDay, IReadOnlyList<DayOfWeek> WorkingDays)> WeekConfigAsync(ISystemSetupAdmin setup, int? yearId)
        {
            var workingDaysSetting = await setup.GetSettingAsync(SettingKeys.WorkingDays, yearId) ?? "Sunday,Monday,Tuesday,Wednesday,Thursday";
            var weekend = new HashSet<DayOfWeek>(WorkingWeek.WeekendDays(workingDaysSetting));
            var firstDay = Enum.TryParse<DayOfWeek>(await setup.GetSettingAsync(SettingKeys.FirstDayOfWeek), true, out var fd) ? fd : DayOfWeek.Sunday;
            return (weekend, firstDay, OrderDays(WorkingWeek.Parse(workingDaysSetting), firstDay));
        }

        public static IReadOnlyList<DayOfWeek> OrderDays(IEnumerable<DayOfWeek> days, DayOfWeek firstDay) =>
            days.Distinct().OrderBy(d => ((int)d - (int)firstDay + 7) % 7).ToList();

        public static DateTime WeekStart(DateTime date, DayOfWeek firstDay) =>
            date.Date.AddDays(-(((int)date.DayOfWeek - (int)firstDay + 7) % 7));

        /// <summary>(day, sequence) → first slot found for that key, plus the ordered day/sequence axes — the grid frame for any set of slots.</summary>
        public static (IReadOnlyList<DayOfWeek> Days, IReadOnlyList<int> Sequences, IReadOnlyDictionary<(DayOfWeek, int), PeriodSlot> Slots) Frame(IEnumerable<PeriodSlot> slots, DayOfWeek firstDay)
        {
            var list = slots.ToList();
            var dict = new Dictionary<(DayOfWeek, int), PeriodSlot>();
            foreach (var s in list.OrderBy(s => s.TimetableShapeId).ThenBy(s => s.StartTime))
            {
                dict.TryAdd((s.DayOfWeek, s.SequenceNumber), s);
            }

            var days = OrderDays(list.Select(s => s.DayOfWeek), firstDay);
            var seqs = list.Count == 0 ? Array.Empty<int>() : Enumerable.Range(1, list.Max(s => s.SequenceNumber)).ToArray();
            return (days, seqs, dict);
        }

        public static IReadOnlyDictionary<(DayOfWeek, int), IReadOnlyList<PlacementCell>> GroupCells(IEnumerable<PlacementCell> cells) =>
            cells.GroupBy(c => (c.Slot.DayOfWeek, c.Slot.SequenceNumber))
                .ToDictionary(g => g.Key, g => (IReadOnlyList<PlacementCell>)g.OrderBy(c => c.Section.NameEn).ToList());

        /// <summary>The operational version for a year: the most recently published one (BR-TTB-002 "exactly one operational"; a later amendment version supersedes).</summary>
        public static async Task<TimetableVersion?> CurrentPublishedAsync(AppDbContext db, int yearId) =>
            await db.TimetableVersions.AsNoTracking()
                .Where(v => v.AcademicYearId == yearId && v.Status == TimetableVersionStatus.Published)
                .OrderByDescending(v => v.PublishedAtUtc).ThenByDescending(v => v.Id)
                .FirstOrDefaultAsync();

        /// <summary>Personal week grid (teacher / section / room) off the operational version, with this week's dated session overlays.</summary>
        public static async Task<PersonalTimetableViewModel> PersonalAsync(
            AppDbContext db, ISystemSetupAdmin setup, string kind, int id, int yearId, DateTime today, int? versionId, bool isRtl)
        {
            var year = await db.AcademicYears.AsNoTracking().SingleOrDefaultAsync(y => y.Id == yearId);
            var version = versionId == null
                ? await CurrentPublishedAsync(db, yearId)
                : await db.TimetableVersions.AsNoTracking().SingleOrDefaultAsync(v => v.Id == versionId && v.AcademicYearId == yearId);
            var (_, firstDay, _) = await WeekConfigAsync(setup, yearId);
            var m = new PersonalTimetableViewModel { Kind = kind, Year = year, Version = version, WeekStart = WeekStart(today, firstDay) };

            var placements = version == null
                ? new List<Placement>()
                : await db.Placements.AsNoTracking().Where(p => p.TimetableVersionId == version.Id
                    && (kind == "teacher" ? p.TeacherProfileId == id : kind == "room" ? p.RoomId == id : p.SectionId == id)).ToListAsync();
            var r = await ResolveAsync(db, yearId, placements);

            var frameSlots = kind == "section" && r.Sections.TryGetValue(id, out var sec) && r.SectionGrade.TryGetValue(id, out var sg) && r.ShapesByStage.TryGetValue(sg.StageId, out var shape)
                ? r.Slots.Values.Where(s => s.TimetableShapeId == shape.Id)
                : r.Slots.Values;
            var (days, seqs, slots) = Frame(frameSlots, firstDay);
            m.Days = days; m.Sequences = seqs; m.Slots = slots; m.Cells = GroupCells(r.Cells); m.Rooms = r.Rooms;

            switch (kind)
            {
                case "teacher":
                    if (r.Teachers.TryGetValue(id, out var t))
                    {
                        m.Title = TimetableLabels.TeacherName(t.Employee, isRtl);
                        m.Subtitle = isRtl ? $"جدول المعلم الأسبوعي · {t.Employee.EmployeeNo}" : $"Teacher week · {t.Employee.EmployeeNo}";
                    }
                    break;
                case "room":
                    if (r.Rooms.TryGetValue(id, out var room))
                    {
                        m.Title = TimetableLabels.RoomName(room, isRtl);
                        m.Subtitle = isRtl ? "جدول القاعة الأسبوعي" : "Room schedule";
                    }
                    break;
                default:
                    if (r.Sections.TryGetValue(id, out var section))
                    {
                        var g = r.SectionGrade.GetValueOrDefault(id).Grade;
                        m.Title = TimetableLabels.SectionName(section, isRtl);
                        m.Subtitle = g == null ? "" : $"{g.Code} · {(isRtl ? g.Name.NameAr : g.Name.NameEn)}";
                    }
                    break;
            }

            if (placements.Count > 0)
            {
                var pids = placements.Select(p => p.Id).ToList();
                var weekEnd = m.WeekStart.AddDays(7);
                var sessions = await db.Sessions.AsNoTracking().Where(s => pids.Contains(s.PlacementId) && s.Date >= m.WeekStart && s.Date < weekEnd).ToListAsync();
                m.WeekSessions = sessions.GroupBy(s => s.PlacementId).ToDictionary(g => g.Key, g => g.OrderBy(s => s.Date).First());
                var sids = sessions.Select(s => s.Id).ToList();
                var subs = await db.Substitutions.AsNoTracking().Where(s => sids.Contains(s.SessionId)).ToListAsync();
                m.Substitutes = subs
                    .Where(s => r.Teachers.ContainsKey(s.SubstituteTeacherProfileId))
                    .GroupBy(s => s.SessionId)
                    .ToDictionary(g => g.Key, g => r.Teachers[g.OrderByDescending(s => s.AssignedAtUtc).First().SubstituteTeacherProfileId].Employee);
            }

            return m;
        }
    }
}
