using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Parents;
using Sms.Domain.Common;
using Sms.Domain.Parents;
using Sms.Domain.Students;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/11 §8.1–8.3: parent directory (search by name/phone/
    /// child, data-quality flags), parent file (identity & contacts ✎,
    /// children with link flags, portal account, audit), dedup workbench
    /// (candidate pairs by shared mobile / identical name; link/dismiss and
    /// merge need the ParentMergeLog engine — deferred, BR-PAR-004).
    /// Family statement (finance) and communications history open with
    /// their module screens; custody restrictions 🔒 with Module 24/25.
    /// </summary>
    [Route("parents")]
    public class ParentsController : Controller
    {
        private readonly IParentAdmin _parents;
        private readonly AppDbContext _db;
        private readonly IAuditContext _audit;
        private readonly IPermissionService _permissions;

        public ParentsController(IParentAdmin parents, AppDbContext db, IAuditContext audit, IPermissionService permissions)
        {
            _parents = parents;
            _db = db;
            _audit = audit;
            _permissions = permissions;
        }

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        [HttpGet("")]
        [RequirePermission(ScreenCatalog.Modules.Parents, ScreenCatalog.Parents.Directory, ActionVerb.View)]
        public async Task<IActionResult> Index(string? q = null, string? filter = null)
        {
            var query = _db.Parents.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var t = q.Trim();
                var childParentIds = await (from s in _db.Students.IgnoreQueryFilters().Where(s => s.SchoolId == _db.CurrentSchoolId && (s.StudentNo.Contains(t) || s.FirstNameAr.Contains(t) || s.FirstNameEn.Contains(t) || s.FamilyNameAr.Contains(t) || s.FamilyNameEn.Contains(t)))
                                            join l in _db.StudentGuardianLinks on s.Id equals l.StudentId
                                            select l.ParentId).Distinct().ToListAsync();
                query = query.Where(p => p.ParentFileNo.Contains(t) || p.NameAr.Contains(t) || p.NameEn.Contains(t) || p.PrimaryMobile.Contains(t) || (p.Email != null && p.Email.Contains(t)) || childParentIds.Contains(p.Id));
            }

            var total = await query.CountAsync();
            var parents = await query.OrderBy(p => p.ParentFileNo).Take(300).ToListAsync();
            var ids = parents.Select(p => p.Id).ToList();
            var links = await _db.StudentGuardianLinks.AsNoTracking().Where(l => ids.Contains(l.ParentId) && l.EffectiveToUtc == null).ToListAsync();
            var mobiles = parents.GroupBy(p => p.PrimaryMobile).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();

            var rows = parents.Select(p =>
            {
                var flags = new List<string>();
                var children = links.Count(l => l.ParentId == p.Id);
                if (children == 0) flags.Add(T("no children linked", "بلا أبناء مرتبطين"));
                if (string.IsNullOrWhiteSpace(p.Email)) flags.Add(T("no email", "بلا بريد"));
                if (mobiles.Contains(p.PrimaryMobile)) flags.Add(T("shared mobile", "جوال مشترك"));
                return new ParentDirectoryViewModel.Row(p, children, p.UserAccountId != null, flags);
            }).Where(r => filter switch { "multi" => r.Children > 1, "flags" => r.Flags.Count > 0, "portal" => r.HasPortalAccount, _ => true }).ToList();

            return View(new ParentDirectoryViewModel { Rows = rows, Query = q, Filter = filter, Total = total });
        }

        [HttpGet("new")]
        [RequirePermission(ScreenCatalog.Modules.Parents, ScreenCatalog.Parents.Directory, ActionVerb.Create)]
        public async Task<IActionResult> Register() => View(new ParentFormViewModel
        {
            Governorates = await _db.Governorates.AsNoTracking().OrderBy(g => g.SortOrder).ToListAsync(),
            IdTypes = await IdTypesAsync(),
            EducationLevels = await LookupAsync("EducationLevel"),
        });

        [HttpPost("new")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Parents, ScreenCatalog.Parents.Directory, ActionVerb.Create)]
        public async Task<IActionResult> Register(ParentFormViewModel form)
        {
            try
            {
                Require(form.NameAr, "Name (Arabic)", "الاسم (عربي)"); Require(form.NameEn, "Name (English)", "الاسم (إنجليزي)"); Require(form.PrimaryMobile, "Primary mobile", "الجوال الأساسي");
                var mobile = form.PrimaryMobile!.Trim();
                var dup = await _db.Parents.AsNoTracking().FirstOrDefaultAsync(p => p.PrimaryMobile == mobile);
                if (dup != null) throw new InvalidOperationException(T($"A parent with this mobile already exists ({dup.ParentFileNo}) — open that file instead (BR-PAR-002).", $"يوجد ولي أمر بهذا الجوال ({dup.ParentFileNo}) — افتح ملفه بدلاً من الإنشاء (BR-PAR-002)."));

                // BR-PAR-002 matches on the ID number before anything weaker, so now that
                // the register holds one it is checked ahead of the phone — two people
                // share a household phone far more often than they share an ID.
                var idNo = Blank(form.PrimaryIdNo);
                if (idNo != null)
                {
                    var byId = await _db.Parents.AsNoTracking().FirstOrDefaultAsync(p => p.PrimaryIdNo == idNo);
                    if (byId != null) throw new InvalidOperationException(T($"A parent with this ID number already exists ({byId.ParentFileNo}) — open that file and link the child to it (BR-PAR-002).", $"يوجد ولي أمر بهذا رقم الهوية ({byId.ParentFileNo}) — افتح ملفه واربط الطالب به (BR-PAR-002)."));
                }

                var p = await _parents.RegisterParentAsync(
                    form.NameAr!.Trim(), form.NameEn!.Trim(), mobile, form.Email, form.Address, form.OccupationEmployer, form.PreferredLanguage,
                    form.PrimaryIdTypeLookupId, idNo, form.LifeStatus, form.LifeStatusNote, form.EducationLookupId);
                if (form.ResidenceAreaId != null)
                {
                    await _parents.SetResidenceAsync(p.Id, form.ResidenceAreaId, form.NeighbourhoodId);
                }

                TempData["Flash"] = T($"Parent {p.ParentFileNo} created.", $"تم إنشاء ولي الأمر {p.ParentFileNo}.");
                return RedirectToAction(nameof(File), new { id = p.Id });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, UserMessage.For(ex, IsArabic));
                form.Governorates = await _db.Governorates.AsNoTracking().OrderBy(g => g.SortOrder).ToListAsync();
                form.IdTypes = await IdTypesAsync();
                form.EducationLevels = await LookupAsync("EducationLevel");
                return View(form);
            }
        }

        [HttpGet("{id:int}")]
        [RequirePermission(ScreenCatalog.Modules.Parents, ScreenCatalog.Parents.File, ActionVerb.View)]
        public async Task<IActionResult> File(int id, string? tab = null)
        {
            var vm = await BuildFileAsync(id, tab);
            return vm == null ? NotFound() : View(vm);
        }

        /// <summary>
        /// Everything the parent file draws, gathered in one place so that a refused save can
        /// redraw the page it was posted from rather than redirect back to a freshly loaded copy
        /// of the stored row. Null when this school has no such parent.
        /// </summary>
        private async Task<ParentFileViewModel?> BuildFileAsync(int id, string? tab)
        {
            var p = await _db.Parents.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.SchoolId == _db.CurrentSchoolId);
            if (p == null) return null;
            var links = await _db.StudentGuardianLinks.AsNoTracking().Where(l => l.ParentId == id).OrderByDescending(l => l.EffectiveFromUtc).ToListAsync();
            var students = await _db.Students.IgnoreQueryFilters().AsNoTracking().Where(s => links.Select(l => l.StudentId).Contains(s.Id)).ToListAsync();
            var rels = await LookupAsync("RelationshipType");
            var enrollments = await _db.Enrollments.AsNoTracking().Where(e => students.Select(s => s.Id).Contains(e.StudentId) && e.ExitDate == null).ToListAsync();
            var profiles = await _db.GradeYearProfiles.AsNoTracking().Where(x => enrollments.Select(e => e.GradeYearProfileId).Contains(x.Id)).ToListAsync();
            var grades = await _db.GradeLevels.AsNoTracking().ToListAsync();
            ParentFileViewModel.ChildRow C(StudentGuardianLink l)
            {
                var s = students.First(x => x.Id == l.StudentId);
                var e = enrollments.Where(x => x.StudentId == s.Id).OrderByDescending(x => x.EnrollmentDate).FirstOrDefault();
                var g = e == null ? null : grades.FirstOrDefault(x => x.Id == profiles.First(pp => pp.Id == e.GradeYearProfileId).GradeLevelId);
                return new ParentFileViewModel.ChildRow(l, s, rels.FirstOrDefault(r => r.Id == l.RelationshipLookupId) is var r && r != default ? (IsArabic ? r.Ar : r.En) : "?", g == null ? null : (IsArabic ? g.Name.NameAr : g.Name.NameEn));
            }

            var portal = p.UserAccountId == null ? null : await _db.UserAccounts.AsNoTracking().Where(u => u.Id == p.UserAccountId).Select(u => u.UserName).FirstOrDefaultAsync();
            var audit = await _db.AuditEntries.AsNoTracking().Where(e => e.EntityType == nameof(Parent) && e.EntityId == id).OrderByDescending(e => e.OccurredAtUtc).Take(100).ToListAsync();
            var dups = await _db.Parents.AsNoTracking().Where(x => x.Id != id && (x.PrimaryMobile == p.PrimaryMobile || x.NameEn == p.NameEn || x.NameAr == p.NameAr)).ToListAsync();

            // Family statement: posted charges only; every reader must subtract credit notes AND discount documents (E-502 rule).
            var statement = new List<FamilyStatementLine>();
            foreach (var s in students.Where(s => links.Any(l => l.StudentId == s.Id && l.EffectiveToUtc == null)))
            {
                var charges = await _db.Charges.AsNoTracking().Where(c => c.StudentId == s.Id && c.Status == Sms.Domain.Fees.ChargeStatus.Posted).Select(c => new { c.Id, c.GrossAmount }).ToListAsync();
                var cids = charges.Select(c => c.Id).ToList();
                var gross = charges.Sum(c => c.GrossAmount);
                var notes = (await _db.CreditNotes.AsNoTracking().Where(n => cids.Contains(n.ChargeId)).Select(n => n.Amount).ToListAsync()).Sum();
                var disc = (await _db.DiscountDocuments.AsNoTracking().Where(d => cids.Contains(d.ChargeId)).Select(d => d.Amount).ToListAsync()).Sum();
                var paid = (await _db.PaymentAllocations.AsNoTracking().Where(a => cids.Contains(a.ChargeId)).Select(a => a.AllocatedAmount).ToListAsync()).Sum();
                statement.Add(new FamilyStatementLine(s, gross, notes, disc, paid, Sms.Application.Fees.StudentFinancialPositionCalculator.Calculate(gross, notes, disc, paid), charges.Count));
            }

            return new ParentFileViewModel
            {
                Parent = p, ActiveTab = tab ?? "identity", FamilyStatement = statement,
                ResidencePath = (await ResidencePathAsync(p)).Path,
                Children = links.Where(l => l.EffectiveToUtc == null).Select(C).ToList(),
                PastChildren = links.Where(l => l.EffectiveToUtc != null).Select(C).ToList(),
                PossibleDuplicates = dups, PortalUserName = portal, IdTypes = await IdTypesAsync(),
                EducationLevels = await LookupAsync("EducationLevel"),
                Audit = audit.Select(a => (a.Action.ToString(), a.FieldName, a.OldValue, a.NewValue, a.OccurredAtUtc, a.ActorUserId, a.Reason)).ToList(),
            };
        }

        [HttpPost("{id:int}/edit")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Parents, ScreenCatalog.Parents.File, ActionVerb.Edit)]
        public async Task<IActionResult> Edit(int id, ParentFormViewModel form)
        {
            try
            {
                Require(form.NameAr, "Name (Arabic)", "الاسم (عربي)"); Require(form.NameEn, "Name (English)", "الاسم (إنجليزي)"); Require(form.PrimaryMobile, "Primary mobile", "الجوال الأساسي");
                var mobile = form.PrimaryMobile!.Trim();
                if (await _db.Parents.AsNoTracking().AnyAsync(x => x.Id != id && x.PrimaryMobile == mobile)) throw new InvalidOperationException(T("Another parent already uses this mobile (BR-PAR-002).", "ولي أمر آخر يستخدم هذا الجوال (BR-PAR-002)."));
                _audit.Reason = string.IsNullOrWhiteSpace(form.Reason) ? null : form.Reason;
                var idNo = Blank(form.PrimaryIdNo);
                if (idNo != null && await _db.Parents.AsNoTracking().AnyAsync(x => x.Id != id && x.PrimaryIdNo == idNo))
                {
                    throw new InvalidOperationException(T("Another parent already carries this ID number (BR-PAR-002).", "ولي أمر آخر يحمل رقم الهوية هذا (BR-PAR-002)."));
                }

                await _parents.UpdateParentAsync(
                    id, form.NameAr!.Trim(), form.NameEn!.Trim(), mobile, form.Email, form.Address, form.OccupationEmployer, form.PreferredLanguage,
                    form.PrimaryIdTypeLookupId, idNo, form.LifeStatus, form.LifeStatusNote, form.EducationLookupId);
                TempData["Flash"] = T("Parent file updated.", "تم تحديث ملف ولي الأمر.");
            }
            catch (InvalidOperationException ex)
            {
                // A refusal must not cost the user the correction it is refusing. Redirecting back
                // to the file would redraw the stored row and silently discard everything just
                // typed, so the tab is re-rendered here with the submitted values still in it.
                TempData["Error"] = UserMessage.For(ex, IsArabic);
                var vm = await BuildFileAsync(id, "identity");
                if (vm == null) return NotFound();
                vm.Submitted = form;
                return View(nameof(File), vm);
            }

            return RedirectToAction(nameof(File), new { id, tab = "identity" });
        }

        // ------------------------------------------------------------------ residence
        //
        // The picker that edits this sits beside every parent dropdown in the product rather than only
        // on the parent file, because the moment somebody notices the address is wrong is the moment
        // they are choosing that parent for something else. These four endpoints are what it talks to.

        /// <summary>
        /// Walks the residence hierarchy up from what the parent actually stores — the locality and,
        /// where there is one, the quarter — and returns the governorate it lands on with the three
        /// levels joined for reading.
        /// </summary>
        private async Task<(int? GovernorateId, string? Path)> ResidencePathAsync(Parent parent)
        {
            var area = parent.ResidenceAreaId is int areaId
                ? await _db.ResidenceAreas.AsNoTracking().SingleOrDefaultAsync(a => a.Id == areaId)
                : null;
            var hood = parent.NeighbourhoodId is int hoodId
                ? await _db.Neighbourhoods.AsNoTracking().SingleOrDefaultAsync(n => n.Id == hoodId)
                : null;
            var governorate = area == null
                ? null
                : await _db.Governorates.AsNoTracking().SingleOrDefaultAsync(g => g.Id == area.GovernorateId);

            static string? Nm(LocalizedName? n) => n == null ? null : (IsArabic ? n.NameAr : n.NameEn);
            var path = string.Join(" · ", new[] { Nm(governorate?.Name), Nm(area?.Name), Nm(hood?.Name) }.Where(x => !string.IsNullOrWhiteSpace(x)));
            return (governorate?.Id, string.IsNullOrWhiteSpace(path) ? null : path);
        }

        /// <summary>The localities of one governorate, fetched as the level above it changes.</summary>
        [HttpGet("residence/areas")]
        [RequirePermission(ScreenCatalog.Modules.Parents, ScreenCatalog.Parents.File, ActionVerb.View)]
        public async Task<IActionResult> ResidenceAreas(int governorateId)
        {
            var areas = await _db.ResidenceAreas.AsNoTracking()
                .Where(a => a.GovernorateId == governorateId)
                .OrderBy(a => a.SortOrder)
                .Select(a => new { id = a.Id, ar = a.Name.NameAr, en = a.Name.NameEn })
                .ToListAsync();
            return Json(areas);
        }

        /// <summary>The quarters of one locality — empty for most of them, which is not an error.</summary>
        [HttpGet("residence/neighbourhoods")]
        [RequirePermission(ScreenCatalog.Modules.Parents, ScreenCatalog.Parents.File, ActionVerb.View)]
        public async Task<IActionResult> ResidenceNeighbourhoods(int areaId)
        {
            var hoods = await _db.Neighbourhoods.AsNoTracking()
                .Where(n => n.ResidenceAreaId == areaId)
                .OrderBy(n => n.SortOrder)
                .Select(n => new { id = n.Id, ar = n.Name.NameAr, en = n.Name.NameEn })
                .ToListAsync();
            return Json(hoods);
        }

        /// <summary>
        /// Where one parent lives, on a page of its own rather than in a dialog: the picker button sits
        /// inside somebody else's form on four screens, and a dialog carrying a second form inside the
        /// first is not something a browser will parse.
        /// </summary>
        [HttpGet("{id:int}/residence")]
        [RequirePermission(ScreenCatalog.Modules.Parents, ScreenCatalog.Parents.File, ActionVerb.View)]
        public async Task<IActionResult> Residence(int id, string? returnUrl = null)
        {
            var vm = await BuildResidenceAsync(id, returnUrl);
            return vm == null ? NotFound() : View(vm);
        }

        /// <summary>
        /// The picker as it opens on what is stored, built apart from the action so that a refused
        /// save can redraw this page with the selection that was refused still on it. Null when
        /// this school has no such parent.
        /// </summary>
        private async Task<ParentResidenceViewModel?> BuildResidenceAsync(int id, string? returnUrl)
        {
            var parent = await _db.Parents.IgnoreQueryFilters().AsNoTracking()
                .SingleOrDefaultAsync(p => p.Id == id && p.SchoolId == _db.CurrentSchoolId);
            if (parent == null) return null;

            // The governorate is not stored — it is walked up to, which is the whole point of keeping a
            // hierarchy rather than three loose fields that can drift apart.
            var (governorateId, path) = await ResidencePathAsync(parent);

            return new ParentResidenceViewModel
            {
                Parent = parent,
                Governorates = await _db.Governorates.AsNoTracking().OrderBy(g => g.SortOrder).ToListAsync(),
                CurrentGovernorateId = governorateId,
                CurrentAreaId = parent.ResidenceAreaId,
                CurrentNeighbourhoodId = parent.NeighbourhoodId,
                CurrentPath = path,
                ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null,
                CanEdit = await _permissions.HasPermissionAsync(
                    ScreenCatalog.Modules.Parents, ScreenCatalog.Parents.File, ActionVerb.Edit, HttpContext.RequestAborted),
            };
        }

        /// <summary>
        /// The governorate a submitted selection sits under, so a refused save can reopen the picker
        /// where the user left it. The locality answers it whenever there is one; a quarter chosen
        /// without its locality — the very selection <c>SetResidenceAsync</c> refuses — is walked up
        /// from instead, because reopening on a blank picker is the loss this exists to prevent.
        /// </summary>
        private async Task<int?> GovernorateOfAsync(int? residenceAreaId, int? neighbourhoodId)
        {
            if (residenceAreaId == null && neighbourhoodId is int hoodId)
            {
                residenceAreaId = await _db.Neighbourhoods.AsNoTracking()
                    .Where(n => n.Id == hoodId).Select(n => (int?)n.ResidenceAreaId).SingleOrDefaultAsync();
            }

            return residenceAreaId is int areaId
                ? await _db.ResidenceAreas.AsNoTracking().Where(a => a.Id == areaId).Select(a => (int?)a.GovernorateId).SingleOrDefaultAsync()
                : null;
        }

        [HttpPost("{id:int}/residence")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Parents, ScreenCatalog.Parents.File, ActionVerb.Edit)]
        public async Task<IActionResult> SaveResidence(int id, int? residenceAreaId, int? neighbourhoodId, string? reason, string? returnUrl)
        {
            try
            {
                _audit.Reason = string.IsNullOrWhiteSpace(reason) ? null : reason;
                await _parents.SetResidenceAsync(id, residenceAreaId, neighbourhoodId);
                TempData["Flash"] = T("Residence updated.", "تم تحديث السكن.");
            }
            catch (InvalidOperationException ex)
            {
                // The same loss as the identity tab, one screen along: redirecting away from a refusal
                // takes the three-level selection and the reason with it, and drops the message on
                // whichever page the picker was opened from. The page is redrawn on what was chosen.
                TempData["Error"] = UserMessage.For(ex, IsArabic);
                var vm = await BuildResidenceAsync(id, returnUrl);
                if (vm == null) return NotFound();
                vm.CurrentGovernorateId = await GovernorateOfAsync(residenceAreaId, neighbourhoodId);
                vm.CurrentAreaId = residenceAreaId;
                vm.CurrentNeighbourhoodId = neighbourhoodId;
                vm.SubmittedReason = reason;
                return View(nameof(Residence), vm);
            }

            // Back where the editing started: the picker lives on four other screens, and being
            // returned to the parent file from a half-finished admission form is its own small loss.
            return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(File), new { id, tab = "identity" });
        }

        [HttpPost("{id:int}/delete")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Parents, ScreenCatalog.Parents.File, ActionVerb.Deactivate)]
        public async Task<IActionResult> Delete(int id, string? q, string? filter)
        {
            var p = await _db.Parents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (p == null) return NotFound();
            try
            {
                await _parents.DeleteParentAsync(id);
                TempData["Flash"] = T($"Parent {p.ParentFileNo} deleted.", $"تم حذف ولي الأمر {p.ParentFileNo}.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Index), new { q, filter });
        }

        [HttpGet("dedup")]
        [RequirePermission(ScreenCatalog.Modules.Parents, ScreenCatalog.Parents.Dedup, ActionVerb.View)]
        public async Task<IActionResult> Dedup()
        {
            var parents = await _db.Parents.AsNoTracking().OrderBy(p => p.Id).ToListAsync();
            var links = await _db.StudentGuardianLinks.AsNoTracking().Where(l => l.EffectiveToUtc == null).ToListAsync();
            var pairs = new List<DedupWorkbenchViewModel.Pair>();
            for (var i = 0; i < parents.Count; i++)
            {
                for (var j = i + 1; j < parents.Count; j++)
                {
                    var a = parents[i]; var b = parents[j];
                    string? reason = a.PrimaryMobile == b.PrimaryMobile ? T("same mobile", "نفس الجوال")
                        : string.Equals(a.NameEn, b.NameEn, StringComparison.OrdinalIgnoreCase) || a.NameAr == b.NameAr ? T("same name", "نفس الاسم")
                        : a.Email != null && string.Equals(a.Email, b.Email, StringComparison.OrdinalIgnoreCase) ? T("same email", "نفس البريد") : null;
                    if (reason != null) pairs.Add(new DedupWorkbenchViewModel.Pair(a, b, reason, links.Count(l => l.ParentId == a.Id), links.Count(l => l.ParentId == b.Id)));
                }
            }

            return View(new DedupWorkbenchViewModel { Pairs = pairs });
        }

        /// <summary>
        /// The field name travels in both languages: an Arabic refusal naming an English field is
        /// half a message, and this is the refusal the identity tab produces most often.
        /// </summary>
        private static void Require(string? v, string en, string ar)
        {
            if (string.IsNullOrWhiteSpace(v)) throw new InvalidOperationException(T($"{en} is required.", $"الحقل {ar} مطلوب."));
        }

        private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        /// <summary>The picker offers what a school can still choose, so the list keeps the soft-active filter.</summary>
        private Task<IReadOnlyList<(int Id, string Ar, string En)>> IdTypesAsync() => LookupAsync("IdType");

        private async Task<IReadOnlyList<(int Id, string Ar, string En)>> LookupAsync(string category)
        {
            var cat = await _db.LookupCategories.AsNoTracking().SingleOrDefaultAsync(c => c.Code == category);
            return cat == null ? Array.Empty<(int, string, string)>() : await _db.LookupValues.AsNoTracking().Where(v => v.LookupCategoryId == cat.Id).OrderBy(v => v.SortOrder).Select(v => new ValueTuple<int, string, string>(v.Id, v.Name.NameAr, v.Name.NameEn)).ToListAsync();
        }
    }
}
