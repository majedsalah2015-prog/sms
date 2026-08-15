namespace Sms.Application.Security
{
    public enum PasswordPolicyViolation
    {
        TooShort,
        MissingUppercase,
        MissingLowercase,
        MissingDigit,
        MissingSymbol,
        ReusesRecentPassword,
    }
}
