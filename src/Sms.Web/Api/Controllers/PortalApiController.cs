using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Portal;
using Sms.Application.Security;
using Sms.Application.Setup;
using Sms.Domain.Messaging;
using Sms.Domain.Security;
using Sms.Infrastructure.Persistence;
using Sms.Web.Api.Models;
using Sms.Web.Security;
using Sms.Web.Services;
using Sms.Web.Timetable;

namespace Sms.Web.Api.Controllers
{
    /// <summary>
    /// The family's half of the app — doc/Modules/37 §5, §8.10 and the E-304
    /// portal essentials, over the same <see cref="IParentPortalQuery"/> the
    /// browser portal reads.
    /// <para>
    /// BR-SEC-011 decides every read and it is asked in exactly one place: the
    /// query port. A refusal surfaces as <b>404</b>, never 403 — telling a
    /// parent that the student id they guessed exists is the disclosure the rule
    /// prevents, and an API that answered differently would undo it for the one
    /// client that reads status codes (BR-SEC-010).
    /// </para>
    /// <para>
    /// Staff accounts may call these too, exactly as they may open /portal in a
    /// browser: they see an empty family, because the gate answers about
    /// guardianship and not about seniority.
    /// </para>
    /// </summary>
    [Route(V1 + "/portal")]
    [PortalReachable]
    public sealed class PortalApiController : ApiControllerBase
    {
        private readonly IParentPortalQuery _portal;
        private readonly AppDbContext _db;
        private readonly IWorkingYearContext _workingYear;
        private readonly ICurrentUser _user;
        private readonly AttachmentIntake _intake;
        private readonly ISystemSetupAdmin _setup;
        private readonly IClock _clock;

        public PortalApiController(
            IParentPortalQuery portal,
            AppDbContext db,
            IWorkingYearContext workingYear,
            ICurrentUser user,
            AttachmentIntake intake,
            ISystemSetupAdmin setup,
            IClock clock)
        {
            _portal = portal;
            _db = db;
            _workingYear = workingYear;
            _user = user;
            _intake = intake;
            _setup = setup;
            _clock = clock;
        }

        /// <summary>
        /// The family. A guardian gets the children the link makes visible
        /// (BR-PAR-004); a student gets themselves. Each row carries the two
        /// figures the app's home screen shows without a second call —
        /// attendance and what is outstanding.
        /// </summary>
        [HttpGet("children")]
        [RequirePermission(ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Home, ActionVerb.View)]
        public async Task<ActionResult<IReadOnlyList<ApiPortalChild>>> Children()
        {
            var rows = new List<ApiPortalChild>();
            foreach (var (student, isSelf) in await FamilyAsync())
            {
                var (grade, section) = await PlacementAsync(student.Id);
                var row = new ApiPortalChild
                {
                    StudentId = student.Id,
                    StudentNo = student.StudentNo,
                    NameAr = $"{student.FirstNameAr} {student.FamilyNameAr}".Trim(),
                    NameEn = $"{student.FirstNameEn} {student.FamilyNameEn}".Trim(),
                    IsSelf = isSelf,
                    GradeCode = grade?.Code,
                    GradeName = grade == null ? null : T(grade.Name.NameEn, grade.Name.NameAr),
                    SectionName = section == null ? null : T(section.NameEn, section.NameAr),
                };

                // Each figure is asked for separately and each may refuse on its own.
                // A guardian who may see the child but not the money is a real
                // configuration, and the row still belongs in the list.
                try
                {
                    var attendance = await _portal.GetAttendanceSummaryAsync(_user.UserId, student.Id, Ct);
                    row.AttendancePercent = attendance.ScheduledDays == 0 ? null : attendance.AttendancePercent;
                }
                catch (PortalAccessDeniedException)
                {
                }

                try
                {
                    row.FeeBalance = (await _portal.GetFeePositionAsync(_user.UserId, student.Id, Ct)).Position;
                }
                catch (PortalAccessDeniedException)
                {
                }

                rows.Add(row);
            }

            return rows.ToList();
        }

        /// <summary>BR-ATD-009 for one student, over the working academic year.</summary>
        [HttpGet("students/{id:int}/attendance")]
        [RequirePermission(ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Child, ActionVerb.View)]
        public async Task<ActionResult<ApiPortalAttendance>> Attendance(int id)
        {
            if (!await StudentExistsAsync(id))
            {
                return NotFoundError();
            }

            var summary = await _portal.GetAttendanceSummaryAsync(_user.UserId, id, Ct);
            return new ApiPortalAttendance
            {
                StudentId = id,
                ScheduledDays = summary.ScheduledDays,
                ExemptedDays = summary.ExemptedDays,
                AbsentDays = summary.AbsentDays,
                AttendancePercent = summary.AttendancePercent,
            };
        }

