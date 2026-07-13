# QingJian Desktop Todo Widget Design

## 1. Product Goal

QingJian needs a desktop-level todo widget that stays close to the Windows desktop, remains visually quiet through transparency, and gives users a direct way to maintain structured daily todos without opening a full management page.

The first version focuses on a complete desktop widget loop: show todos, switch between three preset views, add and edit todos from the widget, persist widget preferences, and share the same todo data across every preset.

## 2. Scope

This feature includes:

1. An independent todo data model and SQLite persistence, separate from notes.
2. A desktop-layer widget window that appears by default when QingJian starts.
3. A main-window entry to show or hide the desktop widget.
4. Three preset widget modes:
   - 8-day grid.
   - Today list.
   - Monthly calendar.
5. Shared todo data across all three modes.
6. A shared hover/click edit popover for date-based modes.
7. Todo creation, text editing, completion toggling, deletion, and time editing from the widget.
8. Three time forms:
   - No time.
   - Single time.
   - Start and end time.
9. Widget preference persistence for visibility, mode, position, opacity, and lock state.
10. Calendar month navigation with previous month, next month, and return to current month.
11. Default semi-transparent styling, with stronger opacity on hover.

## 3. Non-Goals

This feature does not include:

1. A full main-window todo management page.
2. Todo reminders or Windows notifications.
3. Repeating todos.
4. Cross-day todos.
5. Drag-resizing the widget.
6. Size presets.
7. Cloud sync.
8. Tray residency or keeping QingJian alive after the main window closes.
9. Import/export for todos.
10. Attachment support inside todos.

## 4. Technology Stack

The app remains a WPF application on .NET 8.

Additional implementation areas:

1. SQLite and EF Core for todo persistence.
2. WPF windows and controls for the desktop widget and popover UI.
3. Win32 interop for desktop-layer attachment where available.
4. Existing app settings infrastructure for widget preferences.

The desktop widget should first try to attach to the Windows desktop layer. If that fails, it should degrade to a lowest-level ordinary WPF window and keep the app usable.

## 5. Data Model

Add a new `TodoItems` table. Todos are not stored in `Notes.Content`.

Fields:

1. `Id`: GUID string primary key.
2. `Date`: the local calendar date the todo belongs to.
3. `Text`: todo content.
4. `IsCompleted`: completion state.
5. `StartTime`: nullable local time.
6. `EndTime`: nullable local time.
7. `CreatedAt`: creation timestamp.
8. `UpdatedAt`: last update timestamp.
9. `CompletedAt`: nullable completion timestamp.
10. `IsDeleted`: soft-delete flag.

Time forms:

1. No time: `StartTime = null`, `EndTime = null`.
2. Single time: `StartTime != null`, `EndTime = null`.
3. Time range: `StartTime != null`, `EndTime != null`.

Validation rules:

1. Todo text must not be empty or whitespace.
2. `EndTime` cannot be set without `StartTime`.
3. For time ranges, `EndTime` must be later than `StartTime`.
4. Todos are first-version one-time items. They cannot repeat and cannot cross dates.

## 6. Sorting Rules

Todos are sorted per day:

1. Incomplete todos appear before completed todos.
2. Incomplete todos with a time appear before incomplete todos without a time.
3. Timed incomplete todos sort by `StartTime` ascending.
4. Untimed incomplete todos keep a stable creation/update order.
5. Completed todos stay visible and move to the bottom of the same day.
6. Completed todos sort by completion order, using `CompletedAt` when available.

All three preset modes use the same ordering rules.

## 7. Widget Behavior

The desktop widget appears by default on first launch after this feature is installed. After the user changes widget visibility, QingJian restores that persisted visibility on later launches. The main window provides a show/hide control, but closing the main window still exits QingJian and closes the widget.

The widget is fixed-size in the first version. Users can move it only when it is unlocked. Locking the widget prevents dragging but does not prevent hovering, clicking, editing, completing, or deleting todos.

The widget persists:

1. Visibility.
2. Current preset mode.
3. Position.
4. Opacity.
5. Locked or draggable state.
6. Calendar mode's displayed month, if the user navigates away from the current month.

The widget uses semi-transparent background styling by default. When the pointer hovers over the widget, the background becomes more opaque for readability. Text and controls should remain high-contrast.

## 8. Preset Modes

### 8-Day Grid

The 8-day grid shows 2 rows and 4 columns, starting from yesterday and continuing for 8 consecutive days.

Each day cell shows:

1. Date label.
2. Todo summary in checkbox form.
3. Completed items weakened visually and moved to the bottom.

