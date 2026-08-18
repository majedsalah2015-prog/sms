using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Domain.Schools;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;

namespace Sms.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _db;

        public HomeController(AppDbContext db)
        {
            _db = db;
        }

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
                AuditEntries = await _db.AuditEntries.CountAsync(),
            };

            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        /// <summary>Language toggle: persists the culture cookie and returns to the caller (doc/DesignSystem/02 — one page, dynamic direction).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetLanguage(string culture, string? returnUrl)
        {
            var requested = culture == "ar" ? "ar-SA" : "en-US";
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(requested)),
                new Microsoft.AspNetCore.Http.CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });

            return LocalRedirect(string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl) ? "/" : returnUrl);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
