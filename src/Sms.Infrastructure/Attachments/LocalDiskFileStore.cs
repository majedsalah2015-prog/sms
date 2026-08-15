using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Sms.Application.Attachments;

namespace Sms.Infrastructure.Attachments
{
    /// <summary>
    /// T-7 on-prem/dev implementation: files land under a root directory
    /// keyed by a random, non-guessable reference (BR-ATT-010/BR-SEC-023 —
    /// never a caller-constructible path). A blob-storage implementation is
    /// a drop-in IFileStore replacement for the cloud posture (doc 02 §5).
    /// </summary>
    public class LocalDiskFileStore : IFileStore
    {
        private readonly string _rootPath;

        public LocalDiskFileStore(string rootPath)
        {
            _rootPath = rootPath;
            Directory.CreateDirectory(_rootPath);
        }

        public async Task<string> SaveAsync(byte[] content, string suggestedFileName, CancellationToken cancellationToken = default)
        {
            var reference = $"{Guid.NewGuid():N}{Path.GetExtension(suggestedFileName)}";
            var path = Path.Combine(_rootPath, reference);
            await File.WriteAllBytesAsync(path, content, cancellationToken);
            return reference;
        }

        public Task<byte[]> ReadAsync(string storageReference, CancellationToken cancellationToken = default)
            => File.ReadAllBytesAsync(ResolvePath(storageReference), cancellationToken);

        public Task DeleteAsync(string storageReference, CancellationToken cancellationToken = default)
        {
            var path = ResolvePath(storageReference);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return Task.CompletedTask;
        }

        private string ResolvePath(string storageReference)
        {
            // Guard against a malformed/malicious reference escaping the root (defense in depth — references are always ones we minted).
            var path = Path.GetFullPath(Path.Combine(_rootPath, storageReference));
            if (!path.StartsWith(Path.GetFullPath(_rootPath), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Storage reference resolves outside the file store root.");
            }

            return path;
        }
    }
}
