using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Sms.Domain.Security;
using Sms.TestSupport;
using Sms.Web.Models;
using Sms.Web.Security;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// The guide behind the top bar's help button, and the two things about it that would rot
    /// silently: a chapter that ships in one language, and a shell that stops linking to it.
    /// <para>
    /// Neither failure announces itself. A half-translated chapter renders perfectly — in English,
    /// to an Arabic reader — and a top bar reworked without the button leaves a page that still
    /// exists, still passes every other test, and is reachable only by typing its address.
    /// </para>
    /// </summary>
    public class PlatformGuideTests
    {
        private static IEnumerable<GuideSection> Staff(bool arabic) => PlatformGuide.ForStaff(arabic);

        private static IEnumerable<GuideSection> Portal(bool arabic) => PlatformGuide.ForPortal(arabic);

        public static IEnumerable<object[]> Audiences => new[]
        {
            new object[] { "staff" },
            new object[] { "portal" },
        };

        private static (IReadOnlyList<GuideSection> En, IReadOnlyList<GuideSection> Ar) Both(string audience)
            => audience == "staff"
                ? (Staff(arabic: false).ToList(), Staff(arabic: true).ToList())
                : (Portal(arabic: false).ToList(), Portal(arabic: true).ToList());

        [Theory]
        [MemberData(nameof(Audiences))]
        public void Every_chapter_ships_both_languages(string audience)
        {
            var (en, ar) = Both(audience);

            Assert.NotEmpty(en);
            Assert.Equal(en.Count, ar.Count);

            for (var s = 0; s < en.Count; s++)
            {
                var (e, a) = (en[s], ar[s]);

                Assert.Equal(e.Key, a.Key);
                Assert.Equal(e.Icon, a.Icon);
                Assert.Equal(e.Items.Count, a.Items.Count);

                // A string that comes back identical in both languages is the failure mode this test
                // exists for: T(en, ar) called with the English text twice reads as translated in the
                // source and is not. Icons and keys are exempt above because they are not prose.
                AssertTranslated($"{audience}/{e.Key} title", e.Title, a.Title);
                AssertTranslated($"{audience}/{e.Key} intro", e.Intro, a.Intro);

                for (var i = 0; i < e.Items.Count; i++)
                {
                    AssertTranslated($"{audience}/{e.Key} item {i} heading", e.Items[i].Heading, a.Items[i].Heading);
                    AssertTranslated($"{audience}/{e.Key} item {i} body", e.Items[i].Body, a.Items[i].Body);
                }
            }
        }

        private static void AssertTranslated(string what, string en, string ar)
        {
            Assert.False(string.IsNullOrWhiteSpace(en), $"{what}: the English is empty.");
            Assert.False(string.IsNullOrWhiteSpace(ar), $"{what}: the Arabic is empty.");
            Assert.False(
                string.Equals(en, ar, StringComparison.Ordinal),
                $"{what}: both languages returned the same string, so one of them is untranslated.");
            Assert.True(ar.Any(IsArabicLetter), $"{what}: the Arabic side carries no Arabic letters.");
        }

        // U+0600..U+06FF — the Arabic block. Catches the paste that lost its encoding on the way in
        // as well as the string that was never translated at all.
        private static bool IsArabicLetter(char c) => c >= '؀' && c <= 'ۿ';

        [Theory]
        [MemberData(nameof(Audiences))]
        public void Chapter_keys_are_unique_because_the_contents_strip_links_to_them(string audience)
        {
            var (en, _) = Both(audience);
            var duplicated = en.GroupBy(s => s.Key, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

            // The keys are the anchor ids the jump links target; two chapters sharing one means the
            // second is unreachable from the strip and nothing else goes wrong to say so.
            Assert.True(duplicated.Count == 0, $"Duplicate chapter keys: {string.Join(", ", duplicated)}");
        }

        [Theory]
        [InlineData("_Layout.cshtml")]
        [InlineData("_PortalLayout.cshtml")]
        public void Both_shells_link_to_the_guide(string layout)
        {
            var body = File.ReadAllText(Path.Combine(SharedViews, layout));

            Assert.True(
                body.Contains("asp-controller=\"Help\"", StringComparison.Ordinal),
                $"{layout} no longer links to the user guide. The page still exists and every other test still passes; "
                + "it is simply unreachable except by typing its address.");
        }

        private static string SharedViews
        {
            get
            {
                var repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ThisFile())!, "..", ".."));
                return Path.Combine(repoRoot, "src", "Sms.Web", "Views", "Shared");
            }
        }

        private static string ThisFile([CallerFilePath] string path = "") => path;

        [Fact]
        [BusinessRule("BR-SEC-010")]
        public async Task A_portal_account_may_reach_the_guide()
        {
            // The portal's top bar links to it, so the filter has to let it through — otherwise the
            // button a parent presses answers not-found, which is the one place BR-SEC-010's own
            // refusal would be indistinguishable from a broken product.
            var context = PortalRequestTo("Help", "Index");

            await new PortalAreaFilter().OnActionExecutionAsync(context, Next);

            Assert.Null(context.Result);
        }

        [Fact]
        [BusinessRule("BR-SEC-010")]
        public async Task The_staff_screens_stay_shut_to_a_portal_account()
        {
            // The guard on the line above: letting Help through must not have widened anything else.
            var context = PortalRequestTo("Students", "Index");

            await new PortalAreaFilter().OnActionExecutionAsync(context, Next);

            Assert.IsType<NotFoundResult>(context.Result);
        }

        [Theory]
        [InlineData("Index")]
        [InlineData("MarkRead")]
        [InlineData("MarkAllRead")]
        [BusinessRule("BR-SEC-010")]
        public async Task A_portal_account_may_reach_its_own_notification_inbox(string action)
        {
            // doc 09 §5's bell/list/mark-read. Every InApp delivery this product sends is written to
            // this inbox and most of them are addressed to families — fees due, an absence, a clinic
            // visit. Before this the engine queued them, marked them Delivered, and the parent's own
            // inbox answered not-found, which reads as a lost message rather than a closed door.
            var context = PortalRequestTo("Notifications", action);

            await new PortalAreaFilter().OnActionExecutionAsync(context, Next);

            Assert.Null(context.Result);
        }

        [Theory]
        [InlineData("Templates")]
        [InlineData("Providers")]
        [InlineData("Deliveries")]
        [InlineData("Budget")]
        [InlineData("Subscriptions")]
        [BusinessRule("BR-SEC-010")]
        public async Task The_notification_administration_screens_stay_shut_to_a_portal_account(string action)
        {
            // The guard on the three above. They are allowed by *action*, not by controller, because
            // the same controller holds the studio, the gateways, the delivery log and the budget —
            // screens over every family's messages. Widening to the controller would open all of them.
            var context = PortalRequestTo("Notifications", action);

            await new PortalAreaFilter().OnActionExecutionAsync(context, Next);

            Assert.IsType<NotFoundResult>(context.Result);
        }

        private static Task<ActionExecutedContext> Next() => Task.FromResult<ActionExecutedContext>(null!);

        private static ActionExecutingContext PortalRequestTo(string controller, string action)
        {
            var http = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(SmsClaimTypes.AccountType, AccountType.Parent.ToString()) },
                    authenticationType: "test")),
            };

            var routeData = new RouteData();
            routeData.Values["controller"] = controller;
            routeData.Values["action"] = action;

            return new ActionExecutingContext(
                new ActionContext(http, routeData, new ActionDescriptor()),
                new List<IFilterMetadata>(),
                new Dictionary<string, object?>()!,
                controller: null!);
        }
    }
}
