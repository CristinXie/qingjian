namespace QingJian.App.ViewModels;

public static class NoteNavigationDateHelper
{
    public static string GetGroupName(DateTime updatedAt, DateTime localNow)
    {
        var date = ToLocal(updatedAt).Date;
        var today = ToLocal(localNow).Date;

        if (date >= today)
        {
            return "今天";
        }

        if (date >= today.AddDays(-30))
        {
            return "过去30天";
        }

        return date.Year == today.Year
            ? $"{date.Month}月"
            : $"{date.Year}年";
    }

    public static string FormatCreatedAt(DateTime createdAt, DateTime localNow)
    {
        var date = ToLocal(createdAt);
        var now = ToLocal(localNow);

        return date.Year == now.Year
            ? $"创建于 {date.Month}月{date.Day}日"
            : $"创建于 {date.Year}年{date.Month}月{date.Day}日";
    }

    private static DateTime ToLocal(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value;
    }
}
