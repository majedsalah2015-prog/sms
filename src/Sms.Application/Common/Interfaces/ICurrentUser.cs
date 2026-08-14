namespace Sms.Application.Common.Interfaces
{
    /// <summary>Identity of the acting user, used for audit stamping (BR-GLB-007).</summary>
    public interface ICurrentUser
    {
        int UserId { get; }
    }
}
