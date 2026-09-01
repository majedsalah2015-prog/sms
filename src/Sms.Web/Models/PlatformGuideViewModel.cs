using System;
using System.Collections.Generic;

namespace Sms.Web.Models
{
    /// <summary>
    /// Backing model for <c>Views/Help/Index.cshtml</c> — the product's own user guide, reached from
    /// the help button in the top bar of every screen.
    /// <para>
    /// The per-screen panel (<see cref="HelpPanelViewModel"/>) answers "what does <i>this</i> screen
    /// do"; this answers "how is this product driven at all", which no single screen can say and
    /// which every screen assumes its reader already knows — that nothing is deleted, that a missing
    /// menu entry is a role rather than a fault, that a refusal is a sentence and not a code.
    /// </para>
    /// </summary>
    public sealed class PlatformGuideViewModel
    {
        /// <summary>
        /// True when the reader signed in as a parent or a student. They get their own, much shorter
        /// guide: the staff one describes screens BR-SEC-010 says they must not even learn exist.
        /// </summary>
        public bool ForPortal { get; init; }

        public IReadOnlyList<GuideSection> Sections { get; init; } = Array.Empty<GuideSection>();

        /// <summary>
        /// The modules and screens <b>this</b> reader may open, in module order — empty for a portal
        /// account, and a different length for every staff role (BR-GLB-070).
        /// </summary>
        public IReadOnlyList<GuideModule> Modules { get; init; } = Array.Empty<GuideModule>();

        public bool HasIndex => Modules.Count > 0;
    }

    /// <summary>One chapter of the guide: a heading, a paragraph, and its (heading, body) pairs.</summary>
    public sealed record GuideSection(
        string Key,
        string Icon,
        string Title,
        string Intro,
        IReadOnlyList<HelpPanelViewModel.Item> Items);

    /// <summary>
    /// One module in the index, with the screens of it this reader holds. <see cref="Controller"/>
    /// and <see cref="Action"/> are the module's own entry screen where it has one; the modules whose
    /// engines exist without screens yet have none, and are listed without a link rather than
    /// silently dropped.
    /// </summary>
    public sealed record GuideModule(
        string Code,
        string Number,
        string Title,
        string Icon,
        string? Controller,
        string? Action,
        IReadOnlyList<GuideScreen> Screens);

    /// <summary>
    /// One screen in the index. <see cref="Verbs"/> is what this reader may do on it, already
    /// translated — a screen they can read but not export says exactly that, which is the answer to
    /// most of "why is the button not there".
    /// </summary>
    public sealed record GuideScreen(string Title, IReadOnlyList<string> Verbs);
}
