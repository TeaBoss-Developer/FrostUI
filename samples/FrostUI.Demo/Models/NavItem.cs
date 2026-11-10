namespace FrostUI.Demo.Models;

public sealed class NavItem
{
    public NavItem(string title, string icon, string pageKey, string? badge = null)
    {
        Title = title;
        Icon = icon;
        PageKey = pageKey;
        Badge = badge;
    }

    public string Title { get; }

    public string Icon { get; }

    public string PageKey { get; }

    public string? Badge { get; }

    public bool HasBadge => !string.IsNullOrWhiteSpace(Badge);
}
