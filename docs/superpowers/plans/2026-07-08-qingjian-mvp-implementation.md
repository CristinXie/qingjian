# QingJian MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the v0.1 QingJian Windows sticky-notes MVP with a WPF main window, SQLite persistence, note creation, note editing, soft deletion, auto-save, and restart persistence.

**Architecture:** Use a single WPF project with MVVM-style boundaries. Keep persistence in `Data`, app operations in `Services`, mutable screen state in `ViewModels`, and XAML in `Views` and `Resources`.

**Tech Stack:** WPF, .NET 8 SDK, EF Core SQLite, xUnit, Microsoft.NET.Test.Sdk.

## Global Constraints

- Build a native Windows desktop app using WPF.
- Use .NET 8 as the concrete runtime choice for the MVP.
- Use SQLite for local persistence.
- Use EF Core SQLite for database access and migration-friendly structure.
- Keep the first version as a single WPF project plus one Windows-targeted test project.
- The app opens directly to the usable notes interface, not a landing page.
- The MVP excludes global hotkeys, quick note popup, transparent desktop widget, tray mode, startup on boot, cloud sync, rich text, Markdown preview, tags, search, import, and export.
- Store generated project files under `C:\Users\Cristin\Desktop\VibeCoding\qingjian` unless a later user instruction says otherwise.
- Use `main` for stable work and create `develop` plus `feature/*` branches for implementation tasks.

---

## File Structure

Create this structure:

```text
C:\Users\Cristin\Desktop\VibeCoding\qingjian\
  .gitignore
  Directory.Build.props
  QingJian.sln
  README.md
  docs/
    superpowers/
      specs/
        2026-07-08-qingjian-mvp-design.md
      plans/
        2026-07-08-qingjian-mvp-implementation.md
  src/
    QingJian.App/
      QingJian.App.csproj
      App.xaml
      App.xaml.cs
      Data/
        AppDbContext.cs
        INoteRepository.cs
        NoteRepository.cs
      Models/
        Note.cs
      Resources/
        Styles.xaml
      Services/
        INoteService.cs
        NoteService.cs
      ViewModels/
        AsyncRelayCommand.cs
        MainViewModel.cs
        RelayCommand.cs
        ViewModelBase.cs
      Views/
        MainWindow.xaml
        MainWindow.xaml.cs
  tests/
    QingJian.App.Tests/
      QingJian.App.Tests.csproj
      Data/
        NoteRepositoryTests.cs
      Services/
        NoteServiceTests.cs
      ViewModels/
        MainViewModelTests.cs
```

File responsibilities:

- `.gitignore`: keep build output, local DB files, IDE files, and temporary files out of Git.
- `Directory.Build.props`: centralize nullable, implicit usings, and deterministic build settings.
- `QingJian.App.csproj`: WPF app project and EF Core SQLite dependencies.
- `QingJian.App.Tests.csproj`: xUnit test project targeting `net8.0-windows` and referencing the app project.
- `Models/Note.cs`: EF Core entity and property change notifications for UI binding.
- `Data/AppDbContext.cs`: EF Core DbContext and `Notes` table configuration.
- `Data/INoteRepository.cs`: persistence interface used by services.
- `Data/NoteRepository.cs`: SQLite-backed note CRUD implementation.
- `Services/INoteService.cs`: app operation interface used by ViewModels.
- `Services/NoteService.cs`: create, load, save, and soft-delete behavior.
- `ViewModels/ViewModelBase.cs`: shared `INotifyPropertyChanged` helper.
- `ViewModels/RelayCommand.cs`: synchronous WPF command helper.
- `ViewModels/AsyncRelayCommand.cs`: asynchronous WPF command helper.
- `ViewModels/MainViewModel.cs`: note list, selected note, commands, loading, saving, and delete state.
- `Resources/Styles.xaml`: shared WPF styles for quiet, work-focused UI.
- `Views/MainWindow.xaml`: two-pane UI.
- `Views/MainWindow.xaml.cs`: initializes and flushes the ViewModel on close.

---

### Task 1: Environment, Branches, And Project Bootstrap

**Files:**
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\.gitignore`
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\Directory.Build.props`
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\README.md`
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\QingJian.sln`
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\QingJian.App.csproj`
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\tests\QingJian.App.Tests\QingJian.App.Tests.csproj`

**Interfaces:**
- Consumes: existing Git repository on `main`.
- Produces: a buildable solution with app project `QingJian.App` and test project `QingJian.App.Tests`.

- [ ] **Step 1: Verify .NET SDK prerequisite**

Run:

```powershell
dotnet --version
```

Expected when ready:

```text
8.x.x
```

If the command reports that no SDKs were found, install the .NET 8 SDK from Microsoft, open a new PowerShell session, and run the command again until it prints `8.x.x`.

- [ ] **Step 2: Create integration branch**

Run:

```powershell
git checkout main
git checkout -b develop
git checkout -b feature/project-bootstrap
```

Expected:

```text
Switched to a new branch 'feature/project-bootstrap'
```

- [ ] **Step 3: Create solution and projects**

Run:

```powershell
dotnet new sln -n QingJian
dotnet new wpf -n QingJian.App -o src\QingJian.App -f net8.0-windows
dotnet new xunit -n QingJian.App.Tests -o tests\QingJian.App.Tests -f net8.0
dotnet sln QingJian.sln add src\QingJian.App\QingJian.App.csproj
dotnet sln QingJian.sln add tests\QingJian.App.Tests\QingJian.App.Tests.csproj
dotnet add tests\QingJian.App.Tests\QingJian.App.Tests.csproj reference src\QingJian.App\QingJian.App.csproj
dotnet add src\QingJian.App\QingJian.App.csproj package Microsoft.EntityFrameworkCore.Sqlite
dotnet add tests\QingJian.App.Tests\QingJian.App.Tests.csproj package Microsoft.EntityFrameworkCore.Sqlite
```

Expected:

```text
The template "WPF Application" was created successfully.
The template "xUnit Test Project" was created successfully.
Project `src\QingJian.App\QingJian.App.csproj` added to the solution.
Project `tests\QingJian.App.Tests\QingJian.App.Tests.csproj` added to the solution.
```