        /// <summary>
        /// Published results only (BR-SEC-012). The subject and term names are
        /// resolved here rather than left as ids — a phone showing "offering 41"
        /// is a phone showing nothing.
        /// </summary>
        [HttpGet("students/{id:int}/results")]
        [RequirePermission(ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Child, ActionVerb.View)]
        public async Task<ActionResult<IReadOnlyList<ApiPortalResult>>> Results(int id)
        {
            if (!await StudentExistsAsync(id))
            {
                return NotFoundError();
            }

            var results = await _portal.GetPublishedResultsAsync(_user.UserId, id, Ct);
            if (results.Count == 0)
            {
                return Array.Empty<ApiPortalResult>();
            }

            var offeringIds = results.Select(r => r.CurriculumOfferingId).Distinct().ToList();
            var termIds = results.Select(r => r.TermId).Distinct().ToList();

            var subjects = await _db.CurriculumOfferings.AsNoTracking()
                .Where(o => offeringIds.Contains(o.Id))
                .Join(_db.Subjects.IgnoreQueryFilters().AsNoTracking(), o => o.SubjectId, s => s.Id,
                    (o, s) => new { o.Id, s.Name.NameAr, s.Name.NameEn })
                .ToListAsync(Ct);

            var terms = await _db.Terms.AsNoTracking()
                .Where(t => termIds.Contains(t.Id))
                .Select(t => new { t.Id, t.NameAr, t.NameEn })
                .ToListAsync(Ct);

            return results
                .Select(r =>
                {
                    var subject = subjects.FirstOrDefault(s => s.Id == r.CurriculumOfferingId);
                    var term = terms.FirstOrDefault(t => t.Id == r.TermId);
                    return new ApiPortalResult
                    {
                        CurriculumOfferingId = r.CurriculumOfferingId,
                        SubjectNameAr = subject?.NameAr ?? string.Empty,
                        SubjectNameEn = subject?.NameEn ?? string.Empty,
                        TermId = r.TermId,
                        TermName = term == null ? null : T(term.NameEn, term.NameAr),
                        ScorePercent = r.ScorePercent,
                        BandCode = r.BandCode,
                        PublishedAtUtc = r.PublishedAtUtc,
                    };
                })
                .ToList();
        }

        /// <summary>Posted charges and what is left on them (BR-DIS-010: gross and discount stay apart).</summary>
        [HttpGet("students/{id:int}/fees")]
        [RequirePermission(ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Statement, ActionVerb.View)]
        public async Task<ActionResult<ApiPortalFees>> Fees(int id)
        {
            if (!await StudentExistsAsync(id))
            {
                return NotFoundError();
            }

            return await FeesForAsync(id);
        }

        /// <summary>
        /// The whole family in one call — the statement screen's figure. A child
        /// the fee gate refuses is left out rather than shown as zero: "owes
        /// nothing" and "you may not see this" are different statements.
        /// </summary>
        [HttpGet("statement")]
        [RequirePermission(ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Statement, ActionVerb.View)]
        public async Task<ActionResult<ApiPortalStatement>> Statement()
        {
            var lines = new List<ApiPortalFees>();
            foreach (var (student, _) in await FamilyAsync())
            {
                try
                {
                    lines.Add(await FeesForAsync(student.Id));
                }
                catch (PortalAccessDeniedException)
                {
                }
            }

            return new ApiPortalStatement
            {
                Total = lines.Sum(l => l.Position),
                Currency = await CurrencyAsync(),
                Students = lines,
            };
        }

        /// <summary>doc/Modules/37 §8.10 — issued homework for this student's section, due date first.</summary>
        [HttpGet("students/{id:int}/homework")]
        [RequirePermission(ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Work, ActionVerb.View)]
        public async Task<ActionResult<IReadOnlyList<ApiPortalHomework>>> Homework(int id)
        {
            if (!await StudentExistsAsync(id))
            {
                return NotFoundError();
            }

            var work = await _portal.GetSetWorkAsync(_user.UserId, id, Ct);
            return work
                .Select(w => new ApiPortalHomework
                {
                    HomeworkId = w.HomeworkId,
                    TitleAr = w.TitleAr,
                    TitleEn = w.TitleEn,
                    InstructionsAr = w.InstructionsAr,
                    InstructionsEn = w.InstructionsEn,
                    SubjectNameAr = w.SubjectNameAr,
                    SubjectNameEn = w.SubjectNameEn,
                    DueDate = w.DueDate,
                    MaxMarks = w.MaxMarks,
                    LatePenaltyApplies = w.LatePenaltyApplies,
                    LatePenaltyPercent = w.LatePenaltyPercent,
                })
                .ToList();
        }

