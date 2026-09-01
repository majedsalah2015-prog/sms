using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Common.Interfaces;
using Sms.Application.Schools;
using Sms.Domain.Schools;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/02 §8: School profile (§8.1, tabbed — P-DETAIL), Signatories
    /// (§8.2), Status console (§8.3). §8.4's group tree is marked (Future) in the
    /// doc itself and BR-SCH-007 shows the group only once more than one school
    /// exists, so it is deliberately absent.
    /// <para>
    /// All writes go through <see cref="ISchoolAdmin"/>; identity and status edits
    /// set the ambient audit reason and BR-SCH-002 / doc 02 §4 are enforced by the
    /// <c>[RequiresAuditReason]</c> tags on <see cref="School"/>, not by this class.
    /// What this class adds is telling the operator <em>which</em> fields are about
    /// to demand a reason before the save is refused, rather than after — the
    /// entity is T1 as a whole but only five of its columns bear the attribute, a
    /// distinction the old single reason box at the foot of the form could not
    /// express.
    /// </para>
    /// <para>
    /// Server-side format checks for email / phone / website land here rather than
    /// in the engine, which performs none (doc 02 §9 asks for them, BR-GLB-110 says
    /// validation is server-enforced). The Web boundary is the server, so the rule
    /// is met — but the right long-term home is <see cref="ISchoolAdmin"/>, and a
    /// second caller of the port (an import, the seeder) would bypass these. Stated
    /// rather than hidden.
    /// </para>
    /// <para>
    /// Still deferred, each blocked on something outside this module: logo and seal
    /// upload with the document preview (BR-SCH-006 — needs the attachment screens
    /// of doc 10 and a template render surface), the map pin behind latitude and
    /// longitude (BR-SCH-008 — no map provider is chosen anywhere in docs/, and
    /// <see cref="ISchoolAdmin.DefineSchoolAsync"/> carries no coordinate
    /// parameters, so the boxes stay read-only), and BR-SCH-003's explicit
    /// declaration of stages offered (no SchoolStage entity exists; today every
    /// stage defined in Module 05 counts as offered).
    /// </para>
    /// </summary>
    [Route("school")]
    public class SchoolController : Controller
    {
        /// <summary>Which profile tab a field lives on, so an error can open the tab that holds it.</summary>
        private static readonly IReadOnlyDictionary<string, string> FieldTabs = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(SchoolProfileViewModel.NameAr)] = "identity",
            [nameof(SchoolProfileViewModel.NameEn)] = "identity",
            [nameof(SchoolProfileViewModel.LicenseNumber)] = "licence",
            [nameof(SchoolProfileViewModel.MinistryCode)] = "licence",
            [nameof(SchoolProfileViewModel.LicenseExpiryDate)] = "licence",
            [nameof(SchoolProfileViewModel.City)] = "contacts",
            [nameof(SchoolProfileViewModel.AddressLine)] = "contacts",
            [nameof(SchoolProfileViewModel.ContactEmail)] = "contacts",
            [nameof(SchoolProfileViewModel.ContactPhone)] = "contacts",
            [nameof(SchoolProfileViewModel.Website)] = "contacts",
        };

        private static readonly string[] ProfileTabs = { "identity", "licence", "contacts", "branding", "stages", "history" };

        /// <summary>Permissive on purpose — a phone book holds shapes a regex author never imagined; this only catches text that is not a number at all.</summary>
        private static readonly Regex PhoneShape = new(@"^\+?[0-9][0-9\s\-().]{5,24}$", RegexOptions.Compiled);

        private readonly ISchoolAdmin _schools;
        private readonly AppDbContext _db;
        private readonly ITenantContext _tenant;
        private readonly IAuditContext _audit;
        private readonly IClock _clock;
        private readonly Sms.Web.Services.SchoolBrandingService _branding;

        public SchoolController(
            ISchoolAdmin schools, AppDbContext db, ITenantContext tenant, IAuditContext audit, IClock clock,
            Sms.Web.Services.SchoolBrandingService branding)
        {
            _schools = schools;
            _db = db;
            _tenant = tenant;
            _audit = audit;
            _clock = clock;
            _branding = branding;
        }

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        // ==================================================================
        // §8.1 School profile
        // ==================================================================

        [HttpGet("")]
        [RequirePermission(ScreenCatalog.Modules.Schools, ScreenCatalog.Schools.Profile, ActionVerb.View)]
        public async Task<IActionResult> Profile(string? tab = null)
        {
            var model = await BuildProfileAsync();
            model.ActiveTab = ProfileTabs.Contains(tab, StringComparer.OrdinalIgnoreCase)
                ? tab!.ToLowerInvariant()
                : "identity";
            return View(model);
        }

        [HttpPost("")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Schools, ScreenCatalog.Schools.Profile, ActionVerb.Edit)]
        public async Task<IActionResult> Profile(SchoolProfileViewModel form)
        {
            // One school per tenant (ADR-2): always bind to the tenant's row, never trust a posted id.
            var existing = await _db.Schools.AsNoTracking().SingleOrDefaultAsync(s => s.Id == _tenant.SchoolId);

            Normalize(form);
            ValidateProfile(form, existing);

            if (!ModelState.IsValid)
            {
                form.ActiveTab = TabOfFirstError(form.ActiveTab);
                return View(await BuildProfileAsync(form));
            }

            try
            {
                _audit.Reason = string.IsNullOrWhiteSpace(form.Reason) ? null : form.Reason;
                await _schools.DefineSchoolAsync(
                    existing?.Id, form.NameAr!, form.NameEn!, form.LicenseNumber!, form.MinistryCode!,
                    form.TimeZoneId ?? existing?.TimeZoneId ?? "Arab Standard Time",
                    form.CurrencyCode ?? existing?.CurrencyCode ?? "SAR",
                    form.AddressLine, form.City, form.ContactEmail, form.ContactPhone, form.Website, form.LicenseExpiryDate);
                TempData["Flash"] = existing == null
                    ? T("School profile created.", "تم إنشاء ملف المدرسة.")
                    : T("School profile saved.", "تم حفظ ملف المدرسة.");
                return RedirectToAction(nameof(Profile), new { tab = form.ActiveTab });
            }
            catch (InvalidOperationException ex)
            {
                // Whatever the engine or the audit pipeline refused, the reader meets it in their
                // own language — never the exception's own English sentence (TranslatedRefusalTests).
                ModelState.AddModelError(string.Empty, UserMessage.For(ex, IsArabic));
                form.ActiveTab = TabOfFirstError(form.ActiveTab);
                return View(await BuildProfileAsync(form));
            }
        }

        // ==================================================================
        // §8.1 Branding — BR-SCH-006
        // ==================================================================
        //
        // The logo and the seal are attachments (doc 10), not columns of bytes: same intake, same
        // content inspection, same scan gate, same versioning as any other filed document. The
        // school row keeps a pointer, so replacing a mark is a new version of one slot — which is
        // what will let a certificate issued last year still name the branding it was issued under
        // once a template renderer exists to ask.
        //
        // What BR-SCH-006 also asks for and this build cannot give: "every official template
        // references current branding at render time". There is no template renderer in this
        // product yet, so nothing renders the mark onto a document. The upload, the slot and the
        // pointer are here; the surface that consumes them is not, and the tab says so.

        /// <summary>
        /// Serves a branding mark. An action rather than a data URI in the page: a logo is exactly
        /// the sort of thing a browser should cache for itself, and inlining it would put the image
        /// into every profile response whether or not the Branding tab was the one being read.
        /// </summary>
        [HttpGet("branding/{asset}")]
        [RequirePermission(ScreenCatalog.Modules.Schools, ScreenCatalog.Schools.Profile, ActionVerb.View)]
        public async Task<IActionResult> Branding(SchoolBrandingAsset asset)
        {
            var school = await _db.Schools.AsNoTracking().SingleOrDefaultAsync(s => s.Id == _tenant.SchoolId);
            if (school == null) { return NotFound(); }

            var attachmentId = asset == SchoolBrandingAsset.Logo ? school.LogoAttachmentId : school.SealAttachmentId;
            var file = await _branding.ReadAsync(attachmentId, HttpContext.RequestAborted);
            if (file == null) { return NotFound(); }

            return File(file.Content, file.ContentType);
        }

        [HttpPost("branding/{asset}")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Schools, ScreenCatalog.Schools.Profile, ActionVerb.Edit)]
        public async Task<IActionResult> UploadBranding(SchoolBrandingAsset asset, IFormFile? file)
        {
            // One school per tenant (ADR-2) — the slot belongs to the tenant's row, never to a
            // posted id. A school that does not exist yet has no branding to carry.
            var school = await _db.Schools.AsNoTracking().SingleOrDefaultAsync(s => s.Id == _tenant.SchoolId);
            if (school == null)
            {
                TempData["Error"] = T(
                    "Save the school's identity first — there is no record yet for a logo to belong to.",
                    "احفظ هوية المدرسة أولاً — لا يوجد سجل بعد ينتمي إليه الشعار.");
                return RedirectToAction(nameof(Profile), new { tab = "branding" });
            }

            try
            {
                var attachmentId = await _branding.SaveAsync(
                    file!, asset, school.Id, ScreenCatalog.Modules.Schools, HttpContext.RequestAborted);
                await _schools.SetBrandingAsync(school.Id, asset, attachmentId, HttpContext.RequestAborted);

                TempData["Flash"] = asset == SchoolBrandingAsset.Logo
                    ? T("Logo updated.", "تم تحديث الشعار.")
                    : T("Seal updated.", "تم تحديث الختم.");
            }
            // The policy exception is an InvalidOperationException, so it has to be caught first or
            // its own message — which names a rule rather than a fact — is what reaches the screen.
            catch (Sms.Application.Common.Exceptions.AttachmentPolicyViolationException)
            {
                TempData["Error"] = asset == SchoolBrandingAsset.Logo
                    ? T("That file is not an acceptable logo.", "هذا الملف ليس شعاراً مقبولاً.")
                    : T("That file is not an acceptable seal.", "هذا الملف ليس ختماً مقبولاً.");
            }
            // Also an InvalidOperationException, and it carries a reason rather than a sentence —
            // the wording is chosen here, in the reader's language, never thrown from the service.
            catch (Sms.Web.Services.FileRejectedException ex)
            {
                TempData["Error"] = Labels.FileRejection(ex.Rejection, IsArabic, ex.AllowedFormats, ex.MaxBytes);
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }

            return RedirectToAction(nameof(Profile), new { tab = "branding" });
        }

        [HttpPost("branding/{asset}/remove")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Schools, ScreenCatalog.Schools.Profile, ActionVerb.Edit)]
        public async Task<IActionResult> RemoveBranding(SchoolBrandingAsset asset)
        {
            var school = await _db.Schools.AsNoTracking().SingleOrDefaultAsync(s => s.Id == _tenant.SchoolId);
            if (school == null) { return NotFound(); }

            // The pointer is cleared; the attachment itself stays, because doc 10 does not delete
            // files while the record that owned them exists (BR-ATT-007). There is no Delete verb
            // in this product (BR-GLB-005), and a mark that once headed a document is evidence.
            await _schools.SetBrandingAsync(school.Id, asset, null, HttpContext.RequestAborted);

            TempData["Flash"] = asset == SchoolBrandingAsset.Logo
                ? T("Logo removed.", "تمت إزالة الشعار.")
                : T("Seal removed.", "تمت إزالة الختم.");
            return RedirectToAction(nameof(Profile), new { tab = "branding" });
        }

        /// <summary>Trims what the operator typed, so a trailing space does not read as a changed identity field.</summary>
        private static void Normalize(SchoolProfileViewModel form)
        {
            form.NameAr = Clean(form.NameAr);
            form.NameEn = Clean(form.NameEn);
            form.LicenseNumber = Clean(form.LicenseNumber);
            form.MinistryCode = Clean(form.MinistryCode);
            form.AddressLine = Clean(form.AddressLine);
            form.City = Clean(form.City);
            form.ContactEmail = Clean(form.ContactEmail);
            form.ContactPhone = Clean(form.ContactPhone);
            form.Website = Clean(form.Website);
            form.Reason = Clean(form.Reason);
        }

        private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        /// <summary>
        /// doc 02 §9 and BR-SCH-001. Errors are attached to the field that caused them so
        /// they render inline as well as in the summary (docs/UI/02 §2), and the reason
        /// check names the fields that triggered it instead of demanding one for every save.
        /// </summary>
        private void ValidateProfile(SchoolProfileViewModel form, School? existing)
        {
            RequireField(nameof(form.NameAr), form.NameAr, T("The official Arabic name", "الاسم الرسمي بالعربية"));
            RequireField(nameof(form.NameEn), form.NameEn, T("The official English name", "الاسم الرسمي بالإنجليزية"));
            RequireField(nameof(form.LicenseNumber), form.LicenseNumber, T("The licence number", "رقم الترخيص"));
            RequireField(nameof(form.MinistryCode), form.MinistryCode, T("The ministry code", "الرمز الوزاري"));

            if (form.ContactEmail != null && !new EmailAddressAttribute().IsValid(form.ContactEmail))
            {
                ModelState.AddModelError(nameof(form.ContactEmail), T(
                    "This is not an email address the system can send from — it needs the form name@domain (BR-SCH-008).",
                    "هذا ليس بريداً يستطيع النظام الإرسال منه — الصيغة المطلوبة name@domain (BR-SCH-008)."));
            }

            if (form.ContactPhone != null && !PhoneShape.IsMatch(form.ContactPhone))
            {
                ModelState.AddModelError(nameof(form.ContactPhone), T(
                    "Only digits, spaces and + - ( ) belong in a phone number, and it needs at least six digits (BR-SCH-008).",
                    "رقم الهاتف يقبل الأرقام والمسافات و + - ( ) فقط، وبستة أرقام على الأقل (BR-SCH-008)."));
            }

            if (form.Website != null && !LooksLikeUrl(form.Website))
            {
                ModelState.AddModelError(nameof(form.Website), T(
                    "This is not a web address — expected something like www.school.edu.sa or https://school.edu.sa.",
                    "هذا ليس عنواناً إلكترونياً — المتوقع مثل www.school.edu.sa أو https://school.edu.sa."));
            }

            // BR-SCH-002: only a *change* to one of the reason-bearing columns needs a reason, and
            // only on an existing row — [RequiresAuditReason] fires on Modified, never on Added.
            // So creating the profile asks for nothing and correcting a name asks for everything.
            var changed = ChangedReasonBearingFields(form, existing);
            if (changed.Count > 0 && string.IsNullOrWhiteSpace(form.Reason))
            {
                var names = string.Join(T(", ", "، "), changed.Select(f => SchoolFieldLabels.Name(f, IsArabic)));
                ModelState.AddModelError(nameof(form.Reason), T(
                    $"You changed {names}. These fields print on official documents and are audited at tier 1, so the change is not accepted without a reason (BR-SCH-002). Write what changed and why.",
                    $"غيّرت {names}. هذه الحقول تُطبع على المستندات الرسمية ومدقَّقة بالمستوى الأول، فلا يُقبل التغيير بلا سبب (BR-SCH-002). اكتب ما تغيّر ولماذا."));
            }
        }

        private void RequireField(string key, string? value, string subject)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                ModelState.AddModelError(key, T(
                    $"{subject} is required before the school can be activated, and it is what official documents print (BR-SCH-001).",
                    $"{subject} مطلوب قبل تفعيل المدرسة، وهو ما تطبعه المستندات الرسمية (BR-SCH-001)."));
            }
        }

        /// <summary>The reason-bearing columns whose value the post would actually change.</summary>
        private static IReadOnlyList<string> ChangedReasonBearingFields(SchoolProfileViewModel form, School? existing)
        {
            if (existing == null)
            {
                return Array.Empty<string>();
            }

            var changed = new List<string>();
            if (!string.Equals(existing.NameAr, form.NameAr, StringComparison.Ordinal)) { changed.Add(nameof(School.NameAr)); }
            if (!string.Equals(existing.NameEn, form.NameEn, StringComparison.Ordinal)) { changed.Add(nameof(School.NameEn)); }
            if (!string.Equals(existing.LicenseNumber, form.LicenseNumber, StringComparison.Ordinal)) { changed.Add(nameof(School.LicenseNumber)); }
            if (!string.Equals(existing.MinistryCode, form.MinistryCode, StringComparison.Ordinal)) { changed.Add(nameof(School.MinistryCode)); }
            return changed;
        }

        /// <summary>Accepts a bare host as typed; only refuses text that is not an address at all.</summary>
        private static bool LooksLikeUrl(string value)
        {
            var candidate = value.Contains("://", StringComparison.Ordinal) ? value : "https://" + value;
            return Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                && uri.Host.Contains('.', StringComparison.Ordinal)
                && !uri.Host.EndsWith(".", StringComparison.Ordinal);
        }

        /// <summary>Opens the tab holding the first refused field, so an error is never left behind a tab the reader cannot see.</summary>
        private string TabOfFirstError(string fallback)
        {
            foreach (var pair in FieldTabs)
            {
                if (ModelState.TryGetValue(pair.Key, out var entry) && entry.Errors.Count > 0)
                {
                    return pair.Value;
                }
            }

            return ProfileTabs.Contains(fallback, StringComparer.OrdinalIgnoreCase) ? fallback.ToLowerInvariant() : "identity";
        }

        private async Task<SchoolProfileViewModel> BuildProfileAsync(SchoolProfileViewModel? form = null)
        {
            var school = await _db.Schools.AsNoTracking().SingleOrDefaultAsync(s => s.Id == _tenant.SchoolId);
            var m = form ?? new SchoolProfileViewModel();
            if (form == null && school != null)
            {
                m.SchoolId = school.Id; m.NameAr = school.NameAr; m.NameEn = school.NameEn; m.LicenseNumber = school.LicenseNumber; m.MinistryCode = school.MinistryCode;
                m.LicenseExpiryDate = school.LicenseExpiryDate; m.AddressLine = school.AddressLine; m.City = school.City; m.Latitude = school.Latitude; m.Longitude = school.Longitude;
                m.ContactEmail = school.ContactEmail; m.ContactPhone = school.ContactPhone; m.Website = school.Website; m.TimeZoneId = school.TimeZoneId; m.CurrencyCode = school.CurrencyCode;
            }

            m.SchoolId ??= school?.Id;
            m.TodayUtc = _clock.UtcNow.Date;
            m.Status = school?.Status;
            m.TimeZoneId ??= school?.TimeZoneId;
            m.CurrencyCode ??= school?.CurrencyCode;
            m.Latitude ??= school?.Latitude;
            m.Longitude ??= school?.Longitude;
            m.CreatedAtUtc = school?.CreatedAtUtc;
            m.ModifiedAtUtc = school?.ModifiedAtUtc;
            m.ModifiedByUserId = school?.ModifiedByUserId;
            m.SetupCompletedAtUtc = school?.SetupCompletedAtUtc;

            if (school?.CountryPackId is int packId)
            {
                var pack = await _db.CountryPacks.AsNoTracking().FirstOrDefaultAsync(p => p.Id == packId);
                m.CountryPackCode = pack?.Code;
                m.CountryPackNameAr = pack?.Name.NameAr;
                m.CountryPackNameEn = pack?.Name.NameEn;
            }

            // BR-SCH-006. The marks are described here and served from their own actions: putting
            // the bytes in the model would inline a logo into every profile response, including the
            // five tabs nobody opened.
            var logo = await BrandingSlotAsync(school?.LogoAttachmentId);
            m.HasLogo = logo != null;
            m.LogoFileName = logo?.FileName;
            m.LogoSizeBytes = logo?.SizeBytes ?? 0;
            m.LogoVersion = logo?.VersionNumber ?? 0;

            var seal = await BrandingSlotAsync(school?.SealAttachmentId);
            m.HasSeal = seal != null;
            m.SealFileName = seal?.FileName;
            m.SealSizeBytes = seal?.SizeBytes ?? 0;
            m.SealVersion = seal?.VersionNumber ?? 0;

            // The lookup, not the picker: a retired stage still has grades pointing at it, and a
            // profile that silently drops it reads as "we do not teach that any more" when the
            // enrollments say otherwise. Filters off means tenant scoping goes too — hence the
            // explicit SchoolId predicate.
            var stages = await _db.Stages.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.SchoolId == _tenant.SchoolId).OrderBy(s => s.SequenceOrder).ToListAsync();
            var grades = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking()
                .Where(g => g.SchoolId == _tenant.SchoolId).ToListAsync();
            m.StagesOffered = stages
                .Select(s => new SchoolProfileViewModel.StageRow(s.Name.NameAr, s.Name.NameEn, grades.Count(g => g.StageId == s.Id), s.IsActive))
                .ToList();

            var covered = await _db.Signatories.AsNoTracking()
                .Where(s => s.EffectiveToUtc == null)
                .Select(s => s.DocumentClassCode)
                .ToListAsync();
            m.DocumentClassesWithoutSignatory = SignatoriesViewModel.DocumentClasses
                .Where(c => !covered.Contains(c.Code))
                .Select(c => c.Code)
                .ToList();

            m.Checklist = BuildChecklist(school, m.StagesOffered, m.DocumentClassesWithoutSignatory.Count);
            m.History = await ReadHistoryAsync(school, field: null);
            if (m.History.Count > 0)
            {
                m.ModifiedByUserName = m.History[0].ActorName;
            }

            return m;
        }

        /// <summary>
        /// What a branding slot currently holds, or null when it holds nothing a reader could be
        /// shown — no pointer, no version, or a file the scan gate is holding back (BR-ATT-009).
        /// The status is read here rather than by fetching the bytes, so describing a logo does not
        /// cost reading one.
        /// </summary>
        private async Task<BrandingSlot?> BrandingSlotAsync(int? attachmentId)
        {
            if (attachmentId is not int id) { return null; }

            return await (
                from a in _db.Attachments.AsNoTracking()
                where a.Id == id && a.Status == Sms.Domain.Attachments.AttachmentStatus.Active
                join v in _db.AttachmentVersions.AsNoTracking() on a.Id equals v.AttachmentId
                where v.VersionNumber == a.CurrentVersionNumber
                    && v.ScanStatus == Sms.Domain.Attachments.ScanStatus.Clean
                select new BrandingSlot(v.FileName, v.SizeBytes, v.VersionNumber))
                .SingleOrDefaultAsync();
        }

        private sealed record BrandingSlot(string FileName, long SizeBytes, int VersionNumber);

        /// <summary>
        /// What is still blank and why it matters. BR-SCH-001's four are marked required
        /// because the school cannot be activated without them; everything else is advice
        /// with the consequence attached, so "incomplete" never means "unusable".
        /// </summary>
        private static IReadOnlyList<SchoolChecklistRow> BuildChecklist(
            School? school, IReadOnlyList<SchoolProfileViewModel.StageRow> stages, int classesWithoutSignatory)
        {
            bool Has(string? v) => !string.IsNullOrWhiteSpace(v);

            return new List<SchoolChecklistRow>
            {
                new("Official name (Arabic)", "الاسم الرسمي (عربي)",
                    "Printed on every Arabic certificate and letter, exactly as typed.",
                    "يُطبع على كل شهادة وخطاب بالعربية كما كُتب تماماً.",
                    Has(school?.NameAr), true, "identity"),
                new("Official name (English)", "الاسم الرسمي (إنجليزي)",
                    "Printed on English transcripts and on anything a partner abroad receives.",
                    "يُطبع على كشوف الدرجات الإنجليزية وعلى ما يصل الجهات الخارجية.",
                    Has(school?.NameEn), true, "identity"),
                new("Licence number", "رقم الترخيص",
                    "Quoted on official documents and in ministry exports; without it the school cannot be activated.",
                    "يُقتبس في المستندات الرسمية والتصدير الوزاري؛ ولا تُفعَّل المدرسة بدونه.",
                    Has(school?.LicenseNumber), true, "licence"),
                new("Ministry code", "الرمز الوزاري",
                    "The identifier the ministry matches this school by in statutory returns.",
                    "المعرّف الذي تطابق به الوزارة هذه المدرسة في التقارير النظامية.",
                    Has(school?.MinistryCode), true, "licence"),
                new("Licence expiry date", "تاريخ انتهاء الترخيص",
                    "Nothing stops when it lapses, but a stale date is a real reporting error and there is no other warning.",
                    "لا شيء يتوقف عند انتهائه، لكن التاريخ القديم خطأ تقريري حقيقي ولا تحذير غيره.",
                    school?.LicenseExpiryDate != null, false, "licence"),
                new("Official email", "البريد الرسمي",
                    "The sender identity notifications will go out under (doc 09); without it messages have no return address.",
                    "هوية المُرسِل التي تخرج بها الإشعارات (الوثيقة 09)؛ وبدونها لا عنوان للرد على الرسائل.",
                    Has(school?.ContactEmail), false, "contacts"),
                new("Phone", "الهاتف",
                    "Printed on documents and shown to parents as the school's own number.",
                    "يُطبع على المستندات ويظهر لأولياء الأمور بوصفه رقم المدرسة.",
                    Has(school?.ContactPhone), false, "contacts"),
                new("Address and city", "العنوان والمدينة",
                    "Appears on letters and on anything posted; also what a courier is given.",
                    "يظهر على الخطابات وعلى ما يُرسَل بالبريد، وهو ما يُعطى لمندوب التوصيل.",
                    Has(school?.AddressLine) && Has(school?.City), false, "contacts"),
                new("Stage structure", "الهيكل الدراسي",
                    "A grade cannot exist outside a stage (BR-SCH-003) — with none defined, nothing can be enrolled.",
                    "لا يوجد صف خارج مرحلة (BR-SCH-003) — وبلا مراحل لا يمكن قيد أحد.",
                    stages.Count > 0, false, "stages"),
                new("A signatory for every document class", "موقّع لكل فئة مستند",
                    "A class with nobody in force prints its documents with an empty signature block (BR-SCH-004).",
                    "الفئة التي لا موقّع لها تُطبع مستنداتها بكتلة توقيع فارغة (BR-SCH-004).",
                    classesWithoutSignatory == 0, false, null, "School", nameof(Signatories)),
                new("Setup declared complete", "إعلان اكتمال الإعداد",
                    "The first academic year cannot be activated until the wizard is declared complete (BR-SET-003).",
                    "لا يُفعَّل أول عام دراسي قبل إعلان اكتمال المعالج (BR-SET-003).",
                    school?.SetupCompletedAtUtc != null, false, null, "Setup", "Index"),
            };
        }

        /// <summary>
        /// BR-AUD-008's one-click history for the school record, and BR-GLB-007's
        /// created/modified line behind it. Values are read back raw and localized at
        /// display (BR-AUD-005); the actor is resolved past the filters because the
        /// person who made a change in 2026 may well have left by the time it is read.
        /// </summary>
        private async Task<IReadOnlyList<SchoolChangeRow>> ReadHistoryAsync(School? school, string? field)
        {
            if (school == null)
            {
                return Array.Empty<SchoolChangeRow>();
            }

            long entityId = school.Id;
            var query = _db.AuditEntries.AsNoTracking()
                .Where(e => e.EntityType == nameof(School) && e.EntityId == entityId);
            if (field != null)
            {
                query = query.Where(e => e.FieldName == field);
            }

            var entries = await query.OrderByDescending(e => e.OccurredAtUtc).ThenByDescending(e => e.Id).Take(60).ToListAsync();
            var actorIds = entries.Select(e => e.ActorUserId).Distinct().ToList();
            var actors = await _db.UserAccounts.IgnoreQueryFilters().AsNoTracking()
                .Where(u => actorIds.Contains(u.Id))
                .Select(u => new { u.Id, u.UserName })
                .ToListAsync();

            return entries
                .Select(e => new SchoolChangeRow(
                    e.OccurredAtUtc,
                    e.Action,
                    e.FieldName,
                    e.OldValue,
                    e.NewValue,
                    e.Reason,
                    e.ActorUserId,
                    actors.FirstOrDefault(a => a.Id == e.ActorUserId)?.UserName))
                .ToList();
        }

        // ==================================================================
        // §8.2 Signatories
        // ==================================================================

        [HttpGet("signatories")]
        [RequirePermission(ScreenCatalog.Modules.Schools, ScreenCatalog.Schools.Signatories, ActionVerb.View)]
        public async Task<IActionResult> Signatories(string? cls = null)
        {
            var model = await BuildSignatoriesAsync();
            // "Replace the signatory for this class" arrives as a query string rather than a
            // second action, so the form is prefilled without inventing a screen the catalogue
            // does not know about.
            if (SignatoriesViewModel.DocumentClasses.Any(c => string.Equals(c.Code, cls, StringComparison.OrdinalIgnoreCase)))
            {
                model.DocumentClassCode = cls!.ToUpperInvariant();
            }

            return View(model);
        }

        [HttpPost("signatories")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Schools, ScreenCatalog.Schools.Signatories, ActionVerb.Edit)]
        public async Task<IActionResult> Signatories(SignatoriesViewModel form)
        {
            form.NameAr = Clean(form.NameAr); form.NameEn = Clean(form.NameEn);
            form.TitleAr = Clean(form.TitleAr); form.TitleEn = Clean(form.TitleEn);

            if (!SignatoriesViewModel.DocumentClasses.Any(c => c.Code == form.DocumentClassCode))
            {
                ModelState.AddModelError(nameof(form.DocumentClassCode), T(
                    "Choose the class of document this person signs.", "اختر فئة المستندات التي يوقّعها هذا الشخص."));
            }

            RequireSignatoryField(nameof(form.NameAr), form.NameAr, T("The name in Arabic", "الاسم بالعربية"));
            RequireSignatoryField(nameof(form.NameEn), form.NameEn, T("The name in English", "الاسم بالإنجليزية"));
            RequireSignatoryField(nameof(form.TitleAr), form.TitleAr, T("The title in Arabic", "المسمى بالعربية"));
            RequireSignatoryField(nameof(form.TitleEn), form.TitleEn, T("The title in English", "المسمى بالإنجليزية"));

            var from = (form.EffectiveFrom ?? _clock.UtcNow.Date).Date;
            var current = form.DocumentClassCode == null
                ? null
                : await _db.Signatories.AsNoTracking()
                    .SingleOrDefaultAsync(s => s.DocumentClassCode == form.DocumentClassCode && s.EffectiveToUtc == null);

            // Saving closes the current signatory *on the new one's start date* (BR-SCH-004). Backdate
            // past the current one's own start and that closing date lands before its opening date —
            // a period that ends before it begins, which no reissued document could ever resolve.
            if (current != null && from < current.EffectiveFromUtc.Date)
            {
                var limit = current.EffectiveFromUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                ModelState.AddModelError(nameof(form.EffectiveFrom), T(
                    $"The signatory in force for this class started on {limit}. Saving closes that record on the date you give here, so an earlier date would end it before it began — choose {limit} or later.",
                    $"الموقّع الساري لهذه الفئة بدأ في {limit}. والحفظ يُغلق سجله بالتاريخ المكتوب هنا، فالتاريخ الأسبق ينهيه قبل أن يبدأ — اختر {limit} أو بعده."));
            }

            if (!ModelState.IsValid)
            {
                return View(await RestoreSignatoryFormAsync(form));
            }

            try
            {
                await _schools.DefineSignatoryAsync(
                    form.DocumentClassCode!, form.NameAr!, form.NameEn!, form.TitleAr!, form.TitleEn!,
                    DateTime.SpecifyKind(from, DateTimeKind.Utc));
                var className = SignatoriesViewModel.ClassName(form.DocumentClassCode, IsArabic);
                TempData["Flash"] = current == null
                    ? T($"{form.NameEn} now signs {className}.", $"{form.NameAr} يوقّع الآن {className}.")
                    : T($"{form.NameEn} now signs {className}; {current.NameEn} was closed out on the same date (BR-SCH-004).",
                        $"{form.NameAr} يوقّع الآن {className}، وأُغلق سجل {current.NameAr} بالتاريخ نفسه (BR-SCH-004).");
                return RedirectToAction(nameof(Signatories));
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, UserMessage.For(ex, IsArabic));
                return View(await RestoreSignatoryFormAsync(form));
            }
        }

        private void RequireSignatoryField(string key, string? value, string subject)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                ModelState.AddModelError(key, T(
                    $"{subject} is required — both languages are printed, each on its own document (BR-GLB-001).",
                    $"{subject} مطلوب — فاللغتان تُطبعان، كل واحدة على مستندها (BR-GLB-001)."));
            }
        }

        private async Task<SignatoriesViewModel> RestoreSignatoryFormAsync(SignatoriesViewModel form)
        {
            var model = await BuildSignatoriesAsync();
            model.DocumentClassCode = form.DocumentClassCode;
            model.NameAr = form.NameAr; model.NameEn = form.NameEn;
            model.TitleAr = form.TitleAr; model.TitleEn = form.TitleEn;
            model.EffectiveFrom = form.EffectiveFrom;
            return model;
        }

        private async Task<SignatoriesViewModel> BuildSignatoriesAsync() => new()
        {
            Signatories = await _db.Signatories.AsNoTracking().OrderBy(s => s.DocumentClassCode).ThenByDescending(s => s.EffectiveFromUtc).ToListAsync(),
            EffectiveFrom = _clock.UtcNow.Date,
        };

        // ==================================================================
        // §8.3 School status
        // ==================================================================

        [HttpGet("status")]
        [RequirePermission(ScreenCatalog.Modules.Schools, ScreenCatalog.Schools.Status, ActionVerb.View)]
        public async Task<IActionResult> Status()
        {
            return View(await BuildStatusAsync());
        }

        [HttpPost("status")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Schools, ScreenCatalog.Schools.Status, ActionVerb.Approve)]
        public async Task<IActionResult> Status(SchoolStatusViewModel form)
        {
            var school = await _db.Schools.AsNoTracking().SingleOrDefaultAsync(s => s.Id == _tenant.SchoolId);
            form.Reason = Clean(form.Reason);

            if (school == null)
            {
                ModelState.AddModelError(string.Empty, T(
                    "There is no school profile to move yet — fill in the profile first.",
                    "لا يوجد ملف مدرسة لنقله بعد — أكمل ملف المدرسة أولاً."));
                return View(await BuildStatusAsync(form));
            }

            if (form.Target == null)
            {
                ModelState.AddModelError(nameof(form.Target), T(
                    "Choose the status to move to.", "اختر الحالة المستهدفة."));
            }

            if (string.IsNullOrWhiteSpace(form.Reason))
            {
                ModelState.AddModelError(nameof(form.Reason), T(
                    "Every status move is audited at tier 1 and is refused without a reason. Write it for whoever reads it in two years, not for today.",
                    "كل انتقال في الحالة مدقَّق بالمستوى الأول ومرفوض بلا سبب. اكتبه لمن يقرؤه بعد سنتين لا ليومك."));
            }

            var readiness = BuildActivationReadiness(school);
            var gateUnmet = form.Target == SchoolStatus.Active
                && school.Status == SchoolStatus.Setup
                && readiness.Any(r => r.Required && !r.Done);

            if (form.Target != null && SchoolStatusLabels.IsIrreversible(form.Target.Value) && !form.Acknowledged)
            {
                ModelState.AddModelError(nameof(form.Acknowledged), T(
                    "Closing is permanent: no screen anywhere in this product reopens a closed school. Tick the box to confirm you mean it.",
                    "الإغلاق نهائي: لا توجد شاشة في هذا المنتج تعيد فتح مدرسة مغلقة. علّم المربع لتأكيد أنك تقصد ذلك."));
            }
            else if (gateUnmet && !form.Acknowledged)
            {
                ModelState.AddModelError(nameof(form.Acknowledged), T(
                    "The documents gate activation on the readiness list below (BR-SCH-001, BR-SET-003) and it is not met. This build does not enforce that gate, so the move will go through — tick the box to say you are overriding it deliberately.",
                    "الوثائق تشترط قائمة الجاهزية أدناه قبل التفعيل (BR-SCH-001، BR-SET-003) وهي غير مستوفاة. وهذا الإصدار لا يفرض الشرط، فالانتقال سيتم — علّم المربع لتؤكد أنك تتجاوزه عن قصد."));
            }

            if (!ModelState.IsValid)
            {
                return View(await BuildStatusAsync(form));
            }

            try
            {
                _audit.Reason = form.Reason;
                await _schools.ChangeStatusAsync(school.Id, form.Target!.Value);
                var name = SchoolStatusLabels.Name(form.Target.Value, IsArabic);
                TempData["Flash"] = T($"The school is now {name}.", $"المدرسة الآن {name}.");
                return RedirectToAction(nameof(Status));
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, UserMessage.For(ex, IsArabic));
                return View(await BuildStatusAsync(form));
            }
        }

        private async Task<SchoolStatusViewModel> BuildStatusAsync(SchoolStatusViewModel? form = null)
        {
            var school = await _db.Schools.AsNoTracking().SingleOrDefaultAsync(s => s.Id == _tenant.SchoolId);
            var model = new SchoolStatusViewModel
            {
                School = school,
                Target = form?.Target,
                Reason = form?.Reason,
                Acknowledged = form?.Acknowledged ?? false,
            };

            if (school == null)
            {
                return model;
            }

            // Every state is offered to the map, not only the reachable ones — the console has to
            // be able to say "not from here" as clearly as it says "yes".
            model.Transitions = Enum.GetValues<SchoolStatus>()
                .Where(t => t != school.Status)
                .Select(t => new SchoolStatusViewModel.TransitionRow(
                    t,
                    SchoolStatusTransitions.CanTransition(school.Status, t),
                    SchoolStatusLabels.IsIrreversible(t)))
                .ToList();
            model.AllowedTargets = model.Transitions.Where(t => t.Allowed).Select(t => t.Target).ToList();
            model.ActivationReadiness = school.Status == SchoolStatus.Setup
                ? BuildActivationReadiness(school)
                : Array.Empty<SchoolChecklistRow>();
            model.History = (await ReadHistoryAsync(school, nameof(School.Status))).ToList();
            return model;
        }

        /// <summary>
        /// doc 02 §4: <c>Setup → Active</c> is checklist-gated on BR-SET-003 and BR-SCH-001's four
        /// identity fields. <c>SchoolStatusTransitions</c> does not enforce that gate — its own
        /// comment says the wizard did not exist when it was written, and E-101 has since landed —
        /// so the console shows the condition and asks for a deliberate override rather than
        /// pretending either that the gate exists or that the requirement does not.
        /// </summary>
        private static IReadOnlyList<SchoolChecklistRow> BuildActivationReadiness(School school)
        {
            var identityComplete =
                !string.IsNullOrWhiteSpace(school.NameAr) && !string.IsNullOrWhiteSpace(school.NameEn)
                && !string.IsNullOrWhiteSpace(school.LicenseNumber) && !string.IsNullOrWhiteSpace(school.MinistryCode);

            return new List<SchoolChecklistRow>
            {
                new("Identity is complete", "الهوية مكتملة",
                    "Both official names, the licence number and the ministry code are mandatory before activation (BR-SCH-001).",
                    "الاسمان الرسميان ورقم الترخيص والرمز الوزاري مطلوبة قبل التفعيل (BR-SCH-001).",
                    identityComplete, true, null, "School", nameof(Profile)),
                new("The setup wizard is declared complete", "أُعلن اكتمال معالج الإعداد",
                    "Activation is checklist-gated on the wizard (BR-SET-003) — country pack, currency, time zone, working week, numbering and the stage structure.",
                    "التفعيل مشروط بقائمة المعالج (BR-SET-003) — حزمة الدولة والعملة والمنطقة الزمنية وأيام العمل والترقيم والهيكل الدراسي.",
                    school.SetupCompletedAtUtc != null, true, null, "Setup", "Index"),
            };
        }
    }
}
