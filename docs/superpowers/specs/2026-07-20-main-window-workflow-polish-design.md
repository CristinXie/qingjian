# QingJian Main Window Workflow Polish Design

## 1. Goal

Improve the main-window editing workflow so note deletion is protected, note navigation is organized by recency, keyboard flow moves naturally from title to body, and editor mode/history controls are available as quiet icon actions.

This work extends the existing `feature/ui-polish` visual direction. It must preserve note persistence, autosave, desktop todo behavior, quick-note behavior, attachments, and the existing Markdown document model.

## 2. Scope

This change includes:

1. Confirming before deleting a note.
2. Replacing the text delete action with an icon button and immediate Chinese tooltip.
3. Grouping the note navigation by last modification time.
4. Replacing the visible Toast UI mode switch strip with note creation-date text.
5. Moving Markdown/WYSIWYG switching to a main-window icon button.
6. Adding undo and redo icon buttons for the body editor.
7. Supporting `Ctrl+Y` redo inside the body editor.
8. Moving focus directly from the title to the body editor when pressing `Tab`.
9. Localizing visible prompts and Toast UI strings to Chinese, except the literal word `Markdown`.

## 3. Non-Goals

This change does not include:

1. Permanent deletion or a recycle bin.
2. Changes to note storage or database schema.
3. Per-note editor mode persistence.
4. A new custom undo stack outside Toast UI.
5. Changes to quick-note save/cancel behavior.
6. Changes to desktop todo data or widget interaction.
7. Changes to attachment storage or image handling capability.
8. Search, tags, archive, or filters.

## 4. Note Navigation

The left navigation remains a single keyboard-navigable note list backed by the existing `Notes` collection. A view-layer grouping projection provides section headers without changing the `Note` model.

Notes are sorted by `UpdatedAt` descending. Date boundaries use local time.

Visible groups appear in this order:

1. `今天`
2. `过去30天`
3. Remaining months in the current year, such as `6月`
4. Earlier years, such as `2025年`

Rules:

1. `今天` contains notes whose local updated date is today or later, protecting against small future-clock skew.
2. `过去30天` contains the previous 30 calendar dates and excludes today.
3. Current-year month groups contain notes older than the 30-day window and are ordered newest month first.
4. Earlier-year groups are ordered newest year first.
5. Empty groups are not shown.
6. Notes inside every group remain ordered by exact `UpdatedAt` descending.

The grouped view refreshes after load, create, quick-note insertion, save, and delete. The window also refreshes grouping when it becomes active so a long-running app corrects date boundaries after midnight.

## 5. Deletion Protection

The main window owns the confirmation interaction because confirmation is a view concern. `MainViewModel.DeleteSelectedNoteCommand` remains the source of deletion behavior.

The delete icon click flow is:

1. If no note is selected, do nothing.
2. Show a Chinese Yes/No confirmation dialog owned by the main window.
3. Copy: `确定要删除便签“{title}”吗？`
4. Use `否` as the default response.
5. Execute `DeleteSelectedNoteCommand` only after `是`.

Canceling does not save, delete, change selection, or alter the editor.

## 6. Editor Action Row

The title row contains four 32 DIP icon buttons aligned on the right in this order:

1. Mode switch
2. Undo
3. Redo
4. Delete

Adjacent icon buttons have 8 DIP spacing. They use familiar Segoe MDL2 Assets glyphs and remain visually quiet. Tooltips use `ToolTipService.InitialShowDelay="0"` so they appear immediately.

Tooltips:

- Mode switch: `切换到 Markdown` or `切换到所见即所得`, based on the current editor mode.
- Undo: `撤销`
- Redo: `恢复`
- Delete: `删除`

The delete icon uses the existing danger color. Undo, redo, and mode switching use neutral text color.

## 7. Editor Commands

The hosted editor exposes focused bridge functions through `window.qingjianEditor`:

1. `undo()` executes Toast UI's own undo command.
2. `redo()` executes Toast UI's own redo command.
3. `toggleMode()` switches between `markdown` and `wysiwyg` and returns the resulting mode.
4. `focus()` moves the caret into the active editor surface.

`Ctrl+Y` is handled in the editor document and executes redo. Existing `Ctrl+Z` behavior remains owned by Toast UI. Undo and redo buttons use the same Toast UI history as keyboard commands.

