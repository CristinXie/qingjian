using QingJian.App.QuickNotes;
using Xunit;

namespace QingJian.App.Tests.QuickNotes;

public sealed class QuickNoteTitleGeneratorTests
{
    [Fact]
    public void HasBody_ReturnsFalse_ForNullEmptyOrWhitespace()
    {
        Assert.False(QuickNoteTitleGenerator.HasBody(null));
        Assert.False(QuickNoteTitleGenerator.HasBody(""));
        Assert.False(QuickNoteTitleGenerator.HasBody("   \r\n\t"));
    }

    [Fact]
    public void HasBody_ReturnsTrue_ForNonWhitespaceBody()
    {
        Assert.True(QuickNoteTitleGenerator.HasBody("remember this"));
    }

    [Fact]
    public void CreateTitle_UsesTrimmedRequestedTitle_WhenProvided()
    {
        var title = QuickNoteTitleGenerator.CreateTitle("  Meeting notes  ", "body");

        Assert.Equal("Meeting notes", title);
    }

    [Fact]
    public void CreateTitle_UsesFirstNonEmptyBodyLine_WhenTitleIsBlank()
    {
        var title = QuickNoteTitleGenerator.CreateTitle(" ", "\r\n  First line  \r\nSecond line");

        Assert.Equal("First line", title);
    }

    [Fact]
    public void CreateTitle_TruncatesLongGeneratedTitle()
    {
        var body = new string('a', QuickNoteTitleGenerator.MaxTitleLength + 10);

        var title = QuickNoteTitleGenerator.CreateTitle(null, body);

        Assert.Equal(QuickNoteTitleGenerator.MaxTitleLength, title.Length);
        Assert.EndsWith("...", title);
    }
}
