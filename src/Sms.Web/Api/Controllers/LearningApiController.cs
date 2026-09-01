using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Application.Learning;
using Sms.Application.Security;
using Sms.Domain.Attachments;
using Sms.Domain.Learning;
using Sms.Domain.Security;
using Sms.Infrastructure.Persistence;
using Sms.Web.Api.Models;
using Sms.Web.Security;
using Sms.Web.Services;

namespace Sms.Web.Api.Controllers
{
    /// <summary>
    /// doc/Modules/37 §8.1–8.3 for the app — the lesson planner, the resource
    /// library and the homework desk, over the same <see cref="ILessonAdmin"/>
    /// and <see cref="IHomeworkAdmin"/> the browser screens use.
    /// <para>
    /// <b>Reach is not decided here.</b> BR-LRN-002 ("who may put this in front
    /// of which students") lives in the ports, which resolve the caller's own
    /// placements from the published timetable version. This controller passes
    /// <c>hasSchoolWideReach: false</c> exactly as
    /// <c>LearningController</c> does — nobody holds school-wide authoring reach
    /// in this build, and granting it from a second transport would be a
    /// security change made by accident.
    /// </para>
    /// <para>
    /// <b>Stated gap.</b> §8.10's student submission and §8.11's timed sitting
    /// have no entity in this product yet — <c>PortalSetWork</c> says so in as
    /// many words ("carries no submission and no mark"). So there is no
    /// submit endpoint and no marking endpoint here, and there cannot be one
    /// until that slice is built. This is a gap in the module, not in the API.
    /// </para>
    /// </summary>
    [Route(V1 + "/learning")]
    public sealed class LearningApiController : ApiControllerBase
    {
        /// <summary>
        /// Deny by default, and the same value the browser passes. Changing it
        /// would widen every teacher's authoring reach to the whole school.
        /// </summary>
        private const bool SchoolWide = false;

        private readonly ILessonAdmin _lessons;
        private readonly IHomeworkAdmin _homework;
        private readonly AppDbContext _db;
        private readonly IWorkingYearContext _workingYear;
        private readonly AttachmentIntake _attachments;

        public LearningApiController(
            ILessonAdmin lessons,
            IHomeworkAdmin homework,
            AppDbContext db,
            IWorkingYearContext workingYear,
            AttachmentIntake attachments)
        {
            _lessons = lessons;
            _homework = homework;
            _db = db;
            _workingYear = workingYear;
            _attachments = attachments;
        }

        // ---------------------------------------------------------------- reach

