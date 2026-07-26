using QingJian.App.Models;

namespace QingJian.App.ViewModels;

public sealed class BatchSelectionState
{
    private readonly HashSet<string> _scopeIds = new(StringComparer.Ordinal);
    private readonly List<Note> _selectedNotes = new();

    public IReadOnlySet<string> ScopeIds => _scopeIds;

    public IReadOnlyList<Note> SelectedNotes => _selectedNotes;

    public int SelectedCount => _selectedNotes.Count;

    public bool AllSelectedAreFavorite =>
        _selectedNotes.Count > 0 && _selectedNotes.All(note => note.IsFavorite);

    public bool AllSelectedAreNotFavorite =>
        _selectedNotes.Count > 0 && _selectedNotes.All(note => !note.IsFavorite);

    public void Enter(IEnumerable<Note> visibleNotes)
    {
        _scopeIds.Clear();
        _selectedNotes.Clear();

        foreach (var note in visibleNotes)
        {
            if (!string.IsNullOrWhiteSpace(note.Id))
            {
                _scopeIds.Add(note.Id);
            }
        }
    }

    public void SetSelection(IEnumerable<Note> selectedNotes)
    {
        _selectedNotes.Clear();
        var selectedIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var note in selectedNotes)
        {
            if (_scopeIds.Contains(note.Id) && selectedIds.Add(note.Id))
            {
                _selectedNotes.Add(note);
            }
        }
    }

    public void ClearSelection()
    {
        _selectedNotes.Clear();
    }

    public void Exit()
    {
        _scopeIds.Clear();
        _selectedNotes.Clear();
    }
}
