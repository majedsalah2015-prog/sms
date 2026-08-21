using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.TestSupport;
using Sms.Web.Security;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// The last untested link. The catalogue is tested, the role defaults are
    /// tested, and an architecture test proves every action declares a
    /// permission — but all of that is inert if the filter itself lets the
    /// request through. It had never run in this codebase: it was used in zero
    /// controllers until the screens were annotated.
    /// </summary>
    public class RequirePermissionFilterTests
    {
        private sealed class StubPermissions : IPermissionService
        {
            private readonly bool _answer;

            public StubPermissions(bool answer) => _answer = answer;

            public (string Module, string Screen, ActionVerb Action)? Asked { get; private set; }

            public Task<bool> HasPermissionAsync(string moduleCode, string screenCode, ActionVerb action, CancellationToken cancellationToken = default)
            {
                Asked = (moduleCode, screenCode, action);
                return Task.FromResult(_answer);
            }

            public Task<EffectiveScope?> GetEffectiveScopeAsync(string moduleCode, string screenCode, ActionVerb action, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<IReadOnlyList<string>> GetGrantedScreenCodesAsync(int userAccountId, string moduleCode, ActionVerb action, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();
        }

        private static ActionExecutingContext Context() => new(
            new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>()!,
            controller: null!);

        [Fact]
        [BusinessRule("BR-GLB-070")]
        public async Task A_missing_permission_stops_the_action_and_answers_not_found()
        {
            var permissions = new StubPermissions(answer: false);
            var filter = new RequirePermissionFilter(permissions, "FEE", "Structure", ActionVerb.Approve);
            var context = Context();
            var ran = false;

            await filter.OnActionExecutionAsync(context, () =>
            {
                ran = true;
                return Task.FromResult<ActionExecutedContext>(null!);
            });

            Assert.False(ran);

            // BR-SEC-010: not-found, never access-denied. Access-denied confirms the screen exists
            // and tells the reader they found something worth pressing on.
            Assert.IsType<NotFoundResult>(context.Result);
            Assert.Equal(("FEE", "Structure", ActionVerb.Approve), permissions.Asked);
        }

        [Fact]
        [BusinessRule("BR-GLB-070")]
        public async Task A_granted_permission_lets_the_action_run_untouched()
        {
            var filter = new RequirePermissionFilter(new StubPermissions(answer: true), "FEE", "Structure", ActionVerb.Approve);
            var context = Context();
            var ran = false;

            await filter.OnActionExecutionAsync(context, () =>
            {
                ran = true;
                return Task.FromResult<ActionExecutedContext>(null!);
            });

            Assert.True(ran);
            Assert.Null(context.Result);
        }

        [Fact]
        [BusinessRule("BR-GLB-070")]
        public void The_attribute_hands_the_filter_exactly_what_it_was_given()
        {
            // The triple travels as TypeFilterAttribute.Arguments, which is untyped: a reordering
            // here would silently guard the wrong thing, and both other tests would still pass.
            var attribute = new RequirePermissionAttribute("STU", "SocialProfile", ActionVerb.Edit);

            Assert.Equal(new object[] { "STU", "SocialProfile", ActionVerb.Edit }, attribute.Arguments);
            Assert.Equal(typeof(RequirePermissionFilter), attribute.ImplementationType);
        }
    }
}
