namespace Sms.Web.Navigation
{
    /// <summary>
    /// One entry being rendered, how deep it sits, and which entry the whole menu settled on as the
    /// screen being shown. The sidebar renders itself recursively, and a partial takes one model, so
    /// both travel with the item.
    /// <para>
    /// <see cref="Depth"/> is presentation only: it picks the indent class. Nothing decides
    /// behaviour from it, so a menu that grows a level deeper renders correctly and merely stops
    /// getting more indented.
    /// </para>
    /// <para>
    /// <see cref="Active"/> is resolved once for the tree rather than asked of each entry in turn,
    /// because two entries may legitimately name one controller — the Sections module and its
    /// assignment board — and only the more specific of them is the screen you are on.
    /// </para>
    /// </summary>
    public sealed record SidebarNode(NavItem Item, int Depth, NavItem? Active = null);
}
