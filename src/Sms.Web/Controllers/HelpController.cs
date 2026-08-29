using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Sms.Application.Security;
using Sms.Application.Setup;
using Sms.Domain.Security;
using Sms.Web.Models;
using Sms.Web.Navigation;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// The product's user guide — how its screens are driven — reached from the help button that the
    /// shell puts in the top bar of every page, staff and portal alike.
    /// <para>
    /// Every screen already carries a panel of its own, held there by
    /// <c>Sms.Web.Tests/HelpCoverageTests</c>. This is the other half: the conventions all of those
    /// screens assume their reader has already been told, which no one screen is the right place to
    /// say and which were, until now, only written in <c>docs/</c> — where the person filling in the
    /// form does not have them open.
    /// </para>
    /// <para>
    /// The index at the foot is built from the same two catalogues the menu is
    /// (<see cref="ModuleCatalog"/>, <see cref="ScreenCatalog"/>) and filtered the same two ways — by
    /// role (BR-GLB-070) and by feature toggle (BR-SET-006) — so the guide describes the reader's own
    /// product and never advertises a screen they would be answered not-found on.
    /// </para>
    /// </summary>
    public sealed class HelpController : Controller
    {
        private readonly ModuleVisibility _visibility;
        private readonly ISystemSetupAdmin _setup;
        private readonly IPermissionService _permissions;

        public HelpController(ModuleVisibility visibility, ISystemSetupAdmin setup, IPermissionService permissions)
        {
            _visibility = visibility;
            _setup = setup;
            _permissions = permissions;
        }

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        /// <summary>
        /// The guide. Two audiences: a portal account gets the portal's own four chapters and no
        /// index at all, because BR-SEC-010 keeps staff screens not merely closed to it but
        /// unannounced — and a list of screens is an announcement.
        /// </summary>
        [NoPermissionRequired("The product's own instructions. It reads the two catalogues the menu already reads and lists only screens this user holds, so it discloses nothing a permission was protecting.")]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var arabic = IsArabic;
            ViewData["Title"] = arabic ? "دليل الاستخدام" : "User guide";
            ViewData["Breadcrumb"] = arabic ? "المساعدة / دليل الاستخدام" : "Help / User guide";

            if (PortalAreaFilter.IsPortalAccount(User.FindFirst(SmsClaimTypes.AccountType)?.Value))
            {
                return View(new PlatformGuideViewModel
                {
                    ForPortal = true,
                    Sections = PlatformGuide.ForPortal(arabic),
                });
            }

            return View(new PlatformGuideViewModel
            {
                Sections = PlatformGuide.ForStaff(arabic),
                Modules = await BuildIndexAsync(arabic, cancellationToken),
            });
        }

        /// <summary>
        /// Every module this user can open something in, and inside it every screen they hold a verb
        /// on, with the verbs themselves — which is the answer to "the screen opened but the button
        /// is missing" that no screen can give about itself.
        /// <para>
        /// Costs one query: <c>PermissionService</c> loads the user's assignments once per request
        /// and evaluates the rest in memory, so asking about every screen in the catalogue is the
        /// same round trip as asking about one.
        /// </para>
        /// </summary>
        private async Task<IReadOnlyList<GuideModule>> BuildIndexAsync(bool arabic, CancellationToken cancellationToken)
        {
            var featureStates = await _setup.GetFeatureStatesAsync(cancellationToken);
            var index = new List<GuideModule>();

            foreach (var module in ModuleCatalog.Modules)
            {
                // BR-SET-006 first: a module this deployment switched off is gone for everybody, and
                // asking about permissions on it would be asking about a screen that is not there.
                if (FeatureCatalog.ForModule(module.Code) is { } feature && !featureStates[feature.Code])
                {
                    continue;
                }

                var screens = new List<GuideScreen>();
                foreach (var screen in ScreenCatalog.ForModule(module.Code))
                {
                    var verbs = new List<string>();
                    foreach (var verb in screen.Verbs)
                    {
                        if (await _permissions.HasPermissionAsync(module.Code, screen.ScreenCode, verb, cancellationToken))
                        {
                            verbs.Add(Labels.Verb(verb, arabic));
                        }
                    }

                    if (verbs.Count > 0)
                    {
                        screens.Add(new GuideScreen(arabic ? screen.TitleAr : screen.TitleEn, verbs));
                    }
                }

                if (screens.Count == 0)
                {
                    continue;
                }

                // The module's own entry screen, but only where this user may actually open it: a
                // linked heading they are answered not-found on is worse than a plain one, and some
                // modules have no screens of their own yet at all.
                var canOpenModule = module.ScreenController != null && await _visibility.CanSeeAsync(module.Code, cancellationToken);

                index.Add(new GuideModule(
                    module.Code,
                    module.Number,
                    arabic ? module.TitleAr : module.TitleEn,
                    module.Icon,
                    canOpenModule ? module.ScreenController : null,
                    canOpenModule ? module.ScreenAction : null,
                    screens));
            }

            return index;
        }
    }
}
