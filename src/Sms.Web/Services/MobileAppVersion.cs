using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Sms.Web.Services
{
    /// <summary>
    /// A build of the school's app, in the one form this product writes it:
    /// <c>1.4.0+12</c> — the dotted version a family reads, and Android's
    /// versionCode behind the <c>+</c>.
    /// <para>
    /// It exists because the two obvious ways to answer "is this phone behind?"
    /// are both wrong here. Comparing the strings ordinally makes <c>1.10.0</c>
    /// older than <c>1.9.0</c>, so the first school to reach a tenth minor
    /// release stops being offered updates and nobody finds out; and
    /// <see cref="Version"/> does not parse the <c>+N</c> half at all — it is not
    /// in that grammar — which matters because <c>mobile/pubspec.yaml</c> makes
    /// the build number the thing that must move on <b>every</b> published
    /// build, hotfixes of one version included. A comparison that ignored it
    /// would call <c>1.4.0+13</c> the same build as <c>1.4.0+12</c>.
    /// </para>
    /// <para>
    /// <b>Not part of approved Analysis v1.0.</b> Native mobile apps are
    /// <c>Future/</c> GAP <b>G5</b> / roadmap <b>R2</b>, so no module doc
    /// specifies this and no <c>BR-</c> rule governs it. Built on the owner's
    /// request; a reader looking for a numbered requirement should not conclude
    /// they missed it.
    /// </para>
    /// </summary>
    public readonly struct MobileAppVersion : IEquatable<MobileAppVersion>, IComparable<MobileAppVersion>
    {
        /// <summary>
        /// Deliberately forgiving about what is missing and strict about what is
        /// there. <c>1</c>, <c>1.4</c>, <c>1.4.0</c> and <c>1.4.0+12</c> all
        /// parse; <c>1.4.0-beta</c> and <c>latest</c> do not, because a caller
        /// that means something this cannot order should be told so rather than
        /// silently treated as <c>0.0.0</c> — which is what a lenient parse
        /// would do, and it would read as "hopelessly out of date".
        /// </summary>
        private static readonly Regex Shape = new(
            @"^\s*v?(?<major>\d{1,9})(?:\.(?<minor>\d{1,9}))?(?:\.(?<patch>\d{1,9}))?(?:\+(?<build>\d{1,9}))?\s*$",
            RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture);

        private MobileAppVersion(int major, int minor, int patch, int build)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            Build = build;
        }

        public int Major { get; }

        public int Minor { get; }

        public int Patch { get; }

        /// <summary>
        /// Android's versionCode, or <c>0</c> when the string did not carry one.
        /// Zero rather than null so ordering needs no special case, and it is
        /// never a real value: a published versionCode starts at 1.
        /// </summary>
        public int Build { get; }

        /// <summary>Whether the string this came from actually stated a build number.</summary>
        public bool HasBuild => Build > 0;

        /// <summary>
        /// True when the text was a version this can order. A caller that gets
        /// false must not carry on comparing — see <see cref="Shape"/>.
        /// </summary>
        public static bool TryParse(string? text, out MobileAppVersion version)
        {
            version = default;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var match = Shape.Match(text);
            if (!match.Success)
            {
                return false;
            }

            version = new MobileAppVersion(
                Part(match, "major"),
                Part(match, "minor"),
                Part(match, "patch"),
                Part(match, "build"));
            return true;

            static int Part(Match match, string name)
            {
                var group = match.Groups[name];
                return group.Success
                    ? int.Parse(group.Value, NumberStyles.None, CultureInfo.InvariantCulture)
                    : 0;
            }
        }

        /// <summary>
        /// Major, then minor, then patch, then the build number — so a hotfix
        /// republished as <c>1.4.0+13</c> is newer than <c>1.4.0+12</c>, which is
        /// the only way a school can ship a fix without inventing a version
        /// number for it.
        /// </summary>
        public int CompareTo(MobileAppVersion other)
        {
            if (Major != other.Major) return Major.CompareTo(other.Major);
            if (Minor != other.Minor) return Minor.CompareTo(other.Minor);
            if (Patch != other.Patch) return Patch.CompareTo(other.Patch);
            return Build.CompareTo(other.Build);
        }

        public bool Equals(MobileAppVersion other) => CompareTo(other) == 0;

        public override bool Equals(object? obj) => obj is MobileAppVersion other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch, Build);

        public static bool operator ==(MobileAppVersion left, MobileAppVersion right) => left.Equals(right);

        public static bool operator !=(MobileAppVersion left, MobileAppVersion right) => !left.Equals(right);

        public static bool operator <(MobileAppVersion left, MobileAppVersion right) => left.CompareTo(right) < 0;

        public static bool operator >(MobileAppVersion left, MobileAppVersion right) => left.CompareTo(right) > 0;

        public static bool operator <=(MobileAppVersion left, MobileAppVersion right) => left.CompareTo(right) <= 0;

        public static bool operator >=(MobileAppVersion left, MobileAppVersion right) => left.CompareTo(right) >= 0;

        /// <summary>The dotted half only — what a family is shown and reads aloud.</summary>
        public string ToVersionString()
            => FormattableString.Invariant($"{Major}.{Minor}.{Patch}");

        public override string ToString() => HasBuild
            ? FormattableString.Invariant($"{Major}.{Minor}.{Patch}+{Build}")
            : ToVersionString();
    }
}
