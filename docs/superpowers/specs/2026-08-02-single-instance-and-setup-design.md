# QingJian Single Instance And Setup Design

Date: 2026-08-02
Branch: `feature/single-instance-setup`

## Goal

Prevent concurrent QingJian application instances, keep WebView2 runtime data out of the executable directory, and produce a conventional Windows setup executable that installs the current x64 release for the current user.

## Scope

This change includes:

- One QingJian process per Windows logon session.
- Activation of the existing main window when QingJian is launched again.
- A stable WebView2 user-data directory under `%LOCALAPPDATA%\QingJian\WebView2`.
- A repeatable Windows x64 self-contained publish and Inno Setup build.
- A desktop-delivered `QingJian-Setup.exe` artifact.

It does not add automatic updates, code signing, x86/ARM64 packages, machine-wide installation, data backup, or automatic deletion of the old desktop WebView2 directory.

## Single-Instance Architecture

`SingleInstanceCoordinator` owns two per-session named Windows objects with stable application-specific names:

- A named mutex identifies the primary process.
- A named auto-reset event carries an activation signal from a secondary process to the primary process.

The coordinator is created at the beginning of `App.OnStartup`, before the database context, tray icon, hotkey, todo widget, or windows are initialized.

Primary process flow:

1. Acquire the named mutex.
2. Continue normal application initialization.
3. Start an asynchronous activation listener after the main window is assigned.
4. Marshal activation requests to the WPF dispatcher.
5. Call the existing `MainWindow.ShowFromTray()` behavior so a hidden, minimized, or background main window is restored and activated.
6. Dispose the listener, event, and mutex during application exit.

Secondary process flow:

1. Fail to acquire primary ownership.
2. Signal the named activation event.
3. call `Shutdown()` immediately.
4. Do not initialize SQLite, settings, tray, hotkeys, widgets, or windows.

The listener must tolerate shutdown races and repeated activation requests without showing an error dialog. If the activation signal cannot be opened because the primary process is between startup stages, the secondary process still exits; a concurrent second full instance is never allowed.

## WebView2 Data Directory

The editor creates a `CoreWebView2Environment` explicitly with:

```text
%LOCALAPPDATA%\QingJian\WebView2
```

`MainWindow` receives this path from application composition rather than recomputing the app-data root. It passes the environment to `EnsureCoreWebView2Async(environment)` before configuring virtual host mappings or navigation handlers.

The existing `%LOCALAPPDATA%\QingJian\attachments` directory and editor asset loading remain unchanged. Existing `QingJian.exe.WebView2` directories are not deleted automatically because another older process may still use them and because application code should not remove user-visible directories without an explicit cleanup action.

## Installer Design

Inno Setup 6 compiles `installer/QingJian.iss`. The build script prefers an installed compiler and otherwise downloads the pinned `Tools.InnoSetup 6.7.3` NuGet package, verifies its approved SHA-256, and extracts the compiler under ignored build artifacts. A PowerShell build script performs the complete artifact pipeline:

1. Publish `src/QingJian.App/QingJian.App.csproj` for `win-x64` in Release mode.
2. Use a self-contained single-file host with native and content extraction enabled.
3. Download the official Microsoft Edge WebView2 Evergreen Bootstrapper when it is not already present in the build cache.
4. Invoke Inno Setup Compiler with the publish directory, bootstrapper path, and output directory.
5. Produce `QingJian-Setup.exe`.

The installer behavior is:

- Application name: `QingJian`.
- Version: `0.1.0`.
- Architecture: Windows x64-compatible systems.
- Privileges: current user, no administrative installation requirement for QingJian itself.
- Install directory: `%LOCALAPPDATA%\Programs\QingJian`.
- Installed executable name: `QingJian.exe`.
- Start Menu shortcut: always created.
- Desktop shortcut: selected by default and user-configurable in Setup.
- Uninstaller: registered and available through Windows installed-app management.
- Application icon: `src/QingJian.App/Assets/qingjian.ico` for Setup, installed executable, shortcuts, and uninstall entry.
- WebView2: detect the Evergreen Runtime through Microsoft Edge Update client registry keys; only when missing, run the bundled official bootstrapper silently.
- Upgrade: use a stable Inno Setup `AppId` so later Setup builds replace the same per-user installation.
- Launch: offer to start QingJian after a successful interactive installation.

The app publish remains self-contained for .NET 8. WebView2 Runtime remains a Microsoft-managed component and is installed only when the runtime is absent.

## Error Handling

- A secondary instance exits even if activation notification fails, preserving the one-process guarantee.
- WebView2 initialization failure continues to use the existing editor fallback UI.
- The build script stops on restore, publish, compiler-package integrity, download, or compiler failures and does not report an installer path unless the expected output exists.
- Installer WebView2 bootstrapper errors are surfaced by Setup and do not masquerade as a successful dependency installation.

## Testing

Automated tests cover:

- First coordinator owns a unique application key.
- Second coordinator with the same key is secondary.
- Secondary activation signals the primary listener.
- Disposing the primary releases ownership for a later coordinator.
- Startup wiring checks the single-instance gate occurs before database construction and secondary startup shuts down early.
- WebView2 editor wiring checks the explicit environment and local-app-data path are used.
- Installer source checks the per-user install path, architecture, shortcuts, uninstaller metadata, icon, WebView2 detection, and output name.
- Build script checks the exact self-contained single-file publish and Inno compiler parameters.

Final verification runs the complete test suite, Release build, self-contained publish, Inno Setup compilation, PE/header and hash checks, and a source-worktree cleanliness check.

## Delivery

- Source changes and installer definitions are committed only to `feature/single-instance-setup` until the user requests branch completion.
- The generated installer is copied to `C:\Users\Cristin\Desktop\QingJian-Setup.exe`.
- Generated publish/setup intermediates are excluded from Git.