- [ ] **Step 4: Replace app project file**

Write `C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\QingJian.App.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.11" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Replace test project file**

Write `C:\Users\Cristin\Desktop\VibeCoding\qingjian\tests\QingJian.App.Tests\QingJian.App.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.11" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\QingJian.App\QingJian.App.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 6: Add repository config files**

Write `C:\Users\Cristin\Desktop\VibeCoding\qingjian\.gitignore`:

```gitignore
bin/
obj/
.vs/
.vscode/
*.user
*.suo
*.db
*.db-shm
*.db-wal
TestResults/
```

Write `C:\Users\Cristin\Desktop\VibeCoding\qingjian\Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <Deterministic>true</Deterministic>
  </PropertyGroup>
</Project>
```

Write `C:\Users\Cristin\Desktop\VibeCoding\qingjian\README.md`:

```markdown
# QingJian

QingJian is a native Windows sticky-notes app built with WPF, .NET 8, and SQLite.

## MVP

- Open directly to a notes interface.
- Create, edit, and soft-delete notes.
- Persist notes locally with SQLite.
- Save note edits automatically.

## Development

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project src\QingJian.App\QingJian.App.csproj
```
```

- [ ] **Step 7: Build and test bootstrap**

Run:

```powershell
dotnet restore
dotnet build
dotnet test
```

Expected:

```text
Build succeeded.
Passed!
```

- [ ] **Step 8: Commit bootstrap**

Run:

```powershell
git add .gitignore Directory.Build.props README.md QingJian.sln src\QingJian.App tests\QingJian.App.Tests
git commit -m "chore: bootstrap wpf solution"
git checkout develop
git merge feature/project-bootstrap
```

Expected:

```text
[feature/project-bootstrap ...] chore: bootstrap wpf solution
```

---

### Task 2: Note Entity And Database Context

**Files:**
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\Models\Note.cs`
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\Data\AppDbContext.cs`
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\tests\QingJian.App.Tests\Data\AppDbContextTests.cs`

**Interfaces:**
- Consumes: `Microsoft.EntityFrameworkCore.Sqlite`.
- Produces: entity `QingJian.App.Models.Note` and DbContext `QingJian.App.Data.AppDbContext` with `DbSet<Note> Notes`.

- [ ] **Step 1: Create feature branch**

Run:

```powershell
git checkout develop
git checkout -b feature/sqlite-storage
```

Expected:

```text
Switched to a new branch 'feature/sqlite-storage'
```

- [ ] **Step 2: Write failing DbContext test**

Write `C:\Users\Cristin\Desktop\VibeCoding\qingjian\tests\QingJian.App.Tests\Data\AppDbContextTests.cs`:

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QingJian.App.Data;
using QingJian.App.Models;

namespace QingJian.App.Tests.Data;

public sealed class AppDbContextTests
{
    [Fact]
    public async Task EnsureCreated_CreatesNotesTableAndPersistsNote()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var db = new AppDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.Notes.Add(new Note
            {
                Id = "note-1",
                Title = "First note",
                Content = "Hello",
                CreatedAt = new DateTime(2026, 7, 8, 10, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 7, 8, 10, 0, 0, DateTimeKind.Utc),
                IsDeleted = false
            });
            await db.SaveChangesAsync();
        }

        await using var readDb = new AppDbContext(options);
        var saved = await readDb.Notes.SingleAsync();

        Assert.Equal("note-1", saved.Id);
        Assert.Equal("First note", saved.Title);
        Assert.Equal("Hello", saved.Content);
        Assert.False(saved.IsDeleted);
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run:

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj --filter AppDbContextTests
```

Expected: build fails because `QingJian.App.Data.AppDbContext` and `QingJian.App.Models.Note` do not exist.

- [ ] **Step 4: Implement Note entity**

Write `C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\Models\Note.cs`:

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace QingJian.App.Models;

public sealed class Note : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private string _content = string.Empty;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

    public string Content
    {
        get => _content;
        set => SetField(ref _content, value);
    }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
```

- [ ] **Step 5: Implement AppDbContext**

Write `C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\Data\AppDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using QingJian.App.Models;

namespace QingJian.App.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Note>(entity =>
        {
            entity.ToTable("Notes");
            entity.HasKey(note => note.Id);

            entity.Property(note => note.Id)
                .HasColumnType("TEXT")
                .IsRequired();

            entity.Property(note => note.Title)
                .HasColumnType("TEXT")
                .IsRequired();

            entity.Property(note => note.Content)
                .HasColumnType("TEXT")
                .IsRequired();

            entity.Property(note => note.CreatedAt)
                .HasColumnType("TEXT")
                .IsRequired();

            entity.Property(note => note.UpdatedAt)
                .HasColumnType("TEXT")
                .IsRequired();

            entity.Property(note => note.IsDeleted)
                .HasColumnType("INTEGER")
                .HasDefaultValue(false)
                .IsRequired();
        });
    }
}
```

- [ ] **Step 6: Run test to verify it passes**

Run:

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj --filter AppDbContextTests
```

Expected:

```text
Passed!
```

- [ ] **Step 7: Commit data model**

Run:

```powershell
git add src\QingJian.App\Models\Note.cs src\QingJian.App\Data\AppDbContext.cs tests\QingJian.App.Tests\Data\AppDbContextTests.cs
git commit -m "feat: add note data model"
```

Expected:

```text
[feature/sqlite-storage ...] feat: add note data model
```

---

### Task 3: Note Repository

**Files:**
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\Data\INoteRepository.cs`
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\Data\NoteRepository.cs`
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\tests\QingJian.App.Tests\Data\NoteRepositoryTests.cs`

**Interfaces:**
- Consumes: `AppDbContext`, `Note`.
- Produces: `INoteRepository` with `InitializeAsync`, `GetActiveNotesAsync`, `AddAsync`, `UpdateAsync`, and `SoftDeleteAsync`.

- [ ] **Step 1: Write failing repository tests**

