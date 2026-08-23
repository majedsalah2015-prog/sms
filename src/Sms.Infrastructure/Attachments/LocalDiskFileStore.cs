using System;
using System.IO;
using System.Text;
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
    /// <para>
    /// Each document type gets a folder of its own under the root, derived here from the code the
    /// caller passes rather than composed by the caller. So a school's student photographs sit
    /// together in one directory an administrator can back up, restore or hand to a printer,
    /// instead of being mixed in with every contract and certificate the system has ever stored.
    /// The file name inside it is still a GUID: the folder says what kind of file it is, never
    /// whose it is.
    /// </para>
    /// </summary>
    public class LocalDiskFileStore : IFileStore
    {
        private readonly string _rootPath;

        public LocalDiskFileStore(string rootPath)
        {
            _rootPath = rootPath;
            Directory.CreateDirectory(_rootPath);
        }

        public async Task<string> SaveAsync(byte[] content, string suggestedFileName, string? folder = null, CancellationToken cancellationToken = default)
        {
            var directory = FolderSegment(folder);
            var name = $"{Guid.NewGuid():N}{Path.GetExtension(suggestedFileName)}";
            var reference = directory.Length == 0 ? name : $"{directory}/{name}";

            // Through the same guard the reads use: a save is the one moment the store touches a
            // path built from something a caller said, so it gets checked exactly like a read does.
            var path = ResolvePath(reference);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
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

        /// <summary>
        /// A document type's code reduced to something safe to be a directory name — letters,
        /// digits, dash and underscore, nothing else. A school can define its own document types
        /// and name them in Arabic or with a slash in the code; none of that may reach the file
        /// system, and a code that survives none of it simply stores at the root.
        /// </summary>
        private static string FolderSegment(string? folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(folder.Length);
            foreach (var ch in folder)
            {
                if (ch is >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_') { builder.Append(ch); }
                else if (ch is >= 'A' and <= 'Z') { builder.Append(char.ToLowerInvariant(ch)); }
                if (builder.Length == 40) { break; }
            }

            return builder.ToString();
        }

        private string ResolvePath(string storageReference)
        {
            // Guard against a malformed/malicious reference escaping the root (defense in depth — references are always ones we minted).
            var root = Path.GetFullPath(_rootPath);
            if (!root.EndsWith(Path.DirectorySeparatorChar))
            {
                // Without the separator, a sibling directory whose name merely starts with the
                // root's — "sms-attachments-old" beside "sms-attachments" — would pass the test.
                root += Path.DirectorySeparatorChar;
            }

            var path = Path.GetFullPath(Path.Combine(root, storageReference));
            if (!path.StartsWith(root, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Storage reference resolves outside the file store root.");
            }

            return path;
        }
    }
}
