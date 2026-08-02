using System.Drawing;
using System.Windows.Forms;

namespace QingJian.App.Tray;

public sealed class WindowsTrayIconService : ITrayIconService
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly ToolStripMenuItem _todoMenuItem;
    private readonly Icon? _ownedIcon;
    private bool _disposed;

    public WindowsTrayIconService(string? executablePath = null)
    {
        _contextMenu = new ContextMenuStrip();

        var openItem = new ToolStripMenuItem("打开主窗口");
        openItem.Click += (_, _) => OpenMainWindowRequested?.Invoke(this, EventArgs.Empty);
        var quickNoteItem = new ToolStripMenuItem("新建快捷便签");
        quickNoteItem.Click += (_, _) => QuickNoteRequested?.Invoke(this, EventArgs.Empty);
        _todoMenuItem = new ToolStripMenuItem("显示桌面待办");
        _todoMenuItem.Click += (_, _) => ToggleTodoRequested?.Invoke(this, EventArgs.Empty);
        var exitItem = new ToolStripMenuItem("退出 QingJian");
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        _contextMenu.Items.Add(openItem);
        _contextMenu.Items.Add(quickNoteItem);
        _contextMenu.Items.Add(_todoMenuItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(exitItem);

        var iconPath = executablePath ?? Environment.ProcessPath;
        _ownedIcon = string.IsNullOrWhiteSpace(iconPath)
            ? null
            : Icon.ExtractAssociatedIcon(iconPath);
        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = _contextMenu,
            Icon = _ownedIcon ?? SystemIcons.Application,
            Text = "QingJian"
        };
        _notifyIcon.DoubleClick += (_, _) =>
            OpenMainWindowRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? OpenMainWindowRequested;

    public event EventHandler? QuickNoteRequested;

    public event EventHandler? ToggleTodoRequested;

    public event EventHandler? ExitRequested;

    public bool IsAvailable => !_disposed;

    public void Show()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _notifyIcon.Visible = true;
    }

    public void SetTodoVisible(bool isVisible)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _todoMenuItem.Text = isVisible ? "隐藏桌面待办" : "显示桌面待办";
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();
        _ownedIcon?.Dispose();
    }
}
