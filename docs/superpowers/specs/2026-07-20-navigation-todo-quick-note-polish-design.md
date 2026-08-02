# QingJian Navigation, Todo Widget, and Quick Note Polish Design

## 1. Goal

Improve the main-window navigation workflow, desktop todo widget, and quick-note layout while preserving existing note editing, autosave, attachments, desktop-widget transparency, and quick-note behavior.

This change adds persistent favorites and folder metadata, note search and alternate sorting, denser navigation previews, editor body statistics, a dynamic desktop-todo visibility action, a lighter todo-widget visual treatment, scroll-safe todo creation, quick-note spacing refinements, and a fix for duplicate pasted images.

## 2. Scope

This change includes:

1. Dynamic `显示桌面待办` and `隐藏桌面待办` button text.
2. Note search with title matches ranked before body matches.
3. Time and favorite sorting modes.
4. Persistent note favorite state, favorite time, and folder name.
5. Three-row note navigation previews with a clickable favorite star.
6. Empty settings, folder, and batch-management icon actions at the bottom of the sidebar.
7. Editor footer line and character statistics with right-aligned creation date.
8. Removal of the editor footer divider.
9. Duplicate image-paste prevention.
10. An extremely light green desktop todo widget and matching popovers.
11. Borderless, backgroundless styling for todo icon buttons only.
12. A vertically scrollable quick-add todo form with a fixed text-action footer.
13. A single-line quick-note drag grip and a lower quick-note footer.

## 3. Non-Goals

This change does not include:

1. Functional settings, folder-management, or batch-management dialogs.
2. Creating, renaming, deleting, or moving between managed folder entities.
3. Persisting the selected navigation sort mode between app sessions.
4. Full-text indexing or database-backed search.
5. Changes to note Markdown storage.
6. Changes to quick-note dimensions, validation, close confirmation, or shortcuts.
7. Converting todo text buttons such as `创建`, `关闭`, `保存`, `清空`, or `本月` into icons.

## 4. Note Metadata and Database Compatibility

The `Note` model gains three persistent values:

1. `IsFavorite`: whether the note is favorited.
2. `FavoritedAt`: nullable UTC timestamp recording the latest favorite action.
3. `FolderName`: the displayed folder name.

New and existing notes default to `IsFavorite = false`, `FavoritedAt = null`, and `FolderName = "未分类"`.

The existing application uses `EnsureCreated`, which does not add columns to an existing SQLite database. `NoteRepository.InitializeAsync` therefore becomes an idempotent compatibility upgrade:

1. Ensure the database exists.
2. Inspect `PRAGMA table_info("Notes")`.
3. Add each missing column using `ALTER TABLE`.
4. Backfill null or blank folder names to `未分类`.

The upgrade must work for a new database, the current notes-only schema, and databases that have already been upgraded.

Favorite persistence uses a dedicated repository and service operation. Favoriting changes only `IsFavorite` and `FavoritedAt`; it does not update `UpdatedAt`. A failed favorite save restores the prior in-memory values and shows a Chinese error message.

## 5. Main-Window Sidebar

### 5.1 Desktop Todo Visibility Action

The desktop todo button describes the next action:

- Widget hidden: `显示桌面待办`
- Widget visible: `隐藏桌面待办`

The text follows the coordinator's actual visibility state after startup restoration, main-window toggles, and widget-initiated hiding. It must not rely on an unrelated local boolean.

### 5.2 Search and Sort Controls

A search row appears below the desktop todo button:

1. A text box with Chinese search placeholder text.
2. A 32 DIP icon button on its right for sort switching.

Search behavior:

1. Filtering is immediate and case-insensitive.
2. The title and raw Markdown body are searched.
3. A note matching both fields appears only as a title match.
4. Typing does not automatically change `SelectedNote`.
5. A selected note remains open in the editor even when filtered out.
6. Clicking a result changes the selected note normally.
7. No results show `没有匹配的便签` in the sidebar without triggering the application's no-notes empty state.
8. Clearing search restores the full list and the prior selection.

Sort-switch behavior:

