# Hotkey Quick Note Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a global `Ctrl + Alt + N` shortcut that opens a new draggable quick-note card near the mouse pointer, saves through the existing note service, and syncs visible main-window state.

**Architecture:** Keep OS hotkey registration, quick-note persistence coordination, and quick-note UI separate. Core draft validation and save/sync logic are testable without live WPF windows or OS hotkey registration; WPF-specific code is added after those seams exist.

**Tech Stack:** WPF, .NET 8, SQLite through the existing `INoteService`, Win32 P/Invoke for global hotkey registration and cursor/screen placement, xUnit.

## Global Constraints

- Store generated project files under `C:\Users\Cristin\Desktop\VibeCoding\qingjian\.worktrees\qingjian-mvp` unless a later user instruction says otherwise.
- Work on branch `feature/hotkey-note`.
- Register `Ctrl + Alt + N` as a global hotkey while the app process is running.
- Every hotkey press opens a new independent quick-note window.
- Quick-note windows are borderless, draggable, and positioned near the mouse pointer.
- Quick-note windows provide an optional title field and a required body field.
- Save with `Ctrl + Enter` or the visible save action.
- Cancel with `Esc` or the visible cancel action.
- Canceling a non-empty body requires discard confirmation.
- Blank titles are generated from the first non-empty body line.
- Quick notes save through the existing `INoteService` and SQLite storage.
- If the main window is visible and not minimized, the saved note appears at the top and is selected without activating the main window.
- If the main window is minimized, saving a quick note does not restore or activate it.
- Hotkey registration failure shows one startup warning and does not block normal app use.
- This feature does not add tray residency or a shortcut settings page.

---

## File Structure

- Create `src/QingJian.App/QuickNotes/QuickNoteDraft.cs`: immutable draft request data from the quick-note UI.
- Create `src/QingJian.App/QuickNotes/QuickNoteTitleGenerator.cs`: derives and truncates titles from body text.
- Create `src/QingJian.App/QuickNotes/QuickNoteSaveResult.cs`: small result object for save success/failure.
- Create `src/QingJian.App/QuickNotes/QuickNoteCoordinator.cs`: creates quick-note windows, saves drafts, and syncs the main ViewModel when the main window is visible and not minimized.
- Create `src/QingJian.App/QuickNotes/IQuickNoteWindow.cs`: interface used by the coordinator and tests.
- Create `src/QingJian.App/QuickNotes/IQuickNoteWindowFactory.cs`: factory seam for real and test quick-note windows.
- Create `src/QingJian.App/QuickNotes/QuickNoteWindow.xaml`: borderless note-card UI.
- Create `src/QingJian.App/QuickNotes/QuickNoteWindow.xaml.cs`: UI interactions, drag, key handling, save/cancel events.
- Create `src/QingJian.App/Hotkeys/HotkeyDefinition.cs`: centralized hotkey definition.
- Create `src/QingJian.App/Hotkeys/GlobalHotkeyService.cs`: Win32 hotkey registration and WPF message hook.
- Create `tests/QingJian.App.Tests/QuickNotes/QuickNoteTitleGeneratorTests.cs`: title derivation tests.
- Create `tests/QingJian.App.Tests/QuickNotes/QuickNoteCoordinatorTests.cs`: save and sync tests.
- Modify `src/QingJian.App/ViewModels/MainViewModel.cs`: add `AddSavedNote(Note note, bool select)`.
- Modify `tests/QingJian.App.Tests/ViewModels/MainViewModelTests.cs`: cover `AddSavedNote`.
- Modify `src/QingJian.App/App.xaml.cs`: wire hotkey service, coordinator, warning, and shutdown cleanup.
- Modify `src/QingJian.App/Resources/Styles.xaml`: add quick-note background, border, and error brushes.

---

### Task 1: Quick-Note Draft Title Rules

**Files:**
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\.worktrees\qingjian-mvp\src\QingJian.App\QuickNotes\QuickNoteDraft.cs`
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\.worktrees\qingjian-mvp\src\QingJian.App\QuickNotes\QuickNoteTitleGenerator.cs`
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\.worktrees\qingjian-mvp\tests\QingJian.App.Tests\QuickNotes\QuickNoteTitleGeneratorTests.cs`

**Interfaces:**
- Consumes: no project-specific services.
- Produces:
  - `public sealed record QuickNoteDraft(string? Title, string Body);`
  - `public static class QuickNoteTitleGenerator`
  - `public const int MaxTitleLength = 40;`
  - `public static string CreateTitle(string? requestedTitle, string body);`
  - `public static bool HasBody(string? body);`

- [ ] **Step 1: Write failing title generation tests**

Create `tests\QingJian.App.Tests\QuickNotes\QuickNoteTitleGeneratorTests.cs`:

```csharp
using QingJian.App.QuickNotes;
using Xunit;

namespace QingJian.App.Tests.QuickNotes;

