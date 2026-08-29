using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Domain.Schools;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;
using Sms.Web.Navigation;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _db;
        private readonly WorkspaceBuilder _workspaces;
        private readonly Sms.Web.Services.SchoolBrandMark _brandMark;

        public HomeController(AppDbContext db, WorkspaceBuilder workspaces, Sms.Web.Services.SchoolBrandMark brandMark)
        {
            _db = db;
            _workspaces = workspaces;
            _brandMark = brandMark;
        }

        [NoPermissionRequired("The shell's landing page; every tile behind it is gated on its own.")]
        public async Task<IActionResult> Index()
        {
            // AppDbContext already applies the tenant filter (E-002), so these
            // are the working school's numbers, not cross-tenant totals.
            var school = await _db.Schools.AsNoTracking().OrderBy(s => s.Id).FirstOrDefaultAsync();
            var year = await _db.AcademicYears.AsNoTracking()
                .Where(y => y.Status == AcademicYearStatus.Active)
                .OrderByDescending(y => y.StartDate)
                .FirstOrDefaultAsync()
                ?? await _db.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartDate).FirstOrDefaultAsync();

            var model = new HomeDashboardViewModel
            {
                SchoolNameEn = school?.NameEn,
                SchoolNameAr = school?.NameAr,
                SchoolStatus = school?.Status.ToString(),
                ActiveYearLabelEn = year?.LabelEn,
                ActiveYearLabelAr = year?.LabelAr,
                ActiveYearStatus = year?.Status.ToString(),
                Students = await _db.Students.CountAsync(),
                Employees = await _db.Employees.CountAsync(),
                Sections = await _db.Sections.CountAsync(),
                Parents = await _db.Parents.CountAsync(),
                Workspaces = await _workspaces.BuildAllAsync(User, HttpContext.RequestAborted),
            };

            return View(model);
        }

        /// <summary>
        /// One department's screens. Reached from the landing page's tiles; a department this user
        /// may open nothing in answers 404, like every other screen they do not hold — the page must
        /// not become a way to enumerate what exists behind permissions one does not have
        /// (BR-SEC-010).
        /// </summary>
        [HttpGet("section/{key}")]
        [NoPermissionRequired("A list of links, each of which the user already holds; it reads nothing else.")]
        public async Task<IActionResult> Section(string key)
        {
            var workspace = await _workspaces.BuildAsync(key, User, HttpContext.RequestAborted);
            if (workspace == null)
            {
                return NotFound();
            }

            return View(workspace);
        }

        /// <summary>
        /// The school's logo, as the shell draws it beside the product name and beside the school's
        /// own name (BR-SCH-006). Separate from <c>/school/branding/{asset}</c>, which serves the
        /// same file to the profile screen: that one belongs to the Schools module and is gated on
        /// opening the profile, while this is chrome — a teacher who may not read the school's
        /// licence details still reads its name on every page, and the mark beside that name says
        /// no more than the name does. The seal is deliberately not served here; it is the mark
        /// that authenticates a document, and it has no business in a navigation bar.
        /// <para>
        /// <paramref name="v"/> is the current version number, put on the URL by the caller so a
        /// replaced logo is a new address rather than a stale cache entry. It is not read: the
        /// action always serves what the slot holds now.
        /// </para>
        /// <para>
        /// Anonymous, because the sign-in screen wears the same mark: a school signing its own
        /// people in shows them the mark they came for, and refusing it there would leave the one
        /// screen every reader passes through wearing a different logo from every screen behind it.
        /// The deployment is single-tenant (<c>StaticTenantContext</c>), so there is no other
        /// school's mark this could reach, and a school's logo is the least private thing it owns —
        /// it is on the gate, the letterhead and the website already.
        /// </para>
        /// </summary>
        [HttpGet("brand/logo")]
        [AllowAnonymous]
        [NoPermissionRequired("The school's own logo, drawn beside its name in the shell and on the sign-in screen; it discloses no more than the name already on every page.")]
        public async Task<IActionResult> BrandLogo(int v = 0)
        {
            Sms.Web.Services.AttachmentIntake.StoredFile? file;
            try
            {
                file = await _brandMark.ReadAsync(HttpContext.RequestAborted);
            }
            catch (System.IO.IOException)
            {
                // A branding row whose file has gone missing — a database restored without its file
                // store beside it, an App_Data moved by hand — is a 404, not a 500. The slot says a
                // logo exists, so the page has already drawn the <img>; answering that request with
                // a stack trace would put an unauthenticated server error on the sign-in screen,
                // which is the one page every reader of this product passes through.
                return NotFound();
            }

            if (file == null) { return NotFound(); }

            // Private, because a school's chrome is served to its own readers and not to a shared
            // cache; a day, because the version stamp above is what invalidates it.
            Response.Headers["Cache-Control"] = "private, max-age=86400";
            return File(file.Content, file.ContentType);
        }

        [NoPermissionRequired("Static text.")]
        public IActionResult Privacy()
        {
            return View();
        }

        /// <summary>Language toggle: persists the culture cookie and returns to the caller (doc/DesignSystem/02 — one page, dynamic direction).</summary>
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [NoPermissionRequired("Switching one's own display language.")]
        public IActionResult SetLanguage(string culture, string? returnUrl)
        {
            var requested = culture == "ar" ? "ar-SA" : "en-US";
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(requested)),
                new Microsoft.AspNetCore.Http.CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });

            return LocalRedirect(string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl) ? "/" : returnUrl);
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        [NoPermissionRequired("The error page.")]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