Write `C:\Users\Cristin\Desktop\VibeCoding\qingjian\tests\QingJian.App.Tests\Data\NoteRepositoryTests.cs`:

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QingJian.App.Data;
using QingJian.App.Models;

namespace QingJian.App.Tests.Data;

public sealed class NoteRepositoryTests
{
    [Fact]
    public async Task GetActiveNotesAsync_ReturnsOnlyNonDeletedNotesOrderedByUpdatedAtDescending()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var repository = CreateRepository(connection);
        await repository.InitializeAsync();

        await repository.AddAsync(CreateNote("old", "Old", new DateTime(2026, 7, 8, 8, 0, 0, DateTimeKind.Utc), false));
        await repository.AddAsync(CreateNote("new", "New", new DateTime(2026, 7, 8, 9, 0, 0, DateTimeKind.Utc), false));
        await repository.AddAsync(CreateNote("deleted", "Deleted", new DateTime(2026, 7, 8, 10, 0, 0, DateTimeKind.Utc), true));

        var notes = await repository.GetActiveNotesAsync();

        Assert.Collection(
            notes,
            note => Assert.Equal("new", note.Id),
            note => Assert.Equal("old", note.Id));
    }

    [Fact]
    public async Task SoftDeleteAsync_HidesNoteFromActiveResults()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var repository = CreateRepository(connection);
        await repository.InitializeAsync();

        await repository.AddAsync(CreateNote("note-1", "Visible", new DateTime(2026, 7, 8, 8, 0, 0, DateTimeKind.Utc), false));

        await repository.SoftDeleteAsync("note-1", new DateTime(2026, 7, 8, 11, 0, 0, DateTimeKind.Utc));

        var notes = await repository.GetActiveNotesAsync();
        Assert.Empty(notes);
    }

    private static NoteRepository CreateRepository(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new NoteRepository(new AppDbContext(options));
    }

    private static Note CreateNote(string id, string title, DateTime updatedAt, bool isDeleted)
    {
        return new Note
        {
            Id = id,
            Title = title,
            Content = string.Empty,
            CreatedAt = updatedAt.AddMinutes(-1),
            UpdatedAt = updatedAt,
            IsDeleted = isDeleted
        };
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj --filter NoteRepositoryTests
```

Expected: build fails because `NoteRepository` and `INoteRepository` do not exist.

- [ ] **Step 3: Implement repository interface**

Write `C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\Data\INoteRepository.cs`:

```csharp
using QingJian.App.Models;

namespace QingJian.App.Data;

public interface INoteRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Note>> GetActiveNotesAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Note note, CancellationToken cancellationToken = default);

    Task UpdateAsync(Note note, CancellationToken cancellationToken = default);

    Task SoftDeleteAsync(string noteId, DateTime deletedAt, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Implement repository**

Write `C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\Data\NoteRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using QingJian.App.Models;

namespace QingJian.App.Data;

public sealed class NoteRepository : INoteRepository
{
    private readonly AppDbContext _dbContext;

    public NoteRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.Database.EnsureCreatedAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Note>> GetActiveNotesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Notes
            .Where(note => !note.IsDeleted)
            .OrderByDescending(note => note.UpdatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Note note, CancellationToken cancellationToken = default)
    {
        _dbContext.Notes.Add(note);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Note note, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.Notes
            .SingleAsync(item => item.Id == note.Id, cancellationToken);

        existing.Title = note.Title;
        existing.Content = note.Content;
        existing.UpdatedAt = note.UpdatedAt;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SoftDeleteAsync(string noteId, DateTime deletedAt, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.Notes
            .SingleAsync(note => note.Id == noteId, cancellationToken);

        existing.IsDeleted = true;
        existing.UpdatedAt = deletedAt;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
```

- [ ] **Step 5: Run repository tests**

Run:

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj --filter NoteRepositoryTests
```

Expected:

```text
Passed!
```

- [ ] **Step 6: Commit repository**

Run:

```powershell
git add src\QingJian.App\Data\INoteRepository.cs src\QingJian.App\Data\NoteRepository.cs tests\QingJian.App.Tests\Data\NoteRepositoryTests.cs
git commit -m "feat: add sqlite note repository"
```

Expected:

```text
[feature/sqlite-storage ...] feat: add sqlite note repository
```

---

### Task 4: Note Service

**Files:**
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\Services\INoteService.cs`
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\Services\NoteService.cs`
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\tests\QingJian.App.Tests\Services\NoteServiceTests.cs`

**Interfaces:**
- Consumes: `INoteRepository`.
- Produces: `INoteService` for ViewModels.

- [ ] **Step 1: Write failing service tests**

Write `C:\Users\Cristin\Desktop\VibeCoding\qingjian\tests\QingJian.App.Tests\Services\NoteServiceTests.cs`:

```csharp
using QingJian.App.Data;
using QingJian.App.Models;
using QingJian.App.Services;

namespace QingJian.App.Tests.Services;

public sealed class NoteServiceTests
{
    [Fact]
    public async Task CreateNoteAsync_CreatesDefaultNoteAndPersistsIt()
    {
        var repository = new InMemoryNoteRepository();
        var now = new DateTime(2026, 7, 8, 12, 0, 0, DateTimeKind.Utc);
        var service = new NoteService(repository, () => now);

        var note = await service.CreateNoteAsync();

        Assert.Equal("未命名便签", note.Title);
        Assert.Equal(string.Empty, note.Content);
        Assert.Equal(now, note.CreatedAt);
        Assert.Equal(now, note.UpdatedAt);
        Assert.False(note.IsDeleted);
        Assert.Single(repository.Notes);
    }

    [Fact]
    public async Task SaveNoteAsync_NormalizesBlankTitleAndUpdatesTimestamp()
    {
        var repository = new InMemoryNoteRepository();
        var createdAt = new DateTime(2026, 7, 8, 12, 0, 0, DateTimeKind.Utc);
        var savedAt = new DateTime(2026, 7, 8, 12, 5, 0, DateTimeKind.Utc);
        var service = new NoteService(repository, () => savedAt);
        var note = new Note
        {
            Id = "note-1",
            Title = "   ",
            Content = "Body",
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            IsDeleted = false
        };

        await service.SaveNoteAsync(note);

        Assert.Equal("未命名便签", note.Title);
        Assert.Equal(savedAt, note.UpdatedAt);
        Assert.Single(repository.UpdatedNotes);
    }

    private sealed class InMemoryNoteRepository : INoteRepository
    {
        public List<Note> Notes { get; } = new();

        public List<Note> UpdatedNotes { get; } = new();

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Note>> GetActiveNotesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Note>>(Notes.Where(note => !note.IsDeleted).ToList());
        }

        public Task AddAsync(Note note, CancellationToken cancellationToken = default)
        {
            Notes.Add(note);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Note note, CancellationToken cancellationToken = default)
        {
            UpdatedNotes.Add(note);
            return Task.CompletedTask;
        }

        public Task SoftDeleteAsync(string noteId, DateTime deletedAt, CancellationToken cancellationToken = default)
        {
            var note = Notes.Single(item => item.Id == noteId);
            note.IsDeleted = true;
            note.UpdatedAt = deletedAt;
            return Task.CompletedTask;
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj --filter NoteServiceTests
```

Expected: build fails because `INoteService` and `NoteService` do not exist.

- [ ] **Step 3: Implement service interface**

Write `C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\Services\INoteService.cs`:

```csharp
using QingJian.App.Models;

namespace QingJian.App.Services;

public interface INoteService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Note>> GetActiveNotesAsync(CancellationToken cancellationToken = default);

    Task<Note> CreateNoteAsync(CancellationToken cancellationToken = default);

    Task SaveNoteAsync(Note note, CancellationToken cancellationToken = default);

    Task DeleteNoteAsync(Note note, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Implement service**

Write `C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\Services\NoteService.cs`:

```csharp
using QingJian.App.Data;
using QingJian.App.Models;

namespace QingJian.App.Services;

public sealed class NoteService : INoteService
{
    private const string DefaultTitle = "未命名便签";
    private readonly INoteRepository _noteRepository;
    private readonly Func<DateTime> _utcNow;

    public NoteService(INoteRepository noteRepository)
        : this(noteRepository, () => DateTime.UtcNow)
    {
    }

    public NoteService(INoteRepository noteRepository, Func<DateTime> utcNow)
    {
        _noteRepository = noteRepository;
        _utcNow = utcNow;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return _noteRepository.InitializeAsync(cancellationToken);
    }

    public Task<IReadOnlyList<Note>> GetActiveNotesAsync(CancellationToken cancellationToken = default)
    {
        return _noteRepository.GetActiveNotesAsync(cancellationToken);
    }

    public async Task<Note> CreateNoteAsync(CancellationToken cancellationToken = default)
    {
        var now = _utcNow();
        var note = new Note
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = DefaultTitle,
            Content = string.Empty,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false
        };

        await _noteRepository.AddAsync(note, cancellationToken);
        return note;
    }

    public Task SaveNoteAsync(Note note, CancellationToken cancellationToken = default)
    {
        note.Title = string.IsNullOrWhiteSpace(note.Title) ? DefaultTitle : note.Title.Trim();
        note.UpdatedAt = _utcNow();
        return _noteRepository.UpdateAsync(note, cancellationToken);
    }

    public Task DeleteNoteAsync(Note note, CancellationToken cancellationToken = default)
    {
        return _noteRepository.SoftDeleteAsync(note.Id, _utcNow(), cancellationToken);
    }
}
```

- [ ] **Step 5: Run service tests**

Run:

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj --filter NoteServiceTests
```

