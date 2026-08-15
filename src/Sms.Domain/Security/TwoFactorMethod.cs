namespace Sms.Domain.Security
{
    /// <summary>
    /// doc 06 §3 (BR-SEC-003): TOTP is self-contained; SMS/Email OTP need the
    /// E-007 notification dispatcher and arrive with that dependency.
    /// </summary>
    public enum TwoFactorMethod : short
    {
        Totp = 1,
    }
}
