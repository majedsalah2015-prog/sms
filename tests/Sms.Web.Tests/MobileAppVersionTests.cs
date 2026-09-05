using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Sms.Web.Api.Controllers;
using Sms.Web.Api.Models;
using Sms.Web.Services;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// Telling a family their app is out of date, and — much more carefully —
    /// telling them they may not carry on using it.
    /// <para>
    /// Two failures are being guarded against, and only one of them is a wrong
    /// answer. The first is the silent one: ordinal string comparison makes
    /// <c>1.10.0</c> sort before <c>1.9.0</c>, so the first school to reach a
    /// tenth minor release would simply stop being offered updates, with nothing
    /// anywhere reporting a fault. The second is the loud one: a minimum
    /// supported version nobody can install locks every family out of the app
    /// and sends them to a page offering an older build. This holds both.
    /// </para>
    /// <para>
    /// <b>No <c>BR-</c> id.</b> Native mobile apps are <c>Future/</c> GAP G5 /
    /// roadmap R2 and no module doc numbers this, so there is no business rule to
    /// tag these with — see <c>docs/Integration/03-Mobile-API.md</c>.
    /// </para>
    /// </summary>
    public class MobileAppVersionTests
    {
        // ------------------------------------------------------------- parsing

        [Theory]
        [InlineData("1.4.0", 1, 4, 0, 0)]
        [InlineData("1.4.0+12", 1, 4, 0, 12)]
        [InlineData("1.4", 1, 4, 0, 0)]
        [InlineData("2", 2, 0, 0, 0)]
        [InlineData("  1.1.0+2  ", 1, 1, 0, 2)]
        [InlineData("v1.4.0+12", 1, 4, 0, 12)]
        public void Reads_the_forms_this_product_writes(
            string text, int major, int minor, int patch, int build)
        {
            Assert.True(MobileAppVersion.TryParse(text, out var version));
            Assert.Equal(major, version.Major);
            Assert.Equal(minor, version.Minor);
            Assert.Equal(patch, version.Patch);
            Assert.Equal(build, version.Build);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("latest")]
        [InlineData("1.4.0-beta")]
        [InlineData("1.4.0+")]
        [InlineData("1..0")]
        public void Refuses_what_it_cannot_order(string? text)
        {
            // Not a crash and not a zero. A lenient parse would make every one of
            // these read as 0.0.0 — "hopelessly out of date" — and force an update
            // on a phone whose only fault was sending something unexpected.
            Assert.False(MobileAppVersion.TryParse(text, out var version));
            Assert.Equal(0, version.Major);
        }

        // ---------------------------------------------------------- comparison

        [Fact]
        public void A_tenth_minor_release_is_newer_than_a_ninth()
        {
            // The whole reason this type exists: "1.10.0".CompareTo("1.9.0") is
            // negative, and a school reaching 1.10 would silently stop shipping.
            Assert.True(string.CompareOrdinal("1.10.0", "1.9.0") < 0);

            Assert.True(MobileAppVersion.TryParse("1.10.0", out var ten));
            Assert.True(MobileAppVersion.TryParse("1.9.0", out var nine));
            Assert.True(ten > nine);
        }

        [Fact]
        public void A_rebuilt_hotfix_is_newer_than_the_build_it_replaces()
        {
            // pubspec.yaml requires the +N half to move on every published build,
            // because a phone offered an APK whose versionCode it already has
            // treats the install as a no-op. A comparison that ignored it would
            // call these two the same build.
            Assert.True(MobileAppVersion.TryParse("1.4.0+13", out var fixedUp));
            Assert.True(MobileAppVersion.TryParse("1.4.0+12", out var original));
            Assert.True(fixedUp > original);
            Assert.NotEqual(original, fixedUp);
        }

        [Fact]
        public void Major_outranks_everything_below_it()
        {
            Assert.True(MobileAppVersion.TryParse("2.0.0", out var two));
            Assert.True(MobileAppVersion.TryParse("1.99.99+999", out var one));
            Assert.True(two > one);
        }

        [Fact]
        public void An_unstated_build_is_older_than_a_stated_one()
        {
            // `sms-portal-1.4.0.apk` next to `sms-portal-1.4.0+2.apk`: the second
            // is a later build of the same version, and nothing else is known.
            Assert.True(MobileAppVersion.TryParse("1.4.0", out var bare));
            Assert.True(MobileAppVersion.TryParse("1.4.0+2", out var numbered));
            Assert.False(bare.HasBuild);
            Assert.True(numbered.HasBuild);
            Assert.True(bare < numbered);
        }

        [Theory]
        [InlineData("1.4.0", "1.4.0")]
        [InlineData("1.4.0+12", "1.4.0+12")]
        [InlineData("1.4", "1.4.0")]
        public void Prints_back_the_form_it_read(string text, string expected)
        {
            Assert.True(MobileAppVersion.TryParse(text, out var version));
            Assert.Equal(expected, version.ToString());
        }

        [Fact]
        public void Reads_what_the_package_finder_hands_it()
        {
            // MobileAppPackage pulls the version out of the file name with its own
            // regex. If these two ever disagree the endpoint answers "published,
            // version unknown" and never offers an update, which looks like
            // nothing being wrong.
            using var folder = new TempFolder();
            folder.Publish("sms-portal-1.4.0+12.apk");

            var published = new MobileAppPackage(folder.Path).Current();

            Assert.NotNull(published);
            Assert.True(MobileAppVersion.TryParse(published!.Version, out var version));
            Assert.Equal("1.4.0+12", version.ToString());
        }

        // ------------------------------------------------------- the endpoint

        [Fact]
        public void Says_nothing_when_the_school_has_published_nothing()
        {
            using var folder = new TempFolder();

            var answer = Ask(folder, minimum: null, version: "1.1.0", build: 2);

            // "Nobody has published a build" is not "you are up to date", and it is
            // certainly not "update now" — there would be nothing to update to.
            Assert.False(answer.Published);
            Assert.Null(answer.LatestVersion);
            Assert.False(answer.UpdateAvailable);
            Assert.False(answer.UpdateRequired);
        }

        [Fact]
        public void Offers_a_newer_build_without_demanding_it()
        {
            using var folder = new TempFolder();
            folder.Publish("sms-portal-1.2.0+3.apk");

            var answer = Ask(folder, minimum: null, version: "1.1.0", build: 2);

            Assert.True(answer.Published);
            Assert.Equal("1.2.0", answer.LatestVersion);
            Assert.Equal(3, answer.LatestBuild);
            Assert.True(answer.UpdateAvailable);
            // No minimum is set, which is the default. Nothing is ever forced by
            // merely publishing.
            Assert.False(answer.UpdateRequired);
        }

        [Fact]
        public void Says_nothing_to_a_phone_already_on_the_published_build()
        {
            using var folder = new TempFolder();
            folder.Publish("sms-portal-1.2.0+3.apk");

            var answer = Ask(folder, minimum: "1.0.0", version: "1.2.0", build: 3);

            Assert.False(answer.UpdateAvailable);
            Assert.False(answer.UpdateRequired);
        }

        [Fact]
        public void Requires_the_update_once_the_school_sets_a_floor()
        {
            using var folder = new TempFolder();
            folder.Publish("sms-portal-1.2.0+3.apk");

            var answer = Ask(folder, minimum: "1.2.0", version: "1.1.0", build: 2);

            Assert.True(answer.UpdateAvailable);
            Assert.True(answer.UpdateRequired);
            Assert.Equal("1.2.0", answer.MinimumVersion);
        }

        [Fact]
        public void Will_not_require_a_build_no_family_could_install()
        {
            using var folder = new TempFolder();
            folder.Publish("sms-portal-1.2.0+3.apk");

            // The operator set the floor to a build that was never published — the
            // usual way being to raise it before uploading the APK. Enforcing it
            // would empty the app of every family at once and send them to a page
            // offering 1.2.0, which still would not satisfy it.
            var answer = Ask(folder, minimum: "1.9.0", version: "1.1.0", build: 2);

            Assert.False(answer.UpdateRequired);
            Assert.True(answer.UpdateAvailable);
        }

        [Fact]
        public void A_mistyped_floor_locks_nobody_out()
        {
            using var folder = new TempFolder();
            folder.Publish("sms-portal-1.2.0+3.apk");

            var answer = Ask(folder, minimum: "latest", version: "1.1.0", build: 2);

            Assert.Null(answer.MinimumVersion);
            Assert.False(answer.UpdateRequired);
        }

        [Fact]
        public void Answers_a_phone_whose_version_it_cannot_read()
        {
            using var folder = new TempFolder();
            folder.Publish("sms-portal-1.2.0+3.apk");

            var answer = Ask(folder, minimum: "1.2.0", version: null, build: null);

            // The facts, so a client that knows how can still decide; but no
            // verdict, because a build this cannot order is one it cannot honestly
            // call out of date — and refusing outright would turn the check into
            // one that quietly never runs.
            Assert.True(answer.Published);
            Assert.Equal("1.2.0", answer.LatestVersion);
            Assert.False(answer.UpdateAvailable);
            Assert.False(answer.UpdateRequired);
        }

        [Fact]
        public void Sends_the_family_to_the_page_that_already_explains_installing()
        {
            using var folder = new TempFolder();
            folder.Publish("sms-portal-1.2.0+3.apk");

            var answer = Ask(folder, minimum: null, version: "1.1.0", build: 2);

            Assert.Equal("/portal/app", answer.InstallUrl);
        }

        // -------------------------------------------------------------- harness

        private static ApiAppVersionResponse Ask(
            TempFolder folder,
            string? minimum,
            string? version,
            int? build)
        {
            var settings = new Dictionary<string, string>
            {
                ["MobileApp:PackagePath"] = folder.Path,
            };
            if (minimum != null)
            {
                settings["MobileApp:MinimumSupportedVersion"] = minimum;
            }

            var controller = new AppApiController(
                new MobileAppPackage(folder.Path),
                new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
                NullLogger<AppApiController>.Instance)
            {
                Url = new StubUrlHelper(),
            };

            var result = controller.Version(version, build);
            return Assert.IsType<ApiAppVersionResponse>(result.Value);
        }

        /// <summary>
        /// Only <see cref="IUrlHelper.Content"/> is reached, and it does here what
        /// it does on a deployment with no path base.
        /// </summary>
        private sealed class StubUrlHelper : IUrlHelper
        {
            public ActionContext ActionContext { get; } = new();

            public string Content(string? contentPath)
                => contentPath is not null && contentPath.StartsWith("~", StringComparison.Ordinal)
                    ? contentPath.Substring(1)
                    : contentPath ?? string.Empty;

            public string? Action(UrlActionContext actionContext) => null;

            public bool IsLocalUrl(string? url) => true;

            public string? Link(string? routeName, object? values) => null;

            public string? RouteUrl(UrlRouteContext routeContext) => null;
        }

        /// <summary>A publish folder of this test's own, removed with it.</summary>
        private sealed class TempFolder : IDisposable
        {
            public TempFolder()
            {
                Path = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "sms-app-version-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public string Path { get; }

            /// <summary>The bytes are never read — only the name and the write time.</summary>
            public void Publish(string fileName)
                => File.WriteAllText(System.IO.Path.Combine(Path, fileName), "not really an apk");

            public void Dispose()
            {
                try
                {
                    Directory.Delete(Path, recursive: true);
                }
                catch (IOException)
                {
                    // A leftover temp folder is not worth failing a green test over.
                }
            }
        }
    }
}
