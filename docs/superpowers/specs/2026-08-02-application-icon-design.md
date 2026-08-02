# QingJian Application Icon Design

## Goal

Use the user-provided `C:\Users\Cristin\Desktop\qingjian.ico` as QingJian's single application icon for the executable, taskbar, tray, and every WPF window.

## Constraints

- Work only in `C:\Users\Cristin\Desktop\VibeCoding\qingjian-ui-polish` on `feature/ui-polish`.
- Do not modify or merge `develop`.
- Preserve the provided ICO bytes exactly; do not regenerate, resize, recolor, or optimize it.
- Store one project copy at `src/QingJian.App/Assets/qingjian.ico`.
- The source ICO SHA-256 is `1348A3FD637AFBD61470B38B72A873127BBD93AB8EE608FDB6BED4CBCEC6D51D`.

## Resource Integration

`QingJian.App.csproj` sets `ApplicationIcon` to `Assets\qingjian.ico`, which embeds the icon into the Windows executable. The same file is included as a WPF `Resource`, making it available through the pack URI `/QingJian.App;component/Assets/qingjian.ico`.

Every WPF `Window` root explicitly sets:

```xml
Icon="/QingJian.App;component/Assets/qingjian.ico"
```

This applies to:

- `MainWindow`.
- `RecycleBinWindow`.
- `FolderManagementWindow`.
- `FolderNameDialog`.
- `SettingsWindow`.
- `QuickNoteWindow`.
- `TodoWidgetWindow`.

The last two currently use `WindowStyle="None"`, so no title-bar icon is visible, but retaining the same property keeps their window metadata consistent if their chrome changes later.

## Tray Behavior

`WindowsTrayIconService` continues to call `Icon.ExtractAssociatedIcon` on the running executable. Once `ApplicationIcon` is configured, the tray automatically uses the same QingJian icon. Its existing system icon fallback remains unchanged for extraction failures.

## Testing

- A project-resource test verifies `ApplicationIcon` and WPF `Resource` both reference `Assets\qingjian.ico`.
- The test verifies the copied resource exists and has the approved SHA-256.
- The test enumerates every production XAML file whose root is `Window` and requires the exact shared pack URI.
- Existing tray tests continue to require executable-icon extraction.
- Full Release tests and Release build must pass with zero warnings and errors.
