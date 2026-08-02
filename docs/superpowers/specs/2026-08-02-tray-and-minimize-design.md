# QingJian Tray and Minimize Design

## Goal

Add a Windows system-tray presence, minimize/close-to-tray behavior, startup-hidden behavior, and matching settings that persist and apply immediately.

## Constraints

- Work only in `C:\Users\Cristin\Desktop\VibeCoding\qingjian-ui-polish` on `feature/ui-polish`.
- Do not modify or merge `develop`.
- Use Windows' built-in `System.Windows.Forms.NotifyIcon`; do not add a third-party tray package.
- Existing global hotkey, quick-note, desktop-todo, note autosave, and editor behavior must continue while the main window is hidden.
- Settings controls use existing visual conventions. Binary values use checkboxes; icon commands remain borderless and backgroundless with immediate Chinese tooltips.

## Preferences and Compatibility

`AppSettings` gains a `WindowBehaviorPreferences` value with three booleans:

- `MinimizeToTray`, default `true`.
- `CloseToTray`, default `false` to preserve the previous close-means-exit behavior.
- `StartMinimized`, default `false`.

Missing JSON fields normalize to these defaults. `StartMinimized` is only acted on for a Windows startup launch, not for an ordinary user launch.

The Settings window adds three checkboxes to the existing `常规` section:

- `最小化主窗口时隐藏到系统托盘`.
- `关闭主窗口时隐藏到系统托盘`.
- `开机启动时隐藏到系统托盘`.

The startup-hidden checkbox is enabled only when `开机自动启动 QingJian` is checked. Disabling startup leaves the saved startup-hidden preference intact but makes it inactive. All three values participate in the existing draft, transactional save, and rollback flow. Successful saves apply minimize and close behavior immediately.

## Tray Interaction

The tray icon exists for the entire application lifetime. It uses the executable icon when available and falls back to the Windows application icon. The tooltip is `QingJian`.

Double-clicking the icon shows and activates the main window. The right-click menu is:

1. `打开主窗口`.
2. `新建快捷便签`.
3. `显示桌面待办` or `隐藏桌面待办`, synchronized with current widget visibility.
4. Separator.
5. `退出 QingJian`.

Tray commands call the existing quick-note and todo coordinators. The icon and native menu are disposed during application exit.

## Window Lifecycle

The WPF application shutdown mode changes from `OnMainWindowClose` to `OnExplicitShutdown`, because hiding or closing the main window must not accidentally destroy tray residency.

`MainWindow` owns editor-save-before-hide/close behavior and receives the current `WindowBehaviorPreferences` from a small runtime coordinator.

- Minimize with `MinimizeToTray=true`: pull the current editor Markdown, save real pending edits, hide the window, and remove it from the taskbar.
- Minimize with the setting disabled: use normal Windows taskbar minimization.
- Close with `CloseToTray=true`: cancel the close, save pending edits, then hide to tray.
- Close with the setting disabled: save pending edits and request explicit application shutdown, preserving old behavior.
- Tray restore: show in the taskbar, restore the last non-minimized state, and activate the window.
- Tray exit: bypass close-to-tray, save pending edits, close the main window, and explicitly shut down the application.

Reentrant minimize/close requests are ignored while a save is in progress. Save failures keep the main window visible and show the existing Chinese error pattern instead of hiding or exiting.

## Startup Behavior

The current-user startup registration command becomes:

```text
"<full executable path>" --startup
```

On startup, the app loads settings before showing the main window. When both `--startup` and `StartMinimized=true` are present, the main window is initialized normally but starts hidden in the tray. Ordinary launches always show the main window.

## Components

- `WindowBehaviorPreferences`: serialized defaults and compatibility.
- `WindowBehaviorCoordinator`: current runtime preferences and pure minimize/close/startup decisions.
- `ITrayIconService` / `WindowsTrayIconService`: native tray lifecycle and user-request events.
- `MainWindow`: save-aware hide, restore, and explicit-exit operations.
- `App`: composes tray events with existing quick-note and todo coordinators and owns final shutdown.
- `SettingsCoordinator`: persists and applies window behavior in the existing rollback transaction.

## Error Handling

- Tray icon initialization failure shows a Chinese warning and leaves normal main-window usage available. Since no tray icon exists, close/minimize-to-tray decisions fall back to normal minimize and exit.
- A note-save failure before hide or exit keeps the main window visible and reports the failure.
- A settings apply failure restores the previous runtime behavior and previous JSON settings using the existing rollback path.
- Final shutdown always disposes the tray icon and saves desktop-todo preferences through the existing `OnExit` path.

## Testing

- Settings tests cover old JSON defaults, round-trip persistence, settings draft controls, enabled-state synchronization, transaction application, and rollback.
- Pure lifecycle tests cover minimize, close, explicit exit, startup arguments, and no-tray fallback decisions.
- Tray service source/XAML wiring tests cover menu labels, double-click, dynamic todo text, event wiring, icon fallback, and disposal.
- Main-window tests cover save-before-hide, restore state, reentrancy guards, and explicit exit bypass.
- Startup wiring tests cover `OnExplicitShutdown`, `--startup`, hidden startup, quick-note/todo commands, and tray disposal.
- Full Release tests and Release build must pass with zero warnings and zero errors.

## Out of Scope

- Custom tray-menu rendering, notifications, badges, balloon tips, or a new brand icon.
- Multiple application instances.
- macOS or Linux tray support.
- Changing quick-note, desktop-todo, or main-window visual layout beyond the new settings rows.
