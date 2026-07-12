# QingJian Hotkey Quick Note Design

## 1. Product Goal

QingJian should let users capture a note without switching fully into the main app. While QingJian is running, pressing `Ctrl + Alt + N` opens a small independent quick-note card near the mouse pointer. The card is optimized for fast capture: type the body, optionally add a title, save, and return to the previous workflow.

This feature is the first step toward the roadmap's quick-notes direction. It intentionally avoids tray residency and shortcut customization so the hotkey, multi-window quick capture, and save flow can become stable first.

## 2. Scope

This feature includes:

1. Registering `Ctrl + Alt + N` as a global hotkey while the app process is running.
2. Opening a new independent quick-note window every time the hotkey fires.
3. Showing the quick-note window near the current mouse position with screen-boundary correction.
4. Using a clean borderless WPF window that looks like a note card, has no system minimize/maximize/close buttons, and can be dragged.
5. Providing an optional title field and a primary body field.
6. Saving with `Ctrl + Enter` or a visible save action.
7. Canceling with `Esc` or a visible cancel action.
8. Confirming discard when canceling a non-empty body.
9. Generating a title from the first body line when the title is blank.
10. Saving quick notes through the existing note service and SQLite storage.
11. Updating the main window list when it is visible and not minimized.
12. Continuing normally if hotkey registration fails, with one startup warning.

## 3. Non-Goals

This feature does not include:

1. A shortcut settings page.
2. User-configurable hotkeys.
3. System tray residency.
4. Keeping the process alive after the main window is closed.
5. A rich Markdown editor inside quick-note windows.
6. Attachment insertion from the quick-note window.
7. Restoring unsaved quick-note drafts after canceling or app restart.
8. A single shared quick-note window. Multiple quick-note windows are intentional.

## 4. User Behavior

Hotkey behavior:

1. Pressing `Ctrl + Alt + N` opens a new quick-note window even if other quick-note windows are already open.
2. The hotkey works while QingJian is running, including when the main window is minimized.
3. Closing the main window still exits the app, so the hotkey stops working after exit.

Quick-note window behavior:

1. The window appears near the mouse pointer.
2. The window is corrected inward if the mouse is near a screen edge.
3. The window has a small drag area so users can move it.
4. The body field receives focus by default.
5. The save action is disabled while the body is empty or whitespace.
6. `Ctrl + Enter` saves and closes the window.
7. `Esc` cancels. If the body is empty, the window closes immediately. If the body is non-empty, the user must confirm discard.
8. Save failure keeps the window open and preserves the draft.

Recommended first size is about `420 x 320`. The visual style should use the app's existing warm, quiet resource palette and avoid a normal system-window appearance.

## 5. Architecture

Add three small units:

1. `GlobalHotkeyService`
   - Owns Windows global hotkey registration and unregistration.
   - Wraps `RegisterHotKey`, `UnregisterHotKey`, and WPF window message handling.
   - Exposes a hotkey event or callback when `Ctrl + Alt + N` fires.
   - Keeps the hotkey definition centralized so a future settings page can replace the hard-coded default.

2. `QuickNoteWindow`
   - Owns only the quick-note draft UI and keyboard interactions.
   - Does not know about SQLite or repositories.
   - Emits a save request with a title and body, then lets the coordinator handle persistence.
   - Handles drag, cancel confirmation, disabled save state, and inline save errors.

3. `QuickNoteCoordinator`
   - Creates a new `QuickNoteWindow` for each hotkey event.
   - Positions it near the mouse pointer with screen-boundary correction.
   - Saves quick-note drafts using the existing `INoteService`.
   - Synchronizes the saved note into the main window ViewModel only when the main window is visible and not minimized.

The existing `MainWindow` keeps hosting the Toast UI Markdown editor. The quick-note window uses simple text fields because speed matters more than rich editing in the hotkey flow. Saved body text remains Markdown-compatible plain text in `Notes.Content`.

