namespace Sms.Domain.SysAdmin
{
    /// <summary>BR-SYS-006: expiry grace behavior is read-only degradation, never data lockout (product ethics stance).</summary>
    public enum LicenseStatus : short
    {
        Active = 1,
        Grace = 2,
        ReadOnly = 3,
    }
}
