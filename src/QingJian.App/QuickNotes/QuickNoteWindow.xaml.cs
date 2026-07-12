using System.Windows;
using System.Windows.Input;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Windows.Interop;

namespace QingJian.App.QuickNotes;

public partial class QuickNoteWindow : Window, IQuickNoteWindow
{
    private bool _isSaved;
    private bool _isSaving;
    private bool _allowCloseWithoutConfirmation;

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

    public bool TryBeginSave()
    {
        if (_isSaving)
        {
            return false;
        }

        _isSaving = true;
        ErrorTextBlock.Visibility = Visibility.Collapsed;
        UpdateInteractionState();
        return true;
    }

    public void CompleteSave(bool succeeded)
    {
        _isSaving = false;
        if (succeeded)
        {
            _isSaved = true;
            _allowCloseWithoutConfirmation = true;
            return;
        }

        UpdateInteractionState();
    }

    public void CloseWindow()
    {
        _isSaved = true;
        _allowCloseWithoutConfirmation = true;
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
        AttemptUserClose();
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
            AttemptUserClose();
            e.Handled = true;
        }
    }

    private void Window_OnClosing(object? sender, CancelEventArgs e)
    {
        var hasBody = QuickNoteTitleGenerator.HasBody(BodyTextBox.Text);
        var discardConfirmed = false;

        if (!_allowCloseWithoutConfirmation && !_isSaved && !_isSaving && hasBody)
        {
            var result = MessageBox.Show(
                this,
                "要丢弃这条未保存的便签吗？",
                "QingJian",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            discardConfirmed = result == MessageBoxResult.Yes;
        }

        var decision = DecideClose(
            _allowCloseWithoutConfirmation,
            _isSaved,
            _isSaving,
            hasBody,
            discardConfirmed);

        e.Cancel = decision.ShouldCancel;
        _allowCloseWithoutConfirmation |= decision.ShouldAllowFutureClose;
    }

    private void BodyTextBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdateSaveButtonState();
        ErrorTextBlock.Visibility = Visibility.Collapsed;
    }

    private void RequestSave()
    {
        if (_isSaving)
        {
            return;
        }

        if (!QuickNoteTitleGenerator.HasBody(BodyTextBox.Text))
        {
            ShowSaveError("请输入便签内容。");
            return;
        }

        SaveRequested?.Invoke(this, new QuickNoteDraft(TitleTextBox.Text, BodyTextBox.Text));
    }

    private void AttemptUserClose()
    {
        if (_isSaving)
        {
            return;
        }

        Close();
    }

    private static CloseDecision DecideClose(
        bool allowCloseWithoutConfirmation,
        bool isSaved,
        bool isSaving,
        bool hasBody,
        bool discardConfirmed)
    {
        if (allowCloseWithoutConfirmation || isSaved || !hasBody)
        {
            return new CloseDecision(ShouldCancel: false, ShouldAllowFutureClose: true);
        }

        if (isSaving)
        {
            return new CloseDecision(ShouldCancel: true, ShouldAllowFutureClose: false);
        }

        return discardConfirmed
            ? new CloseDecision(ShouldCancel: false, ShouldAllowFutureClose: true)
            : new CloseDecision(ShouldCancel: true, ShouldAllowFutureClose: false);
    }

    private void UpdateSaveButtonState()
    {
        SaveButton.IsEnabled = !_isSaving && QuickNoteTitleGenerator.HasBody(BodyTextBox.Text);
    }

    private void UpdateInteractionState()
    {
        TitleTextBox.IsEnabled = !_isSaving;
        BodyTextBox.IsEnabled = !_isSaving;
        CancelButton.IsEnabled = !_isSaving;
        UpdateSaveButtonState();
    }

    private void PositionNearMouse()
    {
        if (!TryGetCursorPos(out var cursor))
        {
            CenterInWorkingArea(SystemParameters.WorkArea);
            return;
        }

        if (!TryGetWorkingArea(cursor, out var workingArea, out var scaleX, out var scaleY))
        {
            CenterInWorkingArea(SystemParameters.WorkArea);
            return;
        }

        var placement = CalculateWindowPlacement(cursor, workingArea, scaleX, scaleY, ActualWidth > 0 ? ActualWidth : Width, ActualHeight > 0 ? ActualHeight : Height);
        ApplyPixelPlacement(placement);
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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, MONITOR_DPI_TYPE dpiType, out uint dpiX, out uint dpiY);

    private static bool TryGetCursorPos(out POINT point)
    {
        return GetCursorPos(out point);
    }

    private static bool TryGetWorkingArea(POINT cursor, out Rect workingArea, out double scaleX, out double scaleY)
    {
        const uint monitorDefaultToNearest = 0x00000002;
        var monitor = MonitorFromPoint(cursor, monitorDefaultToNearest);
        var monitorInfo = new MONITORINFO
        {
            cbSize = Marshal.SizeOf<MONITORINFO>()
        };

        if (monitor != IntPtr.Zero
            && GetMonitorInfo(monitor, ref monitorInfo)
            && TryGetMonitorScale(monitor, out scaleX, out scaleY))
        {
            var topLeft = PixelsToDips(new Point(monitorInfo.rcWork.Left, monitorInfo.rcWork.Top), scaleX, scaleY);
            var bottomRight = PixelsToDips(new Point(monitorInfo.rcWork.Right, monitorInfo.rcWork.Bottom), scaleX, scaleY);

            workingArea = new Rect(topLeft, bottomRight);
            return true;
        }

        workingArea = Rect.Empty;
        scaleX = 0;
        scaleY = 0;
        return false;
    }

    private static WindowPlacement CalculateWindowPlacement(
        POINT cursor,
        Rect workingArea,
        double scaleX,
        double scaleY,
        double windowWidthDip,
        double windowHeightDip)
    {
        var widthPixels = Math.Max(0, (int)Math.Round(windowWidthDip * scaleX));
        var heightPixels = Math.Max(0, (int)Math.Round(windowHeightDip * scaleY));
        var workingAreaPixels = DipsToPixels(workingArea, scaleX, scaleY);

        var left = Clamp(cursor.X + 12, workingAreaPixels.Left, workingAreaPixels.Right - widthPixels);
        var top = Clamp(cursor.Y + 12, workingAreaPixels.Top, workingAreaPixels.Bottom - heightPixels);

        return new WindowPlacement(left, top);
    }

    private void ApplyPixelPlacement(WindowPlacement placement)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        const uint noSize = 0x0001;
        const uint noZOrder = 0x0004;
        const uint noActivate = 0x0010;

        SetWindowPos(handle, IntPtr.Zero, placement.Left, placement.Top, 0, 0, noSize | noZOrder | noActivate);
    }

    private static Point PixelsToDips(Point pixelPoint, double scaleX, double scaleY)
    {
        return new Point(pixelPoint.X / scaleX, pixelPoint.Y / scaleY);
    }

    private static RECT DipsToPixels(Rect dipRect, double scaleX, double scaleY)
    {
        return new RECT
        {
            Left = (int)Math.Round(dipRect.Left * scaleX),
            Top = (int)Math.Round(dipRect.Top * scaleY),
            Right = (int)Math.Round(dipRect.Right * scaleX),
            Bottom = (int)Math.Round(dipRect.Bottom * scaleY)
        };
    }

    private static int Clamp(int value, int min, int max)
    {
        if (max < min)
        {
            return min;
        }

        return Math.Min(Math.Max(value, min), max);
    }

    private static bool TryGetMonitorScale(IntPtr monitor, out double scaleX, out double scaleY)
    {
        try
        {
            const double defaultDpi = 96d;
            const int success = 0;

            if (GetDpiForMonitor(monitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dpiX, out var dpiY) == success)
            {
                scaleX = dpiX / defaultDpi;
                scaleY = dpiY / defaultDpi;
                return scaleX > 0 && scaleY > 0;
            }
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }

        scaleX = 0;
        scaleY = 0;
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

    private readonly record struct WindowPlacement(int Left, int Top);

    private readonly record struct CloseDecision(bool ShouldCancel, bool ShouldAllowFutureClose);

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private enum MONITOR_DPI_TYPE
    {
        MDT_EFFECTIVE_DPI = 0
    }
}

public sealed class QuickNoteWindowFactory : IQuickNoteWindowFactory
{
    public IQuickNoteWindow Create()
    {
        return new QuickNoteWindow();
    }
}
