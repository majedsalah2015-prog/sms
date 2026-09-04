using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sms.Web.Services
{
    /// <summary>
    /// The Android package the school publishes for its families, if it has
    /// published one.
    /// <para>
    /// The build output is a binary of tens of megabytes and never belongs in
    /// the repository, so it is dropped into a configured folder
    /// (<c>MobileApp:PackagePath</c>, under <c>App_Data</c> by default, which
    /// <c>.gitignore</c> already excludes) and discovered from there. Nothing
    /// records it in the database: a file either sits in that folder or it does
    /// not, and a row claiming otherwise would be the thing that goes stale.
    /// </para>
    /// <para>
    /// <b>Not part of approved Analysis v1.0.</b> Native mobile apps are
    /// <c>Future/</c> GAP <b>G5</b> / roadmap <b>R2</b>, so no module doc
    /// specifies a distribution screen and no <c>BR-</c> rule governs one. Built
    /// on the owner's request; a reader looking for a numbered requirement
    /// should not conclude they missed it.
    /// </para>
    /// </summary>
    public sealed class MobileAppPackage
    {
        /// <summary>
        /// `sms-portal-1.4.0.apk`, `sms-portal-1.4.0+12.apk`. A file that does
        /// not follow it still downloads; it just shows no version, which is
        /// honest rather than a guess.
        /// </summary>
        private static readonly Regex VersionInName = new(
            @"-(?<v>\d+(?:\.\d+)*(?:\+\d+)?)\.apk$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private readonly string _root;

        public MobileAppPackage(string root)
        {
            _root = root;
        }

        /// <summary>
        /// The newest <c>.apk</c> in the folder, or null when the school has
        /// published nothing yet.
        /// <para>
        /// Newest by write time rather than by parsed version: a school that
        /// re-publishes a hotfix under the same version must not have it
        /// ignored, and comparing version strings would need a scheme nobody
        /// agreed to.
        /// </para>
        /// </summary>
        public PublishedPackage? Current()
        {
            if (!Directory.Exists(_root))
            {
                return null;
            }

            FileInfo? newest;
            try
            {
                newest = new DirectoryInfo(_root)
                    .EnumerateFiles("*.apk", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .FirstOrDefault();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // An unreadable folder is the same answer as an empty one from the
                // family's side, and neither is theirs to fix.
                return null;
            }

            if (newest == null)
            {
                return null;
            }

            var match = VersionInName.Match(newest.Name);
            return new PublishedPackage(
                newest.FullName,
                newest.Name,
                newest.Length,
                newest.LastWriteTimeUtc,
                match.Success ? match.Groups["v"].Value : null);
        }

        /// <summary>One published build.</summary>
        public sealed class PublishedPackage
        {
            public PublishedPackage(
                string fullPath,
                string fileName,
                long sizeBytes,
                DateTime modifiedUtc,
                string? version)
            {
                FullPath = fullPath;
                FileName = fileName;
                SizeBytes = sizeBytes;
                ModifiedUtc = modifiedUtc;
                Version = version;
            }

            /// <summary>
            /// Resolved by this class, never by anything a caller sent. The
            /// download action serves this path and no other, which is what
            /// keeps a file name in a query string from becoming a traversal.
            /// </summary>
            public string FullPath { get; }

            public string FileName { get; }

            public long SizeBytes { get; }

            public DateTime ModifiedUtc { get; }

            /// <summary>Null when the file name does not carry one.</summary>
            public string? Version { get; }
        }
    }
}
