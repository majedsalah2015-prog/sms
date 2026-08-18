namespace Sms.Web.Security
{
    /// <summary>Claims the cookie principal carries beyond the standard NameIdentifier/Name.</summary>
    public static class SmsClaimTypes
    {
        /// <summary>The sec.UserSession token minted by IAuthenticationService (BR-SEC-004 idle/absolute expiry is enforced against it on every request).</summary>
        public const string SessionToken = "sms:session";

        public const string SchoolId = "sms:school";

        /// <summary>"1" while BR-SEC-005 demands a password change before any other action.</summary>
        public const string MustChangePassword = "sms:must-change-password";
    }
}
