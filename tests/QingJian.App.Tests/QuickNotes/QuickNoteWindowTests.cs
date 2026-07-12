using System.Reflection;
using System.Windows;
using QingJian.App.QuickNotes;
using Xunit;

namespace QingJian.App.Tests.QuickNotes;

public sealed class QuickNoteWindowTests
{
    [Fact]
    public void PixelsToDips_UsesProvidedMonitorScale()
    {
        var method = typeof(QuickNoteWindow).GetMethod(
            "PixelsToDips",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var result = Assert.IsType<Point>(method.Invoke(null, new object[] { new Point(240, 216), 1.25d, 1.5d }));

        Assert.Equal(192, result.X);
        Assert.Equal(144, result.Y);
    }
}