        /// <summary>doc/Modules/37 §5 — published lessons and their scan-clean material.</summary>
        [HttpGet("students/{id:int}/lessons")]
        [RequirePermission(ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Lessons, ActionVerb.View)]
        public async Task<ActionResult<IReadOnlyList<ApiPortalLesson>>> Lessons(int id)
        {
            if (!await StudentExistsAsync(id))
            {
                return NotFoundError();
            }

            var lessons = await _portal.GetPublishedLessonsAsync(_user.UserId, id, Ct);
            return lessons
                .Select(l => new ApiPortalLesson
                {
                    LessonId = l.LessonId,
                    WeekNumber = l.WeekNumber,
                    TitleAr = l.TitleAr,
                    TitleEn = l.TitleEn,
                    ObjectivesAr = l.ObjectivesAr,
                    ObjectivesEn = l.ObjectivesEn,
                    SubjectNameAr = l.SubjectNameAr,
                    SubjectNameEn = l.SubjectNameEn,
                    PublishedAtUtc = l.PublishedAtUtc,
                    Resources = l.Resources
                        .Select(r => new ApiPortalLessonResource
                        {
                            ResourceId = r.ResourceId,
                            TitleAr = r.TitleAr,
                            TitleEn = r.TitleEn,
                            DisplayOrder = r.DisplayOrder,
                            DownloadUrl = $"/{V1}/portal/resources/{r.ResourceId}/file",
                        })
                        .ToList(),
                })
                .ToList();
        }

        /// <summary>
        /// The bytes behind one lesson resource. Two gates, both here: BR-SEC-011
        /// asked of the resource id (so a student id in a URL is never trusted),
        /// and BR-LRN-006's scan verdict, applied to the file itself. A resource
        /// whose scan changed between listing and tap is refused — which is
        /// exactly when refusing matters.
        /// </summary>
        [HttpGet("resources/{resourceId:int}/file")]
        [RequirePermission(ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Lessons, ActionVerb.View)]
        public async Task<IActionResult> LessonFile(int resourceId)
        {
            if (!await _portal.CanReadLessonResourceAsync(_user.UserId, resourceId, Ct))
            {
                return NotFoundError();
            }

            var attachmentId = await _db.LessonResources.AsNoTracking()
                .Where(r => r.Id == resourceId)
                .Select(r => (int?)r.AttachmentId)
                .SingleOrDefaultAsync(Ct);
            if (attachmentId == null)
            {
                return NotFoundError();
            }

            AttachmentIntake.StoredFile? stored;
            try
            {
                stored = await _intake.ReadAsync(attachmentId.Value, Ct);
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException)
            {
                // The row says there is a file and the store cannot produce it. From
                // the family's side that is the same as a file still being checked,
                // and neither is theirs to fix.
                stored = null;
            }

            if (stored == null)
            {
                return Refuse(409, "resource_not_available",
                    "That material is not available yet — the school's file check has not cleared it.",
                    "هذه المادة غير متاحة بعد — لم يكتمل فحص الملف لدى المدرسة.");
            }

            return File(stored.Content, stored.ContentType, stored.FileName);
        }

        /// <summary>BR-SEC-012: sent announcements only, newest first.</summary>
        [HttpGet("announcements")]
        [RequirePermission(ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Announcements, ActionVerb.View)]
        public async Task<ActionResult<ApiPage<ApiPortalAnnouncement>>> Announcements(int? page = null, int? pageSize = null)
        {
            var (p, size) = ApiPaging.Clamp(page, pageSize);
            var query = _db.Announcements.AsNoTracking().Where(a => a.Status == AnnouncementStatus.Sent);

            var total = await query.CountAsync(Ct);
            var rows = await query
                .OrderByDescending(a => a.SentAtUtc)
                .Skip(ApiPaging.Skip(p, size))
                .Take(size)
                .Select(a => new ApiPortalAnnouncement
                {
                    Id = a.Id,
                    TitleAr = a.TitleAr,
                    TitleEn = a.TitleEn,
                    BodyAr = a.BodyAr,
                    BodyEn = a.BodyEn,
                    SentAtUtc = a.SentAtUtc,
                })
                .ToListAsync(Ct);

            return Page<ApiPortalAnnouncement>(rows, p, size, total);
        }

