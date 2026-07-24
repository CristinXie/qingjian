using System.Windows;
using System.Windows.Input;
using QingJian.App.Hotkeys;
using QingJian.App.Services;
using QingJian.App.TodoWidgets;

namespace QingJian.App.Settings;

public partial class SettingsWindow : Window
{
    private readonly SettingsCoordinator _coordinator;
    private readonly SettingsWindowDraft _draft = new();
    private bool _isLoading = true;
    private bool _isSaving;
    private uint _hotkeyModifiers;
    private uint _hotkeyVirtualKey;

    public SettingsWindow(SettingsCoordinator coordinator)
    {
        _coordinator = coordinator;
        InitializeComponent();
    }

    private async void SettingsWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var state = await _coordinator.LoadAsync();
            LoadState(state);
            SaveButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            ShowError($"无法加载设置：{ex.Message}");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void LoadState(SettingsState state)
    {
        _draft.LaunchAtStartup = state.LaunchAtStartup;
        _draft.EditorMode = state.AppSettings.EditorMode;
        _draft.QuickNoteHotkey = state.AppSettings.QuickNoteHotkey;
        _draft.TodoVisible = state.TodoWidget.IsVisible;
        _draft.TodoMode = state.TodoWidget.Mode;
        _draft.TodoOpacity = state.TodoWidget.Opacity;
        _draft.TodoLocked = state.TodoWidget.IsLocked;

        LaunchAtStartupCheckBox.IsChecked = _draft.LaunchAtStartup;
        WysiwygModeRadioButton.IsChecked = _draft.EditorMode == AppSettings.DefaultEditorMode;
        MarkdownModeRadioButton.IsChecked = _draft.EditorMode == AppSettings.MarkdownEditorMode;
        HotkeyEnabledCheckBox.IsChecked = _draft.QuickNoteHotkey.IsEnabled;
        _hotkeyModifiers = _draft.QuickNoteHotkey.Modifiers;
        _hotkeyVirtualKey = _draft.QuickNoteHotkey.VirtualKey;
        UpdateHotkeyDisplay();

        TodoVisibleCheckBox.IsChecked = _draft.TodoVisible;
        EightDayModeRadioButton.IsChecked = _draft.TodoMode == TodoWidgetMode.EightDay;
        TodayModeRadioButton.IsChecked = _draft.TodoMode == TodoWidgetMode.Today;
        CalendarModeRadioButton.IsChecked = _draft.TodoMode == TodoWidgetMode.Calendar;
        TodoOpacitySlider.Value = _draft.TodoOpacity;
        TodoLockedCheckBox.IsChecked = _draft.TodoLocked;
        UpdateOpacityDisplay();

        DataFolderTextBox.Text = state.Storage.DataFolder;
        DatabaseSizeTextBlock.Text = FormatBytes(state.Storage.DatabaseBytes);
        AttachmentSizeTextBlock.Text = FormatBytes(state.Storage.AttachmentBytes);
        TotalSizeTextBlock.Text = FormatBytes(state.Storage.TotalBytes);
        AppVersionTextBlock.Text = state.Runtime.AppVersion;
        DotNetVersionTextBlock.Text = state.Runtime.DotNetVersion;
        WebView2VersionTextBlock.Text = state.Runtime.WebView2Version;
    }

