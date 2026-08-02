using QingJian.App.Editor;
using Xunit;

namespace QingJian.App.Tests.Editor;

public sealed class MarkdownEditorStateTests
{
    [Fact]
    public void BeginLoad_StoresSelectedNoteAndNormalizesNullMarkdown()
    {
        var state = new MarkdownEditorState();

        var markdown = state.BeginLoad("note-1", null);

        Assert.Equal("note-1", state.CurrentNoteId);
        Assert.Equal(string.Empty, markdown);
        Assert.Equal(string.Empty, state.CurrentMarkdown);
    }

    [Fact]
    public void TryApplyEditorMarkdown_IgnoresEchoWhileLoading()
    {
        var state = new MarkdownEditorState();
        state.BeginLoad("note-1", "Original");

        var applied = state.TryApplyEditorMarkdown("note-1", "Echo", out var markdown);

        Assert.False(applied);
        Assert.Equal("Original", state.CurrentMarkdown);
        Assert.Equal("Original", markdown);
    }

    [Fact]
    public void TryApplyEditorMarkdown_AppliesChangeAfterLoadEnds()
    {
        var state = new MarkdownEditorState();
        state.BeginLoad("note-1", "Original");
        state.EndLoad();

        var applied = state.TryApplyEditorMarkdown("note-1", "Changed", out var markdown);

        Assert.True(applied);
        Assert.Equal("Changed", markdown);
        Assert.Equal("Changed", state.CurrentMarkdown);
    }

    [Fact]
    public void TryApplyEditorMarkdown_DoesNotApplyUnchangedContent()
    {
        var state = new MarkdownEditorState();
        state.BeginLoad("note-1", "Same");
        state.EndLoad();

        var applied = state.TryApplyEditorMarkdown("note-1", "Same", out var markdown);

        Assert.False(applied);
        Assert.Equal("Same", markdown);
    }

    [Fact]
    public void TryApplyEditorMarkdown_IgnoresChangeForStaleNoteId()
    {
        var state = new MarkdownEditorState();
        state.BeginLoad("note-1", "Original");
        state.EndLoad();

        var applied = state.TryApplyEditorMarkdown("note-2", "Stale", out var markdown);

        Assert.False(applied);
        Assert.Equal("Original", markdown);
        Assert.Equal("Original", state.CurrentMarkdown);
    }

    [Fact]
    public void TryApplyEditorMarkdown_IgnoresChangeWithoutMatchingNoteId()
    {
        var state = new MarkdownEditorState();
        state.BeginLoad("note-1", "Original");
        state.EndLoad();

        var applied = state.TryApplyEditorMarkdown(null, "Missing", out var markdown);

        Assert.False(applied);
        Assert.Equal("Original", markdown);
        Assert.Equal("Original", state.CurrentMarkdown);
    }

    [Fact]
    public void ShouldReloadSelection_ReturnsFalseForCurrentNote()
    {
        var state = new MarkdownEditorState();
        state.BeginLoad("note-1", "Body");
        state.EndLoad();

        Assert.False(state.ShouldReloadSelection("note-1"));
    }

    [Fact]
    public void ShouldReloadSelection_ReturnsTrueForDifferentNote()
    {
        var state = new MarkdownEditorState();
        state.BeginLoad("note-1", "Body");
        state.EndLoad();

        Assert.True(state.ShouldReloadSelection("note-2"));
    }
}
