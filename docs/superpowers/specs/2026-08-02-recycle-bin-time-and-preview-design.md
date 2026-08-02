# QingJian Recycle Bin Time and Preview Design

## Goal

Prevent recycle-bin actions from disturbing note modification times, remove favorite decoration from deleted-note cards, and add a title-and-content preview for the selected deleted note.

## Constraints

- Work only in `C:\Users\Cristin\Desktop\VibeCoding\qingjian-ui-polish` on `feature/ui-polish`.
- Do not modify or merge `develop`.
- Restore and permanent deletion remain immediate actions.
- Restore preserves every stored note property except the deletion lifecycle fields.
- Icon buttons retain the existing borderless/backgroundless styles and immediate Chinese tooltips.

## Root Cause and Time Semantics

Repository restore and permanent-delete operations already isolate their changes to the requested deleted row and preserve `UpdatedAt`. The modification-time drift occurs before the recycle-bin window opens: the main window always calls `SaveSelectedNoteNowAsync`, and `NoteService.SaveNoteAsync` always assigns the current time even when title and content are unchanged.

`MainViewModel` will track a monotonically increasing edit version per note whenever the selected note's title or content changes. An explicit save does nothing when no unsaved version exists. A successful save clears the dirty version only if no newer edit arrived while persistence was running. This keeps the existing save-before-dialog behavior for real edits while preventing opening or closing UI workflows from changing timestamps without content changes.

Repository regression coverage will verify that restoring or permanently deleting one note leaves active notes and other deleted notes unchanged.

## Recycle Bin Layout

The window becomes a two-column master-detail layout. The left column is a `ListBox` bound to `DeletedNotes`; each card keeps title, original last-modified time, folder, restore, and permanent-delete actions. The favorite star is removed entirely.

The right column shows the selected note's full title and a read-only, scrollable plain-text display of its stored Markdown content. Selecting a card updates `SelectedNote`. Initial loading selects the first item. After restoring or deleting the selected item, the view model selects the item now occupying the same index, or the previous item when the removed item was last. An empty list clears selection and shows the existing empty state.

Action buttons handle their clicks independently so clicking restore or permanent delete does not rely on card selection.

## Testing

- Main-view-model tests prove unchanged explicit saves do not persist or change ordering, changed notes still save, and edits arriving during a save remain dirty.
- Repository tests prove restore and permanent delete do not alter timestamps on unrelated active or deleted notes.
- Recycle-bin-view-model tests cover initial selection and adjacent selection after removal.
- XAML tests require card selection, title/content preview bindings, read-only content, and absence of favorite bindings or glyphs.
- Full Release tests and a Release build provide regression verification.
