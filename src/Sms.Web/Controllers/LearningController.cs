using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Learning;
using Sms.Application.Security;
using Sms.Domain.Attachments;
using Sms.Domain.Learning;
using Sms.Domain.Security;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;
using Sms.Web.Security;
using Sms.Web.Services;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/37 §8.1-3: the lesson planner (offering x week), the resource
    /// library — per lesson and across an offering — and the homework desk.
    /// <para>
    /// §8.4-5 (submission tracker, marking queue), §8.6-7 (question bank, paper
    /// builder), §8.8-9 (sitting console, integrity review) and §8.12 (analytics)
    /// are deliberately absent — later slices, not silent omissions. Nothing in
    /// this controller writes a mark, so BR-LRN-012's handoff into Module 17 is
    /// not reachable from here yet.
    /// </para>
    /// <para>
    /// <b>The owner's report — "no exams, no worksheets and no enrichment
    /// material, only homework" — is answered here only in its second and third
    /// parts.</b> §8.2's library existed but hung off a single lesson, so a
    /// teacher had no way to ask "what material is filed against this subject?"
    /// and material of a given kind could not be found without opening every
    /// week in turn; <see cref="Materials"/> is that missing surface, and
    /// <c>LRN-ENRICH</c> the missing kind. The first part — <b>exams</b> — is
    /// §8.6-9 and §8.11, which do not exist in this product at all: there is no
    /// question bank, no paper, no sitting and no portal write surface. That
    /// remains an open gap, scheduled as the next slice, and is stated here
    /// rather than quietly narrowed.
    /// </para>
    /// <para>
    /// DEVIATION from §6: BR-LRN-002 gives Vice-Principal and above school-wide
    /// authoring reach. This slice grants it to nobody — <c>hasSchoolWideReach</c>
    /// is always false — because expressing "VP and above" needs a data-scoped
    /// permission concept (BR-GLB-071) that this screen will not invent. A VP
    /// authors through a placement or a department headship until that decision
    /// is taken. Deny by default (BR-GLB-070) is the safe side of the gap.
    /// </para>
    /// </summary>
    [Route("learning")]
    public partial class LearningController : Controller
    {
        /// <summary>
        /// doc 10 files one attachment per (owning entity, document type), so the
        /// library's breadth is its type list: a lesson holds one live document of
        /// each kind, and re-uploading one is a new version of it — which is what
        /// §8.2 means by "versioned". These are created on first use rather than
        /// seeded, so the screen works on a database that predates module 37.
        /// <para>
        /// <c>LRN-ENRICH</c> was added after the owner reported that the module
        /// offered "no worksheets and no enrichment material, only homework".
        /// The worksheet half of that was a discoverability problem — the type
        /// existed but only inside one lesson — while enrichment material had no
        /// type at all, so material a teacher offers beyond the syllabus had
        /// nowhere to be filed except under "reading", which says something
        /// different about it. §8.2 fixes no type list, so extending it is a
        /// content decision rather than a deviation.
        /// </para>
        /// </summary>
        private static readonly (string Code, string Ar, string En)[] ResourceTypes =
        {
            ("LRN-PLAN", "خطة الدرس", "Lesson plan"),
            ("LRN-SLIDES", "عرض تقديمي", "Slides"),
            ("LRN-WORKSHEET", "ورقة عمل", "Worksheet"),
            ("LRN-READING", "مادة للقراءة", "Reading"),
            ("LRN-ENRICH", "مادة إثرائية", "Enrichment material"),
        };

        private const DocumentFormat ResourceFormats =
            DocumentFormat.Pdf | DocumentFormat.Docx | DocumentFormat.Xlsx | DocumentFormat.Jpg | DocumentFormat.Png;

        private const int ResourceMaxBytes = 20 * 1024 * 1024;

        private readonly ILessonAdmin _lessons;
        private readonly IHomeworkAdmin _homework;
        private readonly AppDbContext _db;
        private readonly IPermissionService _permissions;
        private readonly AttachmentIntake _attachments;

        public LearningController(
            ILessonAdmin lessons,
            IHomeworkAdmin homework,
            AppDbContext db,
            IPermissionService permissions,
            AttachmentIntake attachments)
        {
            _lessons = lessons;
            _homework = homework;
            _db = db;
            _permissions = permissions;
            _attachments = attachments;
        }

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        // ---------------------------------------------------------------- §8.1 planner

        [HttpGet("")]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Planner, ActionVerb.View)]
        public async Task<IActionResult> Index(int? offeringId = null)
        {
            return View(await BuildPlannerAsync(offeringId));
        }

        [HttpPost("new")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Planner, ActionVerb.Create)]
        public async Task<IActionResult> Create(
            int offeringId, int weekNumber, string? titleAr, string? titleEn,
            string? objectivesAr, string? objectivesEn, int? sessionId)
        {
            if (string.IsNullOrWhiteSpace(titleAr) || string.IsNullOrWhiteSpace(titleEn))
            {
                // BR-GLB-001: both names before the record exists, not after.
                TempData["Error"] = T("A lesson needs both an Arabic and an English title.", "الدرس يحتاج عنواناً عربياً وآخر إنجليزياً.");
                return RedirectToAction(nameof(Index), new { offeringId });
            }

            try
            {
                await _lessons.CreateAsync(
                    offeringId, weekNumber, titleAr.Trim(), titleEn.Trim(),
                    string.IsNullOrWhiteSpace(objectivesAr) ? null : objectivesAr.Trim(),
                    string.IsNullOrWhiteSpace(objectivesEn) ? null : objectivesEn.Trim(),
                    sessionId,
                    cancellationToken: HttpContext.RequestAborted);

                TempData["Flash"] = T("Lesson drafted. It is invisible to families until you publish it.", "حُفظت مسوّدة الدرس، ولا تراها الأسر حتى تنشرها.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Index), new { offeringId });
        }

        /// <summary>
        /// §8.1. Editing is separate from publishing on purpose: correcting a
        /// title is not the same act as putting the lesson in front of families.
        /// BR-LRN-016 refuses this once the lesson is retired.
        /// </summary>
        [HttpPost("{id:int}/edit")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Planner, ActionVerb.Edit)]
        public async Task<IActionResult> Edit(
            int id, int offeringId, int weekNumber, string? titleAr, string? titleEn,
            string? objectivesAr, string? objectivesEn, int? sessionId)
        {
            if (string.IsNullOrWhiteSpace(titleAr) || string.IsNullOrWhiteSpace(titleEn))
            {
                TempData["Error"] = T("A lesson needs both an Arabic and an English title.", "الدرس يحتاج عنواناً عربياً وآخر إنجليزياً.");
                return RedirectToAction(nameof(Index), new { offeringId });
            }

            try
            {
                await _lessons.UpdateAsync(
                    id, weekNumber, titleAr.Trim(), titleEn.Trim(),
                    string.IsNullOrWhiteSpace(objectivesAr) ? null : objectivesAr.Trim(),
                    string.IsNullOrWhiteSpace(objectivesEn) ? null : objectivesEn.Trim(),
                    sessionId,
                    cancellationToken: HttpContext.RequestAborted);

                TempData["Flash"] = T("Lesson updated.", "حُدِّث الدرس.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Index), new { offeringId });
        }

        /// <summary>BR-LRN-003. Publish takes <see cref="ActionVerb.Approve"/> — the verb taxonomy's word for publish, and a different authority from editing a draft nobody can read.</summary>
        [HttpPost("{id:int}/publish")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Planner, ActionVerb.Approve)]
        public async Task<IActionResult> Publish(int id, int offeringId)
        {
            try
            {
                await _lessons.PublishAsync(id, cancellationToken: HttpContext.RequestAborted);
                TempData["Flash"] = T("Published. Families can see it now.", "نُشر الدرس، وصار ظاهراً للأسر الآن.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Index), new { offeringId });
        }

        /// <summary>BR-LRN-016: retired, never deleted, and the reason is required because a student who read it will ask.</summary>
        [HttpPost("{id:int}/retire")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Planner, ActionVerb.Deactivate)]
        public async Task<IActionResult> Retire(int id, int offeringId, string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = T("Say why the lesson is being withdrawn.", "اذكر سبب سحب الدرس.");
                return RedirectToAction(nameof(Index), new { offeringId });
            }

            try
            {
                await _lessons.RetireAsync(id, reason.Trim(), cancellationToken: HttpContext.RequestAborted);
                TempData["Flash"] = T("Withdrawn. It stays readable as history.", "سُحب الدرس، ويبقى مقروءاً في السجل.");
            }
            catch (ArgumentException)
            {
                TempData["Error"] = T("Say why the lesson is being withdrawn.", "اذكر سبب سحب الدرس.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Index), new { offeringId });
        }

        // ---------------------------------------------------------------- §8.2 materials library

        /// <summary>
        /// doc/Modules/37 §8.2, read across a whole offering instead of one
        /// lesson: every piece of material filed against every lesson of the
        /// chosen subject, newest week first, filterable by kind.
        ///
        /// <para>
        /// The per-lesson library (<see cref="Resources"/>) answers "what is
        /// attached to this lesson?"; a teacher preparing a term, or looking for
        /// the worksheet they filed in week 3, is asking the other question and
        /// had no screen for it. Same rows, same permission, second reading —
        /// which is why this takes <c>Learning.Resources</c> rather than a new
        /// catalogue entry: a second constant over the same data would seed a
        /// permission that grants nothing new (BR-SEC-010) and would let the two
        /// surfaces drift apart in who may read them.
        /// </para>
        ///
        /// <para>
        /// Draft lessons are included and marked. BR-LRN-003 keeps a draft out of
        /// the <em>portal</em>, not out of its own author's library — the teacher
        /// who has not published week 7 still has to be able to find what they
        /// filed against it.
        /// </para>
        /// </summary>
        [HttpGet("materials")]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Resources, ActionVerb.View)]
        public async Task<IActionResult> Materials(int? offeringId = null, string? typeCode = null)
        {
            return View(await BuildMaterialsAsync(offeringId, typeCode));
        }

        // ---------------------------------------------------------------- §8.2 resource library

        [HttpGet("{id:int}/resources")]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Resources, ActionVerb.View)]
        public async Task<IActionResult> Resources(int id)
        {
            var model = await BuildResourcesAsync(id);
            return model == null ? NotFound() : View(model);
        }

        [HttpPost("{id:int}/resources")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Resources, ActionVerb.Create)]
        public async Task<IActionResult> UploadResource(int id, string? typeCode, string? titleAr, string? titleEn, int displayOrder, IFormFile? file)
        {
            if (string.IsNullOrWhiteSpace(titleAr) || string.IsNullOrWhiteSpace(titleEn))
            {
                TempData["Error"] = T("A resource needs both an Arabic and an English title.", "المصدر يحتاج عنواناً عربياً وآخر إنجليزياً.");
                return RedirectToAction(nameof(Resources), new { id });
            }

            if (string.IsNullOrWhiteSpace(typeCode) || !ResourceTypes.Any(t => t.Code == typeCode))
            {
                TempData["Error"] = T("Choose what kind of material this is.", "اختر نوع هذه المادة.");
                return RedirectToAction(nameof(Resources), new { id });
            }

            try
            {
                await EnsureResourceTypesAsync();

                // The file is stored first: doc 10 owns the bytes, the typing, the
                // size limit and the scan, and the LessonResource is only the link.
                var attachmentId = await _attachments.SaveAsync(
                    file, typeCode, nameof(Lesson), id, titleAr.Trim(), titleEn.Trim(),
                    cancellationToken: HttpContext.RequestAborted);

                await _lessons.AttachResourceAsync(
                    id, attachmentId, titleAr.Trim(), titleEn.Trim(), displayOrder,
                    cancellationToken: HttpContext.RequestAborted);

                TempData["Flash"] = T("Material added. It is served once the virus scan clears it.", "أُضيفت المادة، وتُتاح بعد اجتياز الفحص.");
            }
            catch (FileRejectedException ex)
            {
                TempData["Error"] = Labels.FileRejection(ex.Rejection, IsArabic, ex.AllowedFormats, ex.MaxBytes);
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Resources), new { id });
        }

        [HttpPost("resources/{resourceId:int}/withdraw")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Resources, ActionVerb.Deactivate)]
        public async Task<IActionResult> WithdrawResource(int resourceId, int id)
        {
            try
            {
                await _lessons.WithdrawResourceAsync(resourceId, cancellationToken: HttpContext.RequestAborted);
                TempData["Flash"] = T("Material withdrawn.", "سُحبت المادة.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Resources), new { id });
        }

        /// <summary>
        /// doc/Modules/37 §8.2. BR-LRN-006 / BR-ATT-009: the scan gate is a
        /// serving concern. An unscanned or infected file is refused here, in the
        /// reader's language, rather than handed over and explained afterwards.
        /// <para>
        /// The same refusal covers a file the store can no longer produce, which
        /// is the identical condition the portal already handles
        /// (<see cref="PortalController.LessonFile"/>): the teacher and the
        /// parent meet one broken file and get one translated sentence, not an
        /// unhandled exception page on the staff side and a message on theirs.
        /// </para>
        /// </summary>
        [HttpGet("resources/{resourceId:int}/file")]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Resources, ActionVerb.View)]
        public async Task<IActionResult> DownloadResource(int resourceId)
        {
            var resource = await _db.LessonResources.AsNoTracking()
                .SingleOrDefaultAsync(r => r.Id == resourceId, HttpContext.RequestAborted);
            if (resource == null) { return NotFound(); }

            var lessonId = await _db.Lessons.AsNoTracking()
                .Where(l => l.Id == resource.LessonId).Select(l => (int?)l.Id)
                .SingleOrDefaultAsync(HttpContext.RequestAborted);
            if (lessonId == null) { return NotFound(); }

            try
            {
                var stored = await _attachments.ReadAsync(resource.AttachmentId, HttpContext.RequestAborted);
                if (stored == null) { return NotFound(); }

                return File(stored.Content, stored.ContentType, stored.FileName);
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidOperationException)
            {
                // The row says there is a file and the store cannot produce it — the bytes are
                // gone, or the slot's current version row is. Rare, and a raw 500 is the wrong
                // answer to it here for the reason the portal already gives: an unhandled
                // exception page is neither translated nor anything the reader can act on.
                //
                // The sentence is written out rather than taken from UserMessage.For, which has
                // no case for either exception and would fall through to exception.Message —
                // English to an Arabic reader, and the storage path in the IOException's case.
                // A teacher is inside the school, so unlike the parent's wording this one names
                // the second cause and the way out of it.
                TempData["Error"] = T(
                    "That file could not be opened — either the school's file check has not cleared it yet, or its contents are no longer in the store. Upload the material again if it does not come back.",
                    "تعذّر فتح هذا الملف — إمّا أنّ فحص الملفات لدى المدرسة لم يكتمل بعد، أو أنّ محتواه لم يعد موجوداً في المخزن. أعد رفع المادة إن لم يعُد الملف.");

                // Two screens reach this action — the per-lesson library and the offering-wide
                // materials library — so the reader goes back to the one they were on, keeping
                // its filter, rather than being moved to a lesson page they never opened. The
                // referrer is honoured only when it is this host's own, and only as a path.
                var referrer = Request.Headers["Referer"].ToString();
                if (Uri.TryCreate(referrer, UriKind.Absolute, out var from)
                    && string.Equals(from.Authority, Request.Host.Value, StringComparison.OrdinalIgnoreCase)
                    && Url.IsLocalUrl(from.PathAndQuery))
                {
                    return LocalRedirect(from.PathAndQuery);
                }

                return RedirectToAction(nameof(Resources), new { id = lessonId.Value });
            }
        }

        // ---------------------------------------------------------------- building

        private async Task<LessonPlannerViewModel> BuildPlannerAsync(int? offeringId)
        {
            var reachable = await _lessons.ReachableOfferingIdsAsync(cancellationToken: HttpContext.RequestAborted);

            // The picker offers only what the user may act on (BR-SEC-010's
            // spirit): an option the guard would refuse is worse than no option.
            var offerings = await (
                from o in _db.CurriculumOfferings.AsNoTracking()
                join s in _db.Subjects.IgnoreQueryFilters().AsNoTracking() on o.SubjectId equals s.Id
                where reachable.Contains(o.Id) && s.SchoolId == _db.CurrentSchoolId
                select new { o.Id, s.Name.NameAr, s.Name.NameEn, o.GradeYearProfileId })
                .ToListAsync(HttpContext.RequestAborted);

            var options = offerings
                .Select(o => new OfferingOption(o.Id, IsArabic ? o.NameAr : o.NameEn))
                .OrderBy(o => o.Label, StringComparer.CurrentCulture)
                .ToList();

            int? selected = offeringId is int wanted && reachable.Contains(wanted) ? wanted : null;

            var weeks = new List<WeekGroup>();
            var sessions = new List<SessionOption>();

            if (selected is int chosen)
            {
                var lessons = await _db.Lessons.AsNoTracking()
                    .Where(l => l.CurriculumOfferingId == chosen)
                    .ToListAsync(HttpContext.RequestAborted);

                var lessonIds = lessons.Select(l => l.Id).ToList();

                var resourceCounts = await _db.LessonResources.AsNoTracking()
                    .Where(r => lessonIds.Contains(r.LessonId))
                    .GroupBy(r => r.LessonId)
                    .Select(g => new { g.Key, N = g.Count() })
                    .ToDictionaryAsync(x => x.Key, x => x.N, HttpContext.RequestAborted);

                var sessionDates = await _db.Sessions.AsNoTracking()
                    .Join(_db.Placements.AsNoTracking(), s => s.PlacementId, p => p.Id, (s, p) => new { s.Id, s.Date, p.CurriculumOfferingId })
                    .Where(x => x.CurriculumOfferingId == chosen)
                    .ToListAsync(HttpContext.RequestAborted);

                sessions = sessionDates
                    .OrderBy(s => s.Date)
                    .Select(s => new SessionOption(s.Id, s.Date, s.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)))
                    .ToList();

                var dateById = sessionDates.ToDictionary(s => s.Id, s => s.Date);

                weeks = lessons
                    .GroupBy(l => l.WeekNumber)
                    .OrderBy(g => g.Key)
                    .Select(g => new WeekGroup
                    {
                        WeekNumber = g.Key,
                        Lessons = g.OrderBy(l => l.Id).Select(l => new LessonRow(
                            l.Id,
                            l.WeekNumber,
                            IsArabic ? l.TitleAr : l.TitleEn,
                            IsArabic ? l.ObjectivesAr : l.ObjectivesEn,
                            l.TitleAr,
                            l.TitleEn,
                            l.ObjectivesAr,
                            l.ObjectivesEn,
                            l.Status,
                            l.PublishedAtUtc,
                            l.SessionId is int sid && dateById.ContainsKey(sid) ? dateById[sid] : null,
                            l.SessionId,
                            l.RetiredReason,
                            resourceCounts.ContainsKey(l.Id) ? resourceCounts[l.Id] : 0)).ToList(),
                    })
                    .ToList();
            }

            return new LessonPlannerViewModel
            {
                Offerings = options,
                SelectedOfferingId = selected,
                Weeks = weeks,
                Sessions = sessions,
                CanCreate = await CanAsync(ScreenCatalog.Learning.Planner, ActionVerb.Create),
                CanEdit = await CanAsync(ScreenCatalog.Learning.Planner, ActionVerb.Edit),
                CanPublish = await CanAsync(ScreenCatalog.Learning.Planner, ActionVerb.Approve),
                CanRetire = await CanAsync(ScreenCatalog.Learning.Planner, ActionVerb.Deactivate),
            };
        }

        /// <summary>
        /// The offering-wide read of §8.2. Shares the planner's reach rule
        /// (BR-LRN-002) rather than inventing a second one: material a teacher
        /// may not author is material they may not browse either.
        /// </summary>
        private async Task<MaterialsLibraryViewModel> BuildMaterialsAsync(int? offeringId, string? typeCode)
        {
            var reachable = await _lessons.ReachableOfferingIdsAsync(cancellationToken: HttpContext.RequestAborted);

            var offerings = await (
                from o in _db.CurriculumOfferings.AsNoTracking()
                join s in _db.Subjects.IgnoreQueryFilters().AsNoTracking() on o.SubjectId equals s.Id
                where reachable.Contains(o.Id) && s.SchoolId == _db.CurrentSchoolId
                select new { o.Id, s.Name.NameAr, s.Name.NameEn })
                .ToListAsync(HttpContext.RequestAborted);

            var options = offerings
                .Select(o => new OfferingOption(o.Id, IsArabic ? o.NameAr : o.NameEn))
                .OrderBy(o => o.Label, StringComparer.CurrentCulture)
                .ToList();

            var model = new MaterialsLibraryViewModel
            {
                Offerings = options,
                HasNoReach = options.Count == 0,
                SelectedTypeCode = ResourceTypes.Any(t => t.Code == typeCode) ? typeCode : null,
                CanUpload = await CanAsync(ScreenCatalog.Learning.Resources, ActionVerb.Create),
            };

            if (offeringId is not int wanted || !reachable.Contains(wanted))
            {
                model.Types = ResourceTypes
                    .Select(t => new MaterialTypeFilter(t.Code, IsArabic ? t.Ar : t.En, 0))
                    .ToList();
                return model;
            }

            model.SelectedOfferingId = wanted;

            var lessons = await _db.Lessons.AsNoTracking()
                .Where(l => l.CurriculumOfferingId == wanted)
                .Select(l => new { l.Id, l.WeekNumber, l.TitleAr, l.TitleEn, l.Status })
                .ToListAsync(HttpContext.RequestAborted);

            var lessonIds = lessons.Select(l => l.Id).ToList();
            if (lessonIds.Count == 0)
            {
                model.Types = ResourceTypes
                    .Select(t => new MaterialTypeFilter(t.Code, IsArabic ? t.Ar : t.En, 0))
                    .ToList();
                return model;
            }

            // Withdrawn material is excluded by LessonResource's own soft-active
            // filter, so IsActive is deliberately not restated.
            var resources = await _db.LessonResources.AsNoTracking()
                .Where(r => lessonIds.Contains(r.LessonId))
                .ToListAsync(HttpContext.RequestAborted);

            var attachmentIds = resources.Select(r => r.AttachmentId).Distinct().ToList();

            // Both of these are lookups, not pickers: retiring a document type
            // must not make the material already filed under it vanish from the
            // library that owns it (SoftActiveLookupTests' rule, applied by hand
            // because DocumentType is not one of the four it inspects).
            var attachments = await _db.Attachments.IgnoreQueryFilters().AsNoTracking()
                .Where(a => attachmentIds.Contains(a.Id) && a.SchoolId == _db.CurrentSchoolId)
                .Select(a => new { a.Id, a.DocumentTypeId, a.CurrentVersionNumber })
                .ToListAsync(HttpContext.RequestAborted);

            var types = await _db.DocumentTypes.IgnoreQueryFilters().AsNoTracking()
                .Where(t => t.SchoolId == _db.CurrentSchoolId)
                .Select(t => new { t.Id, t.Code, t.Name.NameAr, t.Name.NameEn })
                .ToListAsync(HttpContext.RequestAborted);

            var versions = await _db.AttachmentVersions.AsNoTracking()
                .Where(v => attachmentIds.Contains(v.AttachmentId))
                .Select(v => new { v.AttachmentId, v.VersionNumber, v.ScanStatus })
                .ToListAsync(HttpContext.RequestAborted);

            var lessonById = lessons.ToDictionary(l => l.Id);
            var attachmentById = attachments.ToDictionary(a => a.Id);
            var typeById = types.ToDictionary(t => t.Id);
            var typeIdByCode = types
                .GroupBy(t => t.Code)
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

            string? CodeOf(int attachmentId)
                => attachmentById.TryGetValue(attachmentId, out var a) && typeById.TryGetValue(a.DocumentTypeId, out var t)
                    ? t.Code
                    : null;

            ScanStatus? ScanOf(int attachmentId)
            {
                var current = attachmentById.TryGetValue(attachmentId, out var a) ? a.CurrentVersionNumber : (int?)null;
                return versions
                    .Where(v => v.AttachmentId == attachmentId && (current == null || v.VersionNumber == current))
                    .OrderByDescending(v => v.VersionNumber)
                    .FirstOrDefault()?.ScanStatus;
            }

            // Counted before the filter is applied: a chip reading zero is how a
            // teacher learns nothing of that kind is filed yet, which is the
            // answer they came for as much as the list itself.
            model.Types = ResourceTypes
                .Select(t => new MaterialTypeFilter(
                    t.Code,
                    IsArabic ? t.Ar : t.En,
                    resources.Count(r => string.Equals(CodeOf(r.AttachmentId), t.Code, StringComparison.OrdinalIgnoreCase))))
                .ToList();

            model.TotalInOffering = resources.Count;

            var filtered = model.SelectedTypeCode is string code
                ? resources.Where(r => typeIdByCode.TryGetValue(code, out var wantedTypeId)
                    && attachmentById.TryGetValue(r.AttachmentId, out var a)
                    && a.DocumentTypeId == wantedTypeId)
                : resources;

            model.Materials = filtered
                .Select(r =>
                {
                    var lesson = lessonById[r.LessonId];
                    var attachmentTypeId = attachmentById.TryGetValue(r.AttachmentId, out var a) ? a.DocumentTypeId : (int?)null;
                    var scan = ScanOf(r.AttachmentId);
                    return new MaterialRow(
                        r.Id,
                        r.LessonId,
                        lesson.WeekNumber,
                        IsArabic ? lesson.TitleAr : lesson.TitleEn,
                        lesson.Status,
                        IsArabic ? r.TitleAr : r.TitleEn,
                        attachmentTypeId is int tid && typeById.TryGetValue(tid, out var dt)
                            ? (IsArabic ? dt.NameAr : dt.NameEn)
                            : T("Unknown", "غير معروف"),
                        scan == ScanStatus.Clean,
                        LearningLabels.ScanStateName(scan, IsArabic),
                        r.DisplayOrder);
                })
                .OrderByDescending(m => m.WeekNumber)
                .ThenBy(m => m.LessonId)
                .ThenBy(m => m.DisplayOrder)
                .ThenBy(m => m.ResourceId)
                .ToList();

            return model;
        }

        private async Task<LessonResourcesViewModel?> BuildResourcesAsync(int lessonId)
        {
            var lesson = await _db.Lessons.AsNoTracking()
                .SingleOrDefaultAsync(l => l.Id == lessonId, HttpContext.RequestAborted);
            if (lesson == null) { return null; }

            var resources = await _db.LessonResources.AsNoTracking()
                .Where(r => r.LessonId == lessonId)
                .OrderBy(r => r.DisplayOrder).ThenBy(r => r.Id)
                .ToListAsync(HttpContext.RequestAborted);

            var attachmentIds = resources.Select(r => r.AttachmentId).ToList();

            // The document type is read past the soft-active filter: retiring a
            // type must not make the material already filed under it vanish from
            // the lesson that owns it.
            var attachments = await _db.Attachments.IgnoreQueryFilters().AsNoTracking()
                .Where(a => attachmentIds.Contains(a.Id) && a.SchoolId == _db.CurrentSchoolId)
                .Select(a => new { a.Id, a.DocumentTypeId, a.CurrentVersionNumber })
                .ToListAsync(HttpContext.RequestAborted);

            var typeById = await _db.DocumentTypes.IgnoreQueryFilters().AsNoTracking()
                .Where(t => t.SchoolId == _db.CurrentSchoolId)
                .Select(t => new { t.Id, t.Name.NameAr, t.Name.NameEn })
                .ToDictionaryAsync(t => t.Id, t => IsArabic ? t.NameAr : t.NameEn, HttpContext.RequestAborted);

            var scanByAttachment = await _db.AttachmentVersions.AsNoTracking()
                .Where(v => attachmentIds.Contains(v.AttachmentId))
                .Select(v => new { v.AttachmentId, v.VersionNumber, v.ScanStatus })
                .ToListAsync(HttpContext.RequestAborted);

            ScanStatus? ScanOf(int attachmentId)
            {
                var current = attachments.FirstOrDefault(a => a.Id == attachmentId)?.CurrentVersionNumber;
                var version = scanByAttachment
                    .Where(v => v.AttachmentId == attachmentId && (current == null || v.VersionNumber == current))
                    .OrderByDescending(v => v.VersionNumber)
                    .FirstOrDefault();
                return version?.ScanStatus;
            }

            var rows = resources.Select(r =>
            {
                var scan = ScanOf(r.AttachmentId);
                var typeId = attachments.FirstOrDefault(a => a.Id == r.AttachmentId)?.DocumentTypeId;
                return new ResourceRow(
                    r.Id,
                    IsArabic ? r.TitleAr : r.TitleEn,
                    typeId is int tid && typeById.ContainsKey(tid) ? typeById[tid] : T("Unknown", "غير معروف"),
                    r.AttachmentId,
                    r.DisplayOrder,
                    scan == ScanStatus.Clean,
                    LearningLabels.ScanStateName(scan, IsArabic));
            }).ToList();

            return new LessonResourcesViewModel
            {
                LessonId = lesson.Id,
                LessonTitle = IsArabic ? lesson.TitleAr : lesson.TitleEn,
                LessonStatus = lesson.Status,
                OfferingId = lesson.CurriculumOfferingId,
                Resources = rows,
                Types = ResourceTypes.Select(t => new ResourceTypeOption(t.Code, IsArabic ? t.Ar : t.En)).ToList(),
                CanUpload = await CanAsync(ScreenCatalog.Learning.Resources, ActionVerb.Create),
                CanWithdraw = await CanAsync(ScreenCatalog.Learning.Resources, ActionVerb.Deactivate),
            };
        }

        private async Task EnsureResourceTypesAsync()
        {
            foreach (var (code, ar, en) in ResourceTypes)
            {
                await _attachments.EnsureTypeAsync(
                    code, ScreenCatalog.Modules.Learning, ar, en, ResourceFormats, ResourceMaxBytes,
                    HttpContext.RequestAborted);
            }
        }

        private Task<bool> CanAsync(string screenCode, ActionVerb verb)
            => _permissions.HasPermissionAsync(ScreenCatalog.Modules.Learning, screenCode, verb, HttpContext.RequestAborted);
    }
}