## 6. Data Flow

Save flow:

1. User enters an optional title and a required body.
2. `QuickNoteWindow` requests save.
3. `QuickNoteCoordinator` trims and validates the body.
4. If the title is blank, the coordinator derives it from the first non-empty body line.
5. The generated title is truncated to a reasonable maximum length.
6. The coordinator calls `INoteService.CreateNoteAsync`.
7. The coordinator updates the new note's title and content.
8. The coordinator calls `INoteService.SaveNoteAsync`.
9. The quick-note window closes after save succeeds.

Main window synchronization:

1. If the main window is visible and not minimized, the saved note is inserted at the top of `MainViewModel.Notes` and selected.
2. The main window is not activated during this sync, so saving a quick note does not steal focus from the user's current app.
3. If the main window is minimized, hidden, or otherwise not visible, the note is saved silently. It will appear when the main note list is loaded again.

To keep the coordinator from manipulating collection details directly, `MainViewModel` should gain a focused method such as `AddSavedNote(Note note, bool select)` that inserts the note, optionally selects it, and raises `IsEmpty` notifications.

## 7. Error Handling

Hotkey errors:

1. If `Ctrl + Alt + N` cannot be registered, show one startup warning that the shortcut may be occupied by another app.
2. The app continues to run even when hotkey registration fails.
3. The app unregisters the hotkey during shutdown.

Quick-note errors:

1. Saving an empty or whitespace-only body is blocked.
2. Persistence failures leave the quick-note window open and display an error message.
3. Canceling a non-empty draft requires confirmation before discarding.
4. One quick-note window failing to save does not affect other open quick-note windows.

Positioning errors:

1. The quick-note window is always kept within the current screen's working area where practical.
2. If screen information cannot be resolved, the window can fall back to a centered position.

## 8. Testing Strategy

Automated tests should cover logic that does not require live OS hotkey registration:

1. Blank title derives from the first non-empty body line.
2. Generated titles are truncated.
3. Whitespace-only bodies cannot be saved.
4. `MainViewModel.AddSavedNote` inserts a note at the top.
5. `MainViewModel.AddSavedNote` selects the note when requested.
6. `MainViewModel.AddSavedNote` raises `IsEmpty` when transitioning from empty to non-empty.
7. `QuickNoteCoordinator` creates and saves a note through `INoteService`.
8. `QuickNoteCoordinator` synchronizes the saved note to the main ViewModel only when the main window is visible and not minimized.

Manual verification should cover OS and WPF behavior:

1. `Ctrl + Alt + N` works globally while QingJian is running.
2. The hotkey still works when the main window is minimized.
3. Each hotkey press opens a separate quick-note window.
4. Quick-note windows appear near the mouse pointer and remain on screen.
5. Quick-note windows are borderless, clean, and draggable.
6. `Ctrl + Enter` saves and closes.
7. `Esc` cancels and confirms discard for non-empty drafts.
8. A visible main window receives and selects the saved note without stealing focus.
9. A minimized main window does not pop up after quick-note save.
10. Startup shows a warning if the hotkey cannot be registered.

## 9. Acceptance Criteria

This feature is accepted when:

1. The app registers `Ctrl + Alt + N` as a global hotkey while running.
2. Hotkey registration failure is reported once without preventing normal app use.
3. Every hotkey press opens a new quick-note window.
4. Quick-note windows are borderless, draggable, and positioned near the mouse pointer.
5. Users can enter an optional title and required body.
6. `Ctrl + Enter` saves and closes the quick-note window.
7. `Esc` cancels, with discard confirmation for non-empty drafts.
8. Blank titles are generated from the first body line.
9. Saved quick notes persist through the existing SQLite note storage.
10. If the main window is visible and not minimized, the saved note appears at the top and is selected without activating the main window.
11. If the main window is minimized, saving a quick note does not restore or activate it.
12. Automated tests and build pass.

