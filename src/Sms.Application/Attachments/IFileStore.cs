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
        /// <summary>
        /// Stores the bytes and returns the reference to read them back by.
        /// <para>
        /// <paramref name="folder"/> is a *category*, not a path: a caller says which kind of
        /// file this is (the document type's code — <c>STUDENT_PHOTO</c>, <c>CONTRACT</c>) and
        /// the store alone decides how that maps onto disk or into a container. That keeps
        /// BR-ATT-010 intact — no caller composes a location — while giving student photographs
        /// a folder of their own instead of one flat directory holding every file the school
        /// has ever uploaded. Null or unrecognisable puts the file at the root, which is where
        /// everything written before this parameter existed still lives and still reads.
        /// </para>
        /// </summary>
        Task<string> SaveAsync(byte[] content, string suggestedFileName, string? folder = null, CancellationToken cancellationToken = default);

        Task<byte[]> ReadAsync(string storageReference, CancellationToken cancellationToken = default);

        Task DeleteAsync(string storageReference, CancellationToken cancellationToken = default);
    }
}
