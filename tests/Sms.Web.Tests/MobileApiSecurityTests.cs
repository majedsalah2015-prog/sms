using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Sms.Domain.Security;
using Sms.TestSupport;
using Sms.Web.Api;
using Sms.Web.Api.Auth;
using Sms.Web.Api.Controllers;
using Sms.Web.Security;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// The mobile API's security posture, asserted rather than assumed.
    /// <para>
    /// Every rule here already exists somewhere in the product; what is new is a
    /// second transport that could reach the same data by a different route. The
    /// failures this file is written against are the quiet ones — a portal
    /// account reaching a staff endpoint, an English refusal arriving on an
    /// Arabic phone, a redirect where a phone expected JSON — none of which
    /// announce themselves in a log.
    /// </para>
    /// </summary>
    public class MobileApiSecurityTests
    {
        private static readonly Assembly Web = typeof(ApiControllerBase).Assembly;

        private static IEnumerable<Type> ApiControllers()
            => Web.GetTypes().Where(t => typeof(ApiControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

        private static IEnumerable<MethodInfo> ActionsOf(Type controller)
            => controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName);

        // ---------------------------------------------------------------- BR-SEC-010

        [Fact]
        [BusinessRule("BR-SEC-010")]
        public void Only_the_portal_and_the_sign_in_are_reachable_by_a_family()
        {
            // Deny by default, restated for the API: a portal account reaches the
            // endpoints that carry [PortalReachable] and nothing else. Pinned exactly
            // — the failure this guards against is a staff controller acquiring the
            // attribute by being copied from a portal one, which no reviewer notices
            // because the attribute reads like boilerplate.
            var reachable = ApiControllers()
                .Where(c => c.GetCustomAttributes<PortalReachableAttribute>(inherit: true).Any())
                .Select(c => c.Name)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(new[] { "AuthApiController", "PortalApiController" }, reachable);
        }

        [Fact]
        [BusinessRule("BR-SEC-010")]
        public async Task A_family_account_is_refused_a_staff_endpoint_with_not_found()
        {
            var filter = new PortalAreaFilter();
            var context = ApiContext(typeof(StudentsApiController), AccountType.Parent);

            await filter.OnActionExecutionAsync(context, () => throw new InvalidOperationException("The action must not run."));

            // Not-found, never access-denied: telling a parent the endpoint exists is
            // the disclosure the rule prevents.
            Assert.IsType<NotFoundResult>(context.Result);
        }

        [Fact]
        [BusinessRule("BR-SEC-010")]
        public async Task A_family_account_reaches_a_portal_endpoint()
        {
            var filter = new PortalAreaFilter();
            var context = ApiContext(typeof(PortalApiController), AccountType.Parent);
            var ran = false;

            await filter.OnActionExecutionAsync(context, () =>
            {
                ran = true;
                return Task.FromResult(new ActionExecutedContext(context, context.Filters, context.Controller));
            });

            Assert.True(ran);
            Assert.Null(context.Result);
        }

        [Fact]
        [BusinessRule("BR-SEC-010")]
        public async Task A_staff_account_is_left_alone_by_the_portal_filter()
        {
            var filter = new PortalAreaFilter();
            var context = ApiContext(typeof(StudentsApiController), AccountType.Staff);
            var ran = false;

            await filter.OnActionExecutionAsync(context, () =>
            {
                ran = true;
                return Task.FromResult(new ActionExecutedContext(context, context.Filters, context.Controller));
            });

            Assert.True(ran);
        }

        // ---------------------------------------------------------------- BR-SEC-005

        [Fact]
        [BusinessRule("BR-SEC-005")]
        public async Task A_pending_password_change_refuses_the_api_in_json_rather_than_redirecting()
        {
            var filter = new RequirePasswordChangeFilter();
            var context = ApiContext(typeof(PortalApiController), AccountType.Parent, mustChangePassword: true);

            await filter.OnActionExecutionAsync(context, () => throw new InvalidOperationException("The action must not run."));

            // A redirect to an HTML form is not something a phone can act on: it reads
            // as a server fault, and the app shows "something went wrong" for "change
            // your password".
            var result = Assert.IsType<ObjectResult>(context.Result);
            Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
            var body = Assert.IsType<ApiErrorResponse>(result.Value);
            Assert.Equal("must_change_password", body.Error.Code);
        }

        [Fact]
        [BusinessRule("BR-SEC-005")]
        public async Task The_change_password_endpoint_itself_stays_reachable()
        {
            var filter = new RequirePasswordChangeFilter();
            var context = ApiContext(typeof(AuthApiController), AccountType.Parent, mustChangePassword: true,
                action: nameof(AuthApiController.ChangePassword));
            var ran = false;

            await filter.OnActionExecutionAsync(context, () =>
            {
                ran = true;
                return Task.FromResult(new ActionExecutedContext(context, context.Filters, context.Controller));
            });

            Assert.True(ran);
        }

        // ---------------------------------------------------------------- the envelope

        [Fact]
        public void A_bare_not_found_is_given_a_body_so_a_client_has_something_to_parse()
        {
            // The permission guard answers NotFoundResult — no content at all, which
            // over JSON is a client parsing an empty string.
            var filter = new ApiStatusEnvelopeAttribute();
            var context = ResultContext(new NotFoundResult());

            filter.OnResultExecuting(context);

            var result = Assert.IsType<ObjectResult>(context.Result);
            Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
            Assert.Equal("not_found", Assert.IsType<ApiErrorResponse>(result.Value).Error.Code);
        }

        [Fact]
        public void A_status_the_envelope_does_not_recognise_is_left_exactly_as_it_was()
        {
            var filter = new ApiStatusEnvelopeAttribute();
            var context = ResultContext(new StatusCodeResult(StatusCodes.Status409Conflict));

            filter.OnResultExecuting(context);

            // Inventing a message for a status this filter does not know would put words
            // in the mouth of whatever raised it.
            Assert.IsType<StatusCodeResult>(context.Result);
        }

        [Fact]
        public void A_successful_result_is_untouched()
        {
            var filter = new ApiStatusEnvelopeAttribute();
            var context = ResultContext(new OkResult());

            filter.OnResultExecuting(context);

            Assert.IsType<OkResult>(context.Result);
        }

        // ---------------------------------------------------------------- the bearer scheme

        [Fact]
        public void Every_api_controller_authorizes_against_the_session_token_scheme()
        {
            // Inherited from ApiControllerBase. Asserted anyway: a controller that
            // declared [Authorize] of its own would silently fall back to the cookie
            // scheme, and a phone with a perfectly good token would be redirected to a
            // login page.
            foreach (var controller in ApiControllers())
            {
                var authorize = controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true).ToList();
                Assert.True(authorize.Count > 0, $"{controller.Name} carries no [Authorize].");
                Assert.All(authorize, a => Assert.Equal(SessionTokenDefaults.Scheme, a.AuthenticationSchemes));
            }
        }

        [Fact]
        public void The_bearer_header_is_built_the_way_the_documentation_says()
            => Assert.Equal("Bearer abc123", SessionTokenDefaults.Header("abc123"));

        // ---------------------------------------------------------------- bilingual refusals

        [Theory]
        [InlineData("invalid_credentials")]
        [InlineData("not_found")]
        [InlineData("forbidden")]
        [InlineData("unauthenticated")]
        [InlineData("must_change_password")]
        public void Every_standing_refusal_says_something_different_in_each_language(string code)
        {
            var english = WithCulture("en-US", () => Message(code));
            var arabic = WithCulture("ar-SA", () => Message(code));

            Assert.False(string.IsNullOrWhiteSpace(english));
            Assert.False(string.IsNullOrWhiteSpace(arabic));

            // The failure this catches is a refusal added with the same string on both
            // sides — which compiles, renders, and shows an Arabic reader English.
            Assert.NotEqual(english, arabic);
        }

        [Fact]
        public void A_translated_domain_refusal_reaches_the_client_in_arabic()
        {
            var arabic = WithCulture("ar-SA", () =>
            {
                Assert.True(ApiProblem.TryTranslate(
                    new Sms.Application.Common.Exceptions.PortalAccessDeniedException(7), out var status, out var error));
                Assert.Equal(StatusCodes.Status404NotFound, status);
                return error.Message;
            });

            Assert.Equal("غير موجود.", arabic);
        }

        [Fact]
        [BusinessRule("BR-SEC-011")]
        public void A_portal_refusal_is_reported_as_not_found_and_never_as_forbidden()
        {
            Assert.True(ApiProblem.TryTranslate(
                new Sms.Application.Common.Exceptions.PortalAccessDeniedException(7), out var status, out var error));

            Assert.Equal(StatusCodes.Status404NotFound, status);

            // Same code as a genuinely absent record. A distinguishable one would tell a
            // parent that the student id they guessed exists.
            Assert.Equal("not_found", error.Code);
        }

        [Fact]
        public void The_json_readers_own_english_never_reaches_the_client()
        {
            // Regression, found by smoke-testing the API in Arabic on 2026-08-31.
            // MVC treats an InputFormatterException message as safe to show a client and
            // copies it into model state verbatim, so a body the parser could not read
            // produced "The JSON value could not be converted to System.String. Path:
            // $.nameAr | LineNumber: 0 | BytePositionInLine: 13" — in English, inside an
            // otherwise translated envelope, which reads as deliberate.
            var modelState = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary();
            modelState.AddModelError(
                "$.nameAr",
                "The JSON value could not be converted to System.String. Path: $.nameAr | LineNumber: 0 | BytePositionInLine: 13.");

            var error = WithCulture("ar-SA", () => ApiProblem.Validation(modelState));

            // The key loses the JSON path so a client can match it to what it sent...
            var field = Assert.Contains("nameAr", (IDictionary<string, string[]>)error.Fields!);
            // ...and the byte offset never reaches a person.
            Assert.Equal("القيمة ليست بالصيغة المتوقعة.", Assert.Single(field));
        }

        [Fact]
        public void A_body_that_is_not_json_at_all_names_the_body_rather_than_a_field()
        {
            var modelState = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary();
            modelState.AddModelError("$", "'n' is an invalid start of a value.");

            var error = WithCulture("en-US", () => ApiProblem.Validation(modelState));

            var field = Assert.Contains("body", (IDictionary<string, string[]>)error.Fields!);
            Assert.Equal("The value is not in the expected format.", Assert.Single(field));
        }

        [Fact]
        public void A_written_rule_still_speaks_for_itself()
        {
            // The other half: the bilingual attributes and the binding message provider
            // already say something precise, and this must not flatten them.
            var modelState = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary();
            modelState.AddModelError("Phone", "حقل «رقم الهاتف» مطلوب.");

            var error = ApiProblem.Validation(modelState);

            var field = Assert.Contains("phone", (IDictionary<string, string[]>)error.Fields!);
            Assert.Equal("حقل «رقم الهاتف» مطلوب.", Assert.Single(field));
        }

        [Fact]
        public void A_framework_fault_is_not_dressed_up_as_a_business_rule()
        {
            // "Sequence contains no matching element" is an InvalidOperationException,
            // exactly like every domain refusal in this product. Translating the base
            // type would turn a broken screen into a tidy 409 nobody ever investigates.
            var fault = new InvalidOperationException("Sequence contains no matching element");

            Assert.False(ApiProblem.TryTranslate(fault, out _, out _));
        }

        // ------------------------------------------------------------------ helpers

        private static string Message(string code) => code switch
        {
            "not_found" => ApiProblem.NotFound().Message,
            "forbidden" => ApiProblem.Forbidden().Message,
            "unauthenticated" => ApiProblem.Unauthenticated().Message,
            "must_change_password" => ApiProblem.MustChangePassword().Message,
            "invalid_credentials" => Translated(new Sms.Application.Common.Exceptions.InvalidCredentialsException()),
            _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown refusal."),
        };

        private static string Translated(Exception exception)
        {
            Assert.True(ApiProblem.TryTranslate(exception, out _, out var error));
            return error.Message;
        }

        private static T WithCulture<T>(string culture, Func<T> body)
        {
            var previous = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = new CultureInfo(culture);
                return body();
            }
            finally
            {
                CultureInfo.CurrentUICulture = previous;
            }
        }

        /// <summary>
        /// An action-executing context that looks like a real API request: the
        /// controller instance decides the API branch of both global filters, and
        /// the endpoint metadata carries the attributes they read.
        /// </summary>
        private static ActionExecutingContext ApiContext(
            Type controllerType, AccountType accountType, bool mustChangePassword = false, string? action = null)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "42"),
                new(SmsClaimTypes.AccountType, accountType.ToString()),
            };
            if (mustChangePassword)
            {
                claims.Add(new Claim(SmsClaimTypes.MustChangePassword, "1"));
            }

            var http = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")),
            };

            var method = action == null ? null : controllerType.GetMethod(action);
            var metadata = new List<object>();
            metadata.AddRange(controllerType.GetCustomAttributes(inherit: true));
            if (method != null)
            {
                metadata.AddRange(method.GetCustomAttributes(inherit: true));
            }

            var descriptor = new ControllerActionDescriptor
            {
                ControllerTypeInfo = controllerType.GetTypeInfo(),
                ControllerName = controllerType.Name,
                ActionName = action ?? "Anything",
                MethodInfo = method ?? typeof(MobileApiSecurityTests).GetMethod(nameof(Message), BindingFlags.NonPublic | BindingFlags.Static)!,
                EndpointMetadata = metadata,
            };

            var routeData = new RouteData();
            routeData.Values["controller"] = controllerType.Name.Replace("Controller", string.Empty, StringComparison.Ordinal);
            routeData.Values["action"] = action ?? "Anything";

            return new ActionExecutingContext(
                new ActionContext(http, routeData, descriptor),
                new List<IFilterMetadata>(),
                new Dictionary<string, object?>()!,
                // The controller instance is what both filters test to decide they are
                // looking at the API rather than at a Razor screen.
                controller: System.Runtime.Serialization.FormatterServices.GetUninitializedObject(controllerType));
        }

        private static ResultExecutingContext ResultContext(IActionResult result) => new(
            new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>(),
            result,
            controller: null!);
    }
}
