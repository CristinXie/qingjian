# QingJian Recycle Bin and Selection Polish Design

## Goal

Fix navigation selection after creating a note, add a 30-day recycle bin with lossless restore and permanent deletion, and change the batch clear-selection icon to a trash can.

## Constraints

- Work only in `C:\Users\Cristin\Desktop\VibeCoding\qingjian-ui-polish` on `feature/ui-polish`.
- Do not modify or merge `develop`.
- Icon buttons use the existing borderless/backgroundless icon styles, Chinese tooltips with zero delay, and 8 DIP spacing.
- Recycle-bin restore and permanent-delete actions execute immediately without confirmation.
- The recycle bin lists only notes deleted within the latest 30 days, including the exact 30-day boundary.
- Restoring a note preserves title, content, creation time, last modification time, folder assignment, favorite state, and favorite time.

## Approaches Considered

### 1. Dedicated deletion timestamp and recycle-bin view model (selected)

Add nullable `DeletedAt` storage, preserve `UpdatedAt` during deletion, and expose explicit repository/service restore and permanent-delete operations. A dedicated recycle-bin view model owns the deleted-note list and action state. This is the smallest approach that can restore all note properties correctly and is independently testable.

### 2. Reuse `UpdatedAt` as deletion time

This requires fewer schema changes, but deleting a note destroys its previous modification time. It cannot satisfy lossless restoration and is rejected.

### 3. Store an archived JSON snapshot

This preserves every property but duplicates the note schema, complicates migrations, and creates synchronization risks. It is unnecessary because soft-deleted rows already retain the note data.

## Data Model and Migration

`Note` gains `DateTime? DeletedAt`. `NoteRepository.InitializeAsync` adds the nullable SQLite column when absent and backfills legacy deleted notes with `UpdatedAt`, because that field was previously used as their deletion timestamp.

New deletions set `IsDeleted = true` and `DeletedAt = now` without modifying `UpdatedAt`. Recent-deleted queries require `IsDeleted`, a non-null `DeletedAt`, and `DeletedAt >= utcNow - 30 days`, sorted by deletion time descending.

Restore sets only `IsDeleted = false` and `DeletedAt = null`. Permanent deletion removes the row. Both operations are persistence-first so an exception leaves the in-memory recycle-bin list unchanged.

Folder rename and deletion affect active notes only, preventing later folder operations from rewriting a deleted note's saved folder assignment. When restoring a note whose non-system folder no longer exists, `NoteService` recreates that folder before restoring the note. `未分类` is restored directly.

## Application Structure

`RecycleBinViewModel` loads recent deleted notes and exposes `RestoreAsync` and `PermanentlyDeleteAsync`. Successful actions remove the note from its collection and raise a notification so the main window can refresh active notes and folder counts after the modal closes.

`RecycleBinWindow` displays each note in a three-line card matching the navigation card:

1. Title and read-only favorite star state.
2. Original last-modified time.
3. Folder icon and folder name.

The right side contains restore and permanent-delete icon buttons. The window uses an empty state when no recent deleted notes exist. All errors are shown in Chinese and failed actions keep the card visible.

The main sidebar footer adds `RecycleBinButton` after `BatchManagementButton`. `MainWindow` saves current editor content before opening the modal, then refreshes active notes and folder summaries after it closes.

## New Note Selection

`NoteListBox.SelectedItem` becomes a direct one-way binding to `SelectedNote`; only `SelectionMode` remains in the style trigger. When `SelectedNote` changes, the window synchronizes and scrolls the selected card into view. This avoids the style-setter binding being displaced by WPF selection state during collection-view refreshes.

## Batch Icon

`BatchClearSelectionButton` changes its Segoe MDL2 glyph to the same trash-can glyph used by delete actions. It keeps the regular icon style, `清空选择` tooltip, and existing behavior.

## Error Handling

- Opening failure: `打开回收站失败。`
- Restore failure: `恢复便签失败。`
- Permanent-delete failure: `永久删除便签失败。`
- Failed operations do not remove cards or mutate note state.

## Testing

- Repository SQLite tests cover migration, 30-day boundary, deletion field isolation, restore, and permanent deletion.
- Service tests cover lossless restore, missing-folder recreation, persistence-first failures, and cutoff calculation.
- View-model tests cover list loading and removal only after successful actions.
- XAML/code-behind tests cover footer order, card structure, icon styles, immediate tooltips, direct selection binding, and trash glyph.
- Full Release tests, Release build, and runtime UI automation verify the completed behavior.
