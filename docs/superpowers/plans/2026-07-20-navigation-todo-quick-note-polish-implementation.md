# QingJian Navigation, Todo Widget, and Quick Note Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add persistent note favorites and folders, searchable alternate navigation sorting, richer note previews, editor statistics, dynamic todo visibility copy, lighter and scroll-safe todo surfaces, quick-note spacing polish, and single-insertion image paste.

**Architecture:** Extend the existing `Notes` table through an idempotent SQLite compatibility upgrade, keep favorite updates separate from content saves, and drive the sidebar through a filtered and custom-sorted `ListCollectionView`. Put search classification, grouping, comparison, time formatting, and body statistics in pure helpers; keep WPF-only selection preservation and click routing in `MainWindow`; keep browser paste ownership in `editor-host.js`.

**Tech Stack:** .NET 8, WPF/XAML, SQLite, Entity Framework Core 8, `ListCollectionView`, WebView2, Toast UI Editor, JavaScript, xUnit, XML/source contract tests.

## Global Constraints

- Work only in `C:\Users\Cristin\Desktop\VibeCoding\qingjian-ui-polish` on `feature/ui-polish`.
- Do not modify, merge, or check out `develop` during implementation.
- Preserve note Markdown storage, autosave, attachments, quick-note behavior, todo transparency, and current 8 DIP rounded main-window styles.
- Existing and new notes default to folder `未分类` and not favorited.
- Favorite changes must not update `UpdatedAt`.
- Search is immediate, case-insensitive, and checks title plus raw Markdown body.
- Time sort is the non-persisted startup default.
- Todo widget and popover background color is `#F2FAF4`; existing opacity mechanics remain unchanged.
- Only todo icon buttons lose borders and button-tile backgrounds; todo text buttons retain their current treatment.
- Run all .NET test and build commands serially with `-c Release`.

---

### Task 1: Add Persistent Note Metadata and Idempotent Schema Upgrade

**Files:**
- Modify: `src/QingJian.App/Models/Note.cs`
- Modify: `src/QingJian.App/Data/AppDbContext.cs`
- Modify: `src/QingJian.App/Data/NoteRepository.cs`
- Modify: `tests/QingJian.App.Tests/Data/AppDbContextTests.cs`
- Modify: `tests/QingJian.App.Tests/Data/NoteRepositoryTests.cs`

**Interfaces:**
- Produces: `Note.IsFavorite`, `Note.FavoritedAt`, `Note.FolderName`, and an upgraded `NoteRepository.InitializeAsync`.
- Preserves: the existing `Notes` primary key, soft deletion, active-note ordering, and content columns.

- [ ] **Step 1: Write failing model and schema-upgrade tests**

Add an old-schema compatibility test that creates the current six-column `Notes` table manually, inserts a note, initializes the repository twice, then inspects and loads the upgraded note:

```csharp
[Fact]
public async Task InitializeAsync_AddsFavoriteAndFolderColumnsToExistingNotesDatabase()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();
    await CreateLegacyNotesTableAsync(connection);
    await InsertLegacyNoteAsync(connection, "legacy");
    var repository = CreateRepository(connection);

    await repository.InitializeAsync();
    await repository.InitializeAsync();

    var columns = await ReadColumnNamesAsync(connection, "Notes");
    Assert.Contains("IsFavorite", columns);
    Assert.Contains("FavoritedAt", columns);
    Assert.Contains("FolderName", columns);

    var note = Assert.Single(await repository.GetActiveNotesAsync());
    Assert.False(note.IsFavorite);
    Assert.Null(note.FavoritedAt);
    Assert.Equal("未分类", note.FolderName);
}
```

Extend the new-database persistence test to save and reload a favorited note with a folder name:

```csharp
Assert.True(loaded.IsFavorite);
Assert.Equal(favoritedAt, loaded.FavoritedAt);
Assert.Equal("项目", loaded.FolderName);
```

- [ ] **Step 2: Run the focused tests to verify RED**

Run:

```powershell
dotnet test -c Release --filter "FullyQualifiedName~AppDbContextTests|FullyQualifiedName~NoteRepositoryTests"
```

Expected: compile failures because the metadata properties do not exist, followed by schema failures until initialization upgrades old databases.

- [ ] **Step 3: Add metadata properties and EF mappings**

In `Note`, add notifying properties for values rendered in the sidebar:

