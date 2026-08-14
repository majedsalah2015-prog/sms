namespace Sms.Domain.Common
{
    /// <summary>
    /// Every tenant-owned entity carries SchoolId (ADR-2, BR-GLB-010).
    /// The DbContext applies the tenant query filter and write guard centrally;
    /// no screen or module may re-implement school scoping.
    /// </summary>
    public interface ISchoolScoped
    {
        int SchoolId { get; set; }
    }
}