Mode changes continue to post `editorModeChanged`, preserving the existing global editor-mode setting. The main window updates the mode-switch tooltip whenever that message arrives.

## 8. Title-to-Body Keyboard Flow

The title text box handles forward `Tab` before normal WPF focus traversal:

1. Commit the title binding.
2. Allow the existing lost-focus save path to run.
3. Focus the WebView2 control.
4. Call `window.qingjianEditor.focus()`.
5. Mark the key event handled so focus does not visit the action buttons first.

`Shift+Tab` keeps normal reverse focus traversal.

## 9. Creation Date Footer

Toast UI is initialized with its visible mode switch hidden. The bottom of the editor shell instead shows note creation text supplied by WPF.

Formatting uses the note's local creation date:

- Current year: `创建于 M月d日`
- Other year: `创建于 yyyy年M月d日`

The footer is secondary metadata, uses muted text, and does not compete with the body editor.

## 10. Chinese Localization

All newly introduced prompts and tooltips are Chinese except the literal word `Markdown`.

Toast UI registers a `zh-CN` language dictionary before editor initialization. Visible labels, tooltips, dialogs, table/image prompts, mode names, and action text are translated. `WYSIWYG` is displayed as `所见即所得`; `Markdown` remains `Markdown`.

Existing visible English error text in the main-window close-save path is changed to Chinese. Generated image alt text becomes `图片`.

## 11. Component Boundaries

Expected implementation areas:

1. `ViewModels/MainViewModel.cs` exposes and refreshes the grouped note view.
2. A focused navigation/date helper owns group-name and creation-date formatting logic.
3. `Views/MainWindow.xaml` owns group headers, creation metadata, and icon layout.
4. `Views/MainWindow.xaml.cs` owns confirmation, editor bridge commands, dynamic tooltips, focus transfer, and activation refresh.
5. `EditorAssets/editor-host.js` owns Toast UI commands, `Ctrl+Y`, mode toggling, focus, and Chinese language registration.
6. `EditorAssets/editor-host.css` hides the original mode switch strip and keeps the editor layout stable.

No database, repository, note-service, attachment-service, quick-note, or todo-widget contract changes are required.

## 12. Error Handling

1. Editor commands are ignored when WebView2 or Toast UI is not ready.
2. A failed script call does not crash the app or mutate note content.
3. Delete confirmation never executes the command when selection has changed or disappeared before confirmation completes.
4. Existing editor fallback behavior remains available if WebView2 initialization fails.
5. Close-save failures use a Chinese error dialog and preserve the existing cancellation behavior.

## 13. Validation

Automated tests cover:

1. Navigation group boundaries, names, and descending order.
2. Group refresh after create, save, delete, and quick-note insertion.
3. Current-year and historical creation-date formatting.
4. Delete confirmation gating.
5. XAML icon order, 8 DIP spacing, Chinese immediate tooltips, creation footer, and title `Tab` handler.
6. Editor bridge functions, `Ctrl+Y`, hidden mode switch, and Chinese language registration.
7. Existing note, editor, quick-note, todo-widget, attachment, and persistence tests.

Manual validation covers:

1. Confirming and canceling note deletion.
2. Tab movement from title to body.
3. Undo/redo buttons and `Ctrl+Y` in both editor modes.
4. Dynamic mode tooltip and mode persistence.
5. Group headers and note movement after edits.
6. Creation-date display for current-year and earlier notes.
7. Chinese Toast UI tooltips and dialogs.

## 14. Acceptance Criteria

This work is complete when:

1. Notes cannot be deleted from the main window without confirmation.
2. The delete action is an icon with immediate `删除` tooltip.
3. Notes are grouped and sorted by the agreed local-time rules.
4. The original mode switch strip is replaced by creation-date metadata.
5. Title `Tab` moves directly into the body editor.
6. Undo, redo, mode switch, and delete icons appear in the agreed order with 8 DIP spacing.
7. `Ctrl+Y` and the redo icon restore body-editor operations in both modes.
8. Mode switching works from the new icon and its tooltip describes the target mode.
9. Visible prompts are Chinese except `Markdown`.
10. Full automated tests and build pass without warnings or errors.
