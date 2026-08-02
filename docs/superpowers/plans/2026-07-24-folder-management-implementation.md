# QingJian Folder Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add persistent single-level folder management, folder-filtered note navigation, and safe single-note folder assignment without changing `develop`.

**Architecture:** Keep `Note.FolderName` as the note ownership field and add a `Folders` registry table so empty folders persist. Put transactional rename/delete behavior in `FolderRepository`, name and system-folder rules in `FolderService`, navigation state in `MainViewModel`, and management UI in a focused WPF window/ViewModel pair.

**Tech Stack:** .NET 8, WPF, EF Core 8, SQLite, WebView2, xUnit

## Global Constraints

- Work only in `C:\Users\Cristin\Desktop\VibeCoding\qingjian-ui-polish` on `feature/ui-polish`; do not modify `develop`.
- Each note belongs to exactly one single-level folder.
- Keep `Note.FolderName`; do not add a `FolderId` foreign key in this feature.
- “未分类” is permanent and cannot be renamed or deleted; “全部便签” is not a persisted folder.
- Folder names are trimmed, non-empty, at most 30 Unicode text elements, case-insensitively unique, and cannot be “全部便签” or “未分类”.
- Existing overlength legacy names remain readable but cannot be selected as a move target until renamed.
- Icon buttons are borderless and backgroundless with `ToolTipService.InitialShowDelay="0"` and Chinese tooltips.
- Folder moves do not alter `Note.UpdatedAt`.
- Quick notes continue to save into “未分类”; batch management remains unimplemented.
- Run .NET commands serially with `-c Release`.
- Do not update `docs/project-status.md` until the user says `完成分支收尾工作`.

---

## File Map

**Create:**

- `src/QingJian.App/Models/Folder.cs`: persisted registry entity and fixed system-folder ID.
- `src/QingJian.App/Models/FolderSummary.cs`: immutable folder/count projection shared by services and ViewModels.
- `src/QingJian.App/Services/FolderNamePolicy.cs`: Unicode-aware trimming, validation, and normalized-name generation.
- `src/QingJian.App/Data/IFolderRepository.cs`: folder persistence contract.
- `src/QingJian.App/Data/FolderRepository.cs`: schema upgrade, query, and transactional folder operations.
- `src/QingJian.App/Services/IFolderService.cs`: UI-facing folder operation contract.
- `src/QingJian.App/Services/FolderService.cs`: validation and system-folder rules.
- `src/QingJian.App/ViewModels/FolderListItemViewModel.cs`: one management-window row and inline edit state.
- `src/QingJian.App/ViewModels/FolderManagementViewModel.cs`: management commands, errors, counts, and selection result.
- `src/QingJian.App/Views/FolderManagementWindow.xaml`: folder manager layout.
- `src/QingJian.App/Views/FolderManagementWindow.xaml.cs`: row selection, delete confirmation, and close result.
- `src/QingJian.App/Views/FolderNameDialog.xaml`: lightweight name entry used from a note card.
- `src/QingJian.App/Views/FolderNameDialog.xaml.cs`: validate/submit/cancel dialog behavior.
- Focused tests matching each new production file under `tests/QingJian.App.Tests/`.

**Modify:**

- `src/QingJian.App/Data/AppDbContext.cs`: map `Folder`.
- `src/QingJian.App/Data/INoteRepository.cs` and `NoteRepository.cs`: persist folder-only updates.
- `src/QingJian.App/Services/INoteService.cs` and `NoteService.cs`: create in a folder and move with rollback.
- `src/QingJian.App/ViewModels/MainViewModel.cs`: folder catalog, filter, selection, and note-specific autosave.
- `src/QingJian.App/Views/MainWindow.xaml` and `.xaml.cs`: filter strip, folder menu, and window request.
- `src/QingJian.App/App.xaml.cs`: construct services and own the single folder-manager dialog.
- Existing fake services/repositories and XAML/code-behind tests that implement changed interfaces.

---

### Task 1: Folder Domain Model and Idempotent Schema Upgrade

**Files:**
- Create: `src/QingJian.App/Models/Folder.cs`
- Create: `src/QingJian.App/Models/FolderSummary.cs`
- Create: `src/QingJian.App/Services/FolderNamePolicy.cs`
- Create: `src/QingJian.App/Data/IFolderRepository.cs`
- Create: `src/QingJian.App/Data/FolderRepository.cs`
- Modify: `src/QingJian.App/Data/AppDbContext.cs`
- Test: `tests/QingJian.App.Tests/Data/FolderRepositoryInitializationTests.cs`
- Test: `tests/QingJian.App.Tests/Services/FolderNamePolicyTests.cs`

