using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Application.Portal;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.Infrastructure.Persistence;
using Sms.Web.Api.Models;
using Sms.Web.Security;
using IAuthenticationService = Sms.Application.Security.IAuthenticationService;

namespace Sms.Web.Api.Controllers
{
    /// <summary>
    /// Sign-in for the mobile app (doc 06 §3) — the same
    /// <see cref="IAuthenticationService"/> the browser uses, answering in JSON.
    /// <para>
    /// Every rule stays where it already is: lockout (BR-SEC-002), TOTP
    /// (BR-SEC-003), session lifetime (BR-SEC-004), the forced first change
    /// (BR-SEC-005) and every audit event are the service's, and this controller
    /// only turns outcomes into status codes. Nothing here decides who may sign
    /// in.
    /// </para>
    /// </summary>
    [Route(V1 + "/auth")]
    [PortalReachable]
    public sealed class AuthApiController : ApiControllerBase
    {
        /// <summary>
        /// Namespaces the five-minute proof-of-password token below. Data
        /// protection keys are the application's own, so the value is opaque and
        /// unforgeable outside this process without a table to hold it.
        /// </summary>
        private const string TwoFactorPurpose = "Sms.Api.TwoFactor.v1";

        private static readonly TimeSpan TwoFactorWindow = TimeSpan.FromMinutes(5);

        private readonly IAuthenticationService _auth;
        private readonly AppDbContext _db;
        private readonly IPermissionService _permissions;
        private readonly IParentPortalQuery _portal;
        private readonly IWorkingYearContext _workingYear;
        private readonly ITimeLimitedDataProtector _protector;

        public AuthApiController(
            IAuthenticationService auth,
            AppDbContext db,
            IPermissionService permissions,
            IParentPortalQuery portal,
            IWorkingYearContext workingYear,
            IDataProtectionProvider dataProtection)
        {
            _auth = auth;
            _db = db;
            _permissions = permissions;
            _portal = portal;
            _workingYear = workingYear;
            _protector = dataProtection.CreateProtector(TwoFactorPurpose).ToTimeLimitedDataProtector();
        }

        /// <summary>
        /// Username and password in, a session token out — or a five-minute
        /// ticket to the second factor.
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        [PasswordChangeExempt]
        [NoPermissionRequired("Signing in is what happens before there is anyone to check permissions for.")]
        public async Task<ActionResult<ApiLoginResponse>> Login([FromBody] ApiLoginRequest request)
        {
            // InvalidCredentialsException / AccountLockedOutException are translated
            // by ApiExceptionFilter; catching them here would only restate it.
            var outcome = await _auth.AuthenticateAsync(
                request.UserName.Trim(), request.Password, ClientIp, DeviceAgent(request), Ct);

            if (outcome.RequiresTwoFactor)
            {
                return new ApiLoginResponse
                {
                    RequiresTwoFactor = true,
                    TwoFactorToken = _protector.Protect(
                        outcome.UserAccountId.ToString(CultureInfo.InvariantCulture), TwoFactorWindow),
                };
            }

            return Issued(outcome.Session!, outcome.MustChangePassword);
        }

