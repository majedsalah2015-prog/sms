using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.Web.Security;
using Xunit;

namespace Sms.ArchitectureTests
{
    /// <summary>
    /// Keeps deny-by-default (BR-GLB-070) true of the actual code rather than of
    /// the design document.
    /// <para>
    /// Before this test existed, <c>RequirePermissionAttribute</c> was used in
    /// zero controllers: the filter, the evaluator and the service were all
    /// written and none of them ran, so every screen in the product — the
    /// cashier's drawer, the discount approvals, the ledger export — was open to
    /// anyone who could sign in. Nothing announced that, because an unguarded
    /// action looks exactly like a guarded one until you read its attributes.
    /// </para>
    /// <para>
    /// So the rule is not "guard the screens that matter" but "every action
    /// declares, and the build fails otherwise". An action may declare
    /// <see cref="NoPermissionRequiredAttribute"/> instead, which is a decision
    /// with a stated reason, not an omission.
    /// </para>
    /// </summary>
    public class ScreenPermissionTests
    {
        private static readonly Assembly Web = typeof(Sms.Web.Security.RequirePermissionAttribute).Assembly;

        private static IEnumerable<(Type Controller, MethodInfo Action)> Actions()
            => Web.GetTypes()
                .Where(t => typeof(Controller).IsAssignableFrom(t) && !t.IsAbstract)
                .SelectMany(t => t
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(m => !m.IsSpecialName && typeof(IActionResult).IsAssignableFrom(Unwrap(m.ReturnType)))
                    .Select(m => (t, m)));

        private static Type Unwrap(Type returnType)
            => returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(System.Threading.Tasks.Task<>)
                ? returnType.GetGenericArguments()[0]
                : returnType;

        [Fact]
        public void Every_controller_action_declares_its_permission_or_says_why_it_needs_none()
        {
            var undeclared = Actions()
                .Where(a =>
                    a.Action.GetCustomAttributes<RequirePermissionAttribute>(inherit: true).Any() == false
                    && a.Action.GetCustomAttributes<NoPermissionRequiredAttribute>(inherit: true).Any() == false
                    && a.Controller.GetCustomAttributes<NoPermissionRequiredAttribute>(inherit: true).Any() == false)
                .Select(a => $"{a.Controller.Name}.{a.Action.Name}")
                .OrderBy(x => x)
                .ToList();

            Assert.True(undeclared.Count == 0,
                "These actions are reachable by any signed-in user because they declare no permission. "
                + "Add [RequirePermission(...)], or [NoPermissionRequired(\"why\")] if that is deliberate:"
                + Environment.NewLine + string.Join(Environment.NewLine, undeclared));
        }

        [Fact]
        public void Every_declared_permission_exists_in_the_catalogue()
        {
            // A typo in a module or screen code is not a compile error and not a runtime error either:
            // the evaluator simply never matches, and the screen becomes unreachable for everyone,
            // including the system administrator. That failure is silent and looks like a bug in the
            // data, which is why it is caught here instead.
            var unknown = new List<string>();

            foreach (var (controller, action) in Actions())
            {
                foreach (var attribute in Describe(action))
                {
                    if (!ScreenCatalog.Defines(attribute.Module, attribute.Screen, attribute.Action))
                    {
                        unknown.Add($"{controller.Name}.{action.Name} -> {attribute.Module}/{attribute.Screen}/{attribute.Action}");
                    }
                }
            }

            Assert.True(unknown.Count == 0,
                "These actions name a permission the screen catalogue does not define:"
                + Environment.NewLine + string.Join(Environment.NewLine, unknown.OrderBy(x => x)));
        }

        [Fact]
        public void Every_catalogued_permission_is_reachable_from_an_action()
        {
            // The other direction. A catalogued verb no action ever requires is a grant an
            // administrator can hand out that buys nothing — it reads as capability and is not one.
            var used = new HashSet<(string, string, ActionVerb)>();
            foreach (var (_, action) in Actions())
            {
                foreach (var attribute in Describe(action))
                {
                    used.Add((attribute.Module.ToUpperInvariant(), attribute.Screen.ToUpperInvariant(), attribute.Action));
                }
            }

            var orphans = ScreenCatalog.Permissions()
                .Where(p => !used.Contains((p.ModuleCode.ToUpperInvariant(), p.ScreenCode.ToUpperInvariant(), p.Action)))
                .Select(p => $"{p.ModuleCode}/{p.ScreenCode}/{p.Action}")
                .OrderBy(x => x)
                .ToList();

            // Not every permission guards a screen of its own. A few guard a *region* of one — a tab
            // whose data is a restricted category — and those are enforced by an explicit
            // IPermissionService.HasPermissionAsync call inside the host screen's action, which no
            // attribute can express: the host screen has its own permission, and requiring both would
            // withhold the whole file from everyone who may not see the region.
            //
            // Pinned exactly, like the anonymous actions below, so this stays a decision with a stated
            // reason rather than a hole. A new orphan fails here; so does an entry that has since been
            // given an action of its own and should come off the list.
            var enforcedInsideAScreen = new[]
            {
                // Rendered as a tab of the student file (StudentsController.File), never as a page.
                // BR-GLB-072: without the check, STU/File/View would hand over a family's
                // circumstances, and this permission would exist only on paper. Its Edit verb is a
                // real action (UpdateSocialProfile) and is therefore not listed here.
                "STU/SocialProfile/View",
            };

            var unguarded = orphans.Except(enforcedInsideAScreen).ToList();
            var stale = enforcedInsideAScreen.Except(orphans).ToList();

            Assert.True(unguarded.Count == 0,
                "The catalogue defines these permissions and no action requires them:"
                + Environment.NewLine + string.Join(Environment.NewLine, unguarded));

            Assert.True(stale.Count == 0,
                "These are listed as enforced inside another screen, but an action now requires them. "
                + "Take them off the list — the attribute is the stronger guard, and the list should "
                + "hold only what an attribute cannot express:"
                + Environment.NewLine + string.Join(Environment.NewLine, stale));
        }

        [Fact]
        public void Anonymous_actions_are_confined_to_signing_in_and_the_error_page()
        {
            // [AllowAnonymous] removes authentication itself, not just the permission check, so the
            // list of actions carrying it is short by definition and worth pinning.
            var anonymous = Actions()
                .Where(a => a.Action.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any())
                .Select(a => $"{a.Controller.Name}.{a.Action.Name}")
                .OrderBy(x => x)
                .ToList();

            var expected = new[]
            {
                "AccountController.AccessDenied",
                "AccountController.Login",
                "AccountController.TwoFactor",
                // The school's logo, which the sign-in screen wears (BR-SCH-006). Anonymous because
                // the screen that draws it is: behind the fallback policy the browser's request for
                // the image is answered with a redirect to the sign-in page, so the one screen every
                // reader passes through would show a broken mark. It serves one image from one slot
                // of one school — the deployment is single-tenant — and a school's logo is the least
                // private thing it owns. Nothing else about the school is reachable through it.
                "HomeController.BrandLogo",
                "HomeController.Error",
                "HomeController.SetLanguage",
            };

            Assert.Equal(expected, anonymous.Distinct().ToArray());
        }

        private static IEnumerable<(string Module, string Screen, ActionVerb Action)> Describe(MethodInfo action)
            => action.GetCustomAttributes<RequirePermissionAttribute>(inherit: true)
                .Select(a => (
                    (string)a.Arguments[0],
                    (string)a.Arguments[1],
                    (ActionVerb)a.Arguments[2]));
    }
}