**Interfaces:**
- Produces: `Folder.SystemUncategorizedId`, `FolderSummary`, `FolderNamePolicy.NormalizeDisplayName`, `FolderNamePolicy.NormalizeKey`, and `IFolderRepository.InitializeAsync`.
- Consumes: the existing `AppDbContext` and `Notes.FolderName` column created by `NoteRepository.InitializeAsync`.

- [ ] **Step 1: Write failing policy and initialization tests**

Add tests proving Unicode text-element length, compatibility normalization, old-name backfill, reserved legacy-name repair, case-insensitive merge, and repeated initialization:

```csharp
[Fact]
public void Validate_RejectsMoreThanThirtyTextElements()
{
    var value = string.Concat(Enumerable.Repeat("便", 31));
    var error = FolderNamePolicy.GetValidationError(value, allowUncategorized: false);
    Assert.Equal("文件夹名称不能超过 30 个字符。", error);
}

[Fact]
public async Task InitializeAsync_BackfillsAndCanonicalizesLegacyFolderNamesIdempotently()
{
    await noteRepository.InitializeAsync();
    db.Notes.AddRange(
        CreateNote("one", " 项目 "),
        CreateNote("two", "项目"),
        CreateNote("three", "全部便签"));
    await db.SaveChangesAsync();

    var repository = new FolderRepository(db);
    await repository.InitializeAsync();
    await repository.InitializeAsync();

    var folders = await db.Folders.OrderBy(folder => folder.Name).ToListAsync();
    Assert.Contains(folders, folder => folder.Id == Folder.SystemUncategorizedId && folder.IsSystem);
    Assert.Single(folders.Where(folder => folder.Name == "项目"));
    Assert.Equal("未分类", (await db.Notes.SingleAsync(note => note.Id == "three")).FolderName);
}
```

- [ ] **Step 2: Run focused tests and verify RED**

Run:

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj -c Release --filter "FullyQualifiedName~FolderRepositoryInitializationTests|FullyQualifiedName~FolderNamePolicyTests"
```

Expected: FAIL because the folder model, policy, repository, and `DbSet` do not exist.

- [ ] **Step 3: Add the domain types and exact repository contract**

Use these public shapes:

```csharp
public sealed class Folder
{
    public const string SystemUncategorizedId = "system-uncategorized";
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsSystem { get; set; }
}

public sealed record FolderSummary(
    string Id,
    string Name,
    bool IsSystem,
    int NoteCount,
    bool IsValidMoveTarget);

public interface IFolderRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FolderSummary>> GetSummariesAsync(CancellationToken cancellationToken = default);
    Task<bool> NormalizedNameExistsAsync(string normalizedName, string? excludedFolderId = null, CancellationToken cancellationToken = default);
    Task AddAsync(Folder folder, CancellationToken cancellationToken = default);
    Task RenameAsync(string folderId, string oldName, string newName, string normalizedName, CancellationToken cancellationToken = default);
    Task DeleteAsync(string folderId, string folderName, CancellationToken cancellationToken = default);
}
```

Implement `FolderNamePolicy` with `StringInfo.ParseCombiningCharacters`, `NormalizationForm.FormKC`, and `ToUpperInvariant()`. Return exact Chinese validation text and expose `const string UncategorizedName = "未分类"` and `const string AllNotesName = "全部便签"`.

- [ ] **Step 4: Map the entity and implement schema initialization**

Add `DbSet<Folder> Folders` and map `Folders(Id TEXT PK, Name TEXT, NormalizedName TEXT, CreatedAt TEXT, IsSystem INTEGER)` with a unique index on `NormalizedName`.

Because `EnsureCreatedAsync` does not add tables to an old database, `FolderRepository.InitializeAsync` must execute idempotent `CREATE TABLE IF NOT EXISTS` and `CREATE UNIQUE INDEX IF NOT EXISTS` SQL inside a transaction, seed “未分类”, canonicalize existing note names, and insert missing registry rows. Preserve overlength legacy names and set `IsValidMoveTarget = false` for them.

- [ ] **Step 5: Run focused tests and verify GREEN**

Run the command from Step 2.

Expected: all folder initialization and policy tests PASS.

- [ ] **Step 6: Commit the domain and schema upgrade**

```powershell
git add src/QingJian.App/Models src/QingJian.App/Services/FolderNamePolicy.cs src/QingJian.App/Data/IFolderRepository.cs src/QingJian.App/Data/FolderRepository.cs src/QingJian.App/Data/AppDbContext.cs tests/QingJian.App.Tests/Data/FolderRepositoryInitializationTests.cs tests/QingJian.App.Tests/Services/FolderNamePolicyTests.cs
git commit -m "feat: register note folders"
```

---

### Task 2: Transactional Folder Repository Operations

**Files:**
- Modify: `src/QingJian.App/Data/FolderRepository.cs`
- Test: `tests/QingJian.App.Tests/Data/FolderRepositoryTests.cs`

**Interfaces:**
- Consumes: the `IFolderRepository` signatures and mapped `Folders` table from Task 1.
- Produces: reliable folder summaries, add, rename, and delete behavior for `FolderService`.

- [ ] **Step 1: Write failing real-SQLite transaction tests**

Cover active-note counts, excluded-ID duplicate checks, rename of active and soft-deleted notes, delete migration, system-folder rejection, and rollback. The core assertions are:

```csharp
await repository.RenameAsync(folder.Id, "项目", "工作", FolderNamePolicy.NormalizeKey("工作"));
Assert.All(await db.Notes.Where(note => note.Id == "active" || note.Id == "deleted").ToListAsync(),
    note => Assert.Equal("工作", note.FolderName));