```csharp
private bool _isFavorite;
private DateTime? _favoritedAt;
private string _folderName = "未分类";

public bool IsFavorite
{
    get => _isFavorite;
    set => SetField(ref _isFavorite, value);
}

public DateTime? FavoritedAt
{
    get => _favoritedAt;
    set => SetField(ref _favoritedAt, value);
}

public string FolderName
{
    get => _folderName;
    set => SetField(ref _folderName, string.IsNullOrWhiteSpace(value) ? "未分类" : value);
}
```

Map them in `AppDbContext`:

```csharp
entity.Property(note => note.IsFavorite)
    .HasColumnType("INTEGER")
    .HasDefaultValue(false)
    .IsRequired();
entity.Property(note => note.FavoritedAt)
    .HasColumnType("TEXT");
entity.Property(note => note.FolderName)
    .HasColumnType("TEXT")
    .HasDefaultValue("未分类")
    .IsRequired();
```

- [ ] **Step 4: Implement the idempotent compatibility upgrade**

Change `NoteRepository.InitializeAsync` to ensure creation and add missing columns before any query:

```csharp
public async Task InitializeAsync(CancellationToken cancellationToken = default)
{
    await _dbContext.Database.EnsureCreatedAsync(cancellationToken);
    var columns = await GetNoteColumnNamesAsync(cancellationToken);

    if (!columns.Contains("IsFavorite"))
        await _dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE Notes ADD COLUMN IsFavorite INTEGER NOT NULL DEFAULT 0;", cancellationToken);
    if (!columns.Contains("FavoritedAt"))
        await _dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE Notes ADD COLUMN FavoritedAt TEXT NULL;", cancellationToken);
    if (!columns.Contains("FolderName"))
        await _dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE Notes ADD COLUMN FolderName TEXT NOT NULL DEFAULT '未分类';", cancellationToken);

    await _dbContext.Database.ExecuteSqlRawAsync(
        "UPDATE Notes SET FolderName = '未分类' WHERE FolderName IS NULL OR trim(FolderName) = '';",
        cancellationToken);
}
```

Use `DbConnection.CreateCommand()` and `PRAGMA table_info("Notes")` in `GetNoteColumnNamesAsync`. Open the connection only when it is closed and restore its prior state afterward.

- [ ] **Step 5: Verify GREEN and commit**

Run the focused command from Step 2, then:

```powershell
git add src/QingJian.App/Models/Note.cs src/QingJian.App/Data/AppDbContext.cs src/QingJian.App/Data/NoteRepository.cs tests/QingJian.App.Tests/Data/AppDbContextTests.cs tests/QingJian.App.Tests/Data/NoteRepositoryTests.cs
git commit -m "feat: persist note favorite and folder metadata"
```

---

### Task 2: Add Favorite Persistence Without Updating Modified Time

**Files:**
- Modify: `src/QingJian.App/Data/INoteRepository.cs`
- Modify: `src/QingJian.App/Data/NoteRepository.cs`
- Modify: `src/QingJian.App/Services/INoteService.cs`
- Modify: `src/QingJian.App/Services/NoteService.cs`
- Modify: `tests/QingJian.App.Tests/Data/NoteRepositoryTests.cs`
- Modify: `tests/QingJian.App.Tests/Services/NoteServiceTests.cs`
- Modify: test doubles implementing `INoteRepository` or `INoteService`

**Interfaces:**
- Produces: `INoteRepository.UpdateFavoriteAsync(Note, CancellationToken)` and `INoteService.SetFavoriteAsync(Note, bool, CancellationToken)`.
- Consumes: metadata properties from Task 1 and the service UTC clock.

- [ ] **Step 1: Write failing favorite transition tests**

Add service tests for favorite and unfavorite behavior:

```csharp
[Fact]
public async Task SetFavoriteAsync_UpdatesFavoriteTimestampWithoutChangingUpdatedAt()
{
    var repository = new InMemoryNoteRepository();
    var updatedAt = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
    var favoritedAt = new DateTime(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);
    var note = CreateNote(updatedAt);
    var service = new NoteService(repository, () => favoritedAt);

    await service.SetFavoriteAsync(note, true);

    Assert.True(note.IsFavorite);
    Assert.Equal(favoritedAt, note.FavoritedAt);
    Assert.Equal(updatedAt, note.UpdatedAt);
    Assert.Single(repository.FavoriteUpdates);
}
```

Add a failure test whose repository throws and assert the original `IsFavorite`, `FavoritedAt`, and `UpdatedAt` values are restored. Add a repository test that reloads favorite values while preserving content and modification time.

