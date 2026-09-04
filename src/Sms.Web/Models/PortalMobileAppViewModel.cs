using System;

namespace Sms.Web.Models
{
    /// <summary>
    /// What <c>/portal/app</c> shows. Every field is null when the school has
    /// published nothing yet, which the view says outright rather than drawing a
    /// download button that would refuse.
    /// </summary>
    public sealed class PortalMobileAppViewModel
    {
        public string? FileName { get; set; }

        public long? SizeBytes { get; set; }

        public DateTime? PublishedAtUtc { get; set; }

        /// <summary>Null when the published file name does not carry one.</summary>
        public string? Version { get; set; }

        public bool IsPublished => FileName != null;

        /// <summary>
        /// Megabytes, one decimal. Kept LTR-digit by the view like every other
        /// figure in this product; a parent on a metered connection is entitled
        /// to know what they are about to pull down.
        /// </summary>
        public string SizeText => SizeBytes is long bytes
            ? (bytes / 1048576d).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " MB"
            : string.Empty;
    }
}
