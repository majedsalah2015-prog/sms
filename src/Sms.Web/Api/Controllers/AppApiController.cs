using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sms.Web.Api.Models;
using Sms.Web.Security;
using Sms.Web.Services;

namespace Sms.Web.Api.Controllers
{
    /// <summary>
    /// What build of the school's app the family should be running
    /// (docs/Integration/03-Mobile-API.md §5).
    /// <para>
    /// The school already publishes its Android package by dropping a file into
    /// a folder, and <c>/portal/app</c> already serves it with its install
    /// instructions. What was missing was anyone telling the phone: a family that
    /// installed once had no way to learn a newer build existed, so a fix shipped
    /// to answer a school's complaint sat on the server while the complaint
    /// carried on arriving. This endpoint is the missing half of that
    /// arrangement, and nothing more — it publishes nothing, decides nothing
    /// about the family, and touches no record.
    /// </para>
    /// <para>
    /// <b>Not part of approved Analysis v1.0.</b> Native mobile apps are
    /// <c>Future/</c> GAP <b>G5</b> / roadmap <b>R2</b>, so no module doc
    /// numbers this and no <c>BR-</c> rule governs it. Built on the owner's
    /// request (2026-09-05).
    /// </para>
    /// <para>
    /// <b>This is not a push notification.</b> Push needs a device registry and a
    /// provider decision, both still listed as pending in <c>docs/Status/</c>,
    /// and neither is invented here. The phone asks on launch; the school
    /// answers. A family that never opens the app is never told, and that is the
    /// honest limit of what this can do.
    /// </para>
    /// </summary>
    [Route(V1 + "/app")]
    [PortalReachable]
    public sealed class AppApiController : ApiControllerBase
    {
        private readonly MobileAppPackage _packages;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AppApiController> _logger;

        public AppApiController(
            MobileAppPackage packages,
            IConfiguration configuration,
            ILogger<AppApiController> logger)
        {
            _packages = packages;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// The newest published build, and whether the caller is behind it.
        /// <para>
        /// <b><see cref="AllowAnonymousAttribute"/>, deliberately.</b> The case
        /// this exists for at its sharpest is a client too old to sign in: if the
        /// check needed a token, that phone would show a sign-in failure instead
        /// of "update the app", which is the one message that would actually help
        /// it. It is also the first call of a cold start, before the keystore has
        /// been read. What it discloses is a version string and a path the school
        /// hands to every family anyway — no school name, no person, no record —
        /// so the reconnaissance value that keeps <c>/api/docs</c> out of
        /// production does not apply. The package itself stays behind sign-in,
        /// exactly as before.
        /// </para>
        /// <para>
        /// The caller's build arrives as two parameters rather than one
        /// <c>1.1.0+2</c> string because <c>+</c> in a query string decodes to a
        /// space: the one-parameter form would have to be percent-encoded by
        /// every client, forever, and the failure when one forgot would be a
        /// version that silently parsed as <c>1.1.0</c> with no build.
        /// </para>
        /// <para>
        /// An unreadable <paramref name="version"/> is answered rather than
        /// refused. A check endpoint that returns <c>400</c> is a check that
        /// quietly never runs again, and the facts are still worth sending: the
        /// verdicts simply stay false, because a build this cannot order is one
        /// it cannot honestly call out of date.
        /// </para>
        /// </summary>
        [HttpGet("version")]
        [AllowAnonymous]
        [PasswordChangeExempt]
        [NoPermissionRequired(
            "The school's own client software announcing itself, not a record. It must answer a " +
            "phone too old to sign in — that is the whole point — and it discloses only the " +
            "version the school publishes to every family.")]
        public ActionResult<ApiAppVersionResponse> Version(
            [FromQuery] string? version = null,
            [FromQuery] int? build = null)
        {
            var response = new ApiAppVersionResponse
            {
                // ~/ rather than a literal: a deployment behind a reverse proxy
                // with a path base would otherwise be sent to the wrong host root.
                // The annotation allows null; the path is the same one either way,
                // and an empty install link is the one field worth a fallback.
                InstallUrl = Url.Content("~/portal/app") ?? "/portal/app",
            };

            var package = _packages.Current();
            MobileAppVersion latest = default;
            var hasLatest = package != null && MobileAppVersion.TryParse(package.Version, out latest);

            if (package != null)
            {
                response.Published = true;
                response.PublishedAtUtc = package.ModifiedUtc;

                if (hasLatest)
                {
                    response.LatestVersion = latest.ToVersionString();
                    response.LatestBuild = latest.HasBuild ? latest.Build : null;
                }
            }

            var hasMinimum = TryReadMinimum(out var minimum);
            if (hasMinimum)
            {
                response.MinimumVersion = minimum.ToVersionString();
                response.MinimumBuild = minimum.HasBuild ? minimum.Build : null;
            }

            var hasCaller = MobileAppVersion.TryParse(Compose(version, build), out var caller);

            if (hasCaller && hasLatest)
            {
                response.UpdateAvailable = caller < latest;
            }

            if (hasCaller && hasMinimum && caller < minimum)
            {
                // A minimum nobody can reach is a locked door with no key: it would
                // stop every family using the app and send them to a page offering
                // an older build, or none. The school's mistake is not the family's
                // to absorb, so the requirement is withheld and the operator is told.
                if (hasLatest && minimum <= latest)
                {
                    response.UpdateRequired = true;
                }
                else
                {
                    _logger.LogWarning(
                        "MobileApp:MinimumSupportedVersion is {Minimum}, but the newest published package is {Latest}. " +
                        "No family can satisfy it, so no update is being required. Publish that build or lower the setting.",
                        minimum.ToString(),
                        package?.Version ?? "(nothing published)");
                }
            }

            return response;
        }

        /// <summary>
        /// The school's floor, or none. A value that does not parse is treated as
        /// none and logged: a typo in one configuration key must not be able to
        /// lock every family out of the app, which is what the other reading of
        /// an unreadable minimum would do.
        /// </summary>
        private bool TryReadMinimum(out MobileAppVersion minimum)
        {
            var configured = _configuration["MobileApp:MinimumSupportedVersion"];
            if (string.IsNullOrWhiteSpace(configured))
            {
                minimum = default;
                return false;
            }

            if (MobileAppVersion.TryParse(configured, out minimum))
            {
                return true;
            }

            _logger.LogWarning(
                "MobileApp:MinimumSupportedVersion is {Configured}, which is not a version this can order. " +
                "No update is being required. Use the form 1.4.0 or 1.4.0+12.",
                configured);
            return false;
        }

        /// <summary>Puts the two query parameters back into the one form the parser reads.</summary>
        private static string? Compose(string? version, int? build)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                return null;
            }

            var trimmed = version.Trim();
            return build is > 0 && !trimmed.Contains('+')
                ? FormattableString.Invariant($"{trimmed}+{build.Value}")
                : trimmed;
        }
    }
}
