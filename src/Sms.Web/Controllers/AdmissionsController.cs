using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Admissions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Parents;
using Sms.Domain.Admissions;
using Sms.Domain.Common;
using Sms.Domain.Schools;
using Sms.Domain.Sections;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.Web.Security;
using AdmissionApplication = Sms.Domain.Admissions.Application;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/09 §8.1, 8.3–8.7: campaign setup, counter application
    /// capture (with parent pick / quick-create by mobile — BR-GLB-003/004
    /// dedup step), pipeline board (kanban by status, aging + SLA flag),
    /// application detail (applicant, family, assessment, decision actions,
    /// history), waiting-list manager (offer / accept / decline), and the
    /// registration wizard (section pick → one-transaction RegisterAsync,
    /// BR-ADM-007). §8.2 portal form and document checklist / fee preview
    /// wait for the portal shell, attachment screens and Module 19.
    /// </summary>
    [Route("admissions")]
    public class AdmissionsController : Controller
    {
        private const int ReviewSlaDays = 5;

        private readonly IAdmissionAdmin _admissions;
        private readonly IParentAdmin _parents;
        private readonly AppDbContext _db;
        private readonly IWorkingYearContext _workingYear;
        private readonly ICurrentUser _currentUser;
        private readonly IClock _clock;

        public AdmissionsController(IAdmissionAdmin admissions, IParentAdmin parents, AppDbContext db, IWorkingYearContext workingYear, ICurrentUser currentUser, IClock clock)
        {
            _admissions = admissions;
            _parents = parents;
            _db = db;
            _workingYear = workingYear;
            _currentUser = currentUser;
            _clock = clock;
        }

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        // ------------------------------------------------------------ Campaigns

        [HttpGet("")]
        [RequirePermission(ScreenCatalog.Modules.Admissions, ScreenCatalog.Admissions.Applications, ActionVerb.View)]
        public async Task<IActionResult> Index(int? year = null)
        {
            var years = await _db.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartDate).ToListAsync();
            var selected = years.FirstOrDefault(y => y.Id == (year ?? _workingYear.AcademicYearId)) ?? years.FirstOrDefault(y => y.Status == AcademicYearStatus.Preparation) ?? years.FirstOrDefault();
            var m = new CampaignListViewModel { Years = years, Year = selected, OpenDate = _clock.UtcNow.Date, CloseDate = _clock.UtcNow.Date.AddMonths(3) };
            if (selected != null)
            {
                var grades = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking().Where(g => g.SchoolId == _db.CurrentSchoolId).ToListAsync();
                var profiles = await _db.GradeYearProfiles.AsNoTracking().Where(p => p.AcademicYearId == selected.Id).ToListAsync();
                var campaigns = await _db.AdmissionCampaigns.AsNoTracking().Where(c => c.AcademicYearId == selected.Id).OrderBy(c => c.OpenDate).ToListAsync();
                var apps = await _db.Applications.AsNoTracking().Where(a => a.AcademicYearId == selected.Id).ToListAsync();
                var today = _clock.UtcNow.Date;
                m.Profiles = profiles.Select(p => (p.Id, grades.First(g => g.Id == p.GradeLevelId))).OrderBy(x => x.Item2.SequenceOrder).ToList();
                m.Rows = campaigns.Select(c =>
                {
                    var p = profiles.First(x => x.Id == c.GradeYearProfileId);
                    var ca = apps.Where(a => a.CampaignId == c.Id).ToList();
                    return new CampaignListViewModel.Row(c, grades.First(g => g.Id == p.GradeLevelId), p, ca.Count,
                        ca.Count(a => a.Status == ApplicationStatus.Approved), ca.Count(a => a.Status == ApplicationStatus.Registered), ca.Count(a => a.Status == ApplicationStatus.Waitlisted),
                        c.IsActive && c.OpenDate.Date <= today && today <= c.CloseDate.Date);
                }).ToList();
            }

            return View(m);
        }

        [HttpPost("campaign")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Admissions, ScreenCatalog.Admissions.Campaigns, ActionVerb.Create)]
        public async Task<IActionResult> DefineCampaign(CampaignListViewModel form, int? year)
        {
            try
            {
                if (form.GradeYearProfileId == null || form.OpenDate == null || form.CloseDate == null) throw new InvalidOperationException(T("Grade, open and close dates are required.", "الصف وتاريخا الفتح والإغلاق مطلوبة."));
                if (form.CloseDate < form.OpenDate) throw new InvalidOperationException(T("Close date must be after the open date.", "تاريخ الإغلاق بعد الفتح."));
                await _admissions.DefineCampaignAsync(form.GradeYearProfileId.Value, form.OpenDate.Value, form.CloseDate.Value, form.RequiresAssessment, form.ApplicationFeeAmount);
                TempData["Flash"] = T("Campaign created.", "تم إنشاء حملة القبول.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Index), new { year });
        }

        [HttpPost("campaign/{id:int}/edit")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Admissions, ScreenCatalog.Admissions.Campaigns, ActionVerb.Edit)]
        public async Task<IActionResult> EditCampaign(int id, DateTime? openDate, DateTime? closeDate, bool requiresAssessment, decimal? applicationFeeAmount, int? year)
        {
            try
            {
                if (openDate == null || closeDate == null) throw new InvalidOperationException(T("Open and close dates are required.", "تاريخا الفتح والإغلاق مطلوبان."));
                await _admissions.UpdateCampaignAsync(id, openDate.Value, closeDate.Value, requiresAssessment, applicationFeeAmount);
                TempData["Flash"] = T("Campaign updated.", "تم تحديث الحملة.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Index), new { year });
        }

        [HttpPost("campaign/{id:int}/delete")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Admissions, ScreenCatalog.Admissions.Campaigns, ActionVerb.Deactivate)]
        public async Task<IActionResult> DeleteCampaign(int id, int? year)
        {
            try
            {
                var applications = await _db.Applications.AsNoTracking().CountAsync(a => a.CampaignId == id);
                var registered = await _db.Applications.AsNoTracking().CountAsync(a => a.CampaignId == id && a.RegisteredStudentId != null);
                await _admissions.DeleteCampaignAsync(id);
                TempData["Flash"] = applications == 0
                    ? T("Campaign deleted.", "تم حذف الحملة.")
                    : registered == 0
                        ? T($"Campaign and its {applications} application(s) deleted.", $"تم حذف الحملة و{applications} طلب/طلبات.")
                        : T($"Campaign and its {applications} application(s) deleted; the {registered} student(s) already registered from it were kept.", $"تم حذف الحملة و{applications} طلب/طلبات؛ تم الإبقاء على {registered} طالب/طلاب سبق تسجيلهم منها.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Index), new { year });
        }

        // ------------------------------------------------------------ Pipeline board

        [HttpGet("board")]
        [RequirePermission(ScreenCatalog.Modules.Admissions, ScreenCatalog.Admissions.Board, ActionVerb.View)]
        public async Task<IActionResult> Board(int? campaign = null, string? view = null)
        {
            var campaigns = await _db.AdmissionCampaigns.AsNoTracking().OrderByDescending(c => c.OpenDate).ToListAsync();
            var selected = campaigns.FirstOrDefault(c => c.Id == campaign) ?? campaigns.FirstOrDefault();
            if (selected == null)
            {
                TempData["Error"] = T("Create a campaign first.", "أنشئ حملة قبول أولاً.");
                return RedirectToAction(nameof(Index));
            }

            var labels = await CampaignLabelsAsync(campaigns);
            var profile = await _db.GradeYearProfiles.AsNoTracking().SingleAsync(p => p.Id == selected.GradeYearProfileId);
            var grade = await _db.GradeLevels.AsNoTracking().SingleAsync(g => g.Id == profile.GradeLevelId);
            var year = await _db.AcademicYears.AsNoTracking().SingleAsync(y => y.Id == selected.AcademicYearId);
            var apps = await _db.Applications.AsNoTracking().Where(a => a.CampaignId == selected.Id).OrderBy(a => a.CreatedAtUtc).ToListAsync();
            var scores = await _db.ApplicationAssessments.AsNoTracking().Where(s => apps.Select(a => a.Id).Contains(s.ApplicationId)).ToListAsync();
            var now = _clock.UtcNow;
            var order = new[] { ApplicationStatus.Draft, ApplicationStatus.Submitted, ApplicationStatus.UnderReview, ApplicationStatus.Recommended, ApplicationStatus.Approved, ApplicationStatus.Waitlisted, ApplicationStatus.Registered, ApplicationStatus.Rejected, ApplicationStatus.Lapsed };

            return View(new PipelineBoardViewModel
            {
                Campaign = selected, Grade = grade, Year = year, Campaigns = campaigns, CampaignLabels = labels, ReviewSlaDays = ReviewSlaDays, ViewMode = view == "grid" ? "grid" : "board",
                Columns = order.Select(s => new PipelineBoardViewModel.Column(s, apps.Where(a => a.Status == s).Select(a =>
                {
                    var age = (int)(now - (a.ModifiedAtUtc ?? a.CreatedAtUtc)).TotalDays;
                    var inReview = s is ApplicationStatus.Submitted or ApplicationStatus.UnderReview or ApplicationStatus.Recommended;
                    return new PipelineBoardViewModel.Card(a, age, scores.Where(x => x.ApplicationId == a.Id).OrderByDescending(x => x.AssessedAtUtc).FirstOrDefault()?.Score, a.ParentId != null, inReview && age > ReviewSlaDays);
                }).ToList())).ToList(),
            });
        }

        [HttpPost("{id:int}/status")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Admissions, ScreenCatalog.Admissions.Applications, ActionVerb.Approve)]
        public async Task<IActionResult> ChangeStatus(int id, ApplicationStatus target, string? returnTo)
        {
            try
            {
                await _admissions.ChangeStatusAsync(id, target);
                TempData["Flash"] = T($"Application moved to {target}.", $"انتقل الطلب إلى {target}.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return returnTo is "board" or "grid" ? RedirectToAction(nameof(Board), new { campaign = (await _db.Applications.AsNoTracking().SingleAsync(a => a.Id == id)).CampaignId, view = returnTo }) : RedirectToAction(nameof(Details), new { id });
        }

        // ------------------------------------------------------------ Counter capture

        [HttpGet("apply")]
        [RequirePermission(ScreenCatalog.Modules.Admissions, ScreenCatalog.Admissions.Applications, ActionVerb.Create)]
        public async Task<IActionResult> Apply(int campaign)
        {
            var c = await _db.AdmissionCampaigns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == campaign);
            if (c == null) return NotFound();
            return View(await BuildApplyAsync(c));
        }

        [HttpPost("apply")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Admissions, ScreenCatalog.Admissions.Applications, ActionVerb.Create)]
        public async Task<IActionResult> Apply(ApplicationFormViewModel form)
        {
            var c = await _db.AdmissionCampaigns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == form.CampaignId);
            if (c == null) return NotFound();
            try
            {
                foreach (var (v, n) in new[] { (form.FirstNameAr, "First name (Arabic)"), (form.FatherNameAr, "Father name (Arabic)"), (form.GrandfatherNameAr, "Grandfather name (Arabic)"), (form.FamilyNameAr, "Family name (Arabic)"), (form.FirstNameEn, "First name (English)"), (form.FatherNameEn, "Father name (English)"), (form.GrandfatherNameEn, "Grandfather name (English)"), (form.FamilyNameEn, "Family name (English)") })
                {
                    Require(v, n);
                }

                if (form.DateOfBirth == null || form.NationalityLookupId == null) throw new InvalidOperationException(T("Date of birth and nationality are required.", "تاريخ الميلاد والجنسية مطلوبان."));

                // Parent dedup step (BR-GLB-003/004): existing pick wins; otherwise a quick-create keyed on mobile.
                var parentId = form.ParentId;
                if (parentId == null && !string.IsNullOrWhiteSpace(form.NewParentMobile))
                {
                    var mobile = form.NewParentMobile.Trim();
                    var existing = await _db.Parents.AsNoTracking().FirstOrDefaultAsync(p => p.PrimaryMobile == mobile);
                    if (existing != null)
                    {
                        parentId = existing.Id;
                        TempData["Flash"] = T($"Linked to existing parent {existing.ParentFileNo} (same mobile).", $"تم الربط بولي أمر موجود {existing.ParentFileNo} (نفس الجوال).");
                    }
                    else
                    {
                        Require(form.NewParentNameAr, "Parent name (Arabic)"); Require(form.NewParentNameEn, "Parent name (English)");
                        parentId = (await _parents.RegisterParentAsync(form.NewParentNameAr!, form.NewParentNameEn!, mobile, form.NewParentEmail)).Id;
                    }
                }

                var app = await _admissions.SubmitApplicationAsync(form.CampaignId, form.FirstNameAr!, form.FatherNameAr!, form.GrandfatherNameAr!, form.FamilyNameAr!,
                    form.FirstNameEn!, form.FatherNameEn!, form.GrandfatherNameEn!, form.FamilyNameEn!, form.Gender, form.DateOfBirth.Value, form.NationalityLookupId.Value, parentId);
                if (form.SubmitImmediately && app.Status == ApplicationStatus.Draft)
                {
                    await _admissions.ChangeStatusAsync(app.Id, ApplicationStatus.Submitted);
                }

                TempData["Flash"] = (TempData["Flash"] as string ?? "") + " " + T($"Application {app.ApplicationNo} captured.", $"تم تسجيل الطلب {app.ApplicationNo}.");
                return RedirectToAction(nameof(Details), new { id = app.Id });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, UserMessage.For(ex, IsArabic));
                var model = await BuildApplyAsync(c);
                CopyForm(form, model);
                return View(model);
            }
        }

        private async Task<ApplicationFormViewModel> BuildApplyAsync(AdmissionCampaign c)
        {
            var labels = await CampaignLabelsAsync(new[] { c });
            return new ApplicationFormViewModel
            {
                CampaignId = c.Id, CampaignLabel = labels[c.Id],
                Nationalities = await LookupAsync("Nationality"),
                Parents = await _db.Parents.AsNoTracking().OrderBy(p => p.NameEn).Take(500).ToListAsync(),
            };
        }

        private static void CopyForm(ApplicationFormViewModel from, ApplicationFormViewModel to)
        {
            to.FirstNameAr = from.FirstNameAr; to.FatherNameAr = from.FatherNameAr; to.GrandfatherNameAr = from.GrandfatherNameAr; to.FamilyNameAr = from.FamilyNameAr;
            to.FirstNameEn = from.FirstNameEn; to.FatherNameEn = from.FatherNameEn; to.GrandfatherNameEn = from.GrandfatherNameEn; to.FamilyNameEn = from.FamilyNameEn;
            to.Gender = from.Gender; to.DateOfBirth = from.DateOfBirth; to.NationalityLookupId = from.NationalityLookupId; to.ParentId = from.ParentId;
            to.NewParentNameAr = from.NewParentNameAr; to.NewParentNameEn = from.NewParentNameEn; to.NewParentMobile = from.NewParentMobile; to.NewParentEmail = from.NewParentEmail; to.SubmitImmediately = from.SubmitImmediately;
        }

        // ------------------------------------------------------------ Detail

        [HttpGet("{id:int}")]
        [RequirePermission(ScreenCatalog.Modules.Admissions, ScreenCatalog.Admissions.Applications, ActionVerb.View)]
        public async Task<IActionResult> Details(int id)
        {
            var app = await _db.Applications.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id);
            if (app == null) return NotFound();
            var campaign = await _db.AdmissionCampaigns.AsNoTracking().SingleAsync(c => c.Id == app.CampaignId);
            var profile = await _db.GradeYearProfiles.AsNoTracking().SingleAsync(p => p.Id == campaign.GradeYearProfileId);
            var grade = await _db.GradeLevels.AsNoTracking().SingleAsync(g => g.Id == profile.GradeLevelId);
            var year = await _db.AcademicYears.AsNoTracking().SingleAsync(y => y.Id == app.AcademicYearId);
            var nat = await _db.LookupValues.AsNoTracking().SingleOrDefaultAsync(v => v.Id == app.NationalityLookupId);
            var sections = await _db.Sections.AsNoTracking().Where(s => s.GradeYearProfileId == profile.Id && s.Status == SectionStatus.Active).ToListAsync();
            var members = await _db.SectionMemberships.AsNoTracking().Where(m => sections.Select(s => s.Id).Contains(m.SectionId) && m.EffectiveToUtc == null).GroupBy(m => m.SectionId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
            var history = await _db.AuditEntries.AsNoTracking().Where(e => e.EntityType == "Application" && e.EntityId == id).OrderByDescending(e => e.OccurredAtUtc).Take(50).ToListAsync();

            return View(new ApplicationDetailViewModel
            {
                Application = app, Campaign = campaign, Grade = grade, Year = year,
                NationalityName = nat == null ? "?" : (IsArabic ? nat.Name.NameAr : nat.Name.NameEn),
                Parent = app.ParentId == null ? null : await _db.Parents.AsNoTracking().SingleOrDefaultAsync(p => p.Id == app.ParentId),
                Parents = await _db.Parents.AsNoTracking().OrderBy(p => p.NameEn).Take(500).ToListAsync(),
                Assessments = await _db.ApplicationAssessments.AsNoTracking().Where(s => s.ApplicationId == id).OrderByDescending(s => s.AssessedAtUtc).ToListAsync(),
                WaitingListEntry = await _db.WaitingListEntries.AsNoTracking().FirstOrDefaultAsync(w => w.ApplicationId == id),
                AllowedTransitions = Enum.GetValues<ApplicationStatus>().Where(s => ApplicationStatusTransitions.CanTransition(app.Status, s) && s != ApplicationStatus.Registered).ToList(),
                Sections = sections, SectionMembers = members,
                Relationships = await LookupAsync("RelationshipType"),
                Nationalities = await LookupAsync("Nationality"),
                History = history.Select(h => (h.EntityType, h.Action.ToString(), h.FieldName, h.NewValue, h.OccurredAtUtc, h.ActorUserId)).ToList(),
            });
        }

        [HttpPost("{id:int}/edit")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Admissions, ScreenCatalog.Admissions.Applications, ActionVerb.Edit)]
        public async Task<IActionResult> EditApplication(int id, ApplicationFormViewModel form)
        {
            try
            {
                foreach (var (v, n) in new[] { (form.FirstNameAr, "First name (Arabic)"), (form.FatherNameAr, "Father name (Arabic)"), (form.GrandfatherNameAr, "Grandfather name (Arabic)"), (form.FamilyNameAr, "Family name (Arabic)"), (form.FirstNameEn, "First name (English)"), (form.FatherNameEn, "Father name (English)"), (form.GrandfatherNameEn, "Grandfather name (English)"), (form.FamilyNameEn, "Family name (English)") })
                {
                    Require(v, n);
                }

                if (form.DateOfBirth == null || form.NationalityLookupId == null) throw new InvalidOperationException(T("Date of birth and nationality are required.", "تاريخ الميلاد والجنسية مطلوبان."));
                await _admissions.UpdateApplicationAsync(id, form.FirstNameAr!, form.FatherNameAr!, form.GrandfatherNameAr!, form.FamilyNameAr!, form.FirstNameEn!, form.FatherNameEn!, form.GrandfatherNameEn!, form.FamilyNameEn!, form.Gender, form.DateOfBirth.Value, form.NationalityLookupId.Value, form.ParentId);
                TempData["Flash"] = T("Application updated.", "تم تحديث الطلب.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost("{id:int}/delete")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Admissions, ScreenCatalog.Admissions.Applications, ActionVerb.Deactivate)]
        public async Task<IActionResult> DeleteApplication(int id)
        {
            var app = await _db.Applications.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id);
            try
            {
                await _admissions.DeleteApplicationAsync(id);
                TempData["Flash"] = T("Application deleted.", "تم حذف الطلب.");
                return RedirectToAction(nameof(Board), new { campaign = app?.CampaignId });
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost("{id:int}/assess")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Admissions, ScreenCatalog.Admissions.Applications, ActionVerb.Edit)]
        public async Task<IActionResult> Assess(int id, decimal? score, string? notes)
        {
            try
            {
                if (score == null || score < 0 || score > 100) throw new InvalidOperationException(T("Score must be between 0 and 100.", "الدرجة بين 0 و100."));
                await _admissions.RecordAssessmentAsync(id, score.Value, _currentUser.UserId, notes);
                TempData["Flash"] = T("Assessment recorded.", "تم تسجيل التقييم.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost("{id:int}/parent")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Admissions, ScreenCatalog.Admissions.Applications, ActionVerb.Edit)]
        public async Task<IActionResult> LinkParent(int id, int? parentId)
        {
            // The engine has no dedicated "set parent" op; the application is a Draft-to-Approved
            // record whose ParentId is a plain FK. Update it directly (audited by the T-tag).
            var app = await _db.Applications.SingleOrDefaultAsync(a => a.Id == id);
            if (app == null) return NotFound();
            if (app.Status == ApplicationStatus.Registered) { TempData["Error"] = T("Registered applications are frozen.", "الطلبات المسجّلة مجمّدة."); return RedirectToAction(nameof(Details), new { id }); }
            app.ParentId = parentId;
            await _db.SaveChangesAsync();
            TempData["Flash"] = T("Family link updated.", "تم تحديث ربط الأسرة.");
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost("{id:int}/waitlist")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Admissions, ScreenCatalog.Admissions.Waitlist, ActionVerb.Edit)]
        public async Task<IActionResult> Waitlist(int id)
        {
            try
            {
                var app = await _db.Applications.AsNoTracking().SingleAsync(a => a.Id == id);
                var campaign = await _db.AdmissionCampaigns.AsNoTracking().SingleAsync(c => c.Id == app.CampaignId);
                if (app.Status != ApplicationStatus.Waitlisted) await _admissions.ChangeStatusAsync(id, ApplicationStatus.Waitlisted);
                if (!await _db.WaitingListEntries.AnyAsync(w => w.ApplicationId == id))
                {
                    var e = await _admissions.AddToWaitingListAsync(id, campaign.GradeYearProfileId);
                    TempData["Flash"] = T($"Added to the waiting list at rank {e.OrderRank}.", $"أُضيف إلى قائمة الانتظار بالترتيب {e.OrderRank}.");
                }
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost("{id:int}/register")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Admissions, ScreenCatalog.Admissions.Applications, ActionVerb.Approve)]
        public async Task<IActionResult> Register(int id, int? sectionId, DateTime? enrollmentDate, int? relationshipId)
        {
            try
            {
                if (sectionId == null || relationshipId == null) throw new InvalidOperationException(T("Section and guardian relationship are required.", "الشعبة وصلة القرابة مطلوبتان."));
                var student = await _admissions.RegisterAsync(id, sectionId.Value, enrollmentDate ?? _clock.UtcNow.Date, relationshipId.Value);
                TempData["Flash"] = T($"Registered — student number {student.StudentNo} issued (BR-ADM-007, one transaction).", $"تم التسجيل — صدر رقم الطالب {student.StudentNo} (BR-ADM-007، معاملة واحدة).");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Details), new { id });
        }

        // ------------------------------------------------------------ Waiting list

        [HttpGet("waitlist")]
        [RequirePermission(ScreenCatalog.Modules.Admissions, ScreenCatalog.Admissions.Waitlist, ActionVerb.View)]
        public async Task<IActionResult> WaitingList(int? profile = null)
        {
            var grades = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking().Where(g => g.SchoolId == _db.CurrentSchoolId).ToListAsync();
            var years = await _db.AcademicYears.AsNoTracking().ToListAsync();
            var profileIds = await _db.WaitingListEntries.AsNoTracking().Select(w => w.GradeYearProfileId).Distinct().ToListAsync();
            var profiles = await _db.GradeYearProfiles.AsNoTracking().Where(p => profileIds.Contains(p.Id)).ToListAsync();
            var m = new WaitingListViewModel
            {
                Profiles = profiles.Select(p => (p.Id, grades.First(g => g.Id == p.GradeLevelId), years.First(y => y.Id == p.AcademicYearId))).OrderByDescending(x => x.Item3.StartDate).ThenBy(x => x.Item2.SequenceOrder).ToList(),
            };
            m.ProfileId = profile ?? m.Profiles.FirstOrDefault().ProfileId;
            if (m.ProfileId != 0)
            {
                var entries = await _db.WaitingListEntries.AsNoTracking().Where(w => w.GradeYearProfileId == m.ProfileId).OrderBy(w => w.OrderRank).ToListAsync();
                var apps = await _db.Applications.AsNoTracking().Where(a => entries.Select(e => e.ApplicationId).Contains(a.Id)).ToDictionaryAsync(a => a.Id);
                var now = _clock.UtcNow;
                m.Rows = entries.Select(e => new WaitingListViewModel.Row(e, apps[e.ApplicationId], e.OfferedAtUtc != null && e.IsOfferAccepted == null && e.OfferExpiresAtUtc < now)).ToList();
                var p = profiles.First(x => x.Id == m.ProfileId);
                m.PlannedSeats = p.TargetSections * p.TargetSectionSize;
                m.Enrolled = await _db.Enrollments.AsNoTracking().CountAsync(e => e.GradeYearProfileId == p.Id && e.ExitDate == null);
            }

            return View(m);
        }

        [HttpPost("waitlist/{entryId:int}/offer")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Admissions, ScreenCatalog.Admissions.Waitlist, ActionVerb.Approve)]
        public async Task<IActionResult> Offer(int entryId, int? profile, DateTime? expires)
        {
            try
            {
                await _admissions.OfferSeatAsync(entryId, DateTime.SpecifyKind(expires ?? _clock.UtcNow.Date.AddDays(7), DateTimeKind.Utc));
                TempData["Flash"] = T("Seat offered.", "تم عرض المقعد.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(WaitingList), new { profile });
        }

        [HttpPost("waitlist/{entryId:int}/respond")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Admissions, ScreenCatalog.Admissions.Waitlist, ActionVerb.Edit)]
        public async Task<IActionResult> Respond(int entryId, bool accepted, int? profile)
        {
            try
            {
                await _admissions.RespondToOfferAsync(entryId, accepted);
                TempData["Flash"] = accepted ? T("Offer accepted — application Approved; proceed to registration.", "قُبل العرض — الطلب معتمد؛ تابع التسجيل.") : T("Offer declined — application Lapsed.", "رُفض العرض — الطلب ساقط.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(WaitingList), new { profile });
        }

        [HttpPost("waitlist/{entryId:int}/remove")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Admissions, ScreenCatalog.Admissions.Waitlist, ActionVerb.Deactivate)]
        public async Task<IActionResult> RemoveFromWaitlist(int entryId, int? profile)
        {
            try
            {
                await _admissions.RemoveFromWaitingListAsync(entryId);
                TempData["Flash"] = T("Removed from the waiting list.", "تمت الإزالة من قائمة الانتظار.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(WaitingList), new { profile });
        }

        // ------------------------------------------------------------

        private async Task<IReadOnlyDictionary<int, string>> CampaignLabelsAsync(IEnumerable<AdmissionCampaign> campaigns)
        {
            var list = campaigns.ToList();
            var profiles = await _db.GradeYearProfiles.AsNoTracking().Where(p => list.Select(c => c.GradeYearProfileId).Contains(p.Id)).ToListAsync();
            var grades = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking().Where(g => g.SchoolId == _db.CurrentSchoolId).ToListAsync();
            var years = await _db.AcademicYears.AsNoTracking().ToListAsync();
            return list.ToDictionary(c => c.Id, c =>
            {
                var p = profiles.First(x => x.Id == c.GradeYearProfileId);
                var g = grades.First(x => x.Id == p.GradeLevelId);
                var y = years.First(x => x.Id == c.AcademicYearId);
                return $"{(IsArabic ? g.Name.NameAr : g.Name.NameEn)} · {(IsArabic ? y.LabelAr : y.LabelEn)} ({c.OpenDate:yyyy-MM-dd} → {c.CloseDate:yyyy-MM-dd})";
            });
        }

        private async Task<IReadOnlyList<(int Id, string Ar, string En)>> LookupAsync(string category)
        {
            var cat = await _db.LookupCategories.AsNoTracking().SingleOrDefaultAsync(c => c.Code == category);
            return cat == null ? Array.Empty<(int, string, string)>() : await _db.LookupValues.AsNoTracking().Where(v => v.LookupCategoryId == cat.Id).OrderBy(v => v.SortOrder).Select(v => new ValueTuple<int, string, string>(v.Id, v.Name.NameAr, v.Name.NameEn)).ToListAsync();
        }

        private static void Require(string? v, string f)
        {
            if (string.IsNullOrWhiteSpace(v)) throw new InvalidOperationException(T($"{f} is required.", $"الحقل {f} مطلوب."));
        }
    }
}