- [ ] **Step 2: Verify RED**

Run:

```powershell
dotnet test -c Release --filter "FullyQualifiedName~NoteRepositoryTests|FullyQualifiedName~NoteServiceTests"
```

Expected: compile failures for the missing favorite methods.

- [ ] **Step 3: Implement repository and service methods**

Add to `INoteRepository`:

```csharp
Task UpdateFavoriteAsync(Note note, CancellationToken cancellationToken = default);
```

Repository implementation updates only favorite columns:

```csharp
existing.IsFavorite = note.IsFavorite;
existing.FavoritedAt = note.FavoritedAt;
await _dbContext.SaveChangesAsync(cancellationToken);
```

Add to `INoteService`:

```csharp
Task SetFavoriteAsync(Note note, bool isFavorite, CancellationToken cancellationToken = default);
```

Service implementation records old values, applies the new values, awaits repository persistence, and restores old values in `catch` before rethrowing. New notes explicitly initialize `FolderName = "未分类"`, `IsFavorite = false`, and `FavoritedAt = null`.

- [ ] **Step 4: Update all interface test doubles and verify GREEN**

Add no-op or recording implementations to every test double found by:

```powershell
rg -n "class .*INote(Service|Repository)|: INote(Service|Repository)" tests src
```

Run the focused tests from Step 2.

- [ ] **Step 5: Commit**

```powershell
git add src/QingJian.App/Data/INoteRepository.cs src/QingJian.App/Data/NoteRepository.cs src/QingJian.App/Services/INoteService.cs src/QingJian.App/Services/NoteService.cs tests/QingJian.App.Tests
git commit -m "feat: add independent note favorite persistence"
```

---

### Task 3: Build Pure Navigation Search, Sort, Group, and Formatting Rules

**Files:**
- Create: `src/QingJian.App/ViewModels/NoteNavigationSortMode.cs`
- Create: `src/QingJian.App/ViewModels/NoteSearchMatchKind.cs`
- Create: `src/QingJian.App/ViewModels/NoteNavigationHelper.cs`
- Create: `src/QingJian.App/ViewModels/NoteNavigationComparer.cs`
- Modify: `src/QingJian.App/ViewModels/NoteNavigationGroupDescription.cs`
- Modify: `src/QingJian.App/ViewModels/NoteNavigationDateHelper.cs`
- Create: `src/QingJian.App/Views/NoteUpdatedAtDisplayConverter.cs`
- Create: `src/QingJian.App/Views/MarkdownBodyStatsDisplayConverter.cs`
- Create: `tests/QingJian.App.Tests/ViewModels/NoteNavigationHelperTests.cs`
- Modify: `tests/QingJian.App.Tests/ViewModels/NoteNavigationDateHelperTests.cs`

**Interfaces:**
- Produces: search classification, custom comparison, dynamic group names, modified-time formatting, and Markdown body statistics.
- Consumes: `Note`, `NoteNavigationSortMode`, current search text, and local current time.

- [ ] **Step 1: Write failing helper tests**

Cover these exact behaviors:

```csharp
Assert.Equal(NoteSearchMatchKind.Title,
    NoteNavigationHelper.GetSearchMatch(CreateNote("Alpha", "body alpha"), "ALPHA"));
Assert.Equal(NoteSearchMatchKind.Body,
    NoteNavigationHelper.GetSearchMatch(CreateNote("Other", "contains alpha"), "alpha"));
Assert.Equal(NoteSearchMatchKind.None,
    NoteNavigationHelper.GetSearchMatch(CreateNote("Other", "body"), "alpha"));
Assert.Equal("标题匹配", NoteNavigationHelper.GetGroupName(titleMatch, "alpha", NoteNavigationSortMode.Time, now));
Assert.Equal("正文匹配", NoteNavigationHelper.GetGroupName(bodyMatch, "alpha", NoteNavigationSortMode.Favorite, now));
Assert.Equal("收藏", NoteNavigationHelper.GetGroupName(favorite, "", NoteNavigationSortMode.Favorite, now));
Assert.Equal("其他", NoteNavigationHelper.GetGroupName(normal, "", NoteNavigationSortMode.Favorite, now));
Assert.Equal("6/19 15:30", NoteNavigationDateHelper.FormatUpdatedAt(currentYear, now));
Assert.Equal("2025/6/19 15:30", NoteNavigationDateHelper.FormatUpdatedAt(previousYear, now));
Assert.Equal("2 行 4 字", NoteNavigationHelper.FormatBodyStats("你好\r\n世界"));
```

