using QingJian.App.ViewModels;
using Xunit;

namespace QingJian.App.Tests.ViewModels;

public sealed class NoteNavigationDateHelperTests
{
    [Theory]
    [InlineData(2026, 7, 20, "今天")]
    [InlineData(2026, 7, 21, "今天")]
    [InlineData(2026, 7, 19, "过去30天")]
    [InlineData(2026, 6, 20, "过去30天")]
    [InlineData(2026, 6, 19, "6月")]
    [InlineData(2026, 1, 5, "1月")]
    [InlineData(2025, 12, 31, "2025年")]
    public void GetGroupName_UsesAgreedLocalDateBuckets(int year, int month, int day, string expected)
    {
        var now = new DateTime(2026, 7, 20, 12, 0, 0, DateTimeKind.Local);
        var updatedAt = new DateTime(year, month, day, 8, 0, 0, DateTimeKind.Local);

        Assert.Equal(expected, NoteNavigationDateHelper.GetGroupName(updatedAt, now));
    }

    [Theory]
    [InlineData(2026, 1, 2, "创建于 1月2日")]
    [InlineData(2025, 12, 3, "创建于 2025年12月3日")]
    public void FormatCreatedAt_UsesYearSensitiveChineseFormat(int year, int month, int day, string expected)
    {
        var now = new DateTime(2026, 7, 20, 12, 0, 0, DateTimeKind.Local);
        var createdAt = new DateTime(year, month, day, 8, 0, 0, DateTimeKind.Local);

        Assert.Equal(expected, NoteNavigationDateHelper.FormatCreatedAt(createdAt, now));
    }

    [Theory]
    [InlineData(2026, 6, 19, 15, 30, "6/19 15:30")]
    [InlineData(2025, 6, 19, 15, 30, "2025/6/19 15:30")]
    public void FormatUpdatedAt_UsesYearSensitiveNavigationFormat(
        int year,
        int month,
        int day,
        int hour,
        int minute,
        string expected)
    {
        var now = new DateTime(2026, 7, 20, 12, 0, 0, DateTimeKind.Local);
        var updatedAt = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Local);

        Assert.Equal(expected, NoteNavigationDateHelper.FormatUpdatedAt(updatedAt, now));
    }
}
