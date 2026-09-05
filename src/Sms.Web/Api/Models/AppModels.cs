using System;

namespace Sms.Web.Api.Models
{
    /// <summary>
    /// What the school is publishing, and whether the phone asking is behind it.
    /// <para>
    /// The verdicts are the server's, not the client's. An app that decided for
    /// itself would be deciding with whatever comparison the *old* build shipped
    /// — and the old build is precisely the one being asked about, so a bug in
    /// it could never be corrected from the school's side. Sending
    /// <see cref="UpdateAvailable"/> and <see cref="UpdateRequired"/> already
    /// decided means a school can start requiring an update without anyone
    /// installing anything first.
    /// </para>
    /// <para>
    /// The wording is deliberately <b>not</b> here. Every refusal this API gives
    /// is translated at the web boundary because only the server knows the rule;
    /// this is the other case — the sentence is a label on a screen the app owns,
    /// it has both languages for it in <c>lib/l10n/strings.dart</c>, and a phrase
    /// carried over the wire would be the one thing on that banner that goes
    /// blank when the network is slow.
    /// </para>
    /// </summary>
    public sealed class ApiAppVersionResponse
    {
        /// <summary>
        /// False when the school has published no package at all. Everything
        /// below is then null or false: <em>"nobody has published a build"</em>
        /// and <em>"you are up to date"</em> are different statements, and an app
        /// that showed the second for the first would nag a family about an
        /// update that does not exist.
        /// </summary>
        public bool Published { get; set; }

        /// <summary>The dotted version of the newest published package — <c>1.4.0</c>.</summary>
        public string? LatestVersion { get; set; }

        /// <summary>Android's versionCode for it, when the file name carried one.</summary>
        public int? LatestBuild { get; set; }

        /// <summary>When that file was put in the folder. Shown, never compared.</summary>
        public DateTime? PublishedAtUtc { get; set; }

        /// <summary>
        /// The oldest build the school still accepts, from
        /// <c>MobileApp:MinimumSupportedVersion</c>. Null when the school has set
        /// none, which is the default and means nothing is ever forced.
        /// </summary>
        public string? MinimumVersion { get; set; }

        /// <summary>Its versionCode half, when the setting carried one.</summary>
        public int? MinimumBuild { get; set; }

        /// <summary>A newer build than the caller's exists. Worth saying once, dismissible.</summary>
        public bool UpdateAvailable { get; set; }

        /// <summary>
        /// The caller is older than the school's stated minimum. The app blocks
        /// on this rather than nagging — but see the endpoint: it is never true
        /// unless a package new enough to satisfy the minimum is actually
        /// downloadable.
        /// </summary>
        public bool UpdateRequired { get; set; }

        /// <summary>
        /// Where a family goes to install it — the portal's own app page, which
        /// already carries the file, the size, the date and the four install
        /// steps in both languages. The app sends them there instead of
        /// carrying a second copy of that explanation.
        /// </summary>
        public string InstallUrl { get; set; } = string.Empty;
    }
}