Add comparison tests proving title matches precede body matches, favorites precede others, favorite timestamps sort descending, and nonfavorites sort by `UpdatedAt` descending.

- [ ] **Step 2: Verify RED**

Run:

```powershell
dotnet test -c Release --filter "FullyQualifiedName~NoteNavigationHelperTests|FullyQualifiedName~NoteNavigationDateHelperTests"
```

Expected: compile failures for the new enums, helper, comparer, and format methods.

- [ ] **Step 3: Implement enums and pure helper**

Use:

```csharp
public enum NoteNavigationSortMode { Time, Favorite }
public enum NoteSearchMatchKind { None, Title, Body }
```

`GetSearchMatch` trims the search text and uses `IndexOf(..., StringComparison.OrdinalIgnoreCase)`. Blank search text is treated as a visible note but returns `None`; filtering only calls match classification when search is nonblank.

`FormatBodyStats` normalizes CRLF and CR to LF, counts lines as in `QuickNoteWindow.FormatBodyStats`, and excludes LF characters from the character count.

- [ ] **Step 4: Implement comparer and dynamic group description**

`NoteNavigationComparer` accepts delegates for current sort mode and search text. Its comparison order is:

1. Search match rank when search is active.
2. Favorite/nonfavorite rank when favorite sort is active.
3. `FavoritedAt` descending for favorite notes.
4. `UpdatedAt` descending otherwise.
5. `Id` ordinal as a stable final tie-breaker.

Modify `NoteNavigationGroupDescription` to call:

```csharp
NoteNavigationHelper.GetGroupName(note, _searchText(), _sortMode(), _localNow())
```

- [ ] **Step 5: Add converters and verify GREEN**

`NoteUpdatedAtDisplayConverter` calls `FormatUpdatedAt(value, DateTime.Now)`. `MarkdownBodyStatsDisplayConverter` calls `FormatBodyStats(value as string ?? string.Empty)`. Both throw `NotSupportedException` from `ConvertBack`.

Run the focused tests from Step 2.

- [ ] **Step 6: Commit**

```powershell
git add src/QingJian.App/ViewModels src/QingJian.App/Views/NoteUpdatedAtDisplayConverter.cs src/QingJian.App/Views/MarkdownBodyStatsDisplayConverter.cs tests/QingJian.App.Tests/ViewModels
git commit -m "feat: add note navigation search and sort rules"
```

---

### Task 4: Add Search, Sort, Favorite, and No-Result State to MainViewModel

**Files:**
- Modify: `src/QingJian.App/ViewModels/MainViewModel.cs`
- Modify: `tests/QingJian.App.Tests/ViewModels/MainViewModelTests.cs`

**Interfaces:**
- Produces: `SearchText`, `NavigationSortMode`, `SortToggleToolTip`, `ShowNoSearchResults`, `ToggleNavigationSortCommand`, and `ToggleFavoriteAsync(Note)`.
- Consumes: Task 2 favorite service API and Task 3 navigation helper/comparer/group description.

- [ ] **Step 1: Write failing projection tests**

Add tests for:

1. Blank search with time groups.
2. Search groups `标题匹配` then `正文匹配`.
3. Favorite mode groups and ordering.
4. `SearchText` preserving `SelectedNote` when the selected note is filtered out.
5. `ShowNoSearchResults` becoming true only when notes exist but no search result exists.
6. Sort tooltip toggling from `按收藏` to `按时间`.
7. Successful favorite refresh without changing selection or `UpdatedAt`.
8. Failed favorite persistence restoring values and leaving selection unchanged.

Use fixed local and UTC clocks and assert exact note ID order through `NotesView.Cast<Note>()`.

- [ ] **Step 2: Verify RED**

Run:

```powershell
dotnet test -c Release --filter FullyQualifiedName~MainViewModelTests
```

Expected: compile failures for the new properties and method.

- [ ] **Step 3: Replace static sort descriptions with dynamic projection rules**

Initialize:

```csharp
NotesView = new ListCollectionView(Notes)
{
    Filter = FilterNote,
    CustomSort = new NoteNavigationComparer(() => NavigationSortMode, () => SearchText)
};
NotesView.GroupDescriptions.Add(new NoteNavigationGroupDescription(
    localNow,
    () => SearchText,
    () => NavigationSortMode));
```