await repository.DeleteAsync(folder.Id, "工作");
Assert.All(await db.Notes.Where(note => note.FolderName == "工作").ToListAsync(),
    note => Assert.Equal("未分类", note.FolderName));
Assert.DoesNotContain(await repository.GetSummariesAsync(), item => item.Id == folder.Id);
```

Inject a test-only checkpoint through this constructor, throw after note updates, and assert both notes and folder row retain their original values:

```csharp
public FolderRepository(
    AppDbContext dbContext,
    Func<CancellationToken, Task>? transactionCheckpoint = null)
```

Production uses the one-argument form through the optional parameter; tests pass a throwing delegate.

- [ ] **Step 2: Run repository tests and verify RED**

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj -c Release --filter FullyQualifiedName~FolderRepositoryTests
```

Expected: FAIL because operation bodies are not implemented transactionally.

- [ ] **Step 3: Implement query and mutations**

`GetSummariesAsync` must left-join active notes (`IsDeleted == false`), return zero for empty folders, put “未分类” first, then sort ordinary folders using `StringComparer.CurrentCultureIgnoreCase`. `RenameAsync` and `DeleteAsync` must use `Database.BeginTransactionAsync`, bulk-update all matching notes including soft-deleted rows, call the checkpoint, mutate the registry row, save, and commit. Reject `IsSystem` rows again in the repository as defense in depth.

- [ ] **Step 4: Run repository tests and verify GREEN**

Run the command from Step 2.

Expected: all `FolderRepositoryTests` PASS, including rollback.

- [ ] **Step 5: Commit transactional operations**

```powershell
git add src/QingJian.App/Data/FolderRepository.cs tests/QingJian.App.Tests/Data/FolderRepositoryTests.cs
git commit -m "feat: transact folder changes"
```

---

### Task 3: Folder Service Rules and System Protection

**Files:**
- Create: `src/QingJian.App/Services/IFolderService.cs`
- Create: `src/QingJian.App/Services/FolderService.cs`
- Test: `tests/QingJian.App.Tests/Services/FolderServiceTests.cs`

**Interfaces:**
- Consumes: `IFolderRepository` and `FolderNamePolicy`.
- Produces: the only folder API used by ViewModels and application wiring.

- [ ] **Step 1: Write failing service tests**

Test trim-on-create, case-insensitive duplicates, reserved names, overlength names, rename-with-self exclusion, system protection, and normalized outputs:

```csharp
[Fact]
public async Task CreateAsync_TrimsAndPersistsValidName()
{
    var repository = new FakeFolderRepository();
    var service = new FolderService(repository, () => UtcNow);

    var created = await service.CreateAsync("  项目  ");

    Assert.Equal("项目", created.Name);
    Assert.Equal(FolderNamePolicy.NormalizeKey("项目"), repository.Added!.NormalizedName);
}

[Fact]
public async Task DeleteAsync_RejectsUncategorized()
{
    var service = new FolderService(new FakeFolderRepository(), () => UtcNow);
    var system = new FolderSummary(Folder.SystemUncategorizedId, "未分类", true, 3, true);
    var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(system));
    Assert.Equal("未分类文件夹不能删除。", error.Message);
}
```

