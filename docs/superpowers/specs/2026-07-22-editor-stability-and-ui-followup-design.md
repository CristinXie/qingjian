# QingJian Editor Stability and UI Follow-up Design

## Goal

Eliminate cursor jumps and incorrect body statistics during main-window editing, then refine note-card spacing, sort-mode icon semantics, and todo popover input colors.

## Confirmed Root Cause

The cursor bug occurs only in the main-window body editor.

1. Toast UI posts `markdownChanged` while the user types.
2. `MainWindow` updates `SelectedNote.Content`.
3. `MainViewModel` autosaves after 700 ms and refreshes note navigation.
4. `RefreshNoteNavigationCore` raises `PropertyChanged` for `SelectedNote` even though the selected note reference has not changed, so the one-way list selection can be restored after filtering.
5. `MainWindow` treats every `SelectedNote` notification as a real note switch and calls `LoadSelectedNoteIntoEditorAsync`.
6. The resulting `window.qingjianEditor.setMarkdown(...)` resets Toast UI's selection and races with editor change messages, causing the cursor jump and inaccurate statistics.

## Editor Stability Design

`MarkdownEditorState` remains the source of truth for the note currently loaded into Toast UI. It gains a pure check that distinguishes an actual note switch from a repeated notification for the same note ID.

`MainWindow` continues to update title placeholder state and allows the one-way list selection binding to refresh, but it reloads Markdown only when the selected note ID differs from `MarkdownEditorState.CurrentNoteId`. Autosave and navigation reordering therefore do not call `setMarkdown`, preserving cursor position, IME composition, undo history, and live body statistics.

Real note changes, note deletion, new-note selection, and initial editor startup still load Markdown normally.

## Note Card Spacing

Each navigation preview uses three equal 24 DIP rows. Text and icons are vertically centered, and the previous second/third-row top margins are removed. The note-item container uses 8 DIP vertical padding, producing balanced top, inter-row, and bottom spacing. The favorite button is constrained to 24 by 24 DIP inside the first row.

## Sort Icon Semantics

The sort button icon represents the active mode:

- Time mode: clock icon.
- Favorite mode: star icon.

The tooltip continues to describe the click action:

- Time mode: `按收藏`.
- Favorite mode: `按时间`.

## Todo Popover Input Colors

The edit and quick-add popovers continue using `TodoWidgetPopoverBackgroundBrush`. Their body text boxes, time-panel borders, and hour/minute text boxes also use that same brush instead of white. Existing borders, validation colors, text-button styling, scrolling, and fixed footers remain unchanged.

## Validation

Automated tests cover:

1. Same-note selection refresh does not request an editor reload.
2. A different selected note still requests an editor reload.
3. Main-window code uses the reload guard before calling `LoadSelectedNoteIntoEditorAsync` from selection notifications.
4. Note previews use three equal rows, centered content, 24 DIP favorite action, and balanced item padding.
5. Time and favorite modes display their current-mode icons while retaining next-action tooltips.
6. Quick-add and edit body/time input surfaces use the shared light-green brush.

Runtime validation pauses beyond the autosave delay while typing and confirms the cursor remains in place and the line/character count matches the current Markdown.