        /// <summary>
        /// The student's section week off the operational timetable version,
        /// flattened into a list a phone can render per day (doc/Modules/15 §11).
        /// Dated overlays for the current week are folded in, so a substitution
        /// or a room change shows here the moment it is made (BR-TTB-008).
        /// </summary>
        [HttpGet("students/{id:int}/timetable")]
        [RequirePermission(ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Child, ActionVerb.View)]
        public async Task<ActionResult<ApiPortalTimetable>> Timetable(int id)
        {
            if (!await StudentExistsAsync(id))
            {
                return NotFoundError();
            }

            // Asked of the gate, not of the URL: the cheapest read that carries the
            // BR-SEC-011 check, so a guessed student id never reaches the timetable.
            await _portal.GetAttendanceSummaryAsync(_user.UserId, id, Ct);

            var (grade, section) = await PlacementAsync(id);
            var result = new ApiPortalTimetable
            {
                StudentId = id,
                GradeCode = grade?.Code,
                SectionName = section == null ? null : T(section.NameEn, section.NameAr),
                WeekStart = _clock.UtcNow.Date,
            };

            if (section == null)
            {
                return result;
            }

            var week = await TimetableQueries.PersonalAsync(
                _db, _setup, "section", section.Id, _workingYear.AcademicYearId, _clock.UtcNow.Date, null, IsArabic);

            result.WeekStart = week.WeekStart;
            result.Entries = Flatten(week);
            return result;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// The weekly grid as a flat list. The browser renders a day × period
        /// table; a phone renders a day at a time, and doing the transposition
        /// here keeps every client from reinventing it.
        /// </summary>
        private IReadOnlyList<ApiTimetableEntry> Flatten(Sms.Web.Models.PersonalTimetableViewModel week)
        {
            var entries = new List<ApiTimetableEntry>();
            foreach (var day in week.Days)
            {
                foreach (var sequence in week.Sequences)
                {
                    if (!week.Cells.TryGetValue((day, sequence), out var cells))
                    {
                        continue;
                    }

                    week.Slots.TryGetValue((day, sequence), out var slot);

                    foreach (var cell in cells)
                    {
                        week.WeekSessions.TryGetValue(cell.Placement.Id, out var session);
                        var room = session?.OverrideRoomId != null && week.Rooms.TryGetValue(session.OverrideRoomId.Value, out var moved)
                            ? moved
                            : cell.Room;

                        entries.Add(new ApiTimetableEntry
                        {
                            DayOfWeek = (int)day,
                            PeriodSequence = sequence,
                            StartTime = slot?.StartTime.ToString(@"hh\:mm"),
                            EndTime = slot?.EndTime.ToString(@"hh\:mm"),
                            SubjectNameAr = cell.Subject.Name.NameAr,
                            SubjectNameEn = cell.Subject.Name.NameEn,
                            TeacherNameAr = $"{cell.Teacher.FirstNameAr} {cell.Teacher.FamilyNameAr}".Trim(),
                            TeacherNameEn = $"{cell.Teacher.FirstNameEn} {cell.Teacher.FamilyNameEn}".Trim(),
                            RoomName = room == null ? null : T(room.Name.NameEn, room.Name.NameAr),
                            SectionName = T(cell.Section.NameEn, cell.Section.NameAr),
                            ChangeKind = ChangeOf(session, cell),
                        });
                    }
                }
            }

            return entries;
        }

        /// <summary>
        /// BR-TTB-008's overlay, named for a client that will show a badge.
        /// Null on an ordinary week, which is most of them.
        /// </summary>
        private static string? ChangeOf(Sms.Domain.Timetable.Session? session, Sms.Web.Models.PlacementCell cell)
        {
            if (session == null)
            {
                return null;
            }

            if (session.Status == Sms.Domain.Timetable.SessionStatus.Cancelled)
            {
                return "cancelled";
            }

            if (session.OverrideRoomId != null && session.OverrideRoomId != cell.Room?.Id)
            {
                return "room-change";
            }

            return null;
        }

        private async Task<ApiPortalFees> FeesForAsync(int studentId)
        {
            var position = await _portal.GetFeePositionAsync(_user.UserId, studentId, Ct);
            return new ApiPortalFees
            {
                StudentId = studentId,
                Position = position.Position,
                GrossCharges = position.GrossCharges,
                Discounts = position.Discounts,
                Currency = await CurrencyAsync(),
                Charges = position.Charges
                    .Select(c => new ApiPortalChargeLine
                    {
                        ChargeNo = c.ChargeNo,
                        GrossAmount = c.GrossAmount,
                        PostedAtUtc = c.PostedAtUtc,
                    })
                    .ToList(),
            };
        }

        /// <summary>
        /// Whether this student exists at all, asked before the BR-SEC-011 gate is.
        /// <para>
        /// <b>Why the gate is not enough.</b> <c>IParentPortalQuery</c> resolves the
        /// student with <c>SingleAsync</c>, which throws
        /// <c>InvalidOperationException("Sequence contains no elements")</c> for an id
        /// that is not there — a fault, not a refusal, and
        /// <see cref="ApiProblem"/> rightly declines to dress it up as a business rule.
        /// So an unknown id came back as <b>500</b> while an id belonging to another
        /// family came back as 404, and the difference told a caller which student ids
        /// exist. That is precisely the disclosure BR-SEC-011 is written to prevent, so
        /// the two are made indistinguishable here rather than being left to the shape
        /// of an exception.
        /// </para>
        /// <para>
        /// The browser portal has always done this — <c>PortalController.Student</c>
        /// checks the row and returns NotFound before it asks the gate anything. This
        /// is the same check, not a new rule; found by smoke-testing the API against a
        /// student id the signed-in parent does not own.
        /// </para>
        /// <para>
        /// Read through the ordinary filters, deliberately: the port sees the same
        /// filtered set, so a student it could not resolve is one this answers "not
        /// found" for — which is the truthful answer to give.
        /// </para>
        /// </summary>
        private Task<bool> StudentExistsAsync(int studentId)
            => _db.Students.AsNoTracking().AnyAsync(s => s.Id == studentId, Ct);

        /// <summary>Guardian-visible children (BR-PAR-004 / BR-SEC-011) plus the caller's own student record.</summary>
        private async Task<IReadOnlyList<(Sms.Domain.Students.Student Student, bool IsSelf)>> FamilyAsync()
        {
            var list = new List<(Sms.Domain.Students.Student, bool)>();

            var self = await _db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.UserAccountId == _user.UserId, Ct);
            if (self != null)
            {
                list.Add((self, true));
            }

            var children = await _portal.GetVisibleChildrenAsync(_user.UserId, Ct);
            var ids = children.Select(c => c.StudentId).Where(id => self == null || id != self.Id).ToList();
            var students = await _db.Students.AsNoTracking()
                .Where(s => ids.Contains(s.Id))
                .OrderBy(s => s.StudentNo)
                .ToListAsync(Ct);

            list.AddRange(students.Select(s => (s, false)));
            return list;
        }

