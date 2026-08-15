using System.Threading;
using System.Threading.Tasks;

namespace Sms.Application.Attachments
{
    /// <summary>
    /// T-7 storage abstraction (disk on-prem / blob cloud, doc 02 §5). The
    /// database only ever holds the opaque reference this returns
    /// (BR-ATT-010) — callers never see or construct real paths/URLs.
    /// </summary>
    public interface IFileStore
    {
        Task<string> SaveAsync(byte[] content, string suggestedFileName, CancellationToken cancellationToken = default);

        Task<byte[]> ReadAsync(string storageReference, CancellationToken cancellationToken = default);

        Task DeleteAsync(string storageReference, CancellationToken cancellationToken = default);
    }
}
