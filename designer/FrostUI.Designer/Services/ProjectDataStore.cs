using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;

namespace FrostUI.Designer.Services;

/// <summary>Project-level shared values used by every window/dialog.</summary>
public static class ProjectDataStore
{
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> Values =
        new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, bool> Loaded = new(StringComparer.Ordinal);
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FrostUI.Designer",
        "project-data");

    public static string? Get(string project, string key)
    {
        EnsureLoaded(project);
        if (Values.TryGetValue(project, out var values) &&
            values.TryGetValue(key, out var value))
        {
            return value;
        }

        return null;
    }

    public static void Set(string project, string key, string value)
    {
        EnsureLoaded(project);
        Values.GetOrAdd(project, _ => new ConcurrentDictionary<string, string>(StringComparer.Ordinal))[key] = value;
        Persist(project);
    }

    public static void Clear(string project)
    {
        Values.TryRemove(project, out _);
        Loaded.TryRemove(project, out _);
        try
        {
            var path = PathFor(project);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Project data is best-effort.
        }
    }

    private static void EnsureLoaded(string project)
    {
        if (!Loaded.TryAdd(project, true))
        {
            return;
        }

        try
        {
            var path = PathFor(project);
            if (!File.Exists(path))
            {
                return;
            }

            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
            if (values is not null)
            {
                Values[project] = new ConcurrentDictionary<string, string>(
                    values,
                    StringComparer.Ordinal);
            }
        }
        catch
        {
            // Corrupt project data must not stop the runtime.
        }
    }

    private static void Persist(string project)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var values = Values.TryGetValue(project, out var current)
                ? current.ToDictionary(pair => pair.Key, pair => pair.Value)
                : new Dictionary<string, string>();
            File.WriteAllText(
                PathFor(project),
                JsonSerializer.Serialize(values, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Persistence is best-effort.
        }
    }

    private static string PathFor(string project)
    {
        var safe = string.Concat(project.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return Path.Combine(DirectoryPath, (string.IsNullOrWhiteSpace(safe) ? "project" : safe) + ".json");
    }
}
