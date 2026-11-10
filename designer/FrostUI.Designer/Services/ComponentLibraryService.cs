using System.Collections.ObjectModel;
using System.IO;
using FrostUI.Designer.Models;

namespace FrostUI.Designer.Services;

public sealed record ComponentEntry(string Name, string Path);

/// <summary>
/// Reusable control snippets. A component is stored as a tiny FrostUI design
/// document so scripts, images, fonts and layout metadata survive a round-trip.
/// Inserting a component clones its controls; it is a snippet, not a live
/// component instance.
/// </summary>
public static class ComponentLibraryService
{
    private static string DirectoryPath => System.IO.Path.Combine(
        Directory.GetCurrentDirectory(),
        "components");

    public static IReadOnlyList<ComponentEntry> List()
    {
        try
        {
            if (!Directory.Exists(DirectoryPath))
            {
                return [];
            }

            return Directory
                .GetFiles(DirectoryPath, "*.frostui.json")
                .Select(path =>
                {
                    try
                    {
                        var document = DesignSerializer.Load(path);
                        return new ComponentEntry(
                            string.IsNullOrWhiteSpace(document.Name)
                                ? System.IO.Path.GetFileNameWithoutExtension(path)
                                : document.Name,
                            path);
                    }
                    catch
                    {
                        return null;
                    }
                })
                .Where(entry => entry is not null)
                .Cast<ComponentEntry>()
                .OrderBy(entry => entry.Name, StringComparer.CurrentCulture)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    public static ComponentEntry Save(string name, IEnumerable<DesignControl> controls)
    {
        Directory.CreateDirectory(DirectoryPath);
        var safeName = MakeSafeName(name);
        var path = System.IO.Path.Combine(DirectoryPath, safeName + ".frostui.json");
        var document = new DesignDocument
        {
            Name = name.Trim(),
            WindowTitle = name.Trim(),
            Controls = new ObservableCollection<DesignControl>(
                controls.Select(control => control.Clone(control.Name))),
        };
        DesignSerializer.Save(document, path);
        return new ComponentEntry(document.Name, path);
    }

    public static IReadOnlyList<DesignControl> Load(ComponentEntry entry) =>
        DesignSerializer.Load(entry.Path).Controls.ToArray();

    public static ComponentEntry? Resolve(string source, string variant = "")
    {
        var entries = List();
        if (!string.IsNullOrWhiteSpace(variant))
        {
            var variantName = $"{source}@{variant}";
            var variantEntry = entries.FirstOrDefault(
                entry => string.Equals(entry.Name, variantName, StringComparison.CurrentCultureIgnoreCase));
            if (variantEntry is not null)
            {
                return variantEntry;
            }
        }

        return entries.FirstOrDefault(
            entry => string.Equals(entry.Name, source, StringComparison.CurrentCultureIgnoreCase));
    }

    public static string LibraryDirectory => DirectoryPath;

    public static void Delete(ComponentEntry entry)
    {
        if (File.Exists(entry.Path))
        {
            File.Delete(entry.Path);
        }
    }

    public static ComponentEntry Duplicate(ComponentEntry entry)
    {
        var document = DesignSerializer.Load(entry.Path);
        return Save($"{document.Name} 副本", document.Controls);
    }

    public static void Export(ComponentEntry entry, string destination) =>
        File.Copy(entry.Path, destination, overwrite: true);

    public static ComponentEntry Import(string sourcePath)
    {
        var document = DesignSerializer.Load(sourcePath);
        var name = string.IsNullOrWhiteSpace(document.Name)
            ? Path.GetFileNameWithoutExtension(sourcePath)
            : document.Name;
        return Save(name, document.Controls);
    }

    public static ComponentEntry Rename(ComponentEntry entry, string newName)
    {
        var document = DesignSerializer.Load(entry.Path);
        document.Name = newName.Trim();
        document.WindowTitle = newName.Trim();
        var renamed = Save(newName, document.Controls);
        if (!string.Equals(entry.Path, renamed.Path, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(entry.Path);
        }

        return renamed;
    }

    private static string MakeSafeName(string name)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var safe = new string(name.Trim()
            .Select(c => invalid.Contains(c) ? '_' : c)
            .ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "组件" : safe;
    }
}
