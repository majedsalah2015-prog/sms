namespace Sms.Application.Common.Interfaces
{
    /// <summary>Ambient school scope of the current request (ADR-2).</summary>
    public interface ITenantContext
    {
        int SchoolId { get; }
    }
}