- [ ] **Step 2: Run service tests and verify RED**

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj -c Release --filter FullyQualifiedName~FolderServiceTests
```

Expected: FAIL because the service contract does not exist.

- [ ] **Step 3: Implement the service API**

Use this contract:

```csharp
public interface IFolderService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FolderSummary>> GetFoldersAsync(CancellationToken cancellationToken = default);
    Task<FolderSummary> CreateAsync(string name, CancellationToken cancellationToken = default);
    Task<FolderSummary> RenameAsync(FolderSummary folder, string newName, CancellationToken cancellationToken = default);
    Task DeleteAsync(FolderSummary folder, CancellationToken cancellationToken = default);
    Task<bool> IsValidMoveTargetAsync(string folderName, CancellationToken cancellationToken = default);
}
```

Validate before repository mutations, perform duplicate checks with `excludedFolderId` during rename, and return refreshed immutable summaries after successful operations. `IsValidMoveTargetAsync` returns true only for a registered folder with `IsValidMoveTarget == true`.

- [ ] **Step 4: Run service tests and verify GREEN**

Run the command from Step 2.

Expected: all `FolderServiceTests` PASS.

- [ ] **Step 5: Commit the folder service**

```powershell
git add src/QingJian.App/Services/IFolderService.cs src/QingJian.App/Services/FolderService.cs tests/QingJian.App.Tests/Services/FolderServiceTests.cs
git commit -m "feat: enforce folder rules"
```

---

### Task 4: Note Folder Persistence and Note-Specific Autosave

**Files:**
- Modify: `src/QingJian.App/Data/INoteRepository.cs`
- Modify: `src/QingJian.App/Data/NoteRepository.cs`
- Modify: `src/QingJian.App/Services/INoteService.cs`
- Modify: `src/QingJian.App/Services/NoteService.cs`
- Modify: `src/QingJian.App/ViewModels/MainViewModel.cs`
- Modify: all test fakes implementing `INoteRepository` or `INoteService`
- Test: `tests/QingJian.App.Tests/Data/NoteRepositoryTests.cs`
- Test: `tests/QingJian.App.Tests/Services/NoteServiceTests.cs`
- Test: `tests/QingJian.App.Tests/ViewModels/MainViewModelTests.cs`

**Interfaces:**
- Consumes: `IFolderService.IsValidMoveTargetAsync`.
- Produces: `CreateNoteInFolderAsync`, `MoveNoteAsync`, and delayed saves that retain the triggering note instance.

- [ ] **Step 1: Write failing persistence and rollback tests**

Add these behaviors:

```csharp
var note = await service.CreateNoteInFolderAsync("项目");
Assert.Equal("项目", note.FolderName);

var originalUpdatedAt = note.UpdatedAt;
await service.MoveNoteAsync(note, "工作");
Assert.Equal("工作", note.FolderName);
Assert.Equal(originalUpdatedAt, note.UpdatedAt);

repository.FolderUpdateException = new InvalidOperationException("boom");
await Assert.ThrowsAsync<InvalidOperationException>(() => service.MoveNoteAsync(note, "项目"));
Assert.Equal("工作", note.FolderName);
```

Add a ViewModel regression test that edits note A, immediately selects note B, waits past the debounce, and asserts `SavedIds` contains A rather than B.

- [ ] **Step 2: Run focused note tests and verify RED**

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj -c Release --filter "FullyQualifiedName~NoteRepositoryTests|FullyQualifiedName~NoteServiceTests|FullyQualifiedName~MainViewModelTests"
```

Expected: FAIL because folder-specific APIs and captured-note autosave are missing.

- [ ] **Step 3: Add repository and service methods**

Extend the contracts without removing the existing quick-note-compatible method:

```csharp
// INoteRepository
Task UpdateFolderAsync(Note note, CancellationToken cancellationToken = default);

// INoteService
Task<Note> CreateNoteInFolderAsync(string folderName, CancellationToken cancellationToken = default);
Task MoveNoteAsync(Note note, string folderName, CancellationToken cancellationToken = default);
```

`NoteRepository.UpdateFolderAsync` updates only `FolderName`. `NoteService` validates the target through `IFolderService`, preserves the old name, updates and persists, and restores on exception. `CreateNoteAsync()` continues to delegate to “未分类”; `CreateNoteInFolderAsync` does not change quick-note behavior.

Preserve existing constructor call sites and add production/test overloads explicitly:

```csharp
public NoteService(INoteRepository noteRepository);
public NoteService(INoteRepository noteRepository, Func<DateTime> utcNow);
public NoteService(INoteRepository noteRepository, IFolderService folderService);
public NoteService(INoteRepository noteRepository, IFolderService folderService, Func<DateTime> utcNow);
```

The first two support existing tests and the fixed “未分类” creation path. Folder-specific creation and movement throw a clear configuration exception if invoked without `IFolderService`; production always uses an overload that supplies it.

- [ ] **Step 4: Capture the actual edited note in autosave**

Change the event path to use `sender`:

```csharp
private void OnSelectedNotePropertyChanged(object? sender, PropertyChangedEventArgs e)
{
    if (_isLoadingSelection || sender is not Note note ||
        e.PropertyName is not (nameof(Note.Title) or nameof(Note.Content)))
    {
        return;
    }

    ScheduleAutoSave(note);
}

private async Task SaveAfterDelayAsync(Note note, CancellationToken cancellationToken)
{
    await Task.Delay(_autoSaveDelay, cancellationToken);
    await _noteService.SaveNoteAsync(note, cancellationToken);
    RefreshNoteNavigation();
}
```

Only move an item in the backing collection when it is still `SelectedNote`; saving an earlier note must not change the current selection.

- [ ] **Step 5: Run focused note tests and verify GREEN**

Run the command from Step 2.

Expected: all focused note, repository, and MainViewModel tests PASS.

- [ ] **Step 6: Commit note folder operations and autosave protection**

```powershell
git add src/QingJian.App/Data src/QingJian.App/Services src/QingJian.App/ViewModels/MainViewModel.cs tests/QingJian.App.Tests
git commit -m "feat: move notes between folders"
```

---

### Task 5: Main Navigation Folder State

**Files:**
- Modify: `src/QingJian.App/ViewModels/MainViewModel.cs`
- Test: `tests/QingJian.App.Tests/ViewModels/MainViewModelTests.cs`
- Test: `tests/QingJian.App.Tests/ViewModels/NoteNavigationHelperTests.cs`

**Interfaces:**
- Consumes: `INoteService.CreateNoteInFolderAsync`, `INoteService.MoveNoteAsync`, and `IFolderService.GetFoldersAsync`.
- Produces: `Folders`, `CurrentFolderName`, `HasFolderFilter`, `ShowFolderEmptyState`, `ApplyFolderFilter`, `RefreshFoldersAsync`, and `MoveNoteAsync` for WPF binding/code-behind.

- [ ] **Step 1: Write failing navigation tests**

Cover folder-only filtering, folder-scoped search, new-note destination, move selection, rename/delete synchronization, and initial all-notes state:

```csharp
viewModel.ApplyFolderFilter("项目");
Assert.Equal(new[] { "project-title", "project-body" },
    viewModel.NotesView.Cast<Note>().Select(note => note.Id));

viewModel.SearchText = "alpha";
Assert.DoesNotContain(viewModel.NotesView.Cast<Note>(), note => note.FolderName == "未分类");

await viewModel.NewNoteAsync();
Assert.Equal("项目", service.LastCreatedFolder);

await viewModel.MoveNoteAsync(projectNote, "未分类");
Assert.DoesNotContain(projectNote, viewModel.NotesView.Cast<Note>());
Assert.Same(nextVisibleNote, viewModel.SelectedNote);
```

- [ ] **Step 2: Run MainViewModel tests and verify RED**

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj -c Release --filter "FullyQualifiedName~MainViewModelTests|FullyQualifiedName~NoteNavigationHelperTests"
```

Expected: FAIL because folder navigation state does not exist.

- [ ] **Step 3: Extend MainViewModel with folder state**

Preserve the existing `MainViewModel(INoteService, ...)` overloads for non-folder tests, add matching overloads that accept `IFolderService`, and load folders after note schema initialization when the service is present. Expose:

```csharp
public ObservableCollection<FolderSummary> Folders { get; } = new();
public string? CurrentFolderName { get; private set; }
public bool HasFolderFilter => CurrentFolderName is not null;
public bool ShowFolderEmptyState => HasFolderFilter && string.IsNullOrWhiteSpace(SearchText) && NotesView.IsEmpty;
public string CurrentFolderDisplayName => CurrentFolderName ?? FolderNamePolicy.AllNotesName;
```

`FilterNote` checks exact folder ownership with `StringComparison.OrdinalIgnoreCase` before the current search predicate. `ApplyFolderFilter(null)` means all notes. `NewNoteAsync` chooses `CurrentFolderName ?? "未分类"`.

- [ ] **Step 4: Implement deterministic move and folder mutation handling**

Before refresh, capture the visible index of the moved note. After a successful move, refresh and select the item now at that index, otherwise the preceding item. Add:

```csharp
public void ApplyFolderRename(string oldName, string newName);
public void ApplyFolderDeletion(string deletedName);
public async Task RefreshFoldersAsync(CancellationToken cancellationToken = default);
```

Rename follows the new filter name. Deleting the current folder switches to “未分类”. Raise all empty-state and filter-display notifications from one `RefreshNoteNavigationCore` path.

- [ ] **Step 5: Run navigation tests and verify GREEN**

Run the command from Step 2.

Expected: all folder-filter and existing navigation tests PASS.

- [ ] **Step 6: Commit navigation state**

```powershell
git add src/QingJian.App/ViewModels/MainViewModel.cs tests/QingJian.App.Tests/ViewModels
git commit -m "feat: filter notes by folder"
```

---

### Task 6: Folder Management ViewModel

**Files:**
- Create: `src/QingJian.App/ViewModels/FolderListItemViewModel.cs`
- Create: `src/QingJian.App/ViewModels/FolderManagementViewModel.cs`
- Test: `tests/QingJian.App.Tests/ViewModels/FolderManagementViewModelTests.cs`

**Interfaces:**
- Consumes: `IFolderService`.
- Produces: row collection, inline create/rename commands, delete command, errors, and mutation events consumed by the management window/App.

- [ ] **Step 1: Write failing command/state tests**

Test initial order, one active inline editor, confirm/cancel, validation errors staying open, delete count, and mutation events:

```csharp
await viewModel.LoadAsync();
viewModel.BeginCreateCommand.Execute(null);
viewModel.EditorText = "项目";
await viewModel.ConfirmEditAsync();

