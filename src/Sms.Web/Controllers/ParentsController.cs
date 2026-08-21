using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Parents;
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

        public ParentsController(IParentAdmin parents, AppDbContext db, IAuditContext audit)
        {
            _parents = parents;
            _db = db;
            _audit = audit;
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
        public IActionResult Register() => View(new ParentFormViewModel());

        [HttpPost("new")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Parents, ScreenCatalog.Parents.Directory, ActionVerb.Create)]
        public async Task<IActionResult> Register(ParentFormViewModel form)
        {
            try
            {
                Require(form.NameAr, "Name (Arabic)"); Require(form.NameEn, "Name (English)"); Require(form.PrimaryMobile, "Primary mobile");
                var mobile = form.PrimaryMobile!.Trim();
                var dup = await _db.Parents.AsNoTracking().FirstOrDefaultAsync(p => p.PrimaryMobile == mobile);
                if (dup != null) throw new InvalidOperationException(T($"A parent with this mobile already exists ({dup.ParentFileNo}) — open that file instead (BR-PAR-002).", $"يوجد ولي أمر بهذا الجوال ({dup.ParentFileNo}) — افتح ملفه بدلاً من الإنشاء (BR-PAR-002)."));
                var p = await _parents.RegisterParentAsync(form.NameAr!.Trim(), form.NameEn!.Trim(), mobile, form.Email, form.Address, form.OccupationEmployer, form.PreferredLanguage);
                TempData["Flash"] = T($"Parent {p.ParentFileNo} created.", $"تم إنشاء ولي الأمر {p.ParentFileNo}.");
                return RedirectToAction(nameof(File), new { id = p.Id });
            }
            catch (InvalidOperationException ex) { ModelState.AddModelError(string.Empty, ex.Message); return View(form); }
        }

        [HttpGet("{id:int}")]
        [RequirePermission(ScreenCatalog.Modules.Parents, ScreenCatalog.Parents.File, ActionVerb.View)]
        public async Task<IActionResult> File(int id, string? tab = null)
        {
            var p = await _db.Parents.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.SchoolId == _db.CurrentSchoolId);
            if (p == null) return NotFound();
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

            return View(new ParentFileViewModel
            {
                Parent = p, ActiveTab = tab ?? "identity", FamilyStatement = statement,
                Children = links.Where(l => l.EffectiveToUtc == null).Select(C).ToList(),
                PastChildren = links.Where(l => l.EffectiveToUtc != null).Select(C).ToList(),
                PossibleDuplicates = dups, PortalUserName = portal,
                Audit = audit.Select(a => (a.Action.ToString(), a.FieldName, a.OldValue, a.NewValue, a.OccurredAtUtc, a.ActorUserId, a.Reason)).ToList(),
            });
        }

        [HttpPost("{id:int}/edit")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Parents, ScreenCatalog.Parents.File, ActionVerb.Edit)]
        public async Task<IActionResult> Edit(int id, ParentFormViewModel form)
        {
            try
            {
                Require(form.NameAr, "Name (Arabic)"); Require(form.NameEn, "Name (English)"); Require(form.PrimaryMobile, "Primary mobile");
                var mobile = form.PrimaryMobile!.Trim();
                if (await _db.Parents.AsNoTracking().AnyAsync(x => x.Id != id && x.PrimaryMobile == mobile)) throw new InvalidOperationException(T("Another parent already uses this mobile (BR-PAR-002).", "ولي أمر آخر يستخدم هذا الجوال (BR-PAR-002)."));
                _audit.Reason = string.IsNullOrWhiteSpace(form.Reason) ? null : form.Reason;
                await _parents.UpdateParentAsync(id, form.NameAr!.Trim(), form.NameEn!.Trim(), mobile, form.Email, form.Address, form.OccupationEmployer, form.PreferredLanguage);
                TempData["Flash"] = T("Parent file updated.", "تم تحديث ملف ولي الأمر.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(File), new { id, tab = "identity" });
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
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
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

        private static void Require(string? v, string f)
        {
            if (string.IsNullOrWhiteSpace(v)) throw new InvalidOperationException(T($"{f} is required.", $"الحقل {f} مطلوب."));
        }

        private async Task<IReadOnlyList<(int Id, string Ar, string En)>> LookupAsync(string category)
        {
            var cat = await _db.LookupCategories.AsNoTracking().SingleOrDefaultAsync(c => c.Code == category);
            return cat == null ? Array.Empty<(int, string, string)>() : await _db.LookupValues.AsNoTracking().Where(v => v.LookupCategoryId == cat.Id).OrderBy(v => v.SortOrder).Select(v => new ValueTuple<int, string, string>(v.Id, v.Name.NameAr, v.Name.NameEn)).ToListAsync();
        }
    }
}