Hovering over a day cell opens the shared edit popover. Clicking the day cell pins the popover so it stays open after the mouse leaves the cell. Clicking elsewhere or closing the popover unpins it.

### Today List

The today list shows all todos for the current local date.

It directly displays:

1. Untimed todos.
2. Single-time todos.
3. Time-range todos.
4. Completed todos at the bottom with weaker text and a completed checkbox.

This mode is optimized for exact time-of-day review. The list allows quick completion toggles, while add/edit/delete and time-form changes use the same shared edit popover as the other modes.

### Monthly Calendar

The monthly calendar shows a full month grid.

It includes:

1. Previous-month control.
2. Next-month control.
3. Return-to-current-month control.
4. Day cells with todo markers only, not full todo text.

Hovering over a date opens the same shared edit popover used by the 8-day grid. Clicking a date pins the popover for editing.

## 9. Edit Popover

The shared edit popover supports:

1. Adding a todo for the selected date.
2. Editing todo text.
3. Marking a todo complete or incomplete.
4. Deleting a todo through soft delete.
5. Setting the time form:
   - No time.
   - Single time.
   - Start and end time.

Saving updates the shared todo data immediately. The current widget mode refreshes from the same service data, so changes made in one preset appear in the others.

If saving fails, the popover remains open and preserves the user's draft.

## 10. Architecture

Add todo-specific units alongside existing notes infrastructure:

1. `Models/TodoItem.cs`
   - EF Core entity and UI-observable model where needed.

2. `Data/ITodoRepository.cs` and `Data/TodoRepository.cs`
   - Initialize todo storage.
   - Query by date and date range.
   - Add, update, and soft-delete todos.

3. `Services/ITodoService.cs` and `Services/TodoService.cs`
   - Validate todo text and time forms.
   - Create, edit, complete, reopen, and delete todos.
   - Apply shared sorting rules.

4. `TodoWidgets/`
   - Desktop widget coordinator.
   - Desktop layer host behavior.
   - Widget view model.
   - Three preset views.
   - Shared edit popover.

5. Settings extension
   - Persist widget visibility, mode, position, opacity, lock state, and calendar month.

6. `MainWindow`
   - Adds only a show/hide widget entry for the first version.
   - Does not become a todo management surface.

## 11. Error Handling

Expected handling:

1. Desktop-layer attachment failure degrades to a lowest-level ordinary window.
2. Todo database initialization failure shows a widget-unavailable state and should not break existing note features.
3. Save failures keep the edit popover open with the draft intact.
4. Invalid time ranges are blocked before save.
5. Delete is soft delete.
6. Widget preference save failures should not prevent todo editing, but the app may fall back to defaults on next launch.
7. Month navigation in calendar mode should never change todo dates by itself; it only changes the displayed month.

## 12. Testing Strategy

Automated tests should cover:

1. Todo repository initialization and SQLite persistence.
2. Querying todos by exact date and date range.
3. Creating todos with no time, a single time, and a time range.
4. Rejecting empty text.
5. Rejecting invalid time ranges.
6. Editing todo text and time fields.
7. Soft deletion.
8. Completion and reopening behavior.
9. Shared sorting rules.
10. Widget preference serialization and restore behavior.

Manual verification should cover:

1. QingJian starts and shows the desktop widget by default.
2. The main window can hide and show the widget.
3. The widget attaches to the desktop layer, or degrades gracefully if attachment fails.
4. Locked widgets cannot be dragged but remain interactive.
5. Unlocked widgets can be dragged and restore position after restart.
6. The 8-day grid starts from yesterday.
7. Hovering and clicking day cells opens and pins the edit popover.
8. Today list shows timed, untimed, and completed todos in the expected order.
9. Monthly calendar switches months and returns to the current month.
10. Edits made in one preset appear in the other presets.

Run before merging:

```powershell
dotnet test
dotnet build
```

Do not run test and build in parallel because WPF build outputs can be locked.

## 13. Acceptance Criteria

This feature is accepted when:

1. Todos are stored independently from notes.
2. QingJian shows the desktop todo widget by default on startup.
3. The main window can hide and show the widget.
4. The widget attempts to attach to the desktop layer and degrades gracefully if needed.
5. The widget supports locked and draggable states.
6. The 8-day grid displays yesterday through the following 7 days.
7. The today list displays the current day's todos with time information.
8. The monthly calendar displays a full month and supports previous, next, and current-month navigation.
9. The shared edit popover supports add, edit, complete, delete, and time form changes.
10. Completed todos remain visible and move to the bottom of their day.
11. Widget preferences persist across restarts.
12. Automated tests pass and the app builds.
