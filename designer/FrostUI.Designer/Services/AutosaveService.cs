using System.IO;
using FrostUI.Designer.Models;

namespace FrostUI.Designer.Services;

public static class AutosaveService
{
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FrostUI.Designer");

    public static string FilePath => Path.Combine(DirectoryPath, "autosave.frostui.json");

    public static DateTime? LastSaved =>
        File.Exists(FilePath) ? File.GetLastWriteTime(FilePath) : null;

    public static void Save(DesignDocument document)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var temporary = FilePath + ".tmp";
            DesignSerializer.Save(document, temporary);
            File.Move(temporary, FilePath, overwrite: true);
        }
        catch
        {
            // Autosave is best-effort; a full disk must not break editing.
        }
    }

    public static DesignDocument? TryLoad(out DateTime timestamp)
    {
        timestamp = default;
        if (!File.Exists(FilePath))
        {
            return null;
        }

        try
        {
            timestamp = File.GetLastWriteTime(FilePath);
            return DesignSerializer.Load(FilePath);
        }
        catch
        {
            return null;
        }
    }

    public static void Delete()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }

            var temporary = FilePath + ".tmp";
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
        catch
        {
            // ignored
        }
    }
}
