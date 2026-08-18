using System;
using System.Collections.Generic;

namespace Sms.Web.Models
{
    /// <summary>
    /// Backing model for <c>Views/Shared/_HelpModal.cshtml</c>: a "?" button that
    /// opens a modal with a short intro and a list of (heading, body) items —
    /// the per-screen help every module page can drop in.
    /// </summary>
    public sealed class HelpPanelViewModel
    {
        public sealed record Item(string Heading, string Body);

        /// <summary>DOM id of the modal; must be unique per page.</summary>
        public string Id { get; set; } = "help";

        public string Title { get; set; } = string.Empty;

        public string? Intro { get; set; }

        public IReadOnlyList<Item> Items { get; set; } = Array.Empty<Item>();

        /// <summary>Button caption; defaults to "Help" in the partial when null.</summary>
        public string? ButtonLabel { get; set; }
    }
}
