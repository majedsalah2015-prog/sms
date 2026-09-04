using System;
using Microsoft.AspNetCore.Mvc;
using Sms.Web.Models;
using Sms.Web.Security;
using Sms.Web.Services;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// Getting the school's Android app onto a phone.
    /// <para>
    /// <b>Not part of approved Analysis v1.0.</b> Native mobile apps sit in
    /// <c>Future/</c> as GAP <b>G5</b> / roadmap <b>R2</b>, so no module doc
    /// numbers this screen and no <c>BR-</c> rule governs it. Built on the
    /// owner's request (2026-09-03).
    /// </para>
    /// <para>
    /// A separate file rather than more lines in <c>PortalController.cs</c>:
    /// that file is large, shared, and edited by other work in flight.
    /// </para>
    /// </summary>
    public partial class PortalController
    {
        /// <summary>
        /// The download page.
        /// <para>
        /// <b><see cref="NoPermissionRequiredAttribute"/>, deliberately.</b> The
        /// three audiences the owner named — student, guardian, teacher — do not
        /// share a permission: a family holds <c>POR/*</c> and a teacher holds
        /// none of it, so any <c>POR</c> gate here would hide the page from the
        /// staff half, and a new catalogue entry would be 404 for everybody
        /// (system administrator included) until <c>tools/Sms.Seeder</c> is
        /// re-run on every deployment. What is served is also not data: it is
        /// the school's own client software, and the sign-in behind it is what
        /// decides who ever sees a record. The global
        /// <c>FallbackPolicy</c> still requires an authenticated user, so this
        /// is not a public endpoint.
        /// </para>
        /// </summary>
        [HttpGet("app")]
        [NoPermissionRequired(
            "The school's own app installer, not a record. Student, guardian and teacher hold no " +
            "permission in common, and sign-in is what gates every screen the app then opens.")]
        public IActionResult App()
        {
            var package = HttpContext.RequestServices
                .GetService(typeof(MobileAppPackage)) as MobileAppPackage;

            var current = package?.Current();
            return View(new PortalMobileAppViewModel
            {
                FileName = current?.FileName,
                SizeBytes = current?.SizeBytes,
                PublishedAtUtc = current?.ModifiedUtc,
                Version = current?.Version,
            });
        }

        /// <summary>
        /// The bytes.
        /// <para>
        /// The path served is the one <see cref="MobileAppPackage"/> resolved,
        /// never anything the caller sent — there is no file name in the route
        /// for a traversal to travel through. <c>enableRangeProcessing</c> is on
        /// because this is tens of megabytes over a phone's connection, and a
        /// download that cannot resume is one a parent on a weak signal never
        /// finishes.
        /// </para>
        /// </summary>
        [HttpGet("app/download")]
        [NoPermissionRequired("The file behind the page above; same reasoning.")]
        public IActionResult AppDownload()
        {
            var package = HttpContext.RequestServices
                .GetService(typeof(MobileAppPackage)) as MobileAppPackage;

            var current = package?.Current();
            if (current == null)
            {
                // Nothing published. Not an error page: the family is sent back to
                // the screen that says so in their own language.
                TempData["Error"] = T(
                    "The app has not been published yet. Ask the school office.",
                    "لم يُنشر التطبيق بعد. راجع إدارة المدرسة.");
                return RedirectToAction(nameof(App));
            }

            System.IO.Stream stream;
            try
            {
                stream = System.IO.File.OpenRead(current.FullPath);
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException)
            {
                // The folder listed a file and the disk will not produce it. From the
                // family's side that is the same as nothing being published, and an
                // unhandled exception page is neither translated nor actionable.
                TempData["Error"] = T(
                    "The app could not be sent just now. Try again in a moment.",
                    "تعذّر إرسال التطبيق الآن. أعد المحاولة بعد قليل.");
                return RedirectToAction(nameof(App));
            }

            return File(
                stream,
                "application/vnd.android.package-archive",
                current.FileName,
                enableRangeProcessing: true);
        }
    }
}