public sealed class QuickNoteTitleGeneratorTests
{
    [Fact]
    public void HasBody_ReturnsFalse_ForNullEmptyOrWhitespace()
    {
        Assert.False(QuickNoteTitleGenerator.HasBody(null));
        Assert.False(QuickNoteTitleGenerator.HasBody(""));
        Assert.False(QuickNoteTitleGenerator.HasBody("   \r\n\t"));
    }

    [Fact]
    public void HasBody_ReturnsTrue_ForNonWhitespaceBody()
    {
        Assert.True(QuickNoteTitleGenerator.HasBody("remember this"));
    }

    [Fact]
    public void CreateTitle_UsesTrimmedRequestedTitle_WhenProvided()
    {
        var title = QuickNoteTitleGenerator.CreateTitle("  Meeting notes  ", "body");

        Assert.Equal("Meeting notes", title);
    }

    [Fact]
    public void CreateTitle_UsesFirstNonEmptyBodyLine_WhenTitleIsBlank()
    {
        var title = QuickNoteTitleGenerator.CreateTitle(" ", "\r\n  First line  \r\nSecond line");

        Assert.Equal("First line", title);
    }

    [Fact]
    public void CreateTitle_TruncatesLongGeneratedTitle()
    {
        var body = new string('a', QuickNoteTitleGenerator.MaxTitleLength + 10);

        var title = QuickNoteTitleGenerator.CreateTitle(null, body);

        Assert.Equal(QuickNoteTitleGenerator.MaxTitleLength, title.Length);
        Assert.EndsWith("...", title);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj --filter QuickNoteTitleGeneratorTests
```

Expected: build fails because `QingJian.App.QuickNotes.QuickNoteTitleGenerator` does not exist.

- [ ] **Step 3: Add draft and title generator**

Create `src\QingJian.App\QuickNotes\QuickNoteDraft.cs`:

```csharp
namespace QingJian.App.QuickNotes;

public sealed record QuickNoteDraft(string? Title, string Body);
```

Create `src\QingJian.App\QuickNotes\QuickNoteTitleGenerator.cs`:

```csharp
namespace QingJian.App.QuickNotes;

public static class QuickNoteTitleGenerator
{
    public const int MaxTitleLength = 40;

    public static bool HasBody(string? body)
    {
        return !string.IsNullOrWhiteSpace(body);
    }

    public static string CreateTitle(string? requestedTitle, string body)
    {
        var title = string.IsNullOrWhiteSpace(requestedTitle)
            ? FirstBodyLine(body)
            : requestedTitle.Trim();

        return Truncate(title);
    }

    private static string FirstBodyLine(string body)
    {
        var lines = body.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        return lines.Select(line => line.Trim()).FirstOrDefault(line => line.Length > 0) ?? string.Empty;
    }

    private static string Truncate(string title)
    {
        if (title.Length <= MaxTitleLength)
        {
            return title;
        }

        return string.Concat(title.AsSpan(0, MaxTitleLength - 3), "...");
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run:

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj --filter QuickNoteTitleGeneratorTests
```

Expected: all `QuickNoteTitleGeneratorTests` pass.

- [ ] **Step 5: Commit title rules**

Run:

```powershell
git add src\QingJian.App\QuickNotes\QuickNoteDraft.cs src\QingJian.App\QuickNotes\QuickNoteTitleGenerator.cs tests\QingJian.App.Tests\QuickNotes\QuickNoteTitleGeneratorTests.cs
git commit -m "feat: add quick note title rules"
```

Expected: commit succeeds on `feature/hotkey-note`.

---

### Task 2: Main ViewModel Saved-Note Sync

**Files:**
- Modify: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\.worktrees\qingjian-mvp\src\QingJian.App\ViewModels\MainViewModel.cs`
- Modify: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\.worktrees\qingjian-mvp\tests\QingJian.App.Tests\ViewModels\MainViewModelTests.cs`

**Interfaces:**
- Consumes: `QingJian.App.Models.Note`.
- Produces: `public void AddSavedNote(Note note, bool select);`

- [ ] **Step 1: Add failing ViewModel tests**

Append these tests inside `MainViewModelTests` before `CreateNote`:

```csharp
[Fact]
public void AddSavedNote_InsertsNoteAtTopAndSelectsWhenRequested()
{
    var existing = CreateNote("existing", "Existing");
    var added = CreateNote("added", "Added");
    var service = new InMemoryNoteService(existing);
    var viewModel = new MainViewModel(service);
    viewModel.Notes.Add(existing);
    viewModel.SelectedNote = existing;

    viewModel.AddSavedNote(added, select: true);

    Assert.Equal("added", viewModel.Notes[0].Id);
    Assert.Same(added, viewModel.SelectedNote);
}

[Fact]
public void AddSavedNote_DoesNotSelectWhenSelectIsFalse()
{
    var existing = CreateNote("existing", "Existing");
    var added = CreateNote("added", "Added");
    var service = new InMemoryNoteService(existing);
    var viewModel = new MainViewModel(service);
    viewModel.Notes.Add(existing);
    viewModel.SelectedNote = existing;

    viewModel.AddSavedNote(added, select: false);

    Assert.Equal("added", viewModel.Notes[0].Id);
    Assert.Same(existing, viewModel.SelectedNote);
}

[Fact]
public void AddSavedNote_RaisesIsEmptyChanged_WhenFirstNoteIsInserted()
{
    var added = CreateNote("added", "Added");
    var service = new InMemoryNoteService();
    var viewModel = new MainViewModel(service);
    var raised = false;

    viewModel.PropertyChanged += (_, args) =>
    {
        if (args.PropertyName == nameof(MainViewModel.IsEmpty))
        {
            raised = true;
        }
    };

    viewModel.AddSavedNote(added, select: true);

    Assert.True(raised);
    Assert.False(viewModel.IsEmpty);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj --filter "AddSavedNote"
```

Expected: build fails because `MainViewModel.AddSavedNote` does not exist.

- [ ] **Step 3: Implement `AddSavedNote`**

Add this method to `src\QingJian.App\ViewModels\MainViewModel.cs` after `DeleteSelectedNoteAsync`:

```csharp
public void AddSavedNote(Note note, bool select)
{
    var wasEmpty = IsEmpty;

    Notes.Insert(0, note);

    if (select)
    {
        SelectedNote = note;
    }

    if (wasEmpty)
    {
        OnPropertyChanged(nameof(IsEmpty));
    }
}
```

- [ ] **Step 4: Run ViewModel tests**

Run:

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj --filter MainViewModelTests
```

Expected: all `MainViewModelTests` pass.

- [ ] **Step 5: Commit ViewModel sync seam**

Run:

```powershell
git add src\QingJian.App\ViewModels\MainViewModel.cs tests\QingJian.App.Tests\ViewModels\MainViewModelTests.cs
git commit -m "feat: sync saved quick notes into main view model"
```

Expected: commit succeeds.

---

### Task 3: Quick-Note Coordinator Save Logic

**Files:**
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\.worktrees\qingjian-mvp\src\QingJian.App\QuickNotes\QuickNoteSaveResult.cs`
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\.worktrees\qingjian-mvp\src\QingJian.App\QuickNotes\IQuickNoteWindow.cs`
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\.worktrees\qingjian-mvp\src\QingJian.App\QuickNotes\IQuickNoteWindowFactory.cs`
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\.worktrees\qingjian-mvp\src\QingJian.App\QuickNotes\QuickNoteCoordinator.cs`
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\.worktrees\qingjian-mvp\tests\QingJian.App.Tests\QuickNotes\QuickNoteCoordinatorTests.cs`

**Interfaces:**
- Consumes:
  - `INoteService.CreateNoteAsync(CancellationToken)`
  - `INoteService.SaveNoteAsync(Note, CancellationToken)`
  - `MainViewModel.AddSavedNote(Note note, bool select)`
- Produces:
  - `public sealed record QuickNoteSaveResult(bool Succeeded, Note? Note, string? ErrorMessage);`
  - `public interface IQuickNoteWindow`
  - `public interface IQuickNoteWindowFactory`
  - `public sealed class QuickNoteCoordinator`
  - `public Task<QuickNoteSaveResult> SaveDraftAsync(QuickNoteDraft draft, bool syncToMainWindow, CancellationToken cancellationToken = default);`
  - `public void OpenQuickNote();`

- [ ] **Step 1: Write failing coordinator tests**

Create `tests\QingJian.App.Tests\QuickNotes\QuickNoteCoordinatorTests.cs`:

```csharp
using QingJian.App.Models;
using QingJian.App.QuickNotes;
using QingJian.App.Services;
using QingJian.App.ViewModels;
using Xunit;

namespace QingJian.App.Tests.QuickNotes;

public sealed class QuickNoteCoordinatorTests
{
    [Fact]
    public async Task SaveDraftAsync_RejectsEmptyBody()
    {
        var service = new InMemoryNoteService();
        var viewModel = new MainViewModel(service);
        var coordinator = new QuickNoteCoordinator(service, viewModel, new TestWindowFactory(), () => false);

        var result = await coordinator.SaveDraftAsync(new QuickNoteDraft("Title", "   "), syncToMainWindow: true);

        Assert.False(result.Succeeded);
        Assert.Null(result.Note);
        Assert.Equal("请输入便签内容。", result.ErrorMessage);
        Assert.Empty(service.SavedNotes);
    }

    [Fact]
    public async Task SaveDraftAsync_CreatesAndSavesNoteWithGeneratedTitle()
    {
        var service = new InMemoryNoteService();
        var viewModel = new MainViewModel(service);
        var coordinator = new QuickNoteCoordinator(service, viewModel, new TestWindowFactory(), () => false);

        var result = await coordinator.SaveDraftAsync(new QuickNoteDraft(null, "First line\nSecond line"), syncToMainWindow: false);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Note);
        Assert.Equal("First line", result.Note.Title);
        Assert.Equal("First line\nSecond line", result.Note.Content);
        Assert.Single(service.SavedNotes);
    }

    [Fact]
    public async Task SaveDraftAsync_SyncsToMainViewModel_WhenRequested()
    {
        var service = new InMemoryNoteService();
        var viewModel = new MainViewModel(service);
        var coordinator = new QuickNoteCoordinator(service, viewModel, new TestWindowFactory(), () => false);

        var result = await coordinator.SaveDraftAsync(new QuickNoteDraft("Title", "Body"), syncToMainWindow: true);

        Assert.True(result.Succeeded);
        Assert.Single(viewModel.Notes);
        Assert.Same(result.Note, viewModel.SelectedNote);
    }

    [Fact]
    public async Task SaveDraftAsync_DoesNotSyncToMainViewModel_WhenNotRequested()
    {
        var service = new InMemoryNoteService();
        var viewModel = new MainViewModel(service);
        var coordinator = new QuickNoteCoordinator(service, viewModel, new TestWindowFactory(), () => false);

        var result = await coordinator.SaveDraftAsync(new QuickNoteDraft("Title", "Body"), syncToMainWindow: false);

        Assert.True(result.Succeeded);
        Assert.Empty(viewModel.Notes);
    }

    [Fact]
    public void OpenQuickNote_CreatesNewWindowEachTime()
    {
        var service = new InMemoryNoteService();
        var viewModel = new MainViewModel(service);
        var factory = new TestWindowFactory();
        var coordinator = new QuickNoteCoordinator(service, viewModel, factory, () => false);

        coordinator.OpenQuickNote();
        coordinator.OpenQuickNote();

        Assert.Equal(2, factory.Windows.Count);
        Assert.All(factory.Windows, window => Assert.True(window.WasShown));
    }

    private sealed class InMemoryNoteService : INoteService
    {
        private readonly List<Note> _notes = new();

        public List<Note> SavedNotes { get; } = new();

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<Note>> GetActiveNotesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Note>>(_notes.ToList());
        }

        public Task<Note> CreateNoteAsync(CancellationToken cancellationToken = default)
        {
            var note = new Note
            {
                Id = $"note-{_notes.Count + 1}",
                Title = NoteService.DefaultTitle,
                Content = string.Empty,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false
            };
            _notes.Add(note);
            return Task.FromResult(note);
        }

        public Task SaveNoteAsync(Note note, CancellationToken cancellationToken = default)
        {
            SavedNotes.Add(note);
            return Task.CompletedTask;
        }

        public Task DeleteNoteAsync(Note note, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TestWindowFactory : IQuickNoteWindowFactory
    {
        public List<TestQuickNoteWindow> Windows { get; } = new();

        public IQuickNoteWindow Create()
        {
            var window = new TestQuickNoteWindow();
            Windows.Add(window);
            return window;
        }
    }

    private sealed class TestQuickNoteWindow : IQuickNoteWindow
    {
        public event EventHandler<QuickNoteDraft>? SaveRequested;

        public bool WasShown { get; private set; }

        public void ShowWindow()
        {
            WasShown = true;
        }

        public void CloseWindow()
        {
        }

        public void ShowSaveError(string message)
        {
        }

        public void RequestSave(QuickNoteDraft draft)
        {
            SaveRequested?.Invoke(this, draft);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj --filter QuickNoteCoordinatorTests
```

Expected: build fails because the quick-note coordinator interfaces and result type do not exist.

- [ ] **Step 3: Add coordinator interfaces and result**

Create `src\QingJian.App\QuickNotes\QuickNoteSaveResult.cs`:

```csharp
using QingJian.App.Models;

namespace QingJian.App.QuickNotes;

public sealed record QuickNoteSaveResult(bool Succeeded, Note? Note, string? ErrorMessage)
{
    public static QuickNoteSaveResult Success(Note note) => new(true, note, null);

    public static QuickNoteSaveResult Failure(string errorMessage) => new(false, null, errorMessage);
}
```

Create `src\QingJian.App\QuickNotes\IQuickNoteWindow.cs`:

```csharp
namespace QingJian.App.QuickNotes;

public interface IQuickNoteWindow
{
    event EventHandler<QuickNoteDraft> SaveRequested;

    void ShowWindow();

    void CloseWindow();

    void ShowSaveError(string message);
}
```

Create `src\QingJian.App\QuickNotes\IQuickNoteWindowFactory.cs`:

```csharp
namespace QingJian.App.QuickNotes;

public interface IQuickNoteWindowFactory
{
    IQuickNoteWindow Create();
}
```

- [ ] **Step 4: Add coordinator implementation**

Create `src\QingJian.App\QuickNotes\QuickNoteCoordinator.cs`:

```csharp
using QingJian.App.Services;
using QingJian.App.ViewModels;

namespace QingJian.App.QuickNotes;

public sealed class QuickNoteCoordinator
{
    private readonly INoteService _noteService;
    private readonly MainViewModel _mainViewModel;
    private readonly IQuickNoteWindowFactory _windowFactory;
    private readonly Func<bool> _shouldSyncToMainWindow;

    public QuickNoteCoordinator(
        INoteService noteService,
        MainViewModel mainViewModel,
        IQuickNoteWindowFactory windowFactory,
        Func<bool> shouldSyncToMainWindow)
    {
        _noteService = noteService;
        _mainViewModel = mainViewModel;
        _windowFactory = windowFactory;
        _shouldSyncToMainWindow = shouldSyncToMainWindow;
    }

    public void OpenQuickNote()
    {
        var window = _windowFactory.Create();
        window.SaveRequested += OnSaveRequested;
        window.ShowWindow();
    }

    public async Task<QuickNoteSaveResult> SaveDraftAsync(
        QuickNoteDraft draft,
        bool syncToMainWindow,
        CancellationToken cancellationToken = default)
    {
        if (!QuickNoteTitleGenerator.HasBody(draft.Body))
        {
            return QuickNoteSaveResult.Failure("请输入便签内容。");
        }

        var body = draft.Body.Trim();
        var note = await _noteService.CreateNoteAsync(cancellationToken);
        note.Title = QuickNoteTitleGenerator.CreateTitle(draft.Title, body);
        note.Content = body;
        await _noteService.SaveNoteAsync(note, cancellationToken);

        if (syncToMainWindow)
        {
            _mainViewModel.AddSavedNote(note, select: true);
        }

        return QuickNoteSaveResult.Success(note);
    }

    private async void OnSaveRequested(object? sender, QuickNoteDraft draft)
    {
        if (sender is not IQuickNoteWindow window)
        {
            return;
        }

        try
        {
            var result = await SaveDraftAsync(draft, _shouldSyncToMainWindow());
            if (result.Succeeded)
            {
                window.CloseWindow();
                return;
            }

            window.ShowSaveError(result.ErrorMessage ?? "保存失败。");
        }
        catch (Exception ex)
        {
            window.ShowSaveError($"保存失败：{ex.Message}");
        }
    }
}
```

- [ ] **Step 5: Run coordinator tests**

Run:

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj --filter QuickNoteCoordinatorTests
```

Expected: all `QuickNoteCoordinatorTests` pass.

- [ ] **Step 6: Commit coordinator logic**

Run:

```powershell
git add src\QingJian.App\QuickNotes tests\QingJian.App.Tests\QuickNotes
git commit -m "feat: add quick note save coordinator"
```

Expected: commit succeeds.

---

### Task 4: Borderless Quick-Note Window UI

**Files:**
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\.worktrees\qingjian-mvp\src\QingJian.App\QuickNotes\QuickNoteWindow.xaml`
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\.worktrees\qingjian-mvp\src\QingJian.App\QuickNotes\QuickNoteWindow.xaml.cs`
- Modify: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\.worktrees\qingjian-mvp\src\QingJian.App\Resources\Styles.xaml`

**Interfaces:**
- Consumes:
  - `IQuickNoteWindow`
  - `QuickNoteDraft`
- Produces:
  - `public partial class QuickNoteWindow : Window, IQuickNoteWindow`
  - `public sealed class QuickNoteWindowFactory : IQuickNoteWindowFactory`

- [ ] **Step 1: Add quick-note styles**

Append these resources before `</ResourceDictionary>` in `src\QingJian.App\Resources\Styles.xaml`:

```xml
<SolidColorBrush x:Key="QuickNoteBackgroundBrush" Color="#FFF7C8" />
<SolidColorBrush x:Key="QuickNoteBorderBrush" Color="#E4C968" />
<SolidColorBrush x:Key="QuickNoteErrorBrush" Color="#B42318" />
```

- [ ] **Step 2: Create quick-note window XAML**

Create `src\QingJian.App\QuickNotes\QuickNoteWindow.xaml`:

```xml
<Window x:Class="QingJian.App.QuickNotes.QuickNoteWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Quick Note"
        Width="420"
        Height="320"
        ResizeMode="NoResize"
        WindowStyle="None"
        ShowInTaskbar="False"
        Topmost="True"
        Background="Transparent"
        AllowsTransparency="True"
        KeyDown="Window_OnKeyDown">
    <Border Background="{StaticResource QuickNoteBackgroundBrush}"
            BorderBrush="{StaticResource QuickNoteBorderBrush}"
            BorderThickness="1"
            CornerRadius="8"
            Padding="14">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="28" />
                <RowDefinition Height="Auto" />
                <RowDefinition Height="*" />
                <RowDefinition Height="Auto" />
                <RowDefinition Height="Auto" />
            </Grid.RowDefinitions>

            <Border Grid.Row="0"
                    Background="Transparent"
                    MouseLeftButtonDown="DragHandle_OnMouseLeftButtonDown">
                <TextBlock Text="快速便签"
                           Foreground="{StaticResource MutedTextBrush}"
                           FontSize="12"
                           VerticalAlignment="Center" />
            </Border>

            <TextBox x:Name="TitleTextBox"
                     Grid.Row="1"
                     Margin="0,4,0,10"
                     FontSize="18"
                     FontWeight="SemiBold"
                     AcceptsReturn="False"
                     TextWrapping="NoWrap"
                     VerticalScrollBarVisibility="Disabled" />

            <TextBox x:Name="BodyTextBox"
                     Grid.Row="2"
                     FontSize="14"
                     AcceptsReturn="True"
                     TextWrapping="Wrap"
                     VerticalScrollBarVisibility="Auto"
                     TextChanged="BodyTextBox_OnTextChanged" />

            <TextBlock x:Name="ErrorTextBlock"
                       Grid.Row="3"
                       Margin="0,8,0,0"
                       Foreground="{StaticResource QuickNoteErrorBrush}"
                       FontSize="12"
                       TextWrapping="Wrap"
                       Visibility="Collapsed" />

            <DockPanel Grid.Row="4" Margin="0,12,0,0" LastChildFill="False">
                <TextBlock Text="Ctrl+Enter 保存 · Esc 取消"
                           Foreground="{StaticResource MutedTextBrush}"
                           FontSize="12"
                           VerticalAlignment="Center" />
                <Button x:Name="SaveButton"
                        Content="保存"
                        DockPanel.Dock="Right"
                        Margin="8,0,0,0"
                        Click="SaveButton_OnClick" />
                <Button Content="取消"
                        DockPanel.Dock="Right"
                        Click="CancelButton_OnClick" />
            </DockPanel>
        </Grid>
    </Border>
</Window>
```

- [ ] **Step 3: Create quick-note window code-behind and factory**

Create `src\QingJian.App\QuickNotes\QuickNoteWindow.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Input;

namespace QingJian.App.QuickNotes;

public partial class QuickNoteWindow : Window, IQuickNoteWindow
{
    private bool _isSaved;

    public QuickNoteWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            UpdateSaveButtonState();
            BodyTextBox.Focus();
        };
    }

    public event EventHandler<QuickNoteDraft>? SaveRequested;

    public void ShowWindow()
    {
        Show();
    }

    public void CloseWindow()
    {
        _isSaved = true;
        Close();
    }

    public void ShowSaveError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorTextBlock.Visibility = Visibility.Visible;
    }

    private void DragHandle_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        RequestSave();
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        RequestCancel();
    }

    private void Window_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            RequestSave();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            RequestCancel();
            e.Handled = true;
        }
    }

    private void BodyTextBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdateSaveButtonState();
        ErrorTextBlock.Visibility = Visibility.Collapsed;
    }

    private void RequestSave()
    {
        if (!QuickNoteTitleGenerator.HasBody(BodyTextBox.Text))
        {
            ShowSaveError("请输入便签内容。");
            return;
        }

        SaveRequested?.Invoke(this, new QuickNoteDraft(TitleTextBox.Text, BodyTextBox.Text));
    }

    private void RequestCancel()
    {
        if (_isSaved || !QuickNoteTitleGenerator.HasBody(BodyTextBox.Text))
        {
            Close();
            return;
        }

        var result = MessageBox.Show(
            this,
            "要丢弃这条未保存的便签吗？",
            "QingJian",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            Close();
        }
    }

    private void UpdateSaveButtonState()
    {
        SaveButton.IsEnabled = QuickNoteTitleGenerator.HasBody(BodyTextBox.Text);
    }
}

public sealed class QuickNoteWindowFactory : IQuickNoteWindowFactory
{
    public IQuickNoteWindow Create()
    {
        return new QuickNoteWindow();
    }
}
```

- [ ] **Step 4: Build to verify XAML compiles**

Run:

```powershell
dotnet build
```

Expected: build succeeds.

- [ ] **Step 5: Commit quick-note window UI**

Run:

```powershell
git add src\QingJian.App\QuickNotes\QuickNoteWindow.xaml src\QingJian.App\QuickNotes\QuickNoteWindow.xaml.cs src\QingJian.App\Resources\Styles.xaml
git commit -m "feat: add quick note window"
```

Expected: commit succeeds.

---

### Task 5: Global Hotkey Service

**Files:**
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\.worktrees\qingjian-mvp\src\QingJian.App\Hotkeys\HotkeyDefinition.cs`
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\.worktrees\qingjian-mvp\src\QingJian.App\Hotkeys\GlobalHotkeyService.cs`

**Interfaces:**
- Consumes: WPF `Window`.
- Produces:
  - `public sealed record HotkeyDefinition(int Id, uint Modifiers, uint VirtualKey, string DisplayText);`
  - `public static HotkeyDefinition QuickNoteHotkey { get; }`
  - `public sealed class GlobalHotkeyService : IDisposable`
  - `public event EventHandler? HotkeyPressed;`
  - `public bool Register(Window window, HotkeyDefinition hotkey);`

- [ ] **Step 1: Create hotkey definition**

Create `src\QingJian.App\Hotkeys\HotkeyDefinition.cs`:

```csharp
namespace QingJian.App.Hotkeys;

public sealed record HotkeyDefinition(int Id, uint Modifiers, uint VirtualKey, string DisplayText)
{
    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;

