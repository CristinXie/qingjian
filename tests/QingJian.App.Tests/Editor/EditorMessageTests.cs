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

    [Fact]
    public void TryParse_ReturnsExternalLinkRequestedMessage()
    {
        var parsed = EditorMessage.TryParse(
            """{"type":"externalLinkRequested","url":"https://example.com"}""",
            out var message);

        Assert.True(parsed);
        Assert.Equal("externalLinkRequested", message.Type);
        Assert.Equal("https://example.com", message.Url);
    }

    [Fact]
    public void TryParse_ReturnsLocalImageRequestedMessage()
    {
        var parsed = EditorMessage.TryParse(
            """{"type":"localImageRequested","requestId":"request-1","fileName":"照片.png","dataUrl":"data:image/png;base64,abcd"}""",
            out var message);

        Assert.True(parsed);
        Assert.Equal("localImageRequested", message.Type);
        Assert.Equal("request-1", message.RequestId);
        Assert.Equal("照片.png", message.FileName);
        Assert.Equal("data:image/png;base64,abcd", message.DataUrl);
    }

    [Fact]
    public void TryParse_ReturnsEditorModeChangedMessage()
    {
        var parsed = EditorMessage.TryParse(
            """{"type":"editorModeChanged","editorMode":"markdown"}""",
            out var message);

        Assert.True(parsed);
        Assert.Equal("editorModeChanged", message.Type);
        Assert.Equal("markdown", message.EditorMode);
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
    [InlineData("""{"type":"externalLinkRequested","url":{}}""")]
    [InlineData("""{"type":"externalLinkRequested","url":""}""")]
    [InlineData("""{"type":"localImageRequested","requestId":"","fileName":"a.png","dataUrl":"data:image/png;base64,abcd"}""")]
    [InlineData("""{"type":"localImageRequested","requestId":"request-1","fileName":"","dataUrl":"data:image/png;base64,abcd"}""")]
    [InlineData("""{"type":"localImageRequested","requestId":"request-1","fileName":"a.png","dataUrl":""}""")]
    [InlineData("""{"type":"localImageRequested","requestId":{},"fileName":"a.png","dataUrl":"data:image/png;base64,abcd"}""")]
    [InlineData("""{"type":"editorModeChanged","editorMode":"invalid"}""")]
    [InlineData("""{"type":"editorModeChanged","editorMode":{}}""")]
    public void TryParse_RejectsMalformedOrUnsupportedMessages(string? json)
    {
        var parsed = EditorMessage.TryParse(json, out var message);

        Assert.False(parsed);
        Assert.Equal(EditorMessage.Empty, message);
    }
}