    private void HotkeyEnabledCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        HotkeyTextBox.IsEnabled = HotkeyEnabledCheckBox.IsChecked == true;
    }

    private void HotkeyTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CancelAndClose();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Back)
        {
            _hotkeyModifiers = 0;
            _hotkeyVirtualKey = 0;
            UpdateHotkeyDisplay();
            e.Handled = true;
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (IsModifierKey(key))
        {
            e.Handled = true;
            return;
        }

        var modifiers = GetHotkeyModifiers(Keyboard.Modifiers);
        if (modifiers == 0)
        {
            e.Handled = true;
            return;
        }

        _hotkeyModifiers = modifiers;
        _hotkeyVirtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        UpdateHotkeyDisplay();
        e.Handled = true;
    }

    private static bool IsModifierKey(Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin;
    }

    private static uint GetHotkeyModifiers(ModifierKeys modifiers)
    {
        uint result = 0;
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            result |= HotkeyDefinition.ModControl;
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            result |= HotkeyDefinition.ModAlt;
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            result |= HotkeyDefinition.ModShift;
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            result |= HotkeyDefinition.ModWin;
        }

        return result;
    }

    private void UpdateHotkeyDisplay()
    {
        HotkeyTextBox.Text = HotkeyGestureFormatter.Format(_hotkeyModifiers, _hotkeyVirtualKey);
        HotkeyTextBox.IsEnabled = HotkeyEnabledCheckBox.IsChecked == true;
    }

    private void TodoOpacitySlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TodoOpacityValueTextBlock is not null)
        {
            UpdateOpacityDisplay();
        }
    }

    private void UpdateOpacityDisplay()
    {
        TodoOpacityValueTextBlock.Text = $"{TodoOpacitySlider.Value:P0}";
    }

    private void ResetTodoAppearanceButton_OnClick(object sender, RoutedEventArgs e)
    {
        _draft.ResetTodoPositionAndAppearance = true;
        EightDayModeRadioButton.IsChecked = true;
        TodoOpacitySlider.Value = TodoWidgetPreferences.Default.Opacity;
        TodoLockedCheckBox.IsChecked = false;
    }

    private void OpenDataFolderButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            _coordinator.OpenDataFolder();
        }
        catch (Exception ex)
        {
            ShowError($"无法打开数据目录：{ex.Message}");
        }
    }

    private async void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isSaving || _isLoading)
        {
            return;
        }

        _isSaving = true;
        SaveButton.IsEnabled = false;
        SaveErrorTextBlock.Visibility = Visibility.Collapsed;
        try
        {
            var result = await _coordinator.SaveAsync(new SettingsSaveRequest(
                LaunchAtStartupCheckBox.IsChecked == true,
                MarkdownModeRadioButton.IsChecked == true
                    ? AppSettings.MarkdownEditorMode
                    : AppSettings.DefaultEditorMode,
                new QuickNoteHotkeyPreferences(
                    HotkeyEnabledCheckBox.IsChecked == true,
                    _hotkeyModifiers,
                    _hotkeyVirtualKey),
                new TodoWidgetSettingsSelection(
                    TodoVisibleCheckBox.IsChecked == true,
                    GetSelectedTodoMode(),
                    TodoOpacitySlider.Value,
                    TodoLockedCheckBox.IsChecked == true,
                    _draft.ResetTodoPositionAndAppearance)));
            if (!result.Succeeded)
            {
                ShowError(result.ErrorMessage ?? "保存设置失败。");
                return;
            }

            DialogResult = true;
        }
        catch (Exception ex)
        {
            ShowError($"保存设置失败：{ex.Message}");
        }
        finally
        {
            _isSaving = false;
            SaveButton.IsEnabled = true;
        }
    }

    private TodoWidgetMode GetSelectedTodoMode()
    {
        if (TodayModeRadioButton.IsChecked == true)
        {
            return TodoWidgetMode.Today;
        }

        return CalendarModeRadioButton.IsChecked == true
            ? TodoWidgetMode.Calendar
            : TodoWidgetMode.EightDay;
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        CancelAndClose();
    }

    private void SettingsWindow_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        CancelAndClose();
        e.Handled = true;
    }

    private void CancelAndClose()
    {
        DialogResult = false;
        Close();
    }

    private void ShowError(string message)
    {
        SaveErrorTextBlock.Text = message;
        SaveErrorTextBlock.Visibility = Visibility.Visible;
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var value = Math.Max(0, bytes);
        var unitIndex = 0;
        var displayValue = (double)value;
        while (displayValue >= 1024 && unitIndex < units.Length - 1)
        {
            displayValue /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{value} {units[unitIndex]}"
            : $"{displayValue:0.##} {units[unitIndex]}";
    }
}
