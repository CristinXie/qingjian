using QingJian.App.Editor;
using Xunit;

namespace QingJian.App.Tests.Editor;

public sealed class EditorMessageTests
{
    [Fact]
    public void TryParse_ReturnsMarkdownChangedMessage()
    {
        var parsed = EditorMessage.TryParse(
            """{"type":"markdownChanged","markdown":"# Title\n\nBody"}""",
            out var message);

        Assert.True(parsed);
        Assert.Equal("markdownChanged", message.Type);
        Assert.Equal("# Title\n\nBody", message.Markdown);
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
    [InlineData("""{"markdown":"Body"}""")]
    [InlineData("""{"type":"unknown","markdown":"Body"}""")]
    public void TryParse_RejectsMalformedOrUnsupportedMessages(string? json)
    {
        var parsed = EditorMessage.TryParse(json, out var message);

        Assert.False(parsed);
        Assert.Equal(EditorMessage.Empty, message);
    }
}
