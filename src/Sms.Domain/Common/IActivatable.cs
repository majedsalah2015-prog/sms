namespace Sms.Domain.Common
{
    /// <summary>
    /// Master data is deactivated, never hard-deleted (ADR-7, BR-GLB-005, DB-6).
    /// </summary>
    public interface IActivatable
    {
        bool IsActive { get; set; }
    }
}