        /// <summary>BR-SEC-003. Completes a login the password alone did not finish.</summary>
        [HttpPost("two-factor")]
        [AllowAnonymous]
        [PasswordChangeExempt]
        [NoPermissionRequired("Second factor of the sign-in itself.")]
        public async Task<ActionResult<ApiLoginResponse>> TwoFactor([FromBody] ApiTwoFactorRequest request)
        {
            int userAccountId;
            try
            {
                userAccountId = int.Parse(_protector.Unprotect(request.TwoFactorToken), CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or FormatException)
            {
                // Expired, tampered with, or from a previous key ring. All three mean
                // "start again", and none of them should say which.
                return Refuse(401, "two_factor_token_expired",
                    "That sign-in attempt has expired. Sign in again.",
                    "انتهت صلاحية محاولة الدخول. سجّل الدخول من جديد.");
            }

            var session = await _auth.CompleteTwoFactorAsync(userAccountId, request.Code.Trim(), ClientIp, UserAgent, Ct);
            var mustChange = await _db.UserAccounts.AsNoTracking()
                .Where(u => u.Id == userAccountId).Select(u => u.MustChangePassword).SingleAsync(Ct);

            return Issued(session, mustChange);
        }

        /// <summary>
        /// Self-service change, and the only way past BR-SEC-005's block. The
        /// session survives it — the browser reissues its cookie here because a
        /// cookie carries the must-change flag; a bearer token does not, and the
        /// flag is re-read from the account on the next call.
        /// </summary>
        [HttpPost("change-password")]
        [PasswordChangeExempt]
        [NoPermissionRequired("Self-service on one's own credentials; BR-SEC-005 forces it before anything else.")]
        public async Task<IActionResult> ChangePassword([FromBody] ApiChangePasswordRequest request)
        {
            await _auth.ChangePasswordAsync(CurrentUserAccountId, request.CurrentPassword, request.NewPassword, Ct);
            return NoContent();
        }

        /// <summary>
        /// Ends this session server-side (BR-SEC-004), so the token stops working
        /// everywhere rather than only on the device that discarded it.
        /// </summary>
        [HttpPost("logout")]
        [PasswordChangeExempt]
        [NoPermissionRequired("Ending one's own session.")]
        public async Task<IActionResult> Logout()
        {
            var token = User.FindFirst(SmsClaimTypes.SessionToken)?.Value;
            if (!string.IsNullOrEmpty(token))
            {
                await _auth.LogoutAsync(token, Ct);
            }

            return NoContent();
        }

        /// <summary>
        /// Who is signed in, which school and year they are reading, the person
        /// behind the account, the students they may see, and every permission
        /// they hold. The app's first call.
        /// </summary>
        [HttpGet("me")]
        [NoPermissionRequired("Describes the caller to themselves; discloses nothing they do not already hold.")]
        public async Task<ActionResult<ApiMeResponse>> Me()
        {
            var accountId = CurrentUserAccountId;
            var account = await _db.UserAccounts.AsNoTracking().SingleAsync(u => u.Id == accountId, Ct);

            var school = await _db.Schools.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.Id == _db.CurrentSchoolId)
                .Select(s => new { s.NameAr, s.NameEn })
                .SingleOrDefaultAsync(Ct);

            var year = await _db.AcademicYears.AsNoTracking()
                .Where(y => y.Id == _workingYear.AcademicYearId)
                .Select(y => new { y.LabelAr, y.LabelEn })
                .SingleOrDefaultAsync(Ct);

            var sessionToken = User.FindFirst(SmsClaimTypes.SessionToken)?.Value;
            var expires = await _db.UserSessions.AsNoTracking()
                .Where(s => s.SessionToken == sessionToken)
                .Select(s => (DateTime?)s.ExpiresAtUtc)
                .FirstOrDefaultAsync(Ct);

            return new ApiMeResponse
            {
                UserAccountId = account.Id,
                UserName = account.UserName,
                AccountType = account.AccountType.ToString(),
                SchoolId = account.SchoolId,
                SchoolNameAr = school?.NameAr ?? string.Empty,
                SchoolNameEn = school?.NameEn ?? string.Empty,
                WorkingAcademicYearId = _workingYear.AcademicYearId,
                WorkingAcademicYearName = year == null ? null : T(year.LabelEn, year.LabelAr),
                MustChangePassword = account.MustChangePassword,
                TwoFactorEnabled = account.TwoFactorEnabled,
                SessionExpiresAtUtc = expires ?? DateTime.UtcNow,
                Subject = await SubjectAsync(account),
                Children = await ChildrenAsync(account),
                Permissions = await GrantedAsync(),
            };
        }

        // ------------------------------------------------------------------ helpers

        private ApiLoginResponse Issued(UserSession session, bool mustChangePassword) => new()
        {
            Token = session.SessionToken,
            ExpiresAtUtc = session.ExpiresAtUtc,
            MustChangePassword = mustChangePassword,
        };

