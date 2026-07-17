# Eight-Day Todo Preview Row

## Goal

Change the 8-day widget's normal todo preview from a checkbox containing only todo text to a checkbox followed by one wrapping display string that includes time and todo content.

## Display Format

Each preview row contains an independent completion checkbox followed by one text block.

The text block uses these exact formats:

1. No time: `待办内容`
2. Single time: `09:30 待办内容`
3. Time range: `09:30-10:45 待办内容`

No placeholder or empty time column is rendered for no-time todos. The existing `HH:mm` and `HH:mm-HH:mm` formats remain unchanged.

## Layout And Scrolling

The checkbox remains independently clickable and continues to update todo completion through `QuickCompleteTodoCheckBox_OnClick`.

The display string occupies the remaining row width and uses text wrapping. When it exceeds the visible width, it continues on a new line aligned with the beginning of the text block to the right of the checkbox.

Horizontal scrolling remains disabled. The existing `EightDayTodoScrollViewer` continues to provide vertical scrolling when a date contains more todo rows than fit in the cell.

## Architecture

Add a dedicated `TodoPreviewDisplayConverter` for the 8-day compact preview string. It consumes a `TodoItem`, delegates time formatting to the same `HH:mm` rules used by the existing time converter, and appends todo text only when a time prefix exists.

The 8-day item template becomes a two-column grid:

1. Auto-width checkbox column.
2. Remaining-width wrapping text column with a fixed small gap from the checkbox.

The `ItemsControl` stretches item content horizontally so the text block receives a finite width and wraps instead of requesting horizontal space.

## Scope Boundaries

This update changes only the 8-day normal preview. It does not change:

1. The management popover rows.
2. Today-mode rendering.
3. Calendar markers.
4. Todo sorting, persistence, completion semantics, or time validation.
5. Widget size, dragging, Z-order, or popover positioning.

## Testing

Automated tests will verify:

1. The converter formats no-time, single-time, and time-range todos exactly.
2. The 8-day preview uses a separate checkbox and wrapping text block.
3. The checkbox keeps the existing completion event and one-way completion binding.
4. The preview `ItemsControl` stretches rows horizontally.
5. Horizontal scrolling remains disabled and vertical scrolling remains automatic.

Run `dotnet test` followed by `dotnet build` before completion.
