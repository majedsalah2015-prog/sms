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

        public HomeController(AppDbContext db, WorkspaceBuilder workspaces)
        {
            _db = db;
            _workspaces = workspaces;
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
