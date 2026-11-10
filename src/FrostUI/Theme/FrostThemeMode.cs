namespace FrostUI.Theme;

/// <summary>Available FrostUI color themes.</summary>
/// <remarks>
/// Named <c>FrostThemeMode</c> rather than <c>ThemeMode</c> to avoid a collision with
/// <c>System.Windows.ThemeMode</c>, which is available on newer WPF versions.
/// </remarks>
public enum FrostThemeMode
{
    Dark,
    Light,
}