- Current time sort: show a favorite-sort icon and tooltip `按收藏`.
- Current favorite sort: show a clock icon and tooltip `按时间`.
- Time sort is the startup default.
- The current sort mode remains active while searching and after clearing search, but is not persisted across app restarts.

### 5.3 Grouping and Ordering

With no search text:

1. Time sort keeps the existing local-date groups: `今天`, `过去30天`, current-year months, and earlier years. Notes are ordered by `UpdatedAt` descending.
2. Favorite sort uses `收藏` and `其他`. Favorited notes are ordered by `FavoritedAt` descending. Other notes are ordered by `UpdatedAt` descending.

With search text:

1. Groups become `标题匹配` and `正文匹配`.
2. Title matches always appear before body-only matches.
3. Inside each search group, the current time or favorite sort rules remain active.

### 5.4 Note Preview

Each note navigation item uses three rows:

1. Title on the left and favorite star on the right.
2. Last-modified time on the left.
3. Folder icon and folder name on the left.

Time formatting uses local time:

- Current year: `6/19 15:30`
- Other year: `2025/6/19 15:30`

The favorite star:

1. Uses an outline star and tooltip `收藏` when not favorited.
2. Uses a yellow filled star and tooltip `取消收藏` when favorited.
3. Toggles favorite state without changing the selected note.
4. Saves immediately and refreshes grouping and ordering.
5. Does not change `UpdatedAt`.

Folder display uses `未分类` until functional folder management is implemented.

### 5.5 Sidebar Footer

Three fixed icon buttons appear at the bottom left, outside the scrollable note list, in this order:

1. Settings, tooltip `设置`
2. Folder, tooltip `文件夹`
3. Batch management, tooltip `批量管理`

The buttons remain enabled and clickable but perform no action, show no dialog, and display no placeholder message in this change.

## 6. Editor Footer

The editor footer becomes a borderless two-column metadata row:

1. Left: `{lineCount} 行 {characterCount} 字`
2. Right: the existing year-sensitive creation date

The top divider between editor content and metadata is removed.

Statistics use raw Markdown:

1. Empty content: `0 行 0 字`
2. Newlines determine line count.
3. `\r` and `\n` do not count as characters.
4. Spaces and Markdown syntax characters do count.
5. Statistics update when editor Markdown changes and when selection changes.

## 7. Duplicate Image Paste Prevention

The current editor can route one clipboard image through both the document-level paste listener and Toast UI's image hook. Image paste is changed to one authoritative route:

1. If the browser paste event exposes image files, the custom capture listener processes them.
2. It calls `preventDefault`, `stopPropagation`, and `stopImmediatePropagation` before requesting uploads.
3. Toast UI's image hook remains available for image insertions that do not pass through that intercepted paste path.
4. If browser clipboard data has no accessible text or image file, WPF native clipboard fallback is requested.
5. Remote image URLs continue to insert once.
6. Multiple distinct image files pasted in one operation are each inserted once.
7. Ordinary text, Markdown, and link paste behavior remains unchanged.

## 8. Desktop Todo Widget

### 8.1 Color and Transparency

The main todo-widget background changes to `#F2FAF4`. Existing opacity binding and transparent-window mechanics remain unchanged.

Quick-add, edit, management, and preview popovers move to the same extremely light green family. Input surfaces remain white or near-white so fields remain distinguishable.

### 8.2 Button Styling

Only icon buttons change to the new quiet style:

1. Transparent background.
2. Transparent border.
3. No visible button tile.
4. Icon-only content.
5. Hover and pressed feedback through foreground or opacity changes.
6. Existing Chinese tooltips remain available.

Text buttons retain their current text, border, and background treatment.

### 8.3 Quick-Add Scrolling

The quick-add todo popover receives a screen-aware maximum height. Its layout is divided into:

1. A vertically scrollable content area containing todo text, validation, and time settings.
2. A fixed footer containing the `创建` and `关闭` text buttons.

Long todo content must no longer push time controls or footer actions beyond the visible popover. The popover remains positioned within the monitor working area.

The existing edit-popover scrolling behavior remains and only adopts the new color treatment unless a regression is found.