    public static HotkeyDefinition QuickNoteHotkey { get; } = new(
        Id: 0x514A01,
        Modifiers: ModControl | ModAlt,
        VirtualKey: 0x4E,
        DisplayText: "Ctrl + Alt + N");
}
```

- [ ] **Step 2: Create global hotkey service**

Create `src\QingJian.App\Hotkeys\GlobalHotkeyService.cs`:

```csharp
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace QingJian.App.Hotkeys;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private HwndSource? _source;
    private IntPtr _windowHandle;
    private int _registeredId;

    public event EventHandler? HotkeyPressed;

    public bool Register(Window window, HotkeyDefinition hotkey)
    {
        _windowHandle = new WindowInteropHelper(window).Handle;
        if (_windowHandle == IntPtr.Zero)
        {
            return false;
        }

        _source = HwndSource.FromHwnd(_windowHandle);
        _source?.AddHook(WndProc);

        if (!RegisterHotKey(_windowHandle, hotkey.Id, hotkey.Modifiers, hotkey.VirtualKey))
        {
            _source?.RemoveHook(WndProc);
            _source = null;
            _windowHandle = IntPtr.Zero;
            return false;
        }

        _registeredId = hotkey.Id;
        return true;
    }

    public void Dispose()
    {
        if (_windowHandle != IntPtr.Zero && _registeredId != 0)
        {
            UnregisterHotKey(_windowHandle, _registeredId);
        }

        _source?.RemoveHook(WndProc);
        _source = null;
        _windowHandle = IntPtr.Zero;
        _registeredId = 0;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == _registeredId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
```

- [ ] **Step 3: Build to verify hotkey service compiles**

Run:

```powershell
dotnet build
```

Expected: build succeeds.

- [ ] **Step 4: Commit hotkey service**

Run:

```powershell
git add src\QingJian.App\Hotkeys
git commit -m "feat: add global hotkey service"
```

Expected: commit succeeds.

---

### Task 6: Mouse-Near Positioning And App Wiring

**Files:**
- Modify: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\.worktrees\qingjian-mvp\src\QingJian.App\QuickNotes\QuickNoteWindow.xaml.cs`
- Modify: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\.worktrees\qingjian-mvp\src\QingJian.App\App.xaml.cs`

**Interfaces:**
- Consumes:
  - `GlobalHotkeyService.Register(Window, HotkeyDefinition)`
  - `QuickNoteCoordinator.OpenQuickNote()`
- Produces: runtime wiring for global hotkey and quick-note window creation.

- [ ] **Step 1: Add mouse-near positioning to `QuickNoteWindow`**

Add these `using` directives to `src\QingJian.App\QuickNotes\QuickNoteWindow.xaml.cs`:

```csharp
using System.Runtime.InteropServices;
```

Add this call inside the `Loaded` handler before `UpdateSaveButtonState();`:

```csharp
PositionNearMouse();
```

Add these methods and structs inside `QuickNoteWindow` before the final closing brace of the class:

```csharp
private void PositionNearMouse()
{
    if (!TryGetCursorPos(out var cursor))
    {
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        return;
    }

    var workingArea = GetWorkingArea(cursor);
    Left = Math.Min(Math.Max(cursor.X + 12, workingArea.Left), workingArea.Right - Width);
    Top = Math.Min(Math.Max(cursor.Y + 12, workingArea.Top), workingArea.Bottom - Height);
}

[DllImport("user32.dll")]
private static extern bool GetCursorPos(out POINT point);

[DllImport("user32.dll")]
private static extern IntPtr MonitorFromPoint(POINT point, uint flags);

[DllImport("user32.dll", SetLastError = true)]
private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO monitorInfo);

private static bool TryGetCursorPos(out POINT point)
{
    return GetCursorPos(out point);
}

private static Rect GetWorkingArea(POINT cursor)
{
    const uint monitorDefaultToNearest = 0x00000002;
    var monitor = MonitorFromPoint(cursor, monitorDefaultToNearest);
    var monitorInfo = new MONITORINFO
    {
        cbSize = Marshal.SizeOf<MONITORINFO>()
    };

    if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref monitorInfo))
    {
        return new Rect(
            monitorInfo.rcWork.Left,
            monitorInfo.rcWork.Top,
            monitorInfo.rcWork.Right - monitorInfo.rcWork.Left,
            monitorInfo.rcWork.Bottom - monitorInfo.rcWork.Top);
    }

    return SystemParameters.WorkArea;
}