Assert.Equal("项目", viewModel.Items.Single(item => item.Name == "项目").Name);
Assert.False(viewModel.IsEditing);
Assert.Equal(string.Empty, viewModel.ErrorMessage);
Assert.Equal("项目", createdEventName);
```

- [ ] **Step 2: Run ViewModel tests and verify RED**

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj -c Release --filter FullyQualifiedName~FolderManagementViewModelTests
```

Expected: FAIL because the ViewModels do not exist.

- [ ] **Step 3: Implement focused row and window ViewModels**

`FolderListItemViewModel` exposes `Id`, `Name`, `NoteCount`, `IsSystem`, and `CanManage`. `FolderManagementViewModel` exposes:

```csharp
public ObservableCollection<FolderListItemViewModel> Items { get; }
public int AllNotesCount { get; }
public bool IsEditing { get; }
public string EditorText { get; set; }
public string ErrorMessage { get; }
public RelayCommand BeginCreateCommand { get; }
public AsyncRelayCommand ConfirmEditCommand { get; }
public RelayCommand CancelEditCommand { get; }
public void BeginRename(FolderListItemViewModel item);
public Task DeleteAsync(FolderListItemViewModel item, CancellationToken cancellationToken = default);
public event Action<string, string>? FolderRenamed;
public event Action<string>? FolderDeleted;
```

`AllNotesCount` is the sum of actual folder counts and raises `PropertyChanged` whenever items reload. The rename and delete row buttons call the explicit methods from the focused window code-behind. Do not introduce a generic command or a project-wide command refactor.

- [ ] **Step 4: Run ViewModel tests and verify GREEN**

Run the command from Step 2.

Expected: all management ViewModel tests PASS.

- [ ] **Step 5: Commit management state**

```powershell
git add src/QingJian.App/ViewModels/FolderListItemViewModel.cs src/QingJian.App/ViewModels/FolderManagementViewModel.cs tests/QingJian.App.Tests/ViewModels/FolderManagementViewModelTests.cs
git commit -m "feat: manage folder state"
```

---

### Task 7: Folder Management Window and Name Dialog

**Files:**
- Create: `src/QingJian.App/Views/FolderManagementWindow.xaml`
- Create: `src/QingJian.App/Views/FolderManagementWindow.xaml.cs`
- Create: `src/QingJian.App/Views/FolderNameDialog.xaml`
- Create: `src/QingJian.App/Views/FolderNameDialog.xaml.cs`
- Test: `tests/QingJian.App.Tests/Views/FolderManagementWindowXamlTests.cs`
- Test: `tests/QingJian.App.Tests/Views/FolderManagementWindowCodeBehindTests.cs`
- Test: `tests/QingJian.App.Tests/Views/FolderNameDialogTests.cs`

**Interfaces:**
- Consumes: `FolderManagementViewModel` and `IFolderService.CreateAsync`.
- Produces: nullable `SelectedFolderName`, `SelectedAllNotes`, and a validated new-folder name for callers.

- [ ] **Step 1: Write failing XAML and code-behind structure tests**

Parse XAML like existing settings tests and assert named controls, transparent icon styles, exact tooltips, and no actions on the system row. Verify code-behind contains confirmation text built from the count:

```csharp
Assert.Equal("新建文件夹", (string?)newButton.Attribute("ToolTip"));
Assert.Equal("0", (string?)newButton.Attribute(XName.Get("InitialShowDelay", toolTipNamespace)));
Assert.Contains("其中 {item.NoteCount} 条便签将移至未分类", source);
```

