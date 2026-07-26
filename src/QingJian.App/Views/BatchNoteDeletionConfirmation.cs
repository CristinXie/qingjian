using System.Windows;

namespace QingJian.App.Views;

public static class BatchNoteDeletionConfirmation
{
    public static string BuildPrompt(int count)
    {
        return $"确定删除选中的 {count} 条便签吗？\n\n删除后暂时无法在应用内恢复。";
    }

    public static bool IsConfirmed(MessageBoxResult result)
    {
        return result == MessageBoxResult.Yes;
    }
}