        /// <summary>
        /// The student, guardian or employee this account is. Null for a system
        /// account, and for a staff account with no employee file behind it —
        /// which is a real state, not an error.
        /// </summary>
        private async Task<ApiMeSubject?> SubjectAsync(UserAccount account)
        {
            switch (account.AccountType)
            {
                case AccountType.Student:
                {
                    var student = await _db.Students.AsNoTracking()
                        .Where(s => s.UserAccountId == account.Id)
                        .Select(s => new { s.Id, s.StudentNo, s.FirstNameAr, s.FatherNameAr, s.FamilyNameAr, s.FirstNameEn, s.FatherNameEn, s.FamilyNameEn })
                        .FirstOrDefaultAsync(Ct);
                    return student == null ? null : new ApiMeSubject
                    {
                        Kind = "Student",
                        Id = student.Id,
                        Reference = student.StudentNo,
                        NameAr = Join(student.FirstNameAr, student.FatherNameAr, student.FamilyNameAr),
                        NameEn = Join(student.FirstNameEn, student.FatherNameEn, student.FamilyNameEn),
                    };
                }

                case AccountType.Parent:
                {
                    var parent = await _db.Parents.AsNoTracking()
                        .Where(p => p.UserAccountId == account.Id)
                        .Select(p => new { p.Id, p.ParentFileNo, p.NameAr, p.NameEn })
                        .FirstOrDefaultAsync(Ct);
                    return parent == null ? null : new ApiMeSubject
                    {
                        Kind = "Parent",
                        Id = parent.Id,
                        Reference = parent.ParentFileNo,
                        NameAr = parent.NameAr,
                        NameEn = parent.NameEn,
                    };
                }

                case AccountType.Staff:
                {
                    var employee = await _db.Employees.AsNoTracking()
                        .Where(e => e.UserAccountId == account.Id)
                        .Select(e => new { e.Id, e.EmployeeNo, e.FirstNameAr, e.FatherNameAr, e.FamilyNameAr, e.FirstNameEn, e.FatherNameEn, e.FamilyNameEn })
                        .FirstOrDefaultAsync(Ct);
                    return employee == null ? null : new ApiMeSubject
                    {
                        Kind = "Employee",
                        Id = employee.Id,
                        Reference = employee.EmployeeNo,
                        NameAr = Join(employee.FirstNameAr, employee.FatherNameAr, employee.FamilyNameAr),
                        NameEn = Join(employee.FirstNameEn, employee.FatherNameEn, employee.FamilyNameEn),
                    };
                }

                default:
                    return null;
            }
        }

        /// <summary>
        /// BR-SEC-011's answer, asked once at sign-in so the app never has to
        /// guess a student id. A parent gets the children the guardian link makes
        /// visible; a student gets themselves; staff get nothing here — the
        /// student directory is a different permission and a different endpoint.
        /// </summary>
        private async Task<IReadOnlyList<ApiMeChild>> ChildrenAsync(UserAccount account)
        {
            if (account.AccountType == AccountType.Parent)
            {
                var children = await _portal.GetVisibleChildrenAsync(account.Id, Ct);
                return children
                    .Select(c => new ApiMeChild
                    {
                        StudentId = c.StudentId,
                        StudentNo = c.StudentNo,
                        NameAr = c.FirstNameAr,
                        NameEn = c.FirstNameEn,
                    })
                    .ToList();
            }

            if (account.AccountType == AccountType.Student)
            {
                var self = await _db.Students.AsNoTracking()
                    .Where(s => s.UserAccountId == account.Id)
                    .Select(s => new ApiMeChild
                    {
                        StudentId = s.Id,
                        StudentNo = s.StudentNo,
                        NameAr = s.FirstNameAr,
                        NameEn = s.FirstNameEn,
                    })
                    .ToListAsync(Ct);
                return self;
            }

            return Array.Empty<ApiMeChild>();
        }

        /// <summary>
        /// Every catalogued permission this caller actually holds, evaluated by
        /// <see cref="IPermissionService"/> — the same code path the guards use,
        /// so the menu the app draws and the endpoints it may call cannot drift
        /// apart. One database round trip: the service loads the caller's
        /// assignments once per request and evaluates the rest in memory.
        /// </summary>
        private async Task<IReadOnlyList<string>> GrantedAsync()
        {
            var granted = new List<string>();
            foreach (var permission in ScreenCatalog.Permissions())
            {
                if (await _permissions.HasPermissionAsync(permission.ModuleCode, permission.ScreenCode, permission.Action, Ct))
                {
                    granted.Add($"{permission.ModuleCode}/{permission.ScreenCode}/{permission.Action}");
                }
            }

            return granted;
        }

        private static string Join(params string[] parts)
            => string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));

        private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

        private string? UserAgent => Request.Headers["User-Agent"].ToString() is { Length: > 0 } value ? value : null;

        /// <summary>
        /// What the school's session list will show. The device name the app
        /// sends is preferred over the HTTP agent string, which for a native
        /// client is usually a library's name and tells an administrator
        /// reviewing sessions nothing at all.
        /// </summary>
        private string? DeviceAgent(ApiLoginRequest request)
            => string.IsNullOrWhiteSpace(request.DeviceName) ? UserAgent : request.DeviceName.Trim();
    }
}
