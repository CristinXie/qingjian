using System.Globalization;
using System.Text;

namespace QingJian.App.Services;

public static class FolderNamePolicy
{
    public const string UncategorizedName = "未分类";
    public const string AllNotesName = "全部便签";
    public const int MaxTextElements = 30;

    public static string NormalizeDisplayName(string? value)
    {
        return (value ?? string.Empty).Normalize(NormalizationForm.FormKC).Trim();
    }

    public static string NormalizeKey(string? value)
    {
        return NormalizeDisplayName(value).ToUpperInvariant();
    }

    public static string? GetValidationError(string? value, bool allowUncategorized)
    {
        var displayName = NormalizeDisplayName(value);
        if (displayName.Length == 0)
        {
            return "文件夹名称不能为空。";
        }

        if (displayName == AllNotesName)
        {
            return "“全部便签”是系统名称，不能作为文件夹名称。";
        }

        if (!allowUncategorized && displayName == UncategorizedName)
        {
            return "“未分类”是系统文件夹，不能作为普通文件夹名称。";
        }

        var textElementCount = new StringInfo(displayName).LengthInTextElements;
        return textElementCount > MaxTextElements
            ? "文件夹名称不能超过 30 个字符。"
            : null;
    }

    public static bool IsValidMoveTarget(string name)
    {
        return GetValidationError(name, allowUncategorized: true) is null;
    }
}
