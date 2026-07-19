# Eight-Day Manage Popover Polish

## Goal

Improve the 8-day widget's todo management popover so todo rows remain readable, the popover can be dismissed by repeating the date-cell click, and its normal height matches the quick-add popover.

## Todo Row Layout

Each management-popover todo row uses a constrained grid with five columns:

1. A 64-DIP time column aligned left.
2. A fixed 12-DIP gap.
3. A flexible todo-content column.
4. A fixed 12-DIP gap.
5. A fixed-width action column aligned right.

The action column keeps the existing edit and delete icon buttons. The `×` button continues to delete the todo and retains its delete tooltip.

Todo text wraps only inside the middle content column. Long content can add lines and increase that row's height, but it cannot overlap or enter the time and action columns. The list item content stretches to the popover width so the flexible column receives a finite width and WPF text wrapping works consistently.

## Same-Date Click Toggle

Clicking a date cell while the management popover is closed opens it for that date, preserving the existing positioning behavior.

If the management popover is already visible and pinned to the same date, clicking that date cell again closes the management popover. Closing clears the pinned date, date anchor, active edit item, form values, and validation display.

Clicking another date while the management popover is visible switches the pinned date and updates the popover content instead of closing it. The quick-add popover remains a separate state and is not treated as an already-open management popover.

## Popover Height

The management popover uses the quick-add popover's normal opening height as its target height. The quick-add popover is measured while its validation messages are collapsed and its form is in the normal cleared state.

The management height is clamped to the widget's available content area so it cannot extend outside the window. The target height is recalculated when the management popover is shown, avoiding a duplicated fixed numeric height that could drift when quick-add content changes later.

The management layout allocates the available body space to the todo list. The list scrolls internally when there are more rows than fit. When a todo editor is expanded, the editor receives a bounded area with its existing internal scroll behavior and fixed footer, while the overall management popover height stays stable.

## Scope Boundaries

This update changes only the management-popover row layout, the date-cell management toggle, and management-popover sizing. It does not change:

1. Todo deletion semantics.
2. Todo sorting or persistence.
3. Quick-add validation or positioning.
4. Widget dragging, lock state, Z-order, or desktop behavior.
5. Monthly calendar markers or Today-mode todo rendering.

## Testing

Automated tests will verify:

1. A management row uses separate time, content, and action grid columns with fixed gaps.
2. Todo text wraps in the middle column and list items stretch horizontally.
3. The action column still contains edit and delete buttons with the existing commands and tooltips.
4. Clicking the same pinned date while the management popover is visible closes it and clears management state.
5. Clicking a different date switches the active management date.
6. The management popover height is derived from the measured quick-add popover height and clamped to available space.
7. The management todo list scrolls within the stable popover height.

Run `dotnet test` followed by `dotnet build` before completion.
