using System.IO;
using FrostUI.Designer.Models;

namespace FrostUI.Designer.Services;

public sealed record HistoryEntry(
    string Label,
    string Json,
    string? SelectedName,
    DateTime Timestamp)
{
    public string TimestampText => Timestamp.ToString("HH:mm:ss");
}

/// <summary>
/// Snapshot-based undo/redo. Designs are small JSON documents, so storing
/// complete snapshots is simpler and much less error-prone than maintaining
/// inverse commands for every property and collection operation.
/// </summary>
public sealed class DesignHistory
{
    private readonly List<HistoryEntry> _entries = [];
    private int _index = -1;
    private int _savedIndex = -1;

    public const int Capacity = 80;

    public IReadOnlyList<HistoryEntry> Entries => _entries;

    public int CurrentIndex => _index;

    public bool CanUndo => _index > 0;

    public bool CanRedo => _index >= 0 && _index < _entries.Count - 1;

    public bool IsAtSavedState => _index >= 0 && _index == _savedIndex;

    public void Reset(DesignDocument document, string? selectedName)
    {
        _entries.Clear();
        _index = -1;
        _savedIndex = -1;
        Capture(document, selectedName, "初始状态");
        _savedIndex = _index;
    }

    public bool Capture(DesignDocument document, string? selectedName, string label)
    {
        var json = DesignSerializer.Serialize(document);
        if (_entries.Count > 0 &&
            string.Equals(_entries[^1].Json, json, StringComparison.Ordinal))
        {
            return false;
        }

        if (_index < _entries.Count - 1)
        {
            _entries.RemoveRange(_index + 1, _entries.Count - _index - 1);
        }

        _entries.Add(new HistoryEntry(
            string.IsNullOrWhiteSpace(label) ? "编辑" : label,
            json,
            selectedName,
            DateTime.Now));
        _index = _entries.Count - 1;

        if (_entries.Count > Capacity)
        {
            var remove = _entries.Count - Capacity;
            _entries.RemoveRange(0, remove);
            _index -= remove;
            _savedIndex = _savedIndex >= 0 ? Math.Max(-1, _savedIndex - remove) : -1;
        }

        return true;
    }

    public HistoryEntry? Undo()
    {
        if (!CanUndo)
        {
            return null;
        }

        _index--;
        return _entries[_index];
    }

    public HistoryEntry? Redo()
    {
        if (!CanRedo)
        {
            return null;
        }

        _index++;
        return _entries[_index];
    }

    public HistoryEntry? JumpTo(int index)
    {
        if (index < 0 || index >= _entries.Count || index == _index)
        {
            return null;
        }

        _index = index;
        return _entries[_index];
    }

    public void MarkSaved()
    {
        _savedIndex = _index;
    }

    public bool Load(string json)
    {
        try
        {
            _ = DesignSerializer.Deserialize(json);
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }
}