[StructLayout(LayoutKind.Sequential)]
private struct POINT
{
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential)]
private struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

[StructLayout(LayoutKind.Sequential)]
private struct MONITORINFO
{
    public int cbSize;
    public RECT rcMonitor;
    public RECT rcWork;
    public uint dwFlags;
}
```

- [ ] **Step 2: Wire coordinator and hotkey in `App.xaml.cs`**

Replace `src\QingJian.App\App.xaml.cs` with:

```csharp
using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using QingJian.App.Data;
using QingJian.App.Hotkeys;
using QingJian.App.QuickNotes;
using QingJian.App.Services;
using QingJian.App.ViewModels;
using QingJian.App.Views;

namespace QingJian.App;

public partial class App : Application
{
    private GlobalHotkeyService? _hotkeyService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QingJian");
        Directory.CreateDirectory(appDataFolder);

        var databasePath = Path.Combine(appDataFolder, "qingjian.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        var dbContext = new AppDbContext(options);
        var repository = new NoteRepository(dbContext);
        var service = new NoteService(repository);
        var viewModel = new MainViewModel(service);
        var settingsService = new AppSettingsService(appDataFolder);
        var attachmentService = new AttachmentService(Path.Combine(appDataFolder, "attachments"));

        var window = new MainWindow(viewModel, settingsService, attachmentService);
        var coordinator = new QuickNoteCoordinator(
            service,
            viewModel,
            new QuickNoteWindowFactory(),
            () => window.IsVisible && window.WindowState != WindowState.Minimized);

        window.SourceInitialized += (_, _) =>
        {
            _hotkeyService = new GlobalHotkeyService();
            _hotkeyService.HotkeyPressed += (_, _) => coordinator.OpenQuickNote();

            if (!_hotkeyService.Register(window, HotkeyDefinition.QuickNoteHotkey))
            {
                MessageBox.Show(
                    window,
                    $"{HotkeyDefinition.QuickNoteHotkey.DisplayText} 快捷键注册失败，可能已被其他应用占用。",
                    "QingJian",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        };

        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyService?.Dispose();
        base.OnExit(e);
    }
}
```

- [ ] **Step 3: Build app**

Run:

```powershell
dotnet build
```

Expected: build succeeds.

- [ ] **Step 4: Run full tests**

Run:

```powershell
dotnet test
```

Expected: all tests pass.

- [ ] **Step 5: Commit app wiring**

Run:

```powershell
git add src\QingJian.App\App.xaml.cs src\QingJian.App\QuickNotes\QuickNoteWindow.xaml.cs
git commit -m "feat: wire hotkey quick notes"
```

Expected: commit succeeds.

---

### Task 7: Verification And Handoff Docs

**Files:**
- Modify: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\.worktrees\qingjian-mvp\README.md`
- Modify: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\.worktrees\qingjian-mvp\docs\project-status.md`

**Interfaces:**
- Consumes: completed quick-note feature.
- Produces: documented shortcut behavior and verification notes.

- [ ] **Step 1: Update README**

Add this section after the MVP bullets in `README.md`:

```markdown
## Quick Note Shortcut

While QingJian is running, press `Ctrl + Alt + N` to open a quick-note card near the mouse pointer.

- Each shortcut press opens a new independent quick note.
- `Ctrl + Enter` saves.
- `Esc` cancels and asks before discarding non-empty content.
- Closing the main window exits the app, so the shortcut stops working after exit.
```

- [ ] **Step 2: Update project status**

In `docs\project-status.md`, add this item under `Completed Features`:

```markdown
### Hotkey Quick Notes

- `Ctrl + Alt + N` opens a new independent quick-note window while the app is running.
- Quick-note windows are borderless, draggable, and save through the existing local note storage.
- Blank quick-note titles are generated from the first body line.
- Visible main windows receive saved quick notes immediately; minimized windows do not steal focus.
```

Add this known decision under `Known Decisions`:

```markdown
- Quick-note shortcut customization and tray residency are postponed.
```

- [ ] **Step 3: Run full automated verification**

Run:

```powershell
dotnet test
dotnet build
```

Expected:

```text
Passed!
Build succeeded.
```

- [ ] **Step 4: Run manual smoke test**

Run:

```powershell
dotnet run --project src\QingJian.App\QingJian.App.csproj
```

Manual checks:

1. App launches normally.
2. Press `Ctrl + Alt + N`; a borderless quick-note card appears near the mouse pointer.
3. Press `Ctrl + Alt + N` again; a second independent quick-note card appears.
4. Type body text with no title and press `Ctrl + Enter`; the card closes.
5. If the main window is visible, the saved note appears at the top and is selected.
6. Open a quick note, type body text, press `Esc`; discard confirmation appears.
7. Minimize the main window, press `Ctrl + Alt + N`, save a quick note; the main window stays minimized.
8. Close the app.

- [ ] **Step 5: Commit docs and verification notes**

Run:

```powershell
git add README.md docs\project-status.md
git commit -m "docs: describe hotkey quick notes"
```

Expected: commit succeeds.

---

## Spec Coverage Self-Review

- Global `Ctrl + Alt + N` registration is covered by Task 5 and Task 6.
- Multi-instance quick-note windows are covered by Task 3 and Task 4.
- Mouse-near positioning is covered by Task 6.
- Borderless, draggable card UI is covered by Task 4.
- Optional title, required body, save, cancel, `Ctrl + Enter`, and `Esc` are covered by Task 4.
- Discard confirmation is covered by Task 4.
- Title generation from body is covered by Task 1 and used in Task 3.
- Existing note service and SQLite persistence are covered by Task 3.
- Visible main-window sync is covered by Task 2, Task 3, and Task 6.
- Minimized window no-focus behavior is covered by Task 6 and Task 7 manual smoke test.
- Hotkey registration failure warning and continued app use are covered by Task 6.
- Automated verification is included in Tasks 1, 2, 3, 6, and 7.
- Manual OS/WPF behavior verification is included in Task 7.