Remove the existing `SortDescriptions` entry because `CustomSort` and `SortDescriptions` cannot be active together.

- [ ] **Step 4: Add state and refresh behavior**

Implement:

```csharp
public string SearchText { get; set; }
public NoteNavigationSortMode NavigationSortMode { get; private set; } = NoteNavigationSortMode.Time;
public string SortToggleToolTip => NavigationSortMode == NoteNavigationSortMode.Time ? "按收藏" : "按时间";
public bool ShowNoSearchResults => Notes.Count > 0
    && !string.IsNullOrWhiteSpace(SearchText)
    && NotesView.IsEmpty;
public RelayCommand ToggleNavigationSortCommand { get; }
```

Every projection refresh raises `PropertyChanged` for `ShowNoSearchResults`, `SortToggleToolTip`, and `SelectedNote` so a one-way WPF selection binding can restore the existing selected note after clearing search.

- [ ] **Step 5: Add favorite transition method**

Implement:

```csharp
public async Task ToggleFavoriteAsync(Note note)
{
    await _noteService.SetFavoriteAsync(note, !note.IsFavorite);
    RefreshNoteNavigation();
}
```

Do not assign `SelectedNote` in this method. Let the service restore values and rethrow on persistence failure.

- [ ] **Step 6: Verify GREEN and commit**

Run the focused tests from Step 2, then:

```powershell
git add src/QingJian.App/ViewModels/MainViewModel.cs tests/QingJian.App.Tests/ViewModels/MainViewModelTests.cs
git commit -m "feat: add searchable favorite note navigation"
```

---

### Task 5: Build the Main-Window Sidebar and Metadata UI

**Files:**
- Create: `src/QingJian.App/TodoWidgets/TodoWidgetVisibilityAction.cs`
- Modify: `src/QingJian.App/TodoWidgets/TodoWidgetCoordinator.cs`
- Modify: `src/QingJian.App/Views/MainWindow.xaml`
- Modify: `src/QingJian.App/Views/MainWindow.xaml.cs`
- Modify: `src/QingJian.App/Resources/Styles.xaml`
- Create: `tests/QingJian.App.Tests/TodoWidgets/TodoWidgetVisibilityActionTests.cs`
- Modify: `tests/QingJian.App.Tests/TodoWidgets/TodoWidgetCoordinatorTests.cs`
- Modify: `tests/QingJian.App.Tests/Views/MainWindowXamlTests.cs`
- Modify: `tests/QingJian.App.Tests/Views/MainWindowCodeBehindTests.cs`

**Interfaces:**
- Consumes: Task 3 converters and Task 4 view-model state.
- Produces: dynamic todo action copy, search/sort controls, three-row note previews, favorite star actions, sidebar footer actions, preserved selection, and editor statistics.

- [ ] **Step 1: Write failing visibility and XAML contract tests**

Test:

```csharp
Assert.Equal("显示桌面待办", TodoWidgetVisibilityAction.GetLabel(false));
Assert.Equal("隐藏桌面待办", TodoWidgetVisibilityAction.GetLabel(true));
```

Extend coordinator tests to assert a new `VisibilityChanged` event reports `true` after show and `false` after hide.

Extend XAML tests to assert:

1. `TodoWidgetToggleButton` is named and no longer has static `显示/隐藏桌面待办` content.
2. `NoteSearchTextBox` and `NavigationSortButton` share a row below the todo action.
3. The sort button binds its command and uses time/favorite data triggers with tooltips `按收藏` and `按时间`.
4. Note item rows contain `FavoriteButton`, updated-time converter binding, and folder glyph plus `FolderName`.
5. Favorite button has `ToolTipService.InitialShowDelay="0"`, outline/filled star triggers, and yellow fill when favorite.
6. The note list uses one-way selection plus `SelectionChanged="NoteListBox_OnSelectionChanged"`.
7. `NoSearchResultsTextBlock` binds `ShowNoSearchResults` and displays `没有匹配的便签`.
8. Bottom buttons appear in settings/folder/batch order and have no command or click handler.
9. Editor footer has no top border, body stats on the left, and creation date right-aligned.

- [ ] **Step 2: Verify RED**

Run:

```powershell
dotnet test -c Release --filter "FullyQualifiedName~TodoWidgetVisibilityActionTests|FullyQualifiedName~TodoWidgetCoordinatorTests|FullyQualifiedName~MainWindowXamlTests|FullyQualifiedName~MainWindowCodeBehindTests"
```

