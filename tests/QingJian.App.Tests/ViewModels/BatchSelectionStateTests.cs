using QingJian.App.Models;
using QingJian.App.ViewModels;
using Xunit;

namespace QingJian.App.Tests.ViewModels;

public sealed class BatchSelectionStateTests
{
    [Fact]
    public void EnterAndSetSelection_DeduplicateAndRejectOutOfScopeNotes()
    {
        var first = CreateNote("first");
        var second = CreateNote("second");
        var outside = CreateNote("outside");
        var state = new BatchSelectionState();

        state.Enter(new[] { first, first, second });
        state.SetSelection(new[] { second, outside, second });

        Assert.Equal(new HashSet<string> { "first", "second" }, state.ScopeIds);
        Assert.Equal(new[] { "second" }, state.SelectedNotes.Select(note => note.Id));
        Assert.Equal(1, state.SelectedCount);
    }

    [Fact]
    public void FavoriteFlags_ReflectMixedAndUniformSelections()
    {
        var favorite = CreateNote("favorite");
        favorite.IsFavorite = true;
        var normal = CreateNote("normal");
        var state = new BatchSelectionState();
        state.Enter(new[] { favorite, normal });

        state.SetSelection(new[] { favorite, normal });
        Assert.False(state.AllSelectedAreFavorite);
        Assert.False(state.AllSelectedAreNotFavorite);

        state.SetSelection(new[] { favorite });
        Assert.True(state.AllSelectedAreFavorite);
        Assert.False(state.AllSelectedAreNotFavorite);

        state.SetSelection(new[] { normal });
        Assert.False(state.AllSelectedAreFavorite);
        Assert.True(state.AllSelectedAreNotFavorite);
    }

    [Fact]
    public void ClearAndExit_ResetSelectionAndScope()
    {
        var note = CreateNote("note");
        var state = new BatchSelectionState();
        state.Enter(new[] { note });
        state.SetSelection(new[] { note });

        state.ClearSelection();
        Assert.Empty(state.SelectedNotes);
        Assert.Single(state.ScopeIds);

        state.Exit();
        Assert.Empty(state.ScopeIds);
        Assert.Empty(state.SelectedNotes);
    }

    private static Note CreateNote(string id)
    {
        return new Note
        {
            Id = id,
            Title = id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
