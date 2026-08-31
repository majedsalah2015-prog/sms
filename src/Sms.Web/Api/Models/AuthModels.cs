using System;
using System.Collections.Generic;
using Sms.Web.Models;

namespace Sms.Web.Api.Models
{
    /// <summary>What the app posts to <c>POST /api/v1/auth/login</c>.</summary>
    public sealed class ApiLoginRequest
    {
        [RequiredField("username", "اسم المستخدم")]
        public string UserName { get; set; } = string.Empty;

        [RequiredField("password", "كلمة المرور")]
        public string Password { get; set; } = string.Empty;

        /// <summary>Free text the school's session list shows beside the login — "iPhone 14, SMS 1.0".</summary>
        public string? DeviceName { get; set; }
    }

    /// <summary>
    /// The answer to a sign-in. Exactly one of <see cref="Token"/> and
    /// <see cref="TwoFactorToken"/> is ever set — a session exists only once
    /// BR-SEC-003 is satisfied.
    /// </summary>
    public sealed class ApiLoginResponse
    {
        /// <summary>The bearer token: <c>Authorization: Bearer {token}</c>. Null while 2FA is outstanding.</summary>
        public string? Token { get; set; }

        /// <summary>
        /// BR-SEC-004's absolute ceiling for this session — never extended by
        /// activity. The app should sign in again at this moment, not discover
        /// it through a 401 mid-action.
        /// </summary>
        public DateTime? ExpiresAtUtc { get; set; }

        public bool RequiresTwoFactor { get; set; }

        /// <summary>
        /// Proof the password was accepted, good for five minutes, to be posted
        /// back with the TOTP code. It is not a session and grants nothing:
        /// without it a caller who guessed an account id could attack the second
        /// factor alone, which is precisely what the browser's short-lived
        /// second cookie prevents.
        /// </summary>
        public string? TwoFactorToken { get; set; }

        /// <summary>BR-SEC-005. While true, every endpoint but change-password and logout refuses.</summary>
        public bool MustChangePassword { get; set; }
    }

    /// <summary>What the app posts to <c>POST /api/v1/auth/two-factor</c>.</summary>
    public sealed class ApiTwoFactorRequest
    {
        [RequiredField("verification token", "رمز الجلسة المؤقت")]
        public string TwoFactorToken { get; set; } = string.Empty;

        [RequiredField("code", "الرمز")]
        public string Code { get; set; } = string.Empty;
    }

    /// <summary>What the app posts to <c>POST /api/v1/auth/change-password</c>.</summary>
    public sealed class ApiChangePasswordRequest
    {
        [RequiredField("current password", "كلمة المرور الحالية")]
        public string CurrentPassword { get; set; } = string.Empty;

        [RequiredField("new password", "كلمة المرور الجديدة")]
        public string NewPassword { get; set; } = string.Empty;
    }

    /// <summary>
    /// Who is signed in and what they may open — the call the app makes first
    /// and caches for the session.
    /// </summary>
    public sealed class ApiMeResponse
    {
        public int UserAccountId { get; set; }

        public string UserName { get; set; } = string.Empty;

        /// <summary>Staff / Parent / Student / System. Decides which half of the API the app shows.</summary>
        public string AccountType { get; set; } = string.Empty;

        public int SchoolId { get; set; }

        public string SchoolNameAr { get; set; } = string.Empty;

        public string SchoolNameEn { get; set; } = string.Empty;

        /// <summary>The working academic year every year-scoped read below is answered against.</summary>
        public int WorkingAcademicYearId { get; set; }

        public string? WorkingAcademicYearName { get; set; }

        public bool MustChangePassword { get; set; }

        public bool TwoFactorEnabled { get; set; }

        public DateTime SessionExpiresAtUtc { get; set; }

        /// <summary>The person this account is, when it is one — the student, the guardian, or the employee.</summary>
        public ApiMeSubject? Subject { get; set; }

        /// <summary>
        /// A parent's children, or a student's own record. Empty for staff.
        /// The ids the portal endpoints take.
        /// </summary>
        public IReadOnlyList<ApiMeChild> Children { get; set; } = Array.Empty<ApiMeChild>();

        /// <summary>
        /// Every catalogued permission this account holds, as
        /// <c>MODULE/Screen/Verb</c>. The app builds its menu from this instead
        /// of calling endpoints to see which ones 404 — the same evaluator the
        /// server guards with, so the two cannot disagree (BR-SEC-010).
        /// </summary>
        public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();
    }

    /// <summary>The person behind the account.</summary>
    public sealed class ApiMeSubject
    {
        /// <summary>"Student", "Parent" or "Employee".</summary>
        public string Kind { get; set; } = string.Empty;

        public int Id { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        /// <summary>Student number / employee number, whichever applies.</summary>
        public string? Reference { get; set; }
    }

    /// <summary>One student this account may read (BR-SEC-011).</summary>
    public sealed class ApiMeChild
    {
        public int StudentId { get; set; }

        public string StudentNo { get; set; } = string.Empty;

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;
    }
}