Expected: compile and contract failures for missing visibility event, controls, handlers, and bindings.

- [ ] **Step 3: Add todo visibility event and dynamic label helper**

`TodoWidgetVisibilityAction.GetLabel(bool isVisible)` returns the confirmed Chinese label. Add:

```csharp
public event EventHandler<bool>? VisibilityChanged;
```

to the coordinator. Use a private `SetVisibility(bool)` method in `InitializeAsync`, `ShowWidgetAsync`, and `HideWidgetAsync`; raise the event only when the value changes.

In `MainWindow`, subscribe in the constructor, update the named button through `Dispatcher` when needed, and initialize its content from `_todoWidgetCoordinator.IsVisible`.

- [ ] **Step 4: Restructure sidebar XAML**

Use a top action stack, a search `Grid` with `*` and `32` columns, a middle result `Grid`, and a `DockPanel.Dock="Bottom"` footer action stack. Keep the `ListBox` scrollable and footer fixed.

Use Segoe MDL2 Assets glyphs for search-sort target, star, folder, settings, folder management, and batch management. Keep all new icon buttons 32 DIP with 8 DIP spacing and immediate Chinese tooltips.

- [ ] **Step 5: Implement selection and favorite click routing**

Use one-way selection binding and:

```csharp
private void NoteListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
{
    if (e.AddedItems.OfType<Note>().FirstOrDefault() is { } note)
        _viewModel.SelectedNote = note;
}
```

Handle the favorite star at preview mouse-down, set `e.Handled = true` before awaiting `_viewModel.ToggleFavoriteAsync(note)`, and show `收藏状态保存失败。\n\n{ex.Message}` on failure. This prevents the parent list item from changing selection.

- [ ] **Step 6: Update editor footer**

Register `NoteUpdatedAtDisplayConverter` and `MarkdownBodyStatsDisplayConverter` resources. Replace the editor footer border with a plain `Grid` using left/right columns:

```xml
<TextBlock Text="{Binding SelectedNote.Content, Converter={StaticResource MarkdownBodyStatsDisplayConverter}}" />
<TextBlock Grid.Column="1"
           HorizontalAlignment="Right"
           Text="{Binding SelectedNote.CreatedAt, Converter={StaticResource NoteCreatedAtDisplayConverter}}" />
```

Remove `BorderThickness="0,1,0,0"` and its border brush.

- [ ] **Step 7: Verify GREEN and commit**

Run the focused command from Step 2, then:

```powershell
git add src/QingJian.App/TodoWidgets/TodoWidgetVisibilityAction.cs src/QingJian.App/TodoWidgets/TodoWidgetCoordinator.cs src/QingJian.App/Views/MainWindow.xaml src/QingJian.App/Views/MainWindow.xaml.cs src/QingJian.App/Resources/Styles.xaml tests/QingJian.App.Tests
git commit -m "feat: build searchable favorite sidebar"
```

---

### Task 6: Prevent Duplicate Image Paste

**Files:**
- Modify: `src/QingJian.App/EditorAssets/editor-host.js`
- Modify: `tests/QingJian.App.Tests/Editor/EditorHostAssetTests.cs`

**Interfaces:**
- Produces: one upload request per pasted image file while preserving native fallback and normal text paste.
- Preserves: `addImageBlobHook`, remote URL insertion, multiple distinct files, and WPF clipboard fallback.

- [ ] **Step 1: Write failing source-contract tests**

Assert the image branch contains all three event stops before iterating files:

```csharp
Assert.Contains("event.stopImmediatePropagation();", script, StringComparison.Ordinal);
Assert.True(script.IndexOf("event.stopImmediatePropagation();", StringComparison.Ordinal)
    < script.IndexOf("files.forEach", StringComparison.Ordinal));
Assert.Contains("postNativePasteRequested();", script, StringComparison.Ordinal);
Assert.Contains("requestLocalImageUpload(file, insertImageMarkdown);", script, StringComparison.Ordinal);
```

Also assert only one document-level paste listener is registered.

- [ ] **Step 2: Verify RED**

Run:

```powershell
dotnet test -c Release --filter FullyQualifiedName~EditorHostAssetTests
```

Expected: failure because `stopImmediatePropagation` is missing.

- [ ] **Step 3: Implement single-owner image paste**

In the `files.length > 0` branch of `handlePaste`, call:

