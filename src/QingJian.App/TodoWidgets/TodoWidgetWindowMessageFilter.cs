namespace QingJian.App.TodoWidgets;

public static class TodoWidgetWindowMessageFilter
{
    public const int WmSysCommand = 0x0112;
    public const int ScMinimize = 0xF020;
    private const int SystemCommandMask = 0xFFF0;

    public static bool ShouldBlockMinimize(int msg, IntPtr wParam, bool isHidingFromButton)
    {
        if (isHidingFromButton || msg != WmSysCommand)
        {
            return false;
        }

        return ((int)wParam & SystemCommandMask) == ScMinimize;
    }
}
