# Desktop Todo Widget Interaction Update

## Goal

Simplify date-cell interaction, make todo time editing consistent with quick add, and keep every todo reachable when an 8-day cell contains more items than its fixed height can display.

## Behavior Changes

### Edit Time Inputs

The management popover editor uses the same four text inputs as quick add:

1. Start hour.
2. Start minute.
3. End hour.
4. End minute.

The edit form follows the existing quick-add rules:

1. All four fields empty means no-time todo.
2. A complete start hour and minute with an empty end means single-time todo.
3. Complete start and end values with start earlier than end means time-range todo.
4. Partially entered time points, values outside `00:00` through `23:59`, an end without a start, or an end that is not later than the start are invalid.
5. Empty todo text remains invalid.

Editing an existing todo pre-populates the four fields from its stored start and end times. Saving uses the same validation result and error presentation as quick add.

### Date Cell Interaction

The delayed hover preview is removed from both the 8-day grid and monthly calendar. Date cells no longer register mouse-enter or mouse-leave handlers and the window no longer owns a hover preview timer or preview-only popover mode.

Clicking a date cell continues to open the management popover. That popover continues to show todo time and text, with edit and delete actions. The `+` button in the 8-day grid continues to open quick add without also triggering the date-cell click behavior.

### 8-Day Cell Scrolling

The todo list area inside each 8-day cell becomes vertically scrollable. The date header and quick-add button remain fixed while only the todo list scrolls. The vertical scrollbar appears only when the list exceeds the available cell height, and mouse-wheel input scrolls the list normally.

The existing checkbox rendering and completion behavior remain unchanged. No horizontal scrollbar is shown.

## Implementation Boundaries

This update reuses `TodoWidgetDraftParser.ValidateQuickAddDraft` for edit validation instead of creating a second time parser. It does not extract a new shared WPF control because the two forms can share validation without broadening the current UI change.

No todo persistence, sorting, window positioning, drag behavior, Z-order behavior, or widget preference format changes are included.

## Testing

Automated tests will verify:

1. The edit form contains four separate hour/minute inputs and no time-kind selector or `HH:mm` fields.
2. Edit save calls the shared validation path and displays validation errors without hiding the action buttons.
3. Date cells have click handlers but no hover handlers or hover timer implementation.
4. The 8-day todo area has vertical auto-scrolling and disabled horizontal scrolling.
5. Existing quick-add, popover positioning, todo parsing, and widget behavior tests still pass.

Run `dotnet test` followed by `dotnet build` before completion.
