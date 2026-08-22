namespace Sms.Web.Navigation
{
    /// <summary>
    /// One entry being rendered, and how deep it sits. The sidebar renders itself recursively, and a
    /// partial takes one model, so the depth travels with the item.
    /// <para>
    /// <see cref="Depth"/> is presentation only: it picks the indent class. Nothing decides
    /// behaviour from it, so a menu that grows a level deeper renders correctly and merely stops
    /// getting more indented.
    /// </para>
    /// </summary>
    public sealed record SidebarNode(NavItem Item, int Depth);
}