```javascript
event.preventDefault();
event.stopPropagation();
event.stopImmediatePropagation();
```

before requesting uploads. Do not call `stopImmediatePropagation` for normal text or the no-data native fallback branch.

- [ ] **Step 4: Verify syntax, tests, and commit**

Run serially:

```powershell
node --check src/QingJian.App/EditorAssets/editor-host.js
dotnet test -c Release --filter FullyQualifiedName~EditorHostAssetTests
git add src/QingJian.App/EditorAssets/editor-host.js tests/QingJian.App.Tests/Editor/EditorHostAssetTests.cs
git commit -m "fix: prevent duplicate pasted images"
```

---

### Task 7: Polish Todo Widget Colors, Icon Buttons, and Quick-Add Scrolling

**Files:**
- Modify: `src/QingJian.App/Resources/Styles.xaml`
- Modify: `src/QingJian.App/TodoWidgets/TodoWidgetWindow.xaml`
- Modify: `src/QingJian.App/TodoWidgets/TodoWidgetWindow.xaml.cs`
- Modify: `tests/QingJian.App.Tests/TodoWidgets/TodoWidgetWindowXamlTests.cs`

**Interfaces:**
- Produces: `#F2FAF4` widget/popover surfaces, transparent icon buttons, preserved text buttons, and a constrained scrollable quick-add form.
- Preserves: opacity binding, window transparency, text button copy, validation, popover positioning, and edit-popover behavior.

- [ ] **Step 1: Write failing XAML and source tests**

Assert:

1. `TodoWidgetBackgroundColor` equals `#F2FAF4`.
2. A `TodoWidgetPopoverBackgroundBrush` exists and all quick-add/edit/preview popovers use it instead of `#FAF8F4`.
3. `TodoWidgetIconButtonStyle` sets transparent background and border, zero border thickness, and zero padding or icon-safe padding.
4. Existing icon controls such as edit/delete/month arrows use the icon style.
5. Text buttons `创建`, `关闭`, `保存`/`新增`, `清空`, and `本月` keep textual content and do not use the icon-only style.
6. `QuickAddPopover` contains rows for title, scrollable content, and fixed footer.
7. The `ScrollViewer` contains `QuickAddTextBox`, both validation blocks, and `QuickAddTimePickerBorder`.
8. `PositionQuickAddPopoverNear` assigns `QuickAddPopover.MaxHeight` from available content height before measuring.

- [ ] **Step 2: Verify RED**

Run:

```powershell
dotnet test -c Release --filter FullyQualifiedName~TodoWidgetWindowXamlTests
```

Expected: failures for the old color, visible icon-button tiles, and StackPanel-only quick-add layout.

- [ ] **Step 3: Add color resources and quiet icon style**

Set:

```xml
<Color x:Key="TodoWidgetBackgroundColor">#F2FAF4</Color>
<SolidColorBrush x:Key="TodoWidgetPopoverBackgroundBrush" Color="#F2FAF4" />
```

Update only `TodoWidgetIconButtonStyle` and icon-button usages. Do not change the global button style or text-action buttons.

- [ ] **Step 4: Restructure quick-add popover**

Replace the outer `StackPanel` with a `Grid`:

```xml
<Grid.RowDefinitions>
    <RowDefinition Height="Auto" />
    <RowDefinition Height="*" />
    <RowDefinition Height="Auto" />
</Grid.RowDefinitions>
```

Place the title in row 0, a vertical `ScrollViewer` containing the form in row 1, and the existing `创建 / 关闭` DockPanel in row 2.

- [ ] **Step 5: Constrain measurement and positioning**

In `PositionQuickAddPopoverNear`, calculate available size before measuring, then set:

```csharp
QuickAddPopover.MaxHeight = availableSize.Height;
```

Update `MeasureQuickAddPopoverSize` to measure with the fixed width and `MaxHeight` instead of two positive infinities. Ensure the positioner receives the constrained desired height.

- [ ] **Step 6: Verify GREEN and commit**

Run the focused tests from Step 2, then:

```powershell
git add src/QingJian.App/Resources/Styles.xaml src/QingJian.App/TodoWidgets/TodoWidgetWindow.xaml src/QingJian.App/TodoWidgets/TodoWidgetWindow.xaml.cs tests/QingJian.App.Tests/TodoWidgets/TodoWidgetWindowXamlTests.cs
git commit -m "style: lighten and stabilize todo widget popovers"
```

---