- [ ] **Step 2: Run window tests and verify RED**

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj -c Release --filter "FullyQualifiedName~FolderManagementWindow|FullyQualifiedName~FolderNameDialog"
```

Expected: FAIL because both windows are missing.

- [ ] **Step 3: Build the management window**

Use a restrained owned dialog, no nested cards, and an unframed list. Top row contains title plus a 32 DIP add icon. Each item row has folder icon, name, `X 条便签`, and 32 DIP rename/delete icons with 8 DIP spacing. Inline editor uses a TextBox plus confirm/cancel icons. Clicking the row sets either `SelectedAllNotes = true` or `SelectedFolderName = item.Name`, then closes with `DialogResult = true`.

Use `MessageBoxButton.YesNo`, default `No`, and these prompt forms:

```text
确定删除文件夹“项目”吗？

其中 3 条便签将移至未分类。
```

```text
确定删除空文件夹“项目”吗？
```

- [ ] **Step 4: Build the lightweight name dialog**

Expose `FolderNameDialog(IFolderService folderService)`, `string? CreatedFolderName`, and `Task SubmitAsync()`. Keep the text box focused on load; Enter submits and Escape cancels. Service exceptions display in a Chinese error TextBlock and leave the dialog open.

- [ ] **Step 5: Run window tests and verify GREEN**

Run the command from Step 2.

Expected: all management-window and name-dialog tests PASS.

- [ ] **Step 6: Commit the folder windows**

```powershell
git add src/QingJian.App/Views/FolderManagementWindow.xaml src/QingJian.App/Views/FolderManagementWindow.xaml.cs src/QingJian.App/Views/FolderNameDialog.xaml src/QingJian.App/Views/FolderNameDialog.xaml.cs tests/QingJian.App.Tests/Views
git commit -m "feat: add folder manager window"
```

---

### Task 8: Main Window Integration and Application Wiring

**Files:**
- Modify: `src/QingJian.App/Views/MainWindow.xaml`
- Modify: `src/QingJian.App/Views/MainWindow.xaml.cs`
- Modify: `src/QingJian.App/App.xaml.cs`
- Modify: `tests/QingJian.App.Tests/AppStartupWiringTests.cs`
- Modify: `tests/QingJian.App.Tests/Views/MainWindowXamlTests.cs`
- Modify: `tests/QingJian.App.Tests/Views/MainWindowCodeBehindTests.cs`

**Interfaces:**
- Consumes: all APIs from Tasks 3-7.
- Produces: the complete user workflow from the existing folder button, card folder row, filter strip, and new-note command.

- [ ] **Step 1: Write failing wiring and XAML tests**

Assert:

1. `FolderButton` has a click handler.
2. A collapsed `ActiveFolderFilterPanel` binds to `HasFolderFilter` and contains `ClearFolderFilterButton` with tooltip “显示全部便签”.
3. The card folder row is a focusable-false transparent button with immediate tooltip “移动到文件夹”.
4. Empty-folder and no-search-result states are separate.
5. `App` creates `FolderRepository`, `FolderService`, passes the service to `NoteService` and `MainViewModel`, and owns one visible folder manager.

- [ ] **Step 2: Run focused integration tests and verify RED**

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj -c Release --filter "FullyQualifiedName~AppStartupWiringTests|FullyQualifiedName~MainWindow"
```

Expected: FAIL because folder controls and wiring are absent.

- [ ] **Step 3: Add filter strip and clickable folder metadata**

Place `ActiveFolderFilterPanel` directly under the search row. It contains the folder glyph, ellipsized `CurrentFolderDisplayName`, and an icon-only clear button. Replace the existing third-row `StackPanel` with a transparent button preserving the same icon/text alignment and stable 24 DIP row height.

Use exact tooltips:

- Folder management: `文件夹`
- Card assignment: `移动到文件夹`
- Clear filter: `显示全部便签`
- New folder menu entry: `新建文件夹…`

- [ ] **Step 4: Implement safe filter and move orchestration**

Add `FolderManagementRequested` to `MainWindow`. Expose:

```csharp
public async Task ApplyFolderFilterAsync(string? folderName)
{
    await PullLatestEditorMarkdownAsync();
    await _viewModel.SaveSelectedNoteNowAsync();
    _viewModel.ApplyFolderFilter(folderName);
}
```

Build the card `ContextMenu` from `_viewModel.Folders`. Before moving the selected note, pull and save the latest editor Markdown. For a non-selected note, move directly. Selecting “新建文件夹…” opens `FolderNameDialog`; on success refresh folders and move the clicked note. Catch failures and show Chinese messages without changing selection.

