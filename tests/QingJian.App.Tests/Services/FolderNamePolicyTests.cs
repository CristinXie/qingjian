using QingJian.App.Services;
using Xunit;

namespace QingJian.App.Tests.Services;

public sealed class FolderNamePolicyTests
{
    [Fact]
    public void GetValidationError_RejectsMoreThanThirtyUnicodeTextElements()
    {
        var value = string.Concat(Enumerable.Repeat("便", 31));

        var error = FolderNamePolicy.GetValidationError(value, allowUncategorized: false);

        Assert.Equal("文件夹名称不能超过 30 个字符。", error);
    }

    [Fact]
    public void NormalizeKey_UsesTrimmedCompatibilityNormalizedInvariantUppercase()
    {
        var normalized = FolderNamePolicy.NormalizeKey("  ｐｒｏｊｅｃｔ  ");

        Assert.Equal("PROJECT", normalized);
    }

    [Fact]
    public void GetValidationError_RejectsReservedNames()
    {
        Assert.Equal(
            "“全部便签”是系统名称，不能作为文件夹名称。",
            FolderNamePolicy.GetValidationError("全部便签", allowUncategorized: false));
        Assert.Equal(
            "“未分类”是系统文件夹，不能作为普通文件夹名称。",
            FolderNamePolicy.GetValidationError("未分类", allowUncategorized: false));
    }

    [Fact]
    public void GetValidationError_AllowsSystemUncategorizedNameWhenRequested()
    {
        Assert.Null(FolderNamePolicy.GetValidationError("未分类", allowUncategorized: true));
    }
}