        /// <summary>
        /// BR-LRN-002: what this teacher may author against. The app builds its
        /// offering picker from this rather than from the whole curriculum, so an
        /// option it offers is never refused on submit.
        /// </summary>
        [HttpGet("reach/offerings")]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Planner, ActionVerb.View)]
        public async Task<ActionResult<IReadOnlyList<ApiTeachingReach>>> ReachableOfferings()
        {
            var ids = await _lessons.ReachableOfferingIdsAsync(SchoolWide, Ct);
            var subjects = await SubjectNamesAsync(ids);

            return ids
                .Select(id => new ApiTeachingReach
                {
                    CurriculumOfferingId = id,
                    SubjectNameAr = subjects.TryGetValue(id, out var s) ? s.Ar : string.Empty,
                    SubjectNameEn = subjects.TryGetValue(id, out var e) ? e.En : string.Empty,
                })
                .ToList();
        }

        /// <summary>BR-LRN-002 for homework: the (offering, section) pairs this teacher may set work to.</summary>
        [HttpGet("reach/sections")]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Homework, ActionVerb.View)]
        public async Task<ActionResult<IReadOnlyList<ApiTeachingReach>>> ReachableSections()
        {
            var reach = await _homework.ReachableSectionsAsync(SchoolWide, Ct);
            var subjects = await SubjectNamesAsync(reach.Select(r => r.CurriculumOfferingId).Distinct().ToList());

            var sectionIds = reach.Select(r => r.SectionId).Distinct().ToList();
            var sections = await _db.Sections.AsNoTracking()
                .Where(s => sectionIds.Contains(s.Id))
                .Select(s => new { s.Id, s.NameAr, s.NameEn })
                .ToListAsync(Ct);

            return reach
                .Select(r =>
                {
                    var section = sections.FirstOrDefault(s => s.Id == r.SectionId);
                    subjects.TryGetValue(r.CurriculumOfferingId, out var subject);
                    return new ApiTeachingReach
                    {
                        CurriculumOfferingId = r.CurriculumOfferingId,
                        SectionId = r.SectionId,
                        SubjectNameAr = subject.Ar ?? string.Empty,
                        SubjectNameEn = subject.En ?? string.Empty,
                        SectionName = section == null ? null : T(section.NameEn, section.NameAr),
                    };
                })
                .ToList();
        }

        // ---------------------------------------------------------------- §8.1 planner

        /// <summary>
        /// The lessons this teacher may see, newest week first. Scoped to reach:
        /// the planner is not a school-wide content browser, and an endpoint that
        /// returned every lesson would be one.
        /// </summary>
        [HttpGet("lessons")]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Planner, ActionVerb.View)]
        public async Task<ActionResult<ApiPage<ApiLesson>>> Lessons(
            int? offeringId = null, int? week = null, string? status = null, int? page = null, int? pageSize = null)
        {
            var (p, size) = ApiPaging.Clamp(page, pageSize);
            var reachable = await _lessons.ReachableOfferingIdsAsync(SchoolWide, Ct);
            if (reachable.Count == 0)
            {
                return Page(Array.Empty<ApiLesson>(), p, size, 0);
            }

            var query = _db.Lessons.AsNoTracking()
                .Where(l => l.AcademicYearId == _workingYear.AcademicYearId && reachable.Contains(l.CurriculumOfferingId));

            if (offeringId.HasValue)
            {
                query = query.Where(l => l.CurriculumOfferingId == offeringId.Value);
            }

            if (week.HasValue)
            {
                query = query.Where(l => l.WeekNumber == week.Value);
            }

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<LessonStatus>(status, ignoreCase: true, out var wanted))
            {
                query = query.Where(l => l.Status == wanted);
            }

            var total = await query.CountAsync(Ct);
            var rows = await query
                .OrderByDescending(l => l.WeekNumber).ThenBy(l => l.Id)
                .Skip(ApiPaging.Skip(p, size))
                .Take(size)
                .ToListAsync(Ct);

            return Page(await ProjectAsync(rows), p, size, total);
        }

        /// <summary>One lesson with its material.</summary>
        [HttpGet("lessons/{id:int}")]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Planner, ActionVerb.View)]
        public async Task<ActionResult<ApiLesson>> Lesson(int id)
        {
            var lesson = await ReachableLessonAsync(id);
            if (lesson == null)
            {
                return NotFoundError();
            }

            return (await ProjectAsync(new[] { lesson }))[0];
        }

        /// <summary>§8.1. Creates a Draft — BR-LRN-003: no family sees it until it is published.</summary>
        [HttpPost("lessons")]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Planner, ActionVerb.Create)]
        public async Task<ActionResult<ApiLesson>> CreateLesson([FromBody] ApiCreateLessonRequest request)
        {
            var lesson = await _lessons.CreateAsync(
                request.CurriculumOfferingId, request.WeekNumber,
                request.TitleAr.Trim(), request.TitleEn.Trim(),
                request.ObjectivesAr, request.ObjectivesEn, request.SessionId, SchoolWide, Ct);

            return (await ProjectAsync(new[] { lesson }))[0];
        }

        /// <summary>Edits a Draft or Published lesson in place (BR-LRN-016 guards the retired one).</summary>
        [HttpPut("lessons/{id:int}")]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Planner, ActionVerb.Edit)]
        public async Task<ActionResult<ApiLesson>> UpdateLesson(int id, [FromBody] ApiUpdateLessonRequest request)
        {
            var lesson = await _lessons.UpdateAsync(
                id, request.WeekNumber, request.TitleAr.Trim(), request.TitleEn.Trim(),
                request.ObjectivesAr, request.ObjectivesEn, request.SessionId, SchoolWide, Ct);

            return (await ProjectAsync(new[] { lesson }))[0];
        }

        /// <summary>BR-LRN-003: publication is the event families see.</summary>
        [HttpPost("lessons/{id:int}/publish")]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Planner, ActionVerb.Approve)]
        public async Task<IActionResult> PublishLesson(int id)
        {
            await _lessons.PublishAsync(id, SchoolWide, Ct);
            return NoContent();
        }

        /// <summary>BR-LRN-016: content is retired with a stated reason, never deleted.</summary>
        [HttpPost("lessons/{id:int}/retire")]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Planner, ActionVerb.Deactivate)]
        public async Task<IActionResult> RetireLesson(int id, [FromBody] ApiReasonRequest request)
        {
            await _lessons.RetireAsync(id, request.Reason.Trim(), SchoolWide, Ct);
            return NoContent();
        }

        // ---------------------------------------------------------------- §8.2 material

        /// <summary>
        /// §8.2. Links an already-uploaded <c>doc.Attachment</c> to the lesson.
        /// The upload itself stays on the browser's document screens: an
        /// attachment carries a document type, a size limit and a scan, and
        /// duplicating that intake pipeline for a second transport is how the two
        /// stop agreeing about what a valid file is.
        /// </summary>
        [HttpPost("lessons/{id:int}/resources")]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Resources, ActionVerb.Create)]
        public async Task<ActionResult<ApiLessonResource>> AttachResource(int id, [FromBody] ApiAttachResourceRequest request)
        {
            var resource = await _lessons.AttachResourceAsync(
                id, request.AttachmentId, request.TitleAr.Trim(), request.TitleEn.Trim(),
                request.DisplayOrder, SchoolWide, Ct);

            var clean = await ScanCleanAsync(new[] { resource.AttachmentId });
            return Describe(resource, clean.Contains(resource.AttachmentId));
        }

        /// <summary>BR-GLB-005 / BR-LRN-016: a mis-filed document is withdrawn, and the row stays.</summary>
        [HttpPost("resources/{resourceId:int}/withdraw")]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Resources, ActionVerb.Deactivate)]
        public async Task<IActionResult> WithdrawResource(int resourceId)
        {
            await _lessons.WithdrawResourceAsync(resourceId, SchoolWide, Ct);
            return NoContent();
        }

        /// <summary>
        /// The bytes, for a teacher. Reach-gated on the owning lesson, and still
        /// subject to BR-LRN-006 — <c>AttachmentIntake.ReadAsync</c> serves
        /// nothing for a quarantined or unscanned file, to staff or to a family.
        /// </summary>
        [HttpGet("resources/{resourceId:int}/file")]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Resources, ActionVerb.View)]
        public async Task<IActionResult> ResourceFile(int resourceId)
        {
            var reachable = await _lessons.ReachableOfferingIdsAsync(SchoolWide, Ct);
            var resource = await _db.LessonResources.AsNoTracking()
                .Where(r => r.Id == resourceId)
                .Join(_db.Lessons.AsNoTracking(), r => r.LessonId, l => l.Id,
                    (r, l) => new { r.AttachmentId, l.CurriculumOfferingId })
                .FirstOrDefaultAsync(Ct);

            if (resource == null || !reachable.Contains(resource.CurriculumOfferingId))
            {
                return NotFoundError();
            }

            AttachmentIntake.StoredFile? stored;
            try
            {
                stored = await _attachments.ReadAsync(resource.AttachmentId, Ct);
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException)
            {
                stored = null;
            }

            if (stored == null)
            {
                return Refuse(409, "resource_not_available",
                    "That file has not cleared its check yet.",
                    "لم يجتز هذا الملف الفحص بعد.");
            }

            return File(stored.Content, stored.ContentType, stored.FileName);
        }

        // ---------------------------------------------------------------- §8.3 homework desk

        /// <summary>The work this teacher has set, due date first. Scoped to reach like the planner.</summary>
        [HttpGet("homework")]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Homework, ActionVerb.View)]
        public async Task<ActionResult<ApiPage<ApiHomework>>> HomeworkList(
            int? sectionId = null, int? offeringId = null, string? status = null, int? page = null, int? pageSize = null)
        {
            var (p, size) = ApiPaging.Clamp(page, pageSize);
            var reach = await _homework.ReachableSectionsAsync(SchoolWide, Ct);
            if (reach.Count == 0)
            {
                return Page(Array.Empty<ApiHomework>(), p, size, 0);
            }

            var offerings = reach.Select(r => r.CurriculumOfferingId).Distinct().ToList();
            var sections = reach.Select(r => r.SectionId).Distinct().ToList();

            // Filtered by the two id sets rather than by the pairs themselves: EF cannot
            // translate a composite `Contains` here, and the pair check is re-applied in
            // memory below so a teacher never sees a class they do not hold.
            var query = _db.Homeworks.AsNoTracking()
                .Where(h => h.AcademicYearId == _workingYear.AcademicYearId
                    && offerings.Contains(h.CurriculumOfferingId)
                    && sections.Contains(h.SectionId));

            if (sectionId.HasValue)
            {
                query = query.Where(h => h.SectionId == sectionId.Value);
            }

            if (offeringId.HasValue)
            {
                query = query.Where(h => h.CurriculumOfferingId == offeringId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<HomeworkStatus>(status, ignoreCase: true, out var wanted))
            {
                query = query.Where(h => h.Status == wanted);
            }

            var all = await query.OrderByDescending(h => h.DueDate).ThenBy(h => h.Id).ToListAsync(Ct);
            var pairs = reach.Select(r => (r.CurriculumOfferingId, r.SectionId)).ToHashSet();
            var held = all.Where(h => pairs.Contains((h.CurriculumOfferingId, h.SectionId))).ToList();

            var rows = held.Skip(ApiPaging.Skip(p, size)).Take(size).ToList();
            return Page(await ProjectAsync(rows), p, size, held.Count);
        }

        /// <summary>§8.3. Creates a Draft; BR-LRN-004's gate applies at issue, not here.</summary>
        [HttpPost("homework")]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Homework, ActionVerb.Create)]
        public async Task<ActionResult<ApiHomework>> CreateHomework([FromBody] ApiCreateHomeworkRequest request)
        {
            var homework = await _homework.CreateAsync(
                request.CurriculumOfferingId, request.SectionId,
                request.TitleAr.Trim(), request.TitleEn.Trim(), request.DueDate,
                request.InstructionsAr, request.InstructionsEn, request.MaxMarks, request.BlueprintComponentId,
                Lateness(request.LatenessPolicy), request.LatePenaltyPercent, SchoolWide, Ct);

            return (await ProjectAsync(new[] { homework }))[0];
        }

        /// <summary>Edits Draft or Issued work — correcting a typo after setting it is ordinary.</summary>
        [HttpPut("homework/{id:int}")]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Homework, ActionVerb.Edit)]
        public async Task<ActionResult<ApiHomework>> UpdateHomework(int id, [FromBody] ApiUpdateHomeworkRequest request)
        {
            var homework = await _homework.UpdateAsync(
                id, request.TitleAr.Trim(), request.TitleEn.Trim(), request.DueDate,
                request.InstructionsAr, request.InstructionsEn, request.MaxMarks, request.BlueprintComponentId,
                Lateness(request.LatenessPolicy), request.LatePenaltyPercent, SchoolWide, Ct);

            return (await ProjectAsync(new[] { homework }))[0];
        }

        /// <summary>BR-LRN-003/004: issue is the event the section's families see.</summary>
        [HttpPost("homework/{id:int}/issue")]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Homework, ActionVerb.Approve)]
        public async Task<IActionResult> IssueHomework(int id)
        {
            await _homework.IssueAsync(id, SchoolWide, Ct);
            return NoContent();
        }

        /// <summary>BR-LRN-016: work is withdrawn with a reason, because whoever already did it is told why.</summary>
        [HttpPost("homework/{id:int}/withdraw")]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Homework, ActionVerb.Deactivate)]
        public async Task<IActionResult> WithdrawHomework(int id, [FromBody] ApiReasonRequest request)
        {
            await _homework.WithdrawAsync(id, request.Reason.Trim(), SchoolWide, Ct);
            return NoContent();
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// An unknown or absent policy name becomes the port's own default rather
        /// than a refusal — accepting late work without a penalty is the
        /// forgiving choice, and it is what the browser sends when the field is
        /// left alone.
        /// </summary>
        private static LatenessPolicy Lateness(string? name)
            => Enum.TryParse<LatenessPolicy>(name, ignoreCase: true, out var policy)
                ? policy
                : LatenessPolicy.AcceptWithoutPenalty;

        private async Task<Lesson?> ReachableLessonAsync(int id)
        {
            var lesson = await _db.Lessons.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, Ct);
            if (lesson == null)
            {
                return null;
            }

            var reachable = await _lessons.ReachableOfferingIdsAsync(SchoolWide, Ct);
            return reachable.Contains(lesson.CurriculumOfferingId) ? lesson : null;
        }

        private async Task<IReadOnlyList<ApiLesson>> ProjectAsync(IReadOnlyList<Lesson> lessons)
        {
            if (lessons.Count == 0)
            {
                return Array.Empty<ApiLesson>();
            }

            var subjects = await SubjectNamesAsync(lessons.Select(l => l.CurriculumOfferingId).Distinct().ToList());
            var lessonIds = lessons.Select(l => l.Id).ToList();

            var resources = await _db.LessonResources.AsNoTracking()
                .Where(r => lessonIds.Contains(r.LessonId))
                .OrderBy(r => r.DisplayOrder).ThenBy(r => r.Id)
                .ToListAsync(Ct);

            var clean = await ScanCleanAsync(resources.Select(r => r.AttachmentId).Distinct().ToList());

            return lessons
                .Select(l =>
                {
                    subjects.TryGetValue(l.CurriculumOfferingId, out var subject);
                    return new ApiLesson
                    {
                        LessonId = l.Id,
                        CurriculumOfferingId = l.CurriculumOfferingId,
                        SessionId = l.SessionId,
                        WeekNumber = l.WeekNumber,
                        TitleAr = l.TitleAr,
                        TitleEn = l.TitleEn,
                        ObjectivesAr = l.ObjectivesAr,
                        ObjectivesEn = l.ObjectivesEn,
                        SubjectNameAr = subject.Ar ?? string.Empty,
                        SubjectNameEn = subject.En ?? string.Empty,
                        Status = l.Status.ToString(),
                        PublishedAtUtc = l.PublishedAtUtc,
                        RetiredReason = l.RetiredReason,
                        Resources = resources
                            .Where(r => r.LessonId == l.Id)
                            .Select(r => Describe(r, clean.Contains(r.AttachmentId)))
                            .ToList(),
                    };
                })
                .ToList();
        }

        private static ApiLessonResource Describe(LessonResource resource, bool isScanClean) => new()
        {
            ResourceId = resource.Id,
            AttachmentId = resource.AttachmentId,
            TitleAr = resource.TitleAr,
            TitleEn = resource.TitleEn,
            DisplayOrder = resource.DisplayOrder,
            IsScanClean = isScanClean,
            DownloadUrl = $"/{V1}/learning/resources/{resource.Id}/file",
        };

        private async Task<IReadOnlyList<ApiHomework>> ProjectAsync(IReadOnlyList<Homework> homeworks)
        {
            if (homeworks.Count == 0)
            {
                return Array.Empty<ApiHomework>();
            }

            var subjects = await SubjectNamesAsync(homeworks.Select(h => h.CurriculumOfferingId).Distinct().ToList());
            var sectionIds = homeworks.Select(h => h.SectionId).Distinct().ToList();
            var sections = await _db.Sections.AsNoTracking()
                .Where(s => sectionIds.Contains(s.Id))
                .Select(s => new { s.Id, s.NameAr, s.NameEn })
                .ToListAsync(Ct);

            return homeworks
                .Select(h =>
                {
                    subjects.TryGetValue(h.CurriculumOfferingId, out var subject);
                    var section = sections.FirstOrDefault(s => s.Id == h.SectionId);
                    return new ApiHomework
                    {
                        HomeworkId = h.Id,
                        CurriculumOfferingId = h.CurriculumOfferingId,
                        SectionId = h.SectionId,
                        TitleAr = h.TitleAr,
                        TitleEn = h.TitleEn,
                        InstructionsAr = h.InstructionsAr,
                        InstructionsEn = h.InstructionsEn,
                        SubjectNameAr = subject.Ar ?? string.Empty,
                        SubjectNameEn = subject.En ?? string.Empty,
                        SectionName = section == null ? null : T(section.NameEn, section.NameAr),
                        DueDate = h.DueDate,
                        MaxMarks = h.MaxMarks,
                        BlueprintComponentId = h.BlueprintComponentId,
                        LatenessPolicy = h.LatenessPolicy.ToString(),
                        LatePenaltyPercent = h.LatePenaltyPercent,
                        Status = h.Status.ToString(),
                        IssuedAtUtc = h.IssuedAtUtc,
                        WithdrawnReason = h.WithdrawnReason,
                    };
                })
                .ToList();
        }

        /// <summary>
        /// Offering id → subject name. Read through <c>IgnoreQueryFilters</c> on
        /// the subject deliberately: a retired subject still names the lessons
        /// already written against it, and reading it through the soft-active
        /// filter is how this list dies the day a school retires one.
        /// </summary>
        private async Task<Dictionary<int, (string Ar, string En)>> SubjectNamesAsync(IReadOnlyList<int> offeringIds)
        {
            if (offeringIds.Count == 0)
            {
                return new Dictionary<int, (string, string)>();
            }

            var rows = await _db.CurriculumOfferings.AsNoTracking()
                .Where(o => offeringIds.Contains(o.Id))
                .Join(_db.Subjects.IgnoreQueryFilters().AsNoTracking(), o => o.SubjectId, s => s.Id,
                    (o, s) => new { o.Id, s.Name.NameAr, s.Name.NameEn })
                .ToListAsync(Ct);

            return rows.ToDictionary(r => r.Id, r => (r.NameAr, r.NameEn));
        }

        /// <summary>
        /// BR-LRN-006, asked of the row rather than of the bytes. The teacher's
        /// list shows an unclean file and says so; the portal's list does not
        /// show it at all. Two different right answers to the same rule.
        /// </summary>
        private async Task<HashSet<int>> ScanCleanAsync(IReadOnlyList<int> attachmentIds)
        {
            if (attachmentIds.Count == 0)
            {
                return new HashSet<int>();
            }

            var current = await _db.Attachments.IgnoreQueryFilters()
                .Where(a => attachmentIds.Contains(a.Id) && a.SchoolId == _db.CurrentSchoolId)
                .Select(a => new { a.Id, a.CurrentVersionNumber })
                .ToListAsync(Ct);

            var versions = await _db.AttachmentVersions
                .Where(v => attachmentIds.Contains(v.AttachmentId))
                .Select(v => new { v.AttachmentId, v.VersionNumber, v.ScanStatus })
                .ToListAsync(Ct);

            var clean = new HashSet<int>();
            foreach (var attachment in current)
            {
                var forAttachment = versions.Where(v => v.AttachmentId == attachment.Id).ToList();
                var version = forAttachment.FirstOrDefault(v => v.VersionNumber == attachment.CurrentVersionNumber)
                    ?? forAttachment.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
                if (version?.ScanStatus == ScanStatus.Clean)
                {
                    clean.Add(attachment.Id);
                }
            }

            return clean;
        }
    }
}
