using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Runtime.InteropServices;

namespace QingJian.App.QuickNotes;

public partial class QuickNoteWindow : Window, IQuickNoteWindow
{
    private bool _isSaved;

    public QuickNoteWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            PositionNearMouse();
            UpdateSaveButtonState();
            BodyTextBox.Focus();
        };
    }

    public event EventHandler<QuickNoteDraft>? SaveRequested;

    public void ShowWindow()
    {
        Show();
    }

    public void CloseWindow()
    {
        _isSaved = true;
        Close();
    }

    public void ShowSaveError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorTextBlock.Visibility = Visibility.Visible;
    }

    private void DragHandle_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        RequestSave();
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        RequestCancel();
    }

    private void Window_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            RequestSave();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            RequestCancel();
            e.Handled = true;
        }
    }

    private void BodyTextBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdateSaveButtonState();
        ErrorTextBlock.Visibility = Visibility.Collapsed;
    }

    private void RequestSave()
    {
        if (!QuickNoteTitleGenerator.HasBody(BodyTextBox.Text))
        {
            ShowSaveError("请输入便签内容。");
            return;
        }

        SaveRequested?.Invoke(this, new QuickNoteDraft(TitleTextBox.Text, BodyTextBox.Text));
    }

    private void RequestCancel()
    {
        if (_isSaved || !QuickNoteTitleGenerator.HasBody(BodyTextBox.Text))
        {
            Close();
            return;
        }

        var result = MessageBox.Show(
            this,
            "要丢弃这条未保存的便签吗？",
            "QingJian",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            Close();
        }
    }

    private void UpdateSaveButtonState()
    {
        SaveButton.IsEnabled = QuickNoteTitleGenerator.HasBody(BodyTextBox.Text);
    }

    private void PositionNearMouse()
    {
        var transformFromDevice = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice
            ?? Matrix.Identity;

        if (!TryGetCursorPos(out var cursor))
        {
            CenterInWorkingArea(SystemParameters.WorkArea);
            return;
        }

        if (!TryGetWorkingArea(cursor, transformFromDevice, out var workingArea))
        {
            CenterInWorkingArea(SystemParameters.WorkArea);
            return;
        }

        var cursorDip = transformFromDevice.Transform(new Point(cursor.X, cursor.Y));
        Left = Math.Min(Math.Max(cursorDip.X + 12, workingArea.Left), workingArea.Right - Width);
        Top = Math.Min(Math.Max(cursorDip.Y + 12, workingArea.Top), workingArea.Bottom - Height);
    }

    private void CenterInWorkingArea(Rect workingArea)
    {
        Left = workingArea.Left + Math.Max(0, (workingArea.Width - Width) / 2);
        Top = workingArea.Top + Math.Max(0, (workingArea.Height - Height) / 2);
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT point, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO monitorInfo);

    private static bool TryGetCursorPos(out POINT point)
    {
        return GetCursorPos(out point);
    }

    private static bool TryGetWorkingArea(POINT cursor, Matrix transformFromDevice, out Rect workingArea)
    {
        const uint monitorDefaultToNearest = 0x00000002;
        var monitor = MonitorFromPoint(cursor, monitorDefaultToNearest);
        var monitorInfo = new MONITORINFO
        {
            cbSize = Marshal.SizeOf<MONITORINFO>()
        };

        if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref monitorInfo))
        {
            var topLeft = transformFromDevice.Transform(new Point(
                monitorInfo.rcWork.Left,
                monitorInfo.rcWork.Top));
            var bottomRight = transformFromDevice.Transform(new Point(
                monitorInfo.rcWork.Right,
                monitorInfo.rcWork.Bottom));

            workingArea = new Rect(topLeft, bottomRight);
            return true;
        }

        workingArea = Rect.Empty;
        return false;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }
}

public sealed class QuickNoteWindowFactory : IQuickNoteWindowFactory
{
    public IQuickNoteWindow Create()
    {
        return new QuickNoteWindow();
    }
}
