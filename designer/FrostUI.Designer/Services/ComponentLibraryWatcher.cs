using System.IO;

namespace FrostUI.Designer.Services;

/// <summary>Debounced file watcher for automatic component instance refresh.</summary>
public sealed class ComponentLibraryWatcher : IDisposable
{
    private readonly FileSystemWatcher? _watcher;
    private readonly System.Threading.Timer _timer;
    private readonly object _gate = new();
    private string? _pending;
    private bool _disposed;

    public event Action<string>? ComponentChanged;

    public ComponentLibraryWatcher(string directory)
    {
        _timer = new System.Threading.Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
        try
        {
            Directory.CreateDirectory(directory);
        }
        catch
        {
            // The manual refresh path still works if the directory cannot be created.
        }

        if (!Directory.Exists(directory))
        {
            return;
        }

        _watcher = new FileSystemWatcher(directory, "*.frostui.json")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnChanged;
        _watcher.Created += OnChanged;
        _watcher.Renamed += (_, args) => Schedule(args.FullPath);
    }

    private void OnChanged(object sender, FileSystemEventArgs args) => Schedule(args.FullPath);

    private void Schedule(string path)
    {
        lock (_gate)
        {
            _pending = Path.GetFileNameWithoutExtension(path);
        }

        _timer.Change(500, Timeout.Infinite);
    }

    private void Flush()
    {
        if (_disposed)
        {
            return;
        }

        string? name;
        lock (_gate)
        {
            name = _pending;
            _pending = null;
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            ComponentChanged?.Invoke(name);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Dispose();
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
        }
    }
}
