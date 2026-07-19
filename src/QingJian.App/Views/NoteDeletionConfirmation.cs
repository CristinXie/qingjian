using System.Windows;

namespace QingJian.App.Views;

public static class NoteDeletionConfirmation
{
    public static string BuildPrompt(string title)
    {
        return $"确定要删除便签“{title}”吗？";
    }

    public static bool IsConfirmed(MessageBoxResult result)
    {
        return result == MessageBoxResult.Yes;
    }
}