### Task 8: Refine Quick-Note Drag Grip and Footer Position

**Files:**
- Modify: `src/QingJian.App/QuickNotes/QuickNoteWindow.xaml`
- Modify: `tests/QingJian.App.Tests/QuickNotes/QuickNoteWindowTests.cs`

**Interfaces:**
- Produces: one centered 32 by 1 DIP drag line and a footer moved 6 DIP down with an 8 DIP bottom inset.
- Preserves: drag hit area, window dimensions, body statistics, save/cancel behavior, and shortcuts.

- [ ] **Step 1: Update tests to define the new layout**

Replace the three-line expectation with:

```csharp
var lines = grip.Descendants()
    .Where(element => element.Name.LocalName == "Border"
        && ((string?)FindAttributeByLocalName(element, "Name"))?.StartsWith("DragHandleLine", StringComparison.Ordinal) == true)
    .ToArray();
var line = Assert.Single(lines);
Assert.Equal("32", (string?)line.Attribute("Width"));
Assert.Equal("1", (string?)line.Attribute("Height"));
```

Add a footer test asserting its bottom offset is 6 DIP lower than the current layout and retains `BodyStatsTextBlock`, `CancelButton`, and `SaveButton` in one panel.

- [ ] **Step 2: Verify RED**

Run:

```powershell
dotnet test -c Release --filter FullyQualifiedName~QuickNoteWindowTests
```

Expected: failure because three 24 DIP grip lines still exist and the footer has its old inset.

- [ ] **Step 3: Update XAML**

Keep `DragHandle` at row height 16. Replace `DragHandleGrip` contents with one named border:

```xml
<Border x:Name="DragHandleLine"
        Width="32"
        Height="1"
        Background="{StaticResource MutedTextBrush}"
        Opacity="0.5" />
```

Move the footer 6 DIP down by applying a bottom offset while retaining about 8 DIP inside the outer border. Do not move title or body rows and do not change `Width`, `Height`, or `Padding` on unrelated sides.

- [ ] **Step 4: Verify GREEN and commit**

Run the focused tests from Step 2, then:

```powershell
git add src/QingJian.App/QuickNotes/QuickNoteWindow.xaml tests/QingJian.App.Tests/QuickNotes/QuickNoteWindowTests.cs
git commit -m "style: simplify quick note grip and footer"
```

---

### Task 9: Full Verification and Runtime Workflow Check

**Files:**
- Modify only files directly responsible for a regression found during verification.

**Interfaces:**
- Consumes: Tasks 1 through 8.
- Produces: fresh automated, build, runtime, and branch-scope evidence on `feature/ui-polish`.

- [ ] **Step 1: Run all tests serially**

```powershell
dotnet test -c Release
```

Expected: all tests pass with zero failures and zero skipped tests.

- [ ] **Step 2: Run the Release build**

```powershell
dotnet build -c Release --no-restore
```

Expected: zero warnings and zero errors.

- [ ] **Step 3: Launch the Release application and verify the main window**

Run:

```powershell
src\QingJian.App\bin\Release\net8.0-windows\QingJian.App.exe
```

Verify:

1. Todo action text follows actual widget visibility in both directions.
2. Search preserves the editor selection and shows title matches before body matches.
3. Time and favorite modes display correct groups, order, icon, and tooltip.
4. Star clicks do not change selection; favorite state survives restart; time ordering does not change solely from favorite toggles.
5. Note previews show three rows with current-year and historical time formatting.
6. Sidebar footer buttons are fixed and intentionally do nothing.
7. Editor footer statistics update while typing and creation date remains right-aligned without a divider.
8. One clipboard image inserts once in Markdown and rich-text modes; multiple distinct image files each insert once.

- [ ] **Step 4: Verify todo widget and quick note**

Verify:

1. Widget and all popovers use the `#F2FAF4` family at multiple opacity settings.
2. Icon buttons have no border or tile; text buttons remain unchanged.
3. A long quick-add body scrolls vertically while time controls and fixed footer remain reachable.
4. Quick note shows one drag line, still drags normally, and its footer is lower without clipping.

- [ ] **Step 5: Review branch scope**

Run:

```powershell
git diff --check b126a05..HEAD
git diff --name-status b126a05..HEAD
git status --short --branch
git log --oneline --decorate -15
git rev-parse develop
```

Expected: branch is `feature/ui-polish`, worktree is clean, changes match this plan, and `develop` remains unchanged.
