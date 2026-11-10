namespace FrostUI.Interop;

/// <summary>
/// Selects the material that is composited behind a <see cref="Controls.FrostWindow"/>.
/// </summary>
public enum WindowBackdrop
{
    /// <summary>Windows 10/11 acrylic: a live, tinted blur of the content behind the window.</summary>
    Acrylic = 0,

    /// <summary>Windows 11 Mica: samples the desktop wallpaper with a calm tint.</summary>
    Mica = 1,

    /// <summary>Windows 11 Mica Alt: like Mica, with stronger contrast and variation.</summary>
    MicaAlt = 2,

    /// <summary>The legacy Windows 10 blur-behind material (no tint).</summary>
    Blur = 3,

    /// <summary>No system material. The window falls back to its own surface brush.</summary>
    None = 4,
}