## 9. Quick Note

The quick-note drag area keeps its original hit target and drag behavior. Its three visual grip lines become one centered `32 × 1 DIP` line.

The footer containing line and character statistics, cancel, and save moves 6 DIP toward the bottom. Approximately 8 DIP remains between the footer and window edge. Window dimensions, save state, validation, close confirmation, keyboard shortcuts, and title/body layout remain unchanged.

## 10. Component Boundaries

Expected implementation areas:

1. `Models/Note.cs` owns persisted favorite and folder values.
2. `Data/NoteRepository.cs` owns schema compatibility and metadata persistence.
3. `Services/NoteService.cs` owns favorite state transitions without modifying `UpdatedAt`.
4. `ViewModels/MainViewModel.cs` owns search text, sort mode, note projection refresh, and favorite commands.
5. Focused pure helpers own search classification, sorting/group names, time formatting, and body statistics.
6. `Views/MainWindow.xaml` owns the sidebar controls, note preview, footer actions, and editor metadata layout.
7. `Views/MainWindow.xaml.cs` owns view-only click routing, dynamic tooltips, visible-state synchronization, and editor message integration.
8. `EditorAssets/editor-host.js` owns browser paste interception.
9. `TodoWidgets/TodoWidgetWindow.xaml` owns widget colors, icon styles, and scroll-safe popover layout.
10. `QuickNotes/QuickNoteWindow.xaml` owns the grip and footer spacing refinements.

## 11. Error Handling

1. Schema upgrade operations are idempotent and fail startup with the existing application error path if the database cannot be upgraded.
2. A failed favorite save restores the original favorite values and refreshes the navigation projection.
3. Search against null or empty content is treated as no body match.
4. Sort and group refreshes preserve the selected note reference.
5. Editor paste failures use existing image-upload failure handling and do not insert duplicate placeholders.
6. Todo popover scrolling must not prevent keyboard focus or validation messages from becoming visible.

## 12. Validation

Automated tests cover:

1. New database creation and idempotent old-database column upgrades.
2. Favorite persistence, favorite timestamps, and unchanged `UpdatedAt`.
3. Default `未分类` folder values for main and quick notes.
4. Search classification, case-insensitive matching, title priority, and no-result state.
5. Time and favorite sorting in normal and search modes.
6. Date-group, favorite-group, and search-group names and ordering.
7. Current-year and historical modified-time formatting.
8. Favorite star source contracts and selection independence.
9. Dynamic desktop-todo button labels.
10. Editor line and character statistics and borderless footer layout.
11. Single-request image paste, native fallback, URL paste, and multi-image paste.
12. Todo-widget color resources, icon-only button style, preserved text buttons, and quick-add scrolling.
13. Quick-note single grip line and footer offset.
14. Existing notes, attachments, quick notes, todo widgets, and editor behavior.

Manual validation covers:

1. Search and selection preservation.
2. Sort-mode switching and dynamic tooltips.
3. Favorite toggling and immediate list movement.
4. Todo visibility action text synchronization.
5. Long quick-add todo content with reachable time settings and footer.
6. Widget and popover appearance at multiple opacity levels.
7. Repeated clipboard image paste in Markdown and rich-text modes.
8. Quick-note drag behavior and footer positioning.

## 13. Acceptance Criteria

This work is complete when:

1. The desktop-todo action always names the correct next action.
2. Search prioritizes title matches and preserves the active editor selection.
3. Time and favorite sorting follow the confirmed grouping and ordering rules.
4. Favorite and folder metadata survive restart and old databases upgrade safely.
5. Note previews show title, star, modified time, and folder in the agreed layout.
6. The three sidebar footer actions are present and intentionally empty.
7. Editor statistics and creation date share a borderless footer.
8. One clipboard image is inserted once per paste.
9. Todo widget and popovers use the confirmed extremely light green treatment.
10. Todo icon buttons have no border or tile while text buttons remain unchanged.
11. Long quick-add content remains vertically scrollable with fixed footer actions.
12. Quick notes use one drag line and the lower footer spacing.
13. Full Release tests and build pass without warnings or errors.
