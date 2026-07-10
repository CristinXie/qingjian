using QingJian.App.Editor;
using Xunit;

namespace QingJian.App.Tests.Editor;

public sealed class EditorMessageTests
{
    [Fact]
    public void TryParse_ReturnsMarkdownChangedMessage()
    {
        var parsed = EditorMessage.TryParse(
            """{"type":"markdownChanged","noteId":"note-1","markdown":"# Title\n\nBody"}""",
            out var message);

        Assert.True(parsed);
        Assert.Equal("markdownChanged", message.Type);
        Assert.Equal("note-1", message.NoteId);
        Assert.Equal("# Title\n\nBody", message.Markdown);
    }

    [Fact]
    public void TryParse_DefaultsMissingNoteIdToEmptyString()
    {
        var parsed = EditorMessage.TryParse(
            """{"type":"markdownChanged","markdown":"Body"}""",
            out var message);

        Assert.True(parsed);
        Assert.Equal(string.Empty, message.NoteId);
        Assert.Equal("Body", message.Markdown);
    }

    [Fact]
    public void TryParse_AllowsEmptyMarkdown()
    {
        var parsed = EditorMessage.TryParse(
            """{"type":"markdownChanged","markdown":""}""",
            out var message);

        Assert.True(parsed);
        Assert.Equal("markdownChanged", message.Type);
        Assert.Equal(string.Empty, message.Markdown);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-json")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("""{"markdown":"Body"}""")]
    [InlineData("""{"type":"unknown","markdown":"Body"}""")]
    [InlineData("""{"type":{} ,"markdown":"Body"}""")]
    [InlineData("""{"type":"markdownChanged","noteId":{} ,"markdown":"Body"}""")]
    [InlineData("""{"type":"markdownChanged","markdown":{}}""")]
    public void TryParse_RejectsMalformedOrUnsupportedMessages(string? json)
    {
        var parsed = EditorMessage.TryParse(json, out var message);

        Assert.False(parsed);
        Assert.Equal(EditorMessage.Empty, message);
    }
}