Expected:

```text
Passed!
```

- [ ] **Step 6: Run all data and service tests**

Run:

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj
```

Expected:

```text
Passed!
```

- [ ] **Step 7: Commit service**

Run:

```powershell
git add src\QingJian.App\Services tests\QingJian.App.Tests\Services
git commit -m "feat: add note service"
```

Expected:

```text
[feature/sqlite-storage ...] feat: add note service
```

- [ ] **Step 8: Merge storage branch**

Run:

```powershell
git checkout develop
git merge feature/sqlite-storage
```

Expected:

```text
Updating ...
```

---

### Task 5: Main ViewModel And Commands

**Files:**
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\ViewModels\ViewModelBase.cs`
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\ViewModels\RelayCommand.cs`
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\ViewModels\AsyncRelayCommand.cs`
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\ViewModels\MainViewModel.cs`
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\tests\QingJian.App.Tests\ViewModels\MainViewModelTests.cs`

**Interfaces:**
- Consumes: `INoteService`, `Note`.
- Produces: `MainViewModel` with `Notes`, `SelectedNote`, `IsEmpty`, `NewNoteCommand`, `DeleteSelectedNoteCommand`, `LoadAsync`, and `SaveSelectedNoteNowAsync`.

- [ ] **Step 1: Create feature branch**

Run:

```powershell
git checkout develop
git checkout -b feature/note-list-editor
```

Expected:

```text
Switched to a new branch 'feature/note-list-editor'
```

- [ ] **Step 2: Write failing ViewModel tests**

Write `C:\Users\Cristin\Desktop\VibeCoding\qingjian\tests\QingJian.App.Tests\ViewModels\MainViewModelTests.cs`:

```csharp
using QingJian.App.Models;
using QingJian.App.Services;
using QingJian.App.ViewModels;

namespace QingJian.App.Tests.ViewModels;

public sealed class MainViewModelTests
{
    [Fact]
    public async Task LoadAsync_LoadsNotesAndSelectsMostRecent()
    {
        var newest = CreateNote("new", "New");
        var older = CreateNote("old", "Old");
        var service = new InMemoryNoteService(newest, older);
        var viewModel = new MainViewModel(service);

        await viewModel.LoadAsync();

        Assert.Equal(2, viewModel.Notes.Count);
        Assert.Equal("new", viewModel.SelectedNote?.Id);
        Assert.False(viewModel.IsEmpty);
    }

    [Fact]
    public async Task NewNoteCommand_CreatesNoteAndSelectsIt()
    {
        var service = new InMemoryNoteService();
        var viewModel = new MainViewModel(service);

        await viewModel.NewNoteAsync();

        Assert.Single(viewModel.Notes);
        Assert.Equal(viewModel.Notes[0], viewModel.SelectedNote);
        Assert.False(viewModel.IsEmpty);
    }

    [Fact]
    public async Task DeleteSelectedNoteCommand_RemovesSelectedNoteAndSelectsNext()
    {
        var first = CreateNote("first", "First");
        var second = CreateNote("second", "Second");
        var service = new InMemoryNoteService(first, second);
        var viewModel = new MainViewModel(service);
        await viewModel.LoadAsync();

        await viewModel.DeleteSelectedNoteAsync();

        Assert.Single(viewModel.Notes);
        Assert.Equal("second", viewModel.SelectedNote?.Id);
        Assert.Contains("first", service.DeletedIds);
    }

    private static Note CreateNote(string id, string title)
    {
        return new Note
        {
            Id = id,
            Title = title,
            Content = string.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }

    private sealed class InMemoryNoteService : INoteService
    {
        private readonly List<Note> _notes;

        public InMemoryNoteService(params Note[] notes)
        {
            _notes = notes.ToList();
        }

        public List<string> DeletedIds { get; } = new();

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Note>> GetActiveNotesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Note>>(_notes.ToList());
        }

        public Task<Note> CreateNoteAsync(CancellationToken cancellationToken = default)
        {
            var note = CreateNote($"note-{_notes.Count + 1}", "未命名便签");
            _notes.Insert(0, note);
            return Task.FromResult(note);
        }

        public Task SaveNoteAsync(Note note, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteNoteAsync(Note note, CancellationToken cancellationToken = default)
        {
            DeletedIds.Add(note.Id);
            _notes.RemoveAll(item => item.Id == note.Id);
            return Task.CompletedTask;
        }
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run:

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj --filter MainViewModelTests
```

Expected: build fails because `MainViewModel` and command helpers do not exist.

- [ ] **Step 4: Implement ViewModelBase**

Write `C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\ViewModels\ViewModelBase.cs`:

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace QingJian.App.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
```

- [ ] **Step 5: Implement command helpers**

Write `C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\ViewModels\RelayCommand.cs`:

```csharp
using System.Windows.Input;

namespace QingJian.App.ViewModels;