- [ ] **Step 5: Wire the manager in App**

Construct in this order:

```csharp
var noteRepository = new NoteRepository(dbContext);
var folderRepository = new FolderRepository(dbContext);
var folderService = new FolderService(folderRepository);
var noteService = new NoteService(noteRepository, folderService);
var viewModel = new MainViewModel(noteService, folderService);
```

Maintain `_folderManagementWindow` like `_settingsWindow`. Subscribe to ViewModel rename/delete events to call `MainViewModel.ApplyFolderRename`, `ApplyFolderDeletion`, and `RefreshFoldersAsync`. After `ShowDialog`, apply the selected folder or all-notes result through `MainWindow.ApplyFolderFilterAsync`.

- [ ] **Step 6: Run focused integration tests and verify GREEN**

Run the command from Step 2.

Expected: all startup and main-window tests PASS.

- [ ] **Step 7: Commit the complete UI workflow**

```powershell
git add src/QingJian.App/App.xaml.cs src/QingJian.App/Views/MainWindow.xaml src/QingJian.App/Views/MainWindow.xaml.cs tests/QingJian.App.Tests/AppStartupWiringTests.cs tests/QingJian.App.Tests/Views
git commit -m "feat: integrate folder workflows"
```

---

### Task 9: Full Regression and Runtime UI Verification

**Files:**
- Modify only test files needed to correct genuine regression coverage gaps discovered here.
- Do not modify `docs/project-status.md` on the feature branch.

**Interfaces:**
- Consumes: the complete folder feature.
- Produces: verified Release build and runtime evidence.

- [ ] **Step 1: Run the complete test suite serially**

```powershell
dotnet test -c Release
```

Expected: all tests PASS with 0 failed and 0 skipped. Record the exact total.

- [ ] **Step 2: Build without restoring**

```powershell
dotnet build -c Release --no-restore
```

Expected: 0 warnings and 0 errors.

- [ ] **Step 3: Start the application from the feature worktree**

```powershell
dotnet run --project src\QingJian.App\QingJian.App.csproj -c Release --no-build
```

Expected: the main window opens and no startup error appears.

- [ ] **Step 4: Exercise the accepted runtime scenarios**

Verify in order:

1. Open folder manager; create empty “项目” and “工作” folders.
2. Reject blank, duplicate, reserved, and 31-character names with Chinese messages.
3. Rename “项目” to “个人项目”; confirm counts and card labels update.
4. Select “个人项目”; confirm folder filter strip and folder-scoped search.
5. Create a note; confirm it belongs to “个人项目”.
6. Edit its body and immediately move it to “工作”; confirm content persists, `UpdatedAt` ordering is unchanged, and selection advances.
7. Create a folder from a card menu; confirm the note moves there.
8. Delete the populated folder; confirm the exact count prompt and migration to “未分类”.
9. Delete an empty folder; confirm the separate empty-folder prompt.
10. Return to “全部便签”; verify time/favorite sorting and search remain correct.
11. Save a quick note; confirm its card displays “未分类”.

- [ ] **Step 5: Inspect layout at normal and narrow window sizes**

Confirm no clipping or overlap in the three-line cards, active filter strip, folder menu, inline editor, long names, and delete confirmation. Icon buttons must remain borderless/backgroundless and every icon must show its Chinese tooltip immediately.

- [ ] **Step 6: Stop the smoke-test process and verify worktree isolation**

```powershell
Get-Process QingJian.App -ErrorAction SilentlyContinue | Stop-Process
git status --short --branch
git -C C:\Users\Cristin\Desktop\VibeCoding\qingjian status --short --branch
```

Expected: no QingJian process remains; feature changes exist only in `qingjian-ui-polish`; the main worktree remains on `develop` with its pre-existing `outputs/` untouched.

- [ ] **Step 7: Commit only genuine regression-test adjustments**

If Step 1-5 required test-only corrections, commit those exact test files:

```powershell
git add tests/QingJian.App.Tests
git commit -m "test: cover folder workflows"
```

If no test files changed, do not create an empty commit.

---

## Completion Gate

Implementation is complete only when:

1. All accepted requirements in `docs/superpowers/specs/2026-07-24-folder-management-design.md` are covered.
2. `dotnet test -c Release` passes completely.
3. `dotnet build -c Release --no-restore` reports 0 warnings and 0 errors.
4. Runtime verification covers create, rename, delete, filter, search, move, create-from-card, current-folder new note, and quick-note fallback.
5. `develop` has not moved or been modified.

Do not merge. Merge and update `develop` documentation only after the user says `完成分支收尾工作`.