        private async Task<(Sms.Domain.Grades.GradeLevel? Grade, Sms.Domain.Sections.Section? Section)> PlacementAsync(int studentId)
        {
            var enrollment = await _db.Enrollments.AsNoTracking()
                .Where(e => e.StudentId == studentId && e.AcademicYearId == _workingYear.AcademicYearId)
                .FirstOrDefaultAsync(Ct);
            if (enrollment == null)
            {
                return (null, null);
            }

            var profile = await _db.GradeYearProfiles.IgnoreQueryFilters().AsNoTracking()
                .SingleOrDefaultAsync(p => p.Id == enrollment.GradeYearProfileId, Ct);

            // IgnoreQueryFilters on the lookup, deliberately: a retired grade level still
            // names the year a child is already sitting in, and reading it through the
            // soft-active filter is how this page dies the day a school retires one.
            var grade = profile == null
                ? null
                : await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking()
                    .SingleOrDefaultAsync(g => g.Id == profile.GradeLevelId, Ct);

            var membership = await _db.SectionMemberships.AsNoTracking()
                .Where(m => m.EnrollmentId == enrollment.Id && m.EffectiveToUtc == null)
                .FirstOrDefaultAsync(Ct);
            var section = membership == null
                ? null
                : await _db.Sections.AsNoTracking().SingleOrDefaultAsync(s => s.Id == membership.SectionId, Ct);

            return (grade, section);
        }

        /// <summary>The school's own currency, so an amount never travels without one.</summary>
        private async Task<string> CurrencyAsync()
            => await _db.Schools.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.Id == _db.CurrentSchoolId)
                .Select(s => s.CurrencyCode)
                .SingleOrDefaultAsync(Ct) ?? string.Empty;
    }
}