public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke() ?? true;
    }

    public void Execute(object? parameter)
    {
        _execute();
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
```

Write `C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\ViewModels\AsyncRelayCommand.cs`:

```csharp
using System.Windows.Input;

namespace QingJian.App.ViewModels;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return !_isExecuting && (_canExecute?.Invoke() ?? true);
    }

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        try
        {
            _isExecuting = true;
            RaiseCanExecuteChanged();
            await _execute();
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
```

- [ ] **Step 6: Implement MainViewModel**

Write `C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\ViewModels\MainViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using QingJian.App.Models;
using QingJian.App.Services;

namespace QingJian.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly INoteService _noteService;
    private Note? _selectedNote;
    private bool _isBusy;

    public MainViewModel(INoteService noteService)
    {
        _noteService = noteService;
        NewNoteCommand = new AsyncRelayCommand(NewNoteAsync);
        DeleteSelectedNoteCommand = new AsyncRelayCommand(DeleteSelectedNoteAsync, () => SelectedNote is not null);
    }

    public ObservableCollection<Note> Notes { get; } = new();

    public Note? SelectedNote
    {
        get => _selectedNote;
        set
        {
            if (SetField(ref _selectedNote, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
                DeleteSelectedNoteCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsEmpty => Notes.Count == 0;

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    public AsyncRelayCommand NewNoteCommand { get; }

    public AsyncRelayCommand DeleteSelectedNoteCommand { get; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;

        try
        {
            await _noteService.InitializeAsync(cancellationToken);
            var notes = await _noteService.GetActiveNotesAsync(cancellationToken);

            Notes.Clear();
            foreach (var note in notes)
            {
                Notes.Add(note);
            }

            SelectedNote = Notes.FirstOrDefault();
            OnPropertyChanged(nameof(IsEmpty));
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task NewNoteAsync()
    {
        var note = await _noteService.CreateNoteAsync();
        Notes.Insert(0, note);
        SelectedNote = note;
        OnPropertyChanged(nameof(IsEmpty));
    }

    public async Task DeleteSelectedNoteAsync()
    {
        if (SelectedNote is null)
        {
            return;
        }

        var index = Notes.IndexOf(SelectedNote);
        var note = SelectedNote;

        await _noteService.DeleteNoteAsync(note);
        Notes.Remove(note);

        if (Notes.Count == 0)
        {
            SelectedNote = null;
        }
        else
        {
            SelectedNote = Notes[Math.Min(index, Notes.Count - 1)];
        }

        OnPropertyChanged(nameof(IsEmpty));
    }

    public Task SaveSelectedNoteNowAsync(CancellationToken cancellationToken = default)
    {
        return SelectedNote is null
            ? Task.CompletedTask
            : _noteService.SaveNoteAsync(SelectedNote, cancellationToken);
    }
}
```

- [ ] **Step 7: Run ViewModel tests**

Run:

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj --filter MainViewModelTests
```

Expected:

```text
Passed!
```

- [ ] **Step 8: Commit ViewModel**

Run:

```powershell
git add src\QingJian.App\ViewModels tests\QingJian.App.Tests\ViewModels
git commit -m "feat: add main note view model"
```

Expected:

```text
[feature/note-list-editor ...] feat: add main note view model
```

---

### Task 6: WPF App Wiring And Two-Pane UI

**Files:**
- Modify: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\App.xaml`
- Modify: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\App.xaml.cs`
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\Resources\Styles.xaml`
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\Views\MainWindow.xaml`
- Create: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\Views\MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `AppDbContext`, `NoteRepository`, `NoteService`, `MainViewModel`.
- Produces: a launchable WPF app that opens to the note interface.

- [ ] **Step 1: Update App.xaml**

Write `C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\App.xaml`:

```xml
<Application x:Class="QingJian.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="Resources/Styles.xaml" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

- [ ] **Step 2: Update App.xaml.cs**

Write `C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\App.xaml.cs`:

```csharp
using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using QingJian.App.Data;
using QingJian.App.Services;
using QingJian.App.ViewModels;
using QingJian.App.Views;

namespace QingJian.App;

public partial class App : Application
{
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

        var window = new MainWindow(viewModel);
        window.Show();
    }
}
```

- [ ] **Step 3: Add shared styles**

Write `C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\Resources\Styles.xaml`:

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Color x:Key="AppBackgroundColor">#F5F3EF</Color>
    <Color x:Key="PanelBackgroundColor">#FFFFFF</Color>
    <Color x:Key="BorderColor">#DDD7CF</Color>
    <Color x:Key="TextColor">#25211D</Color>
    <Color x:Key="MutedTextColor">#7C756C</Color>
    <Color x:Key="AccentColor">#2F6F73</Color>

    <SolidColorBrush x:Key="AppBackgroundBrush" Color="{StaticResource AppBackgroundColor}" />
    <SolidColorBrush x:Key="PanelBackgroundBrush" Color="{StaticResource PanelBackgroundColor}" />
    <SolidColorBrush x:Key="BorderBrush" Color="{StaticResource BorderColor}" />
    <SolidColorBrush x:Key="TextBrush" Color="{StaticResource TextColor}" />
    <SolidColorBrush x:Key="MutedTextBrush" Color="{StaticResource MutedTextColor}" />
    <SolidColorBrush x:Key="AccentBrush" Color="{StaticResource AccentColor}" />

    <Style TargetType="Button">
        <Setter Property="MinHeight" Value="34" />
        <Setter Property="Padding" Value="12,6" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}" />
        <Setter Property="Background" Value="#FFFFFF" />
        <Setter Property="Foreground" Value="{StaticResource TextBrush}" />
        <Setter Property="Cursor" Value="Hand" />
    </Style>

    <Style TargetType="TextBox">
        <Setter Property="BorderThickness" Value="0" />
        <Setter Property="Background" Value="Transparent" />
        <Setter Property="Foreground" Value="{StaticResource TextBrush}" />
        <Setter Property="AcceptsReturn" Value="True" />
        <Setter Property="TextWrapping" Value="Wrap" />
        <Setter Property="VerticalScrollBarVisibility" Value="Auto" />
    </Style>
</ResourceDictionary>
```

- [ ] **Step 4: Add MainWindow XAML**

Write `C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\Views\MainWindow.xaml`:

```xml
<Window x:Class="QingJian.App.Views.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:models="clr-namespace:QingJian.App.Models"
        Title="QingJian"
        Width="1040"
        Height="680"
        MinWidth="760"
        MinHeight="480"
        Background="{StaticResource AppBackgroundBrush}">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="300" />
            <ColumnDefinition Width="*" />
        </Grid.ColumnDefinitions>

        <Border Grid.Column="0"
                Background="{StaticResource PanelBackgroundBrush}"
                BorderBrush="{StaticResource BorderBrush}"
                BorderThickness="0,0,1,0">
            <DockPanel Margin="16">
                <Button DockPanel.Dock="Top"
                        Content="新建便签"
                        Command="{Binding NewNoteCommand}"
                        Margin="0,0,0,14" />

                <ListBox ItemsSource="{Binding Notes}"
                         SelectedItem="{Binding SelectedNote, Mode=TwoWay}"
                         BorderThickness="0"
                         Background="Transparent">
                    <ListBox.ItemTemplate>
                        <DataTemplate DataType="{x:Type models:Note}">
                            <StackPanel Margin="4,8">
                                <TextBlock Text="{Binding Title}"
                                           Foreground="{StaticResource TextBrush}"
                                           FontWeight="SemiBold"
                                           TextTrimming="CharacterEllipsis" />
                                <TextBlock Text="{Binding Content}"
                                           Foreground="{StaticResource MutedTextBrush}"
                                           FontSize="12"
                                           Margin="0,4,0,0"
                                           MaxHeight="34"
                                           TextWrapping="Wrap" />
                            </StackPanel>
                        </DataTemplate>
                    </ListBox.ItemTemplate>
                </ListBox>
            </DockPanel>
        </Border>

        <Grid Grid.Column="1" Margin="28">
            <Grid.Style>
                <Style TargetType="Grid">
                    <Setter Property="Visibility" Value="Visible" />
                    <Style.Triggers>
                        <DataTrigger Binding="{Binding IsEmpty}" Value="True">
                            <Setter Property="Visibility" Value="Collapsed" />
                        </DataTrigger>
                    </Style.Triggers>
                </Style>
            </Grid.Style>

            <Grid.RowDefinitions>
                <RowDefinition Height="Auto" />
                <RowDefinition Height="Auto" />
                <RowDefinition Height="*" />
            </Grid.RowDefinitions>

            <DockPanel Grid.Row="0" LastChildFill="False">
                <Button Content="删除"
                        Command="{Binding DeleteSelectedNoteCommand}"
                        DockPanel.Dock="Right" />
            </DockPanel>

            <TextBox Grid.Row="1"
                     Text="{Binding SelectedNote.Title, UpdateSourceTrigger=PropertyChanged}"
                     FontSize="26"
                     FontWeight="SemiBold"
                     Margin="0,10,0,18"
                     AcceptsReturn="False" />

            <TextBox Grid.Row="2"
                     Text="{Binding SelectedNote.Content, UpdateSourceTrigger=PropertyChanged}"
                     FontSize="15"
                     LineHeight="24" />
        </Grid>

        <StackPanel Grid.Column="1"
                    HorizontalAlignment="Center"
                    VerticalAlignment="Center">
            <StackPanel.Style>
                <Style TargetType="StackPanel">
                    <Setter Property="Visibility" Value="Collapsed" />
                    <Style.Triggers>
                        <DataTrigger Binding="{Binding IsEmpty}" Value="True">
                            <Setter Property="Visibility" Value="Visible" />
                        </DataTrigger>
                    </Style.Triggers>
                </Style>
            </StackPanel.Style>
            <TextBlock Text="还没有便签"
                       Foreground="{StaticResource TextBrush}"
                       FontSize="22"
                       FontWeight="SemiBold"
                       HorizontalAlignment="Center" />
            <TextBlock Text="创建第一条便签，轻轻放下脑子里的东西。"
                       Foreground="{StaticResource MutedTextBrush}"
                       Margin="0,8,0,18"
                       HorizontalAlignment="Center" />
            <Button Content="新建便签"
                    Command="{Binding NewNoteCommand}"
                    HorizontalAlignment="Center" />
        </StackPanel>
    </Grid>
</Window>
```

- [ ] **Step 5: Add MainWindow code-behind**

Write `C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\Views\MainWindow.xaml.cs`:

```csharp
using System.Windows;
using QingJian.App.ViewModels;

namespace QingJian.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadAsync();
    }

    private async void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        await _viewModel.SaveSelectedNoteNowAsync();
    }
}
```

- [ ] **Step 6: Remove generated root MainWindow files if present**

Run:

```powershell
if (Test-Path src\QingJian.App\MainWindow.xaml) { Remove-Item src\QingJian.App\MainWindow.xaml }
if (Test-Path src\QingJian.App\MainWindow.xaml.cs) { Remove-Item src\QingJian.App\MainWindow.xaml.cs }
```

Expected: command completes without output.

- [ ] **Step 7: Build and launch**

Run:

```powershell
dotnet build
dotnet run --project src\QingJian.App\QingJian.App.csproj
```

Expected:

```text
Build succeeded.
```

Manual check: the app opens a two-pane window with a left note list, right editor area, and visible `新建便签` action.

- [ ] **Step 8: Commit UI shell**

Run:

```powershell
git add src\QingJian.App\App.xaml src\QingJian.App\App.xaml.cs src\QingJian.App\Resources src\QingJian.App\Views
git commit -m "feat: add wpf notes shell"
```

Expected:

```text
[feature/note-list-editor ...] feat: add wpf notes shell
```

---

### Task 7: Auto-Save, Selection Persistence, And MVP Polish

**Files:**
- Modify: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\ViewModels\MainViewModel.cs`
- Modify: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\tests\QingJian.App.Tests\ViewModels\MainViewModelTests.cs`
- Modify: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\README.md`

**Interfaces:**
- Consumes: `INoteService.SaveNoteAsync`.
- Produces: automatic save scheduling using `CancellationTokenSource` debounce and explicit close-time flush.

- [ ] **Step 1: Add failing save test**

Append this test method inside `MainViewModelTests`:

```csharp
[Fact]
public async Task SaveSelectedNoteNowAsync_SavesSelectedNote()
{
    var note = CreateNote("note-1", "Original");
    var service = new InMemoryNoteService(note);
    var viewModel = new MainViewModel(service);
    await viewModel.LoadAsync();

    viewModel.SelectedNote!.Title = "Changed";
    await viewModel.SaveSelectedNoteNowAsync();

    Assert.Single(service.SavedIds);
    Assert.Equal("note-1", service.SavedIds[0]);
}
```

Add this property to `InMemoryNoteService`:

```csharp
public List<string> SavedIds { get; } = new();
```

Replace `SaveNoteAsync` in `InMemoryNoteService`:

```csharp
public Task SaveNoteAsync(Note note, CancellationToken cancellationToken = default)
{
    SavedIds.Add(note.Id);
    return Task.CompletedTask;
}
```

- [ ] **Step 2: Run save test**

Run:

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj --filter SaveSelectedNoteNowAsync_SavesSelectedNote
```

Expected:

```text
Passed!
```

This passes with the Task 5 implementation and proves the explicit save path. Continue with debounce wiring.

- [ ] **Step 3: Update MainViewModel with debounce auto-save**

Replace `C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\ViewModels\MainViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using System.ComponentModel;
using QingJian.App.Models;
using QingJian.App.Services;

namespace QingJian.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly INoteService _noteService;
    private readonly TimeSpan _autoSaveDelay;
    private CancellationTokenSource? _autoSaveCancellation;
    private Note? _selectedNote;
    private bool _isBusy;
    private bool _isLoadingSelection;

    public MainViewModel(INoteService noteService)
        : this(noteService, TimeSpan.FromMilliseconds(700))
    {
    }

    public MainViewModel(INoteService noteService, TimeSpan autoSaveDelay)
    {
        _noteService = noteService;
        _autoSaveDelay = autoSaveDelay;
        NewNoteCommand = new AsyncRelayCommand(NewNoteAsync);
        DeleteSelectedNoteCommand = new AsyncRelayCommand(DeleteSelectedNoteAsync, () => SelectedNote is not null);
    }

    public ObservableCollection<Note> Notes { get; } = new();

    public Note? SelectedNote
    {
        get => _selectedNote;
        set
        {
            if (_selectedNote == value)
            {
                return;
            }

            if (_selectedNote is not null)
            {
                _selectedNote.PropertyChanged -= OnSelectedNotePropertyChanged;
            }

            _selectedNote = value;

            if (_selectedNote is not null)
            {
                _selectedNote.PropertyChanged += OnSelectedNotePropertyChanged;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(IsEmpty));
            DeleteSelectedNoteCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsEmpty => Notes.Count == 0;

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    public AsyncRelayCommand NewNoteCommand { get; }

    public AsyncRelayCommand DeleteSelectedNoteCommand { get; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        _isLoadingSelection = true;

        try
        {
            await _noteService.InitializeAsync(cancellationToken);
            var notes = await _noteService.GetActiveNotesAsync(cancellationToken);

            Notes.Clear();
            foreach (var note in notes)
            {
                Notes.Add(note);
            }

            SelectedNote = Notes.FirstOrDefault();
            OnPropertyChanged(nameof(IsEmpty));
        }
        finally
        {
            _isLoadingSelection = false;
            IsBusy = false;
        }
    }

    public async Task NewNoteAsync()
    {
        var note = await _noteService.CreateNoteAsync();
        Notes.Insert(0, note);
        SelectedNote = note;
        OnPropertyChanged(nameof(IsEmpty));
    }

    public async Task DeleteSelectedNoteAsync()
    {
        if (SelectedNote is null)
        {
            return;
        }

        var index = Notes.IndexOf(SelectedNote);
        var note = SelectedNote;

        await _noteService.DeleteNoteAsync(note);
        Notes.Remove(note);

        SelectedNote = Notes.Count == 0
            ? null
            : Notes[Math.Min(index, Notes.Count - 1)];

        OnPropertyChanged(nameof(IsEmpty));
    }

    public async Task SaveSelectedNoteNowAsync(CancellationToken cancellationToken = default)
    {
        _autoSaveCancellation?.Cancel();

        if (SelectedNote is null)
        {
            return;
        }

        await _noteService.SaveNoteAsync(SelectedNote, cancellationToken);
        MoveSelectedNoteToTop();
    }

    private void OnSelectedNotePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isLoadingSelection || e.PropertyName is not (nameof(Note.Title) or nameof(Note.Content)))
        {
            return;
        }

        ScheduleAutoSave();
    }

    private void ScheduleAutoSave()
    {
        _autoSaveCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _autoSaveCancellation = cancellation;

        _ = SaveAfterDelayAsync(cancellation.Token);
    }

    private async Task SaveAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_autoSaveDelay, cancellationToken);
            if (SelectedNote is not null)
            {
                await _noteService.SaveNoteAsync(SelectedNote, cancellationToken);
                MoveSelectedNoteToTop();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void MoveSelectedNoteToTop()
    {
        if (SelectedNote is null)
        {
            return;
        }

        var index = Notes.IndexOf(SelectedNote);
        if (index > 0)
        {
            Notes.Move(index, 0);
        }
    }
}
```

- [ ] **Step 4: Add debounce test**

Append this test method inside `MainViewModelTests`:

```csharp
[Fact]
public async Task EditingSelectedNote_AutoSavesAfterDelay()
{
    var note = CreateNote("note-1", "Original");
    var service = new InMemoryNoteService(note);
    var viewModel = new MainViewModel(service, TimeSpan.FromMilliseconds(10));
    await viewModel.LoadAsync();

    viewModel.SelectedNote!.Content = "Changed";
    await Task.Delay(80);

    Assert.Contains("note-1", service.SavedIds);
}
```

- [ ] **Step 5: Run ViewModel tests**

Run:

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj --filter MainViewModelTests
```

Expected:

```text
Passed!
```

- [ ] **Step 6: Update README with MVP usage**

Append to `C:\Users\Cristin\Desktop\VibeCoding\qingjian\README.md`:

```markdown

## Data Location

The MVP stores notes in:

```text
%LOCALAPPDATA%\QingJian\qingjian.db
```

## Branch Flow

Feature work starts from `develop`, uses `feature/*` branches, and merges back to `develop` after tests pass.
```

- [ ] **Step 7: Run full verification**

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

- [ ] **Step 8: Commit auto-save and polish**

Run:

```powershell
git add src\QingJian.App\ViewModels\MainViewModel.cs tests\QingJian.App.Tests\ViewModels\MainViewModelTests.cs README.md
git commit -m "feat: add note auto save"
git checkout develop
git merge feature/note-list-editor
```

Expected:

```text
[feature/note-list-editor ...] feat: add note auto save
```

---

### Task 8: MVP Integration And Release Tag

**Files:**
- Modify: `C:\Users\Cristin\Desktop\VibeCoding\qingjian\README.md`

**Interfaces:**
- Consumes: all previous tasks merged into `develop`.
- Produces: verified `main` branch and tag `v0.1.0`.

- [ ] **Step 1: Verify develop branch**

Run:

```powershell
git checkout develop
dotnet test
dotnet build
```

Expected:

```text
Passed!
Build succeeded.
```

- [ ] **Step 2: Manual smoke test**

Run:

```powershell
dotnet run --project src\QingJian.App\QingJian.App.csproj
```

Manual checks:

1. App launches on Windows.
2. Main window opens directly to the notes interface.
3. Empty state appears when there are no notes.
4. `新建便签` creates a note and selects it.
5. Editing title and content updates the visible list item.
6. Waiting at least one second after editing saves the note.
7. Closing and reopening the app restores the note.
8. Deleting a note hides it from the list.

- [ ] **Step 3: Update README acceptance summary**

Append to `C:\Users\Cristin\Desktop\VibeCoding\qingjian\README.md`:

```markdown

## v0.1.0 Acceptance

- Launches as a WPF Windows app.
- Opens directly to the notes interface.
- Creates notes.
- Edits title and content.
- Saves edits automatically.
- Soft-deletes notes.
- Restores notes after restart.
```

- [ ] **Step 4: Commit README release note**

Run:

```powershell
git add README.md
git commit -m "docs: add mvp acceptance notes"
```

Expected:

```text
[develop ...] docs: add mvp acceptance notes
```

- [ ] **Step 5: Merge to main and tag**

Run:

```powershell
git checkout main
git merge develop
git tag v0.1.0
```

Expected:

```text
Updating ...
```

- [ ] **Step 6: Final verification**

Run:

```powershell
git status --short --branch
git log --oneline -5
dotnet test
dotnet build
```

Expected:

```text
## main
Passed!
Build succeeded.
```

---

## Spec Coverage Self-Review

- Product goal is covered by Tasks 1, 6, 7, and 8.
- WPF main window is covered by Task 6.
- Left-side note list is covered by Tasks 5 and 6.
- Right-side note editor is covered by Task 6.
- Creating notes is covered by Tasks 4, 5, 6, and 8.
- Editing note title and content is covered by Tasks 6, 7, and 8.
- Soft deletion is covered by Tasks 3, 4, 5, and 8.
- Automatic saving is covered by Task 7.
- SQLite persistence is covered by Tasks 2, 3, 4, and 8.
- Created and updated timestamps are covered by Tasks 2, 3, and 4.
- Empty state is covered by Tasks 5, 6, and 8.
- Restart persistence is covered by Tasks 3, 6, and the manual smoke test in Task 8.
- Git branch flow is covered by Tasks 1, 3, 5, 7, and 8.

No extra v0.2 features are included in this plan.

