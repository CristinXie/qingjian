# Desktop Todo Widget Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a desktop-layer todo widget with independent todo storage, three shared-data preset modes, in-widget editing, and persisted widget preferences.

**Architecture:** Add a todo persistence/service layer beside the existing note layer, then build a WPF widget surface in `TodoWidgets/` that consumes the service. The widget owns display modes, hover/click popover state, desktop-layer attachment, and preference persistence; the main window only exposes a show/hide entry.

**Tech Stack:** WPF, .NET 8, EF Core SQLite, xUnit, existing JSON `AppSettingsService`, Win32 interop for desktop-layer attachment.

## Global Constraints

- Work on branch `feature/desktop-todo-widget`.
- Todos are stored independently from notes.
- The app remains a WPF application on .NET 8.
- Add a new `TodoItems` table; do not store todos in `Notes.Content`.
- The desktop widget appears by default on first launch after this feature is installed.
- After the user changes widget visibility, QingJian restores that persisted visibility on later launches.
- The main window provides a show/hide control for the widget.
- Closing the main window still exits QingJian and closes the widget.
- The widget first tries to attach to the Windows desktop layer.
- If desktop-layer attachment fails, degrade to a lowest-level ordinary WPF window and keep the app usable.
- First version uses a fixed widget size; do not implement drag-resizing or size presets.
- Locking the widget prevents dragging only; interaction remains enabled.
- Three preset modes are required: 8-day grid, today list, and monthly calendar.
- The 8-day grid shows 2 rows and 4 columns, starting from yesterday.
- Monthly calendar supports previous month, next month, and return to current month.
- A shared edit popover supports add, edit, complete, delete, and time form changes.
- Todo time forms are no time, single time, and start/end time.
- First version does not support reminders, notifications, repeating todos, or cross-day todos.
- Completed todos remain visible and move to the bottom of their day.
- Persist widget visibility, mode, position, opacity, lock state, and calendar month.
- Run `dotnet test` and `dotnet build` sequentially before completion.
- Do not run test and build in parallel because WPF build outputs can be locked.

---

## File Structure

Create:

- `src/QingJian.App/Models/TodoItem.cs`  
  EF Core entity for structured todo data.

- `src/QingJian.App/Data/ITodoRepository.cs`  
  Persistence interface used by `TodoService`.

- `src/QingJian.App/Data/TodoRepository.cs`  
  SQLite-backed todo CRUD and date-range queries.

- `src/QingJian.App/Services/ITodoService.cs`  
  App-level todo operations consumed by widget view models.

- `src/QingJian.App/Services/TodoService.cs`  
  Validation, creation, editing, completion, soft delete, and sorting.

- `src/QingJian.App/TodoWidgets/TodoWidgetMode.cs`  
  Enum for `EightDay`, `Today`, and `Calendar`.

- `src/QingJian.App/TodoWidgets/TodoTimeKind.cs`  
  Enum for `None`, `Single`, and `Range`.

- `src/QingJian.App/TodoWidgets/TodoDraft.cs`  
  UI draft record for popover editing.

- `src/QingJian.App/TodoWidgets/TodoWidgetPreferences.cs`  
  Widget preference record embedded in app settings.

- `src/QingJian.App/TodoWidgets/TodoWidgetViewModel.cs`  
  Widget mode, date ranges, todo lists, popover state, and commands.

- `src/QingJian.App/TodoWidgets/DesktopLayerService.cs`  
  Win32 desktop-layer attachment and graceful fallback.

- `src/QingJian.App/TodoWidgets/TodoWidgetWindow.xaml`  
  Fixed-size desktop widget window.

- `src/QingJian.App/TodoWidgets/TodoWidgetWindow.xaml.cs`  
  Window drag, hover opacity, close/hide, and desktop-layer attach.

- `src/QingJian.App/TodoWidgets/TodoWidgetCoordinator.cs`  
  App-level coordinator that creates, shows, hides, and saves widget preferences.

- `tests/QingJian.App.Tests/Data/TodoRepositoryTests.cs`

- `tests/QingJian.App.Tests/Services/TodoServiceTests.cs`

- `tests/QingJian.App.Tests/Services/AppSettingsServiceTodoWidgetTests.cs`

- `tests/QingJian.App.Tests/TodoWidgets/TodoWidgetViewModelTests.cs`

Modify:

- `src/QingJian.App/Data/AppDbContext.cs`  
  Add `DbSet<TodoItem>` and model mapping.

- `src/QingJian.App/Services/AppSettingsService.cs`  
  Add backwards-compatible `TodoWidgetPreferences` support.

- `src/QingJian.App/App.xaml.cs`  
  Wire todo repository/service/coordinator.

- `src/QingJian.App/Views/MainWindow.xaml`  
  Add show/hide widget control.

- `src/QingJian.App/Views/MainWindow.xaml.cs`  
  Raise show/hide request through coordinator.

- `src/QingJian.App/Resources/Styles.xaml`  
  Add widget brushes and compact control styles.

- `README.md` and `docs/project-status.md`  
  Document the feature after implementation verification.

---

### Task 1: Todo Data Model And Repository

**Files:**
- Create: `src/QingJian.App/Models/TodoItem.cs`
- Create: `src/QingJian.App/Data/ITodoRepository.cs`
- Create: `src/QingJian.App/Data/TodoRepository.cs`
- Modify: `src/QingJian.App/Data/AppDbContext.cs`
- Create: `tests/QingJian.App.Tests/Data/TodoRepositoryTests.cs`

**Interfaces:**
- Produces: `public sealed class TodoItem`
- Produces: `public interface ITodoRepository`
- Produces: `Task InitializeAsync(CancellationToken cancellationToken = default)`
- Produces: `Task<IReadOnlyList<TodoItem>> GetActiveTodosAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)`
- Produces: `Task AddAsync(TodoItem todo, CancellationToken cancellationToken = default)`
- Produces: `Task UpdateAsync(TodoItem todo, CancellationToken cancellationToken = default)`
- Produces: `Task SoftDeleteAsync(string todoId, DateTime deletedAt, CancellationToken cancellationToken = default)`
- Consumes: EF Core SQLite and existing `AppDbContext`.

- [ ] **Step 1: Write failing repository tests**

Create `tests/QingJian.App.Tests/Data/TodoRepositoryTests.cs`:

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QingJian.App.Data;
using QingJian.App.Models;
using Xunit;

namespace QingJian.App.Tests.Data;

public sealed class TodoRepositoryTests
{
    [Fact]
    public async Task InitializeAsync_CreatesTodoItemsTableAndPersistsTodo()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var repository = CreateRepository(connection);

        await repository.InitializeAsync();
        await repository.AddAsync(CreateTodo("todo-1", new DateOnly(2026, 7, 13), "Write plan"));

        var todos = await repository.GetActiveTodosAsync(
            new DateOnly(2026, 7, 13),
            new DateOnly(2026, 7, 13));

        Assert.Single(todos);
        Assert.Equal("todo-1", todos[0].Id);
        Assert.Equal("Write plan", todos[0].Text);
        Assert.Equal(new DateOnly(2026, 7, 13), todos[0].Date);
    }

    [Fact]
    public async Task GetActiveTodosAsync_ReturnsDateRangeAndExcludesDeleted()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var repository = CreateRepository(connection);
        await repository.InitializeAsync();

        await repository.AddAsync(CreateTodo("before", new DateOnly(2026, 7, 12), "Before"));
        await repository.AddAsync(CreateTodo("inside", new DateOnly(2026, 7, 13), "Inside"));
        await repository.AddAsync(CreateTodo("after", new DateOnly(2026, 7, 20), "After"));
        await repository.SoftDeleteAsync("inside", new DateTime(2026, 7, 13, 9, 0, 0, DateTimeKind.Utc));
        await repository.AddAsync(CreateTodo("visible", new DateOnly(2026, 7, 14), "Visible"));

        var todos = await repository.GetActiveTodosAsync(
            new DateOnly(2026, 7, 13),
            new DateOnly(2026, 7, 14));

        Assert.Collection(todos, todo => Assert.Equal("visible", todo.Id));
    }

    [Fact]
    public async Task UpdateAsync_PersistsTodoFields()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var repository = CreateRepository(connection);
        await repository.InitializeAsync();

        var todo = CreateTodo("todo-1", new DateOnly(2026, 7, 13), "Original");
        await repository.AddAsync(todo);

        todo.Text = "Changed";
        todo.StartTime = new TimeOnly(10, 30);
        todo.EndTime = new TimeOnly(11, 15);
        todo.IsCompleted = true;
        todo.CompletedAt = new DateTime(2026, 7, 13, 11, 15, 0, DateTimeKind.Utc);
        await repository.UpdateAsync(todo);

        var todos = await repository.GetActiveTodosAsync(
            new DateOnly(2026, 7, 13),
            new DateOnly(2026, 7, 13));

        Assert.Equal("Changed", todos[0].Text);
        Assert.Equal(new TimeOnly(10, 30), todos[0].StartTime);
        Assert.Equal(new TimeOnly(11, 15), todos[0].EndTime);
        Assert.True(todos[0].IsCompleted);
        Assert.NotNull(todos[0].CompletedAt);
    }

    private static TodoRepository CreateRepository(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new TodoRepository(new AppDbContext(options));
    }

    private static TodoItem CreateTodo(string id, DateOnly date, string text)
    {
        return new TodoItem
        {
            Id = id,
            Date = date,
            Text = text,
            CreatedAt = new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc),
            IsCompleted = false,
            IsDeleted = false
        };
    }
}
```

- [ ] **Step 2: Run tests and verify they fail**

Run:

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj --filter TodoRepositoryTests
```

Expected: build fails because `TodoItem`, `TodoRepository`, and `ITodoRepository` do not exist.

- [ ] **Step 3: Add `TodoItem`**

Create `src/QingJian.App/Models/TodoItem.cs`:

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace QingJian.App.Models;

public sealed class TodoItem : INotifyPropertyChanged
{
    private string _text = string.Empty;
    private bool _isCompleted;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public DateOnly Date { get; set; }

    public string Text
    {
        get => _text;
        set => SetField(ref _text, value);
    }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public bool IsCompleted
    {
        get => _isCompleted;
        set => SetField(ref _isCompleted, value);
    }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

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

- [ ] **Step 4: Add `DbSet<TodoItem>` and model mapping**

Modify `src/QingJian.App/Data/AppDbContext.cs`:

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

    public DbSet<TodoItem> TodoItems => Set<TodoItem>();

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

        modelBuilder.Entity<TodoItem>(entity =>
        {
            entity.ToTable("TodoItems");
            entity.HasKey(todo => todo.Id);

            entity.Property(todo => todo.Id)
                .HasColumnType("TEXT")
                .IsRequired();

            entity.Property(todo => todo.Date)
                .HasColumnType("TEXT")
                .IsRequired();

            entity.Property(todo => todo.Text)
                .HasColumnType("TEXT")
                .IsRequired();

            entity.Property(todo => todo.StartTime)
                .HasColumnType("TEXT");

            entity.Property(todo => todo.EndTime)
                .HasColumnType("TEXT");

            entity.Property(todo => todo.IsCompleted)
                .HasColumnType("INTEGER")
                .HasDefaultValue(false)
                .IsRequired();

            entity.Property(todo => todo.CreatedAt)
                .HasColumnType("TEXT")
                .IsRequired();

            entity.Property(todo => todo.UpdatedAt)
                .HasColumnType("TEXT")
                .IsRequired();

            entity.Property(todo => todo.CompletedAt)
                .HasColumnType("TEXT");

            entity.Property(todo => todo.IsDeleted)
                .HasColumnType("INTEGER")
                .HasDefaultValue(false)
                .IsRequired();

            entity.HasIndex(todo => new { todo.Date, todo.IsDeleted });
        });
    }
}
```

- [ ] **Step 5: Add repository interface and implementation**

Create `src/QingJian.App/Data/ITodoRepository.cs`:

```csharp
using QingJian.App.Models;

namespace QingJian.App.Data;

public interface ITodoRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TodoItem>> GetActiveTodosAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);

    Task AddAsync(TodoItem todo, CancellationToken cancellationToken = default);

    Task UpdateAsync(TodoItem todo, CancellationToken cancellationToken = default);

    Task SoftDeleteAsync(string todoId, DateTime deletedAt, CancellationToken cancellationToken = default);
}
```

Create `src/QingJian.App/Data/TodoRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using QingJian.App.Models;

namespace QingJian.App.Data;

public sealed class TodoRepository : ITodoRepository
{
    private readonly AppDbContext _dbContext;

    public TodoRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.Database.EnsureCreatedAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TodoItem>> GetActiveTodosAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.TodoItems
            .Where(todo => !todo.IsDeleted && todo.Date >= fromDate && todo.Date <= toDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(TodoItem todo, CancellationToken cancellationToken = default)
    {
        _dbContext.TodoItems.Add(todo);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(TodoItem todo, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.TodoItems
            .SingleAsync(item => item.Id == todo.Id, cancellationToken);

        existing.Date = todo.Date;
        existing.Text = todo.Text;
        existing.StartTime = todo.StartTime;
        existing.EndTime = todo.EndTime;
        existing.IsCompleted = todo.IsCompleted;
        existing.UpdatedAt = todo.UpdatedAt;
        existing.CompletedAt = todo.CompletedAt;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SoftDeleteAsync(string todoId, DateTime deletedAt, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.TodoItems
            .SingleAsync(todo => todo.Id == todoId, cancellationToken);

        existing.IsDeleted = true;
        existing.UpdatedAt = deletedAt;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
```

- [ ] **Step 6: Run repository tests**

Run:

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj --filter TodoRepositoryTests
```

Expected: all `TodoRepositoryTests` pass.

- [ ] **Step 7: Commit data model and repository**

Run:

```powershell
git add src\QingJian.App\Models\TodoItem.cs src\QingJian.App\Data\AppDbContext.cs src\QingJian.App\Data\ITodoRepository.cs src\QingJian.App\Data\TodoRepository.cs tests\QingJian.App.Tests\Data\TodoRepositoryTests.cs
git commit -m "feat: add todo persistence"
```

---

### Task 2: Todo Service Validation And Sorting

**Files:**
- Create: `src/QingJian.App/Services/ITodoService.cs`
- Create: `src/QingJian.App/Services/TodoService.cs`
- Create: `src/QingJian.App/TodoWidgets/TodoTimeKind.cs`
- Create: `src/QingJian.App/TodoWidgets/TodoDraft.cs`
- Create: `tests/QingJian.App.Tests/Services/TodoServiceTests.cs`

**Interfaces:**
- Consumes: `ITodoRepository`, `TodoItem`.
- Produces: `public enum TodoTimeKind { None, Single, Range }`
- Produces: `public sealed record TodoDraft(DateOnly Date, string Text, TodoTimeKind TimeKind, TimeOnly? StartTime, TimeOnly? EndTime)`
- Produces: `public interface ITodoService`
- Produces: `Task<IReadOnlyList<TodoItem>> GetTodosAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)`
- Produces: `Task<TodoItem> CreateTodoAsync(TodoDraft draft, CancellationToken cancellationToken = default)`
- Produces: `Task UpdateTodoAsync(TodoItem todo, TodoDraft draft, CancellationToken cancellationToken = default)`
- Produces: `Task SetCompletedAsync(TodoItem todo, bool isCompleted, CancellationToken cancellationToken = default)`
- Produces: `Task DeleteTodoAsync(TodoItem todo, CancellationToken cancellationToken = default)`
- Produces: `IReadOnlyList<TodoItem> SortTodos(IEnumerable<TodoItem> todos)`

- [ ] **Step 1: Write failing service tests**

Create `tests/QingJian.App.Tests/Services/TodoServiceTests.cs`:

```csharp
using QingJian.App.Data;
using QingJian.App.Models;
using QingJian.App.Services;
using QingJian.App.TodoWidgets;
using Xunit;

namespace QingJian.App.Tests.Services;

public sealed class TodoServiceTests
{
    [Fact]
    public async Task CreateTodoAsync_RejectsEmptyText()
    {
        var service = new TodoService(new InMemoryTodoRepository());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateTodoAsync(new TodoDraft(
                new DateOnly(2026, 7, 13),
                "   ",
                TodoTimeKind.None,
                null,
                null)));
    }

    [Fact]
    public async Task CreateTodoAsync_CreatesNoTimeSingleTimeAndRangeTodos()
    {
        var repository = new InMemoryTodoRepository();
        var service = new TodoService(repository);

        var none = await service.CreateTodoAsync(new TodoDraft(new DateOnly(2026, 7, 13), "No time", TodoTimeKind.None, null, null));
        var single = await service.CreateTodoAsync(new TodoDraft(new DateOnly(2026, 7, 13), "Single", TodoTimeKind.Single, new TimeOnly(9, 30), null));
        var range = await service.CreateTodoAsync(new TodoDraft(new DateOnly(2026, 7, 13), "Range", TodoTimeKind.Range, new TimeOnly(10, 0), new TimeOnly(11, 0)));

        Assert.Null(none.StartTime);
        Assert.Null(none.EndTime);
        Assert.Equal(new TimeOnly(9, 30), single.StartTime);
        Assert.Null(single.EndTime);
        Assert.Equal(new TimeOnly(10, 0), range.StartTime);
        Assert.Equal(new TimeOnly(11, 0), range.EndTime);
        Assert.Equal(3, repository.Todos.Count);
    }

    [Fact]
    public async Task CreateTodoAsync_RejectsInvalidTimeRange()
    {
        var service = new TodoService(new InMemoryTodoRepository());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateTodoAsync(new TodoDraft(
                new DateOnly(2026, 7, 13),
                "Invalid",
                TodoTimeKind.Range,
                new TimeOnly(12, 0),
                new TimeOnly(11, 0))));
    }

    [Fact]
    public async Task SetCompletedAsync_MarksCompletedAndReopens()
    {
        var repository = new InMemoryTodoRepository();
        var service = new TodoService(repository);
        var todo = await service.CreateTodoAsync(new TodoDraft(new DateOnly(2026, 7, 13), "Task", TodoTimeKind.None, null, null));

        await service.SetCompletedAsync(todo, true);
        Assert.True(todo.IsCompleted);
        Assert.NotNull(todo.CompletedAt);

        await service.SetCompletedAsync(todo, false);
        Assert.False(todo.IsCompleted);
        Assert.Null(todo.CompletedAt);
    }

    [Fact]
    public void SortTodos_PutsIncompleteTimedTodosFirstAndCompletedTodosLast()
    {
        var service = new TodoService(new InMemoryTodoRepository());
        var date = new DateOnly(2026, 7, 13);
        var todos = new[]
        {
            CreateTodo("done", date, "Done", true, null, null, new DateTime(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc)),
            CreateTodo("untimed", date, "Untimed", false, null, null, null),
            CreateTodo("early", date, "Early", false, new TimeOnly(9, 0), null, null),
            CreateTodo("late", date, "Late", false, new TimeOnly(15, 0), null, null)
        };

        var sorted = service.SortTodos(todos);

        Assert.Collection(
            sorted,
            todo => Assert.Equal("early", todo.Id),
            todo => Assert.Equal("late", todo.Id),
            todo => Assert.Equal("untimed", todo.Id),
            todo => Assert.Equal("done", todo.Id));
    }

    private static TodoItem CreateTodo(
        string id,
        DateOnly date,
        string text,
        bool completed,
        TimeOnly? start,
        TimeOnly? end,
        DateTime? completedAt)
    {
        return new TodoItem
        {
            Id = id,
            Date = date,
            Text = text,
            IsCompleted = completed,
            StartTime = start,
            EndTime = end,
            CreatedAt = new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc),
            CompletedAt = completedAt
        };
    }

    private sealed class InMemoryTodoRepository : ITodoRepository
    {
        public List<TodoItem> Todos { get; } = new();

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<TodoItem>> GetActiveTodosAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<TodoItem>>(
                Todos.Where(todo => !todo.IsDeleted && todo.Date >= fromDate && todo.Date <= toDate).ToList());
        }

        public Task AddAsync(TodoItem todo, CancellationToken cancellationToken = default)
        {
            Todos.Add(todo);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TodoItem todo, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SoftDeleteAsync(string todoId, DateTime deletedAt, CancellationToken cancellationToken = default)
        {
            var todo = Todos.Single(item => item.Id == todoId);
            todo.IsDeleted = true;
            todo.UpdatedAt = deletedAt;
            return Task.CompletedTask;
        }
    }
}
```

- [ ] **Step 2: Run tests and verify they fail**

Run:

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj --filter TodoServiceTests
```

Expected: build fails because `TodoService`, `ITodoService`, `TodoDraft`, and `TodoTimeKind` do not exist.

- [ ] **Step 3: Add draft and time kind**

Create `src/QingJian.App/TodoWidgets/TodoTimeKind.cs`:

```csharp
namespace QingJian.App.TodoWidgets;

public enum TodoTimeKind
{
    None,
    Single,
    Range
}
```

Create `src/QingJian.App/TodoWidgets/TodoDraft.cs`:

```csharp
namespace QingJian.App.TodoWidgets;

public sealed record TodoDraft(
    DateOnly Date,
    string Text,
    TodoTimeKind TimeKind,
    TimeOnly? StartTime,
    TimeOnly? EndTime);
```

- [ ] **Step 4: Add service interface and implementation**

Create `src/QingJian.App/Services/ITodoService.cs`:

```csharp
using QingJian.App.Models;
using QingJian.App.TodoWidgets;

namespace QingJian.App.Services;

public interface ITodoService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TodoItem>> GetTodosAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);

    Task<TodoItem> CreateTodoAsync(TodoDraft draft, CancellationToken cancellationToken = default);

    Task UpdateTodoAsync(TodoItem todo, TodoDraft draft, CancellationToken cancellationToken = default);

    Task SetCompletedAsync(TodoItem todo, bool isCompleted, CancellationToken cancellationToken = default);

    Task DeleteTodoAsync(TodoItem todo, CancellationToken cancellationToken = default);

    IReadOnlyList<TodoItem> SortTodos(IEnumerable<TodoItem> todos);
}
```

Create `src/QingJian.App/Services/TodoService.cs`:

```csharp
using QingJian.App.Data;
using QingJian.App.Models;
using QingJian.App.TodoWidgets;

namespace QingJian.App.Services;

public sealed class TodoService : ITodoService
{
    private readonly ITodoRepository _repository;

    public TodoService(ITodoRepository repository)
    {
        _repository = repository;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return _repository.InitializeAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TodoItem>> GetTodosAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        var todos = await _repository.GetActiveTodosAsync(fromDate, toDate, cancellationToken);
        return SortTodos(todos);
    }

    public async Task<TodoItem> CreateTodoAsync(TodoDraft draft, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var normalized = NormalizeDraft(draft);
        var todo = new TodoItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Date = normalized.Date,
            Text = normalized.Text.Trim(),
            StartTime = normalized.StartTime,
            EndTime = normalized.EndTime,
            CreatedAt = now,
            UpdatedAt = now,
            IsCompleted = false,
            IsDeleted = false
        };

        await _repository.AddAsync(todo, cancellationToken);
        return todo;
    }

    public async Task UpdateTodoAsync(TodoItem todo, TodoDraft draft, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeDraft(draft);
        todo.Date = normalized.Date;
        todo.Text = normalized.Text.Trim();
        todo.StartTime = normalized.StartTime;
        todo.EndTime = normalized.EndTime;
        todo.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(todo, cancellationToken);
    }

    public async Task SetCompletedAsync(TodoItem todo, bool isCompleted, CancellationToken cancellationToken = default)
    {
        if (todo.IsCompleted == isCompleted)
        {
            return;
        }

        var now = DateTime.UtcNow;
        todo.IsCompleted = isCompleted;
        todo.CompletedAt = isCompleted ? now : null;
        todo.UpdatedAt = now;

        await _repository.UpdateAsync(todo, cancellationToken);
    }

    public Task DeleteTodoAsync(TodoItem todo, CancellationToken cancellationToken = default)
    {
        return _repository.SoftDeleteAsync(todo.Id, DateTime.UtcNow, cancellationToken);
    }

    public IReadOnlyList<TodoItem> SortTodos(IEnumerable<TodoItem> todos)
    {
        return todos
            .OrderBy(todo => todo.IsCompleted)
            .ThenBy(todo => todo.IsCompleted ? 1 : todo.StartTime.HasValue ? 0 : 1)
            .ThenBy(todo => todo.IsCompleted ? null : todo.StartTime)
            .ThenBy(todo => todo.IsCompleted ? todo.CompletedAt : todo.CreatedAt)
            .ThenBy(todo => todo.UpdatedAt)
            .ToList();
    }

    private static TodoDraft NormalizeDraft(TodoDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.Text))
        {
            throw new ArgumentException("Todo text cannot be empty.", nameof(draft));
        }

        return draft.TimeKind switch
        {
            TodoTimeKind.None => draft with { StartTime = null, EndTime = null },
            TodoTimeKind.Single when draft.StartTime is not null => draft with { EndTime = null },
            TodoTimeKind.Range when draft.StartTime is not null &&
                                    draft.EndTime is not null &&
                                    draft.EndTime > draft.StartTime => draft,
            TodoTimeKind.Single => throw new ArgumentException("Single-time todos require a start time.", nameof(draft)),
            TodoTimeKind.Range => throw new ArgumentException("Time ranges require an end time later than the start time.", nameof(draft)),
            _ => throw new ArgumentOutOfRangeException(nameof(draft), "Unsupported todo time kind.")
        };
    }
}
```

- [ ] **Step 5: Run service tests**

Run:

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj --filter TodoServiceTests
```

Expected: all `TodoServiceTests` pass.

- [ ] **Step 6: Run repository tests again**

Run:

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj --filter "TodoRepositoryTests|TodoServiceTests"
```

Expected: todo repository and service tests pass.

- [ ] **Step 7: Commit service layer**

Run:

```powershell
git add src\QingJian.App\Services\ITodoService.cs src\QingJian.App\Services\TodoService.cs src\QingJian.App\TodoWidgets\TodoTimeKind.cs src\QingJian.App\TodoWidgets\TodoDraft.cs tests\QingJian.App.Tests\Services\TodoServiceTests.cs
git commit -m "feat: add todo service"
```

---

### Task 3: Widget Preferences In App Settings

**Files:**
- Create: `src/QingJian.App/TodoWidgets/TodoWidgetMode.cs`
- Create: `src/QingJian.App/TodoWidgets/TodoWidgetPreferences.cs`
- Modify: `src/QingJian.App/Services/AppSettingsService.cs`
- Create: `tests/QingJian.App.Tests/Services/AppSettingsServiceTodoWidgetTests.cs`

**Interfaces:**
- Produces: `public enum TodoWidgetMode { EightDay, Today, Calendar }`
- Produces: `public sealed record TodoWidgetPreferences(...)`
- Modifies: `public sealed record AppSettings(string EditorMode, TodoWidgetPreferences TodoWidget)`
- Preserves: loading older settings with only `editorMode` still returns defaults for widget preferences.

- [ ] **Step 1: Write failing settings tests**

Create `tests/QingJian.App.Tests/Services/AppSettingsServiceTodoWidgetTests.cs`:

```csharp
using System.Text.Json;
using QingJian.App.Services;
using QingJian.App.TodoWidgets;
using Xunit;

namespace QingJian.App.Tests.Services;

public sealed class AppSettingsServiceTodoWidgetTests
{
    [Fact]
    public async Task LoadAsync_ReturnsDefaultTodoWidgetPreferences_WhenSettingsFileDoesNotExist()
    {
        var folder = CreateTempFolder();
        var service = new AppSettingsService(folder);

        var settings = await service.LoadAsync();

        Assert.Equal(TodoWidgetPreferences.Default, settings.TodoWidget);
    }

    [Fact]
    public async Task LoadAsync_LoadsOldEditorOnlySettingsWithTodoDefaults()
    {
        var folder = CreateTempFolder();
        await File.WriteAllTextAsync(
            Path.Combine(folder, "settings.json"),
            """{"editorMode":"markdown"}""");
        var service = new AppSettingsService(folder);

        var settings = await service.LoadAsync();

        Assert.Equal(AppSettings.MarkdownEditorMode, settings.EditorMode);
        Assert.Equal(TodoWidgetPreferences.Default, settings.TodoWidget);
    }

    [Fact]
    public async Task SaveAsync_PersistsTodoWidgetPreferences()
    {
        var folder = CreateTempFolder();
        var service = new AppSettingsService(folder);
        var preferences = TodoWidgetPreferences.Default with
        {
            IsVisible = false,
            Mode = TodoWidgetMode.Calendar,
            Left = 120,
            Top = 80,
            IsLocked = true,
            Opacity = 0.62,
            CalendarYear = 2026,
            CalendarMonth = 8
        };

        await service.SaveAsync(new AppSettings(AppSettings.MarkdownEditorMode, preferences));
        var loaded = await service.LoadAsync();

        Assert.Equal(preferences, loaded.TodoWidget);
    }

    [Fact]
    public async Task LoadAsync_NormalizesInvalidTodoWidgetPreferences()
    {
        var folder = CreateTempFolder();
        await File.WriteAllTextAsync(
            Path.Combine(folder, "settings.json"),
            JsonSerializer.Serialize(new
            {
                editorMode = "wysiwyg",
                todoWidget = new
                {
                    isVisible = true,
                    mode = (TodoWidgetMode)99,
                    left = -99999.0,
                    top = -99999.0,
                    opacity = 2.0,
                    isLocked = false,
                    calendarYear = 2026,
                    calendarMonth = 15
                }
            }));
        var service = new AppSettingsService(folder);

        var settings = await service.LoadAsync();

        Assert.Equal(TodoWidgetMode.EightDay, settings.TodoWidget.Mode);
        Assert.Equal(0.75, settings.TodoWidget.Opacity);
        Assert.Equal(DateTime.Today.Year, settings.TodoWidget.CalendarYear);
        Assert.Equal(DateTime.Today.Month, settings.TodoWidget.CalendarMonth);
    }

    private static string CreateTempFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "qingjian-settings-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }
}
```

- [ ] **Step 2: Run tests and verify they fail**

Run:

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj --filter AppSettingsServiceTodoWidgetTests
```

Expected: build fails because `TodoWidgetPreferences`, `TodoWidgetMode`, and the new `AppSettings` constructor do not exist.

- [ ] **Step 3: Add widget preference types**

Create `src/QingJian.App/TodoWidgets/TodoWidgetMode.cs`:

```csharp
namespace QingJian.App.TodoWidgets;

public enum TodoWidgetMode
{
    EightDay,
    Today,
    Calendar
}
```

Create `src/QingJian.App/TodoWidgets/TodoWidgetPreferences.cs`:

```csharp
namespace QingJian.App.TodoWidgets;

public sealed record TodoWidgetPreferences(
    bool IsVisible,
    TodoWidgetMode Mode,
    double Left,
    double Top,
    double Opacity,
    bool IsLocked,
    int CalendarYear,
    int CalendarMonth)
{
    public static TodoWidgetPreferences Default { get; } = new(
        IsVisible: true,
        Mode: TodoWidgetMode.EightDay,
        Left: 80,
        Top: 80,
        Opacity: 0.75,
        IsLocked: false,
        CalendarYear: DateTime.Today.Year,
        CalendarMonth: DateTime.Today.Month);

    public TodoWidgetPreferences Normalize()
    {
        var mode = Enum.IsDefined(typeof(TodoWidgetMode), Mode)
            ? Mode
            : TodoWidgetMode.EightDay;
        var opacity = double.IsFinite(Opacity)
            ? Math.Clamp(Opacity, 0.35, 0.95)
            : Default.Opacity;
        var left = double.IsFinite(Left) && Left > -10000 ? Left : Default.Left;
        var top = double.IsFinite(Top) && Top > -10000 ? Top : Default.Top;
        var calendarYear = CalendarYear is >= 1900 and <= 9999 ? CalendarYear : DateTime.Today.Year;
        var calendarMonth = CalendarMonth is >= 1 and <= 12 ? CalendarMonth : DateTime.Today.Month;

        return this with
        {
            Mode = mode,
            Left = left,
            Top = top,
            Opacity = opacity,
            CalendarYear = calendarYear,
            CalendarMonth = calendarMonth
        };
    }
}
```

- [ ] **Step 4: Extend `AppSettingsService`**

Modify `src/QingJian.App/Services/AppSettingsService.cs`:

```csharp
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using QingJian.App.TodoWidgets;

namespace QingJian.App.Services;

public sealed record AppSettings(string EditorMode, TodoWidgetPreferences TodoWidget)
{
    public const string DefaultEditorMode = "wysiwyg";
    public const string MarkdownEditorMode = "markdown";

    public static AppSettings Default { get; } = new(DefaultEditorMode, TodoWidgetPreferences.Default);

    public AppSettings(string EditorMode)
        : this(EditorMode, TodoWidgetPreferences.Default)
    {
    }

    public AppSettings Normalize()
    {
        var editorMode = EditorMode is DefaultEditorMode or MarkdownEditorMode
            ? EditorMode
            : DefaultEditorMode;

        return this with
        {
            EditorMode = editorMode,
            TodoWidget = (TodoWidget ?? TodoWidgetPreferences.Default).Normalize()
        };
    }
}

public sealed class AppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _settingsPath;

    public AppSettingsService(string appDataFolder)
    {
        _settingsPath = Path.Combine(appDataFolder, "settings.json");
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsPath))
        {
            return AppSettings.Default;
        }

        try
        {
            await using var stream = File.OpenRead(_settingsPath);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken);
            return (settings ?? AppSettings.Default).Normalize();
        }
        catch (JsonException)
        {
            return AppSettings.Default;
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        var normalizedSettings = settings.Normalize();

        await using var stream = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(stream, normalizedSettings, JsonOptions, cancellationToken);
    }
}
```

- [ ] **Step 5: Run settings tests**

Run:

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj --filter "AppSettingsServiceTests|AppSettingsServiceTodoWidgetTests"
```

Expected: existing editor-mode settings tests and new widget settings tests pass.

- [ ] **Step 6: Commit settings extension**

Run:

```powershell
git add src\QingJian.App\TodoWidgets\TodoWidgetMode.cs src\QingJian.App\TodoWidgets\TodoWidgetPreferences.cs src\QingJian.App\Services\AppSettingsService.cs tests\QingJian.App.Tests\Services\AppSettingsServiceTodoWidgetTests.cs
git commit -m "feat: persist todo widget preferences"
```

---

### Task 4: Todo Widget ViewModel Logic

**Files:**
- Create: `src/QingJian.App/TodoWidgets/TodoWidgetViewModel.cs`
- Create: `tests/QingJian.App.Tests/TodoWidgets/TodoWidgetViewModelTests.cs`

**Interfaces:**
- Consumes: `ITodoService`, `TodoWidgetPreferences`, `TodoWidgetMode`, `TodoDraft`.
- Produces: `TodoWidgetViewModel`
- Produces: `ObservableCollection<TodoItem> VisibleTodos`
- Produces: `IReadOnlyList<DateOnly> EightDayDates`
- Produces: `IReadOnlyList<DateOnly> CalendarDates`
- Produces: `DateOnly? PinnedDate`
- Produces: `DateOnly? HoverDate`
- Produces: `Task LoadAsync(CancellationToken cancellationToken = default)`
- Produces: `Task CreateTodoAsync(TodoDraft draft, CancellationToken cancellationToken = default)`
- Produces: `Task UpdateTodoAsync(TodoItem todo, TodoDraft draft, CancellationToken cancellationToken = default)`
- Produces: `Task SetCompletedAsync(TodoItem todo, bool completed, CancellationToken cancellationToken = default)`
- Produces: `Task DeleteTodoAsync(TodoItem todo, CancellationToken cancellationToken = default)`
- Produces: `void SetMode(TodoWidgetMode mode)`, `void ShowPreviousMonth()`, `void ShowNextMonth()`, `void ReturnToCurrentMonth()`

- [ ] **Step 1: Write failing ViewModel tests**

Create `tests/QingJian.App.Tests/TodoWidgets/TodoWidgetViewModelTests.cs`:

```csharp
using QingJian.App.Models;
using QingJian.App.Services;
using QingJian.App.TodoWidgets;
using Xunit;

namespace QingJian.App.Tests.TodoWidgets;

public sealed class TodoWidgetViewModelTests
{
    [Fact]
    public async Task LoadAsync_InEightDayMode_LoadsYesterdayThroughSixDaysAfterToday()
    {
        var today = new DateOnly(2026, 7, 13);
        var service = new InMemoryTodoService();
        var viewModel = new TodoWidgetViewModel(service, TodoWidgetPreferences.Default, () => today);

        await viewModel.LoadAsync();

        Assert.Equal(new DateOnly(2026, 7, 12), viewModel.EightDayDates[0]);
        Assert.Equal(new DateOnly(2026, 7, 19), viewModel.EightDayDates[^1]);
        Assert.Equal((new DateOnly(2026, 7, 12), new DateOnly(2026, 7, 19)), service.LastRange);
    }

    [Fact]
    public async Task SetMode_Today_LoadsCurrentDateOnly()
    {
        var today = new DateOnly(2026, 7, 13);
        var service = new InMemoryTodoService();
        var viewModel = new TodoWidgetViewModel(service, TodoWidgetPreferences.Default, () => today);

        viewModel.SetMode(TodoWidgetMode.Today);
        await viewModel.LoadAsync();

        Assert.Equal(TodoWidgetMode.Today, viewModel.Mode);
        Assert.Equal((today, today), service.LastRange);
    }

    [Fact]
    public async Task CalendarNavigation_LoadsSelectedMonthAndReturnsToCurrentMonth()
    {
        var today = new DateOnly(2026, 7, 13);
        var service = new InMemoryTodoService();
        var viewModel = new TodoWidgetViewModel(
            service,
            TodoWidgetPreferences.Default with { Mode = TodoWidgetMode.Calendar },
            () => today);

        viewModel.ShowNextMonth();
        await viewModel.LoadAsync();
        Assert.Equal(8, viewModel.CalendarMonth.Month);
        Assert.Equal(new DateOnly(2026, 8, 1), service.LastRange.From);
        Assert.Equal(new DateOnly(2026, 8, 31), service.LastRange.To);

        viewModel.ReturnToCurrentMonth();
        await viewModel.LoadAsync();
        Assert.Equal(7, viewModel.CalendarMonth.Month);
    }

    [Fact]
    public async Task CreateTodoAsync_CreatesTodoAndReloadsVisibleRange()
    {
        var today = new DateOnly(2026, 7, 13);
        var service = new InMemoryTodoService();
        var viewModel = new TodoWidgetViewModel(service, TodoWidgetPreferences.Default, () => today);

        await viewModel.CreateTodoAsync(new TodoDraft(today, "Task", TodoTimeKind.None, null, null));

        Assert.Single(service.CreatedDrafts);
        Assert.Single(viewModel.VisibleTodos);
    }

    [Fact]
    public void PinAndHoverDates_TrackPopoverTarget()
    {
        var today = new DateOnly(2026, 7, 13);
        var viewModel = new TodoWidgetViewModel(new InMemoryTodoService(), TodoWidgetPreferences.Default, () => today);

        viewModel.SetHoverDate(today);
        viewModel.PinDate(today);
        viewModel.ClearHoverDate(today);

        Assert.Equal(today, viewModel.PinnedDate);
        Assert.Null(viewModel.HoverDate);

        viewModel.ClearPinnedDate();
        Assert.Null(viewModel.PinnedDate);
    }

    private sealed class InMemoryTodoService : ITodoService
    {
        private readonly List<TodoItem> _todos = new();

        public (DateOnly From, DateOnly To) LastRange { get; private set; }

        public List<TodoDraft> CreatedDrafts { get; } = new();

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<TodoItem>> GetTodosAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
        {
            LastRange = (fromDate, toDate);
            return Task.FromResult<IReadOnlyList<TodoItem>>(
                _todos.Where(todo => todo.Date >= fromDate && todo.Date <= toDate).ToList());
        }

        public Task<TodoItem> CreateTodoAsync(TodoDraft draft, CancellationToken cancellationToken = default)
        {
            CreatedDrafts.Add(draft);
            var todo = new TodoItem
            {
                Id = $"todo-{_todos.Count + 1}",
                Date = draft.Date,
                Text = draft.Text,
                StartTime = draft.StartTime,
                EndTime = draft.EndTime,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _todos.Add(todo);
            return Task.FromResult(todo);
        }

        public Task UpdateTodoAsync(TodoItem todo, TodoDraft draft, CancellationToken cancellationToken = default)
        {
            todo.Date = draft.Date;
            todo.Text = draft.Text;
            todo.StartTime = draft.StartTime;
            todo.EndTime = draft.EndTime;
            return Task.CompletedTask;
        }

        public Task SetCompletedAsync(TodoItem todo, bool isCompleted, CancellationToken cancellationToken = default)
        {
            todo.IsCompleted = isCompleted;
            return Task.CompletedTask;
        }

        public Task DeleteTodoAsync(TodoItem todo, CancellationToken cancellationToken = default)
        {
            _todos.Remove(todo);
            return Task.CompletedTask;
        }

        public IReadOnlyList<TodoItem> SortTodos(IEnumerable<TodoItem> todos) => todos.ToList();
    }
}
```

- [ ] **Step 2: Run tests and verify they fail**

Run:

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj --filter TodoWidgetViewModelTests
```

Expected: build fails because `TodoWidgetViewModel` does not exist.

- [ ] **Step 3: Implement `TodoWidgetViewModel`**

Create `src/QingJian.App/TodoWidgets/TodoWidgetViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using QingJian.App.Models;
using QingJian.App.Services;
using QingJian.App.ViewModels;

namespace QingJian.App.TodoWidgets;

public sealed class TodoWidgetViewModel : ViewModelBase
{
    private readonly ITodoService _todoService;
    private readonly Func<DateOnly> _todayProvider;
    private TodoWidgetMode _mode;
    private DateOnly _calendarMonth;
    private DateOnly? _hoverDate;
    private DateOnly? _pinnedDate;
    private bool _isLocked;
    private double _opacity;

    public TodoWidgetViewModel(ITodoService todoService, TodoWidgetPreferences preferences)
        : this(todoService, preferences, () => DateOnly.FromDateTime(DateTime.Today))
    {
    }

    public TodoWidgetViewModel(
        ITodoService todoService,
        TodoWidgetPreferences preferences,
        Func<DateOnly> todayProvider)
    {
        _todoService = todoService;
        _todayProvider = todayProvider;
        var normalized = preferences.Normalize();
        _mode = normalized.Mode;
        _calendarMonth = new DateOnly(normalized.CalendarYear, normalized.CalendarMonth, 1);
        _isLocked = normalized.IsLocked;
        _opacity = normalized.Opacity;
    }

    public ObservableCollection<TodoItem> VisibleTodos { get; } = new();

    public TodoWidgetMode Mode
    {
        get => _mode;
        private set => SetField(ref _mode, value);
    }

    public DateOnly CalendarMonth
    {
        get => _calendarMonth;
        private set => SetField(ref _calendarMonth, value);
    }

    public DateOnly? HoverDate
    {
        get => _hoverDate;
        private set => SetField(ref _hoverDate, value);
    }

    public DateOnly? PinnedDate
    {
        get => _pinnedDate;
        private set => SetField(ref _pinnedDate, value);
    }

    public bool IsLocked
    {
        get => _isLocked;
        set => SetField(ref _isLocked, value);
    }

    public double Opacity
    {
        get => _opacity;
        set => SetField(ref _opacity, Math.Clamp(value, 0.35, 0.95));
    }

    public IReadOnlyList<DateOnly> EightDayDates
    {
        get
        {
            var start = _todayProvider().AddDays(-1);
            return Enumerable.Range(0, 8).Select(start.AddDays).ToList();
        }
    }

    public IReadOnlyList<DateOnly> CalendarDates
    {
        get
        {
            var first = CalendarMonth;
            var leading = ((int)first.DayOfWeek + 6) % 7;
            var start = first.AddDays(-leading);
            return Enumerable.Range(0, 42).Select(start.AddDays).ToList();
        }
    }

    public DateOnly ActivePopoverDate => PinnedDate ?? HoverDate ?? _todayProvider();

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await _todoService.InitializeAsync(cancellationToken);
        var (from, to) = GetVisibleRange();
        var todos = await _todoService.GetTodosAsync(from, to, cancellationToken);

        VisibleTodos.Clear();
        foreach (var todo in todos)
        {
            VisibleTodos.Add(todo);
        }
    }

    public async Task CreateTodoAsync(TodoDraft draft, CancellationToken cancellationToken = default)
    {
        await _todoService.CreateTodoAsync(draft, cancellationToken);
        await LoadAsync(cancellationToken);
    }

    public async Task UpdateTodoAsync(TodoItem todo, TodoDraft draft, CancellationToken cancellationToken = default)
    {
        await _todoService.UpdateTodoAsync(todo, draft, cancellationToken);
        await LoadAsync(cancellationToken);
    }

    public async Task SetCompletedAsync(TodoItem todo, bool completed, CancellationToken cancellationToken = default)
    {
        await _todoService.SetCompletedAsync(todo, completed, cancellationToken);
        await LoadAsync(cancellationToken);
    }

    public async Task DeleteTodoAsync(TodoItem todo, CancellationToken cancellationToken = default)
    {
        await _todoService.DeleteTodoAsync(todo, cancellationToken);
        await LoadAsync(cancellationToken);
    }

    public void SetMode(TodoWidgetMode mode)
    {
        Mode = mode;
        OnPropertyChanged(nameof(EightDayDates));
        OnPropertyChanged(nameof(CalendarDates));
    }

    public void ShowPreviousMonth()
    {
        CalendarMonth = CalendarMonth.AddMonths(-1);
        OnPropertyChanged(nameof(CalendarDates));
    }

    public void ShowNextMonth()
    {
        CalendarMonth = CalendarMonth.AddMonths(1);
        OnPropertyChanged(nameof(CalendarDates));
    }

    public void ReturnToCurrentMonth()
    {
        var today = _todayProvider();
        CalendarMonth = new DateOnly(today.Year, today.Month, 1);
        OnPropertyChanged(nameof(CalendarDates));
    }

    public void SetHoverDate(DateOnly date)
    {
        HoverDate = date;
    }

    public void ClearHoverDate(DateOnly date)
    {
        if (HoverDate == date)
        {
            HoverDate = null;
        }
    }

    public void PinDate(DateOnly date)
    {
        PinnedDate = date;
    }

    public void ClearPinnedDate()
    {
        PinnedDate = null;
    }

    public TodoWidgetPreferences ToPreferences(double left, double top, bool isVisible)
    {
        return new TodoWidgetPreferences(
            IsVisible: isVisible,
            Mode: Mode,
            Left: left,
            Top: top,
            Opacity: Opacity,
            IsLocked: IsLocked,
            CalendarYear: CalendarMonth.Year,
            CalendarMonth: CalendarMonth.Month).Normalize();
    }

    public IReadOnlyList<TodoItem> TodosForDate(DateOnly date)
    {
        return _todoService.SortTodos(VisibleTodos.Where(todo => todo.Date == date));
    }

    private (DateOnly From, DateOnly To) GetVisibleRange()
    {
        return Mode switch
        {
            TodoWidgetMode.EightDay => (EightDayDates[0], EightDayDates[^1]),
            TodoWidgetMode.Today => (_todayProvider(), _todayProvider()),
            TodoWidgetMode.Calendar => (CalendarMonth, CalendarMonth.AddMonths(1).AddDays(-1)),
            _ => (_todayProvider(), _todayProvider())
        };
    }
}
```

- [ ] **Step 4: Run ViewModel tests**

Run:

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj --filter TodoWidgetViewModelTests
```

Expected: all `TodoWidgetViewModelTests` pass.

- [ ] **Step 5: Commit ViewModel logic**

Run:

```powershell
git add src\QingJian.App\TodoWidgets\TodoWidgetViewModel.cs tests\QingJian.App.Tests\TodoWidgets\TodoWidgetViewModelTests.cs
git commit -m "feat: add todo widget view model"
```

---

### Task 5: Desktop Layer Service And Widget Coordinator

**Files:**
- Create: `src/QingJian.App/TodoWidgets/DesktopLayerService.cs`
- Create: `src/QingJian.App/TodoWidgets/TodoWidgetCoordinator.cs`
- Modify: `src/QingJian.App/App.xaml.cs`

**Interfaces:**
- Consumes: `TodoWidgetViewModel`, `AppSettingsService`, `ITodoService`.
- Produces: `public sealed class DesktopLayerService`
- Produces: `public bool TryAttachToDesktop(Window window)`
- Produces: `public sealed class TodoWidgetCoordinator`
- Produces: `Task InitializeAsync(CancellationToken cancellationToken = default)`
- Produces: `void ShowWidget()`, `void HideWidget()`, `void ToggleWidgetVisibility()`
- Produces: `Task SavePreferencesAsync(CancellationToken cancellationToken = default)`

- [ ] **Step 1: Add desktop layer service**

Create `src/QingJian.App/TodoWidgets/DesktopLayerService.cs`:

```csharp
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace QingJian.App.TodoWidgets;

public sealed class DesktopLayerService
{
    private const int WmSpawnWorker = 0x052C;

    public bool TryAttachToDesktop(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        var progman = FindWindow("Progman", null);
        if (progman == IntPtr.Zero)
        {
            return false;
        }

        SendMessageTimeout(
            progman,
            WmSpawnWorker,
            IntPtr.Zero,
            IntPtr.Zero,
            SendMessageTimeoutFlags.SMTO_NORMAL,
            1000,
            out _);

        var workerW = FindDesktopWorkerW();
        if (workerW == IntPtr.Zero)
        {
            return false;
        }

        return SetParent(handle, workerW) != IntPtr.Zero;
    }

    private static IntPtr FindDesktopWorkerW()
    {
        var result = IntPtr.Zero;

        EnumWindows((topHandle, _) =>
        {
            var shellView = FindWindowEx(topHandle, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shellView == IntPtr.Zero)
            {
                return true;
            }

            result = FindWindowEx(IntPtr.Zero, topHandle, "WorkerW", null);
            return result == IntPtr.Zero;
        }, IntPtr.Zero);

        return result;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        int msg,
        IntPtr wParam,
        IntPtr lParam,
        SendMessageTimeoutFlags flags,
        int timeout,
        out IntPtr result);

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [Flags]
    private enum SendMessageTimeoutFlags : uint
    {
        SMTO_NORMAL = 0x0000
    }
}
```

- [ ] **Step 2: Add coordinator skeleton**

Create `src/QingJian.App/TodoWidgets/TodoWidgetCoordinator.cs`:

```csharp
using System.Windows;
using QingJian.App.Services;

namespace QingJian.App.TodoWidgets;

public sealed class TodoWidgetCoordinator
{
    private readonly ITodoService _todoService;
    private readonly AppSettingsService _settingsService;
    private readonly DesktopLayerService _desktopLayerService;
    private TodoWidgetWindow? _window;
    private TodoWidgetViewModel? _viewModel;
    private AppSettings _settings = AppSettings.Default;

    public TodoWidgetCoordinator(
        ITodoService todoService,
        AppSettingsService settingsService,
        DesktopLayerService desktopLayerService)
    {
        _todoService = todoService;
        _settingsService = settingsService;
        _desktopLayerService = desktopLayerService;
    }

    public bool IsVisible => _window?.IsVisible == true;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _settings = await _settingsService.LoadAsync(cancellationToken);
        _viewModel = new TodoWidgetViewModel(_todoService, _settings.TodoWidget);

        if (_settings.TodoWidget.IsVisible)
        {
            ShowWidget();
            await _viewModel.LoadAsync(cancellationToken);
        }
    }

    public void ShowWidget()
    {
        EnsureWindow();
        _window!.Show();
    }

    public void HideWidget()
    {
        if (_window is null)
        {
            return;
        }

        _window.Hide();
        _ = SavePreferencesAsync();
    }

    public void ToggleWidgetVisibility()
    {
        if (IsVisible)
        {
            HideWidget();
            return;
        }

        ShowWidget();
    }

    public async Task SavePreferencesAsync(CancellationToken cancellationToken = default)
    {
        if (_viewModel is null)
        {
            return;
        }

        var left = _window?.Left ?? _settings.TodoWidget.Left;
        var top = _window?.Top ?? _settings.TodoWidget.Top;
        var preferences = _viewModel.ToPreferences(left, top, IsVisible);
        _settings = _settings with { TodoWidget = preferences };
        await _settingsService.SaveAsync(_settings, cancellationToken);
    }

    private void EnsureWindow()
    {
        if (_window is not null)
        {
            return;
        }

        _viewModel ??= new TodoWidgetViewModel(_todoService, _settings.TodoWidget);
        _window = new TodoWidgetWindow(_viewModel, _desktopLayerService, this)
        {
            Left = _settings.TodoWidget.Left,
            Top = _settings.TodoWidget.Top
        };
    }
}
```

- [ ] **Step 3: Build and verify missing window failure**

Run:

```powershell
dotnet build
```

Expected: build fails because `TodoWidgetWindow` does not exist. This verifies the coordinator is wired to the next task's window type.

- [ ] **Step 4: Do not commit yet**

Do not commit this task until Task 6 adds `TodoWidgetWindow` and build succeeds.

---

### Task 6: Widget Window, Styles, And Preset UI

**Files:**
- Create: `src/QingJian.App/TodoWidgets/TodoWidgetWindow.xaml`
- Create: `src/QingJian.App/TodoWidgets/TodoWidgetWindow.xaml.cs`
- Create: `src/QingJian.App/TodoWidgets/TodoItemsForDateConverter.cs`
- Modify: `src/QingJian.App/Resources/Styles.xaml`

**Interfaces:**
- Consumes: `TodoWidgetViewModel`, `DesktopLayerService`, `TodoWidgetCoordinator`.
- Produces: a fixed-size WPF widget window with three preset views and edit popover controls.
- Produces: `TodoItemsForDateConverter`, which filters the shared visible todo list for one date in date-based cells.

- [ ] **Step 1: Add widget styles**

Append these resources to `src/QingJian.App/Resources/Styles.xaml` before `</ResourceDictionary>`:

```xml
    <Color x:Key="TodoWidgetBackgroundColor">#F8F5EF</Color>
    <Color x:Key="TodoWidgetBorderColor">#CFC5B5</Color>
    <Color x:Key="TodoWidgetAccentColor">#7B6A50</Color>
    <Color x:Key="TodoWidgetDangerColor">#A64032</Color>
    <SolidColorBrush x:Key="TodoWidgetBackgroundBrush" Color="{StaticResource TodoWidgetBackgroundColor}" />
    <SolidColorBrush x:Key="TodoWidgetBorderBrush" Color="{StaticResource TodoWidgetBorderColor}" />
    <SolidColorBrush x:Key="TodoWidgetAccentBrush" Color="{StaticResource TodoWidgetAccentColor}" />
    <SolidColorBrush x:Key="TodoWidgetDangerBrush" Color="{StaticResource TodoWidgetDangerColor}" />
```

- [ ] **Step 2: Add widget XAML**

Create `src/QingJian.App/TodoWidgets/TodoItemsForDateConverter.cs`:

```csharp
using System.Globalization;
using System.Windows.Data;
using QingJian.App.Models;

namespace QingJian.App.TodoWidgets;

public sealed class TodoItemsForDateConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 ||
            values[0] is not DateOnly date ||
            values[1] is not IEnumerable<TodoItem> todos)
        {
            return Array.Empty<TodoItem>();
        }

        return todos.Where(todo => todo.Date == date).ToList();
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
```

Create `src/QingJian.App/TodoWidgets/TodoWidgetWindow.xaml`:

```xml
<Window x:Class="QingJian.App.TodoWidgets.TodoWidgetWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:todo="clr-namespace:QingJian.App.TodoWidgets"
        Title="QingJian Todo Widget"
        Width="520"
        Height="360"
        ResizeMode="NoResize"
        WindowStyle="None"
        ShowInTaskbar="False"
        AllowsTransparency="True"
        Background="Transparent"
        MouseEnter="Window_OnMouseEnter"
        MouseLeave="Window_OnMouseLeave"
        SourceInitialized="Window_OnSourceInitialized"
        Closing="Window_OnClosing">
    <Window.Resources>
        <todo:TodoItemsForDateConverter x:Key="TodoItemsForDateConverter" />
    </Window.Resources>
    <Border x:Name="RootBorder"
            Background="{StaticResource TodoWidgetBackgroundBrush}"
            BorderBrush="{StaticResource TodoWidgetBorderBrush}"
            BorderThickness="1"
            CornerRadius="8"
            Opacity="{Binding Opacity}"
            Padding="10">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="32" />
                <RowDefinition Height="*" />
            </Grid.RowDefinitions>

            <DockPanel Grid.Row="0"
                       MouseLeftButtonDown="DragHandle_OnMouseLeftButtonDown">
                <StackPanel Orientation="Horizontal">
                    <Button Content="8日" Click="EightDayButton_OnClick" />
                    <Button Content="今日" Margin="6,0,0,0" Click="TodayButton_OnClick" />
                    <Button Content="日历" Margin="6,0,0,0" Click="CalendarButton_OnClick" />
                </StackPanel>
                <StackPanel DockPanel.Dock="Right" Orientation="Horizontal">
                    <CheckBox Content="锁定"
                              IsChecked="{Binding IsLocked, Mode=TwoWay}"
                              VerticalAlignment="Center"
                              Margin="0,0,8,0" />
                    <Button Content="隐藏" Click="HideButton_OnClick" />
                </StackPanel>
            </DockPanel>

            <Grid Grid.Row="1">
                <Grid.Style>
                    <Style TargetType="Grid">
                        <Setter Property="Visibility" Value="Collapsed" />
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding Mode}" Value="{x:Static todo:TodoWidgetMode.EightDay}">
                                <Setter Property="Visibility" Value="Visible" />
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </Grid.Style>
                <ItemsControl ItemsSource="{Binding EightDayDates}">
                    <ItemsControl.ItemsPanel>
                        <ItemsPanelTemplate>
                            <UniformGrid Rows="2" Columns="4" />
                        </ItemsPanelTemplate>
                    </ItemsControl.ItemsPanel>
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <Border Margin="4"
                                    Padding="7"
                                    BorderBrush="{StaticResource TodoWidgetBorderBrush}"
                                    BorderThickness="1"
                                    CornerRadius="6"
                                    MouseEnter="DateCell_OnMouseEnter"
                                    MouseLeave="DateCell_OnMouseLeave"
                                    MouseLeftButtonDown="DateCell_OnMouseLeftButtonDown">
                                <StackPanel>
                                    <TextBlock Text="{Binding StringFormat={}{0:MM/dd}}"
                                               FontWeight="SemiBold" />
                                    <ItemsControl>
                                        <ItemsControl.ItemsSource>
                                            <MultiBinding Converter="{StaticResource TodoItemsForDateConverter}">
                                                <Binding />
                                                <Binding Path="DataContext.VisibleTodos"
                                                         RelativeSource="{RelativeSource AncestorType=Window}" />
                                            </MultiBinding>
                                        </ItemsControl.ItemsSource>
                                        <ItemsControl.ItemTemplate>
                                            <DataTemplate>
                                                <CheckBox Content="{Binding Text}"
                                                          IsChecked="{Binding IsCompleted}"
                                                          Tag="{Binding}"
                                                          Click="QuickCompleteTodoCheckBox_OnClick"
                                                          FontSize="11" />
                                            </DataTemplate>
                                        </ItemsControl.ItemTemplate>
                                    </ItemsControl>
                                </StackPanel>
                            </Border>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>
            </Grid>

            <Grid Grid.Row="1">
                <Grid.Style>
                    <Style TargetType="Grid">
                        <Setter Property="Visibility" Value="Collapsed" />
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding Mode}" Value="{x:Static todo:TodoWidgetMode.Today}">
                                <Setter Property="Visibility" Value="Visible" />
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </Grid.Style>
                <ListBox ItemsSource="{Binding VisibleTodos}"
                         BorderThickness="0"
                         Background="Transparent">
                    <ListBox.ItemTemplate>
                        <DataTemplate>
                            <DockPanel Margin="4">
                                <TextBlock DockPanel.Dock="Left"
                                           Width="78"
                                           Foreground="{StaticResource MutedTextBrush}"
                                           Text="{Binding StartTime}" />
                                <CheckBox Content="{Binding Text}"
                                          IsChecked="{Binding IsCompleted}"
                                          Tag="{Binding}"
                                          Click="QuickCompleteTodoCheckBox_OnClick" />
                            </DockPanel>
                        </DataTemplate>
                    </ListBox.ItemTemplate>
                </ListBox>
            </Grid>

            <Grid Grid.Row="1">
                <Grid.Style>
                    <Style TargetType="Grid">
                        <Setter Property="Visibility" Value="Collapsed" />
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding Mode}" Value="{x:Static todo:TodoWidgetMode.Calendar}">
                                <Setter Property="Visibility" Value="Visible" />
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </Grid.Style>
                <DockPanel>
                    <DockPanel DockPanel.Dock="Top" LastChildFill="False" Margin="0,0,0,6">
                        <Button Content="‹" Click="PreviousMonthButton_OnClick" />
                        <TextBlock Text="{Binding CalendarMonth, StringFormat={}{0:yyyy年MM月}}"
                                   Margin="10,0"
                                   VerticalAlignment="Center"
                                   FontWeight="SemiBold" />
                        <Button Content="›" Click="NextMonthButton_OnClick" />
                        <Button Content="本月"
                                Margin="8,0,0,0"
                                Click="CurrentMonthButton_OnClick" />
                    </DockPanel>
                    <ItemsControl ItemsSource="{Binding CalendarDates}">
                        <ItemsControl.ItemsPanel>
                            <ItemsPanelTemplate>
                                <UniformGrid Rows="6" Columns="7" />
                            </ItemsPanelTemplate>
                        </ItemsControl.ItemsPanel>
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <Border Margin="2"
                                        Padding="4"
                                        BorderBrush="{StaticResource TodoWidgetBorderBrush}"
                                        BorderThickness="1"
                                        CornerRadius="4"
                                        MouseEnter="DateCell_OnMouseEnter"
                                        MouseLeave="DateCell_OnMouseLeave"
                                        MouseLeftButtonDown="DateCell_OnMouseLeftButtonDown">
                                    <TextBlock Text="{Binding Day}" FontSize="12" />
                                </Border>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                </DockPanel>
            </Grid>
        </Grid>
    </Border>
</Window>
```

- [ ] **Step 3: Add widget code-behind**

Create `src/QingJian.App/TodoWidgets/TodoWidgetWindow.xaml.cs`:

```csharp
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using QingJian.App.Models;

namespace QingJian.App.TodoWidgets;

public partial class TodoWidgetWindow : Window
{
    private readonly TodoWidgetViewModel _viewModel;
    private readonly DesktopLayerService _desktopLayerService;
    private readonly TodoWidgetCoordinator _coordinator;
    private bool _isHidingFromButton;

    public TodoWidgetWindow(
        TodoWidgetViewModel viewModel,
        DesktopLayerService desktopLayerService,
        TodoWidgetCoordinator coordinator)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _desktopLayerService = desktopLayerService;
        _coordinator = coordinator;
        DataContext = _viewModel;
    }

    private void Window_OnSourceInitialized(object? sender, EventArgs e)
    {
        if (!_desktopLayerService.TryAttachToDesktop(this))
        {
            Topmost = false;
        }
    }

    private void Window_OnMouseEnter(object sender, MouseEventArgs e)
    {
        RootBorder.Opacity = Math.Min(0.95, _viewModel.Opacity + 0.15);
    }

    private void Window_OnMouseLeave(object sender, MouseEventArgs e)
    {
        RootBorder.Opacity = _viewModel.Opacity;
    }

    private void DragHandle_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.IsLocked || e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        DragMove();
        _ = _coordinator.SavePreferencesAsync();
    }

    private void EightDayButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.SetMode(TodoWidgetMode.EightDay);
        _ = _viewModel.LoadAsync();
        _ = _coordinator.SavePreferencesAsync();
    }

    private void TodayButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.SetMode(TodoWidgetMode.Today);
        _ = _viewModel.LoadAsync();
        _ = _coordinator.SavePreferencesAsync();
    }

    private void CalendarButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.SetMode(TodoWidgetMode.Calendar);
        _ = _viewModel.LoadAsync();
        _ = _coordinator.SavePreferencesAsync();
    }

    private void PreviousMonthButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ShowPreviousMonth();
        _ = _viewModel.LoadAsync();
        _ = _coordinator.SavePreferencesAsync();
    }

    private void NextMonthButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ShowNextMonth();
        _ = _viewModel.LoadAsync();
        _ = _coordinator.SavePreferencesAsync();
    }

    private void CurrentMonthButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ReturnToCurrentMonth();
        _ = _viewModel.LoadAsync();
        _ = _coordinator.SavePreferencesAsync();
    }

    private void DateCell_OnMouseEnter(object sender, MouseEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is DateOnly date)
        {
            _viewModel.SetHoverDate(date);
        }
    }

    private void DateCell_OnMouseLeave(object sender, MouseEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is DateOnly date)
        {
            _viewModel.ClearHoverDate(date);
        }
    }

    private void DateCell_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is DateOnly date)
        {
            _viewModel.PinDate(date);
        }
    }

    private async void QuickCompleteTodoCheckBox_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not TodoItem todo)
        {
            return;
        }

        await _viewModel.SetCompletedAsync(todo, !todo.IsCompleted);
    }

    private void HideButton_OnClick(object sender, RoutedEventArgs e)
    {
        _isHidingFromButton = true;
        _coordinator.HideWidget();
        _isHidingFromButton = false;
    }

    private void Window_OnClosing(object? sender, CancelEventArgs e)
    {
        if (_isHidingFromButton)
        {
            return;
        }

        _ = _coordinator.SavePreferencesAsync();
    }
}
```

- [ ] **Step 4: Build**

Run:

```powershell
dotnet build
```

Expected: build succeeds.

- [ ] **Step 5: Commit desktop layer, coordinator, and window skeleton**

Run:

```powershell
git add src\QingJian.App\TodoWidgets\DesktopLayerService.cs src\QingJian.App\TodoWidgets\TodoWidgetCoordinator.cs src\QingJian.App\TodoWidgets\TodoItemsForDateConverter.cs src\QingJian.App\TodoWidgets\TodoWidgetWindow.xaml src\QingJian.App\TodoWidgets\TodoWidgetWindow.xaml.cs src\QingJian.App\Resources\Styles.xaml
git commit -m "feat: add desktop todo widget shell"
```

---

### Task 7: Edit Popover And Todo Commands In The Widget

**Files:**
- Modify: `src/QingJian.App/TodoWidgets/TodoWidgetViewModel.cs`
- Modify: `src/QingJian.App/TodoWidgets/TodoWidgetWindow.xaml`
- Modify: `src/QingJian.App/TodoWidgets/TodoWidgetWindow.xaml.cs`
- Modify: `tests/QingJian.App.Tests/TodoWidgets/TodoWidgetViewModelTests.cs`

**Interfaces:**
- Consumes: `TodoWidgetViewModel.CreateTodoAsync`, `UpdateTodoAsync`, `SetCompletedAsync`, `DeleteTodoAsync`.
- Produces: popover editing UI for selected date.

- [ ] **Step 1: Add ViewModel tests for popover date todo filtering**

Append to `TodoWidgetViewModelTests`:

```csharp
[Fact]
public async Task TodosForDate_ReturnsOnlySelectedDateTodos()
{
    var today = new DateOnly(2026, 7, 13);
    var service = new InMemoryTodoService();
    var viewModel = new TodoWidgetViewModel(service, TodoWidgetPreferences.Default, () => today);

    await viewModel.CreateTodoAsync(new TodoDraft(today, "Today", TodoTimeKind.None, null, null));
    await viewModel.CreateTodoAsync(new TodoDraft(today.AddDays(1), "Tomorrow", TodoTimeKind.None, null, null));

    var todos = viewModel.TodosForDate(today);

    Assert.Single(todos);
    Assert.Equal("Today", todos[0].Text);
}
```

- [ ] **Step 2: Run tests**

Run:

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj --filter TodoWidgetViewModelTests
```

Expected: all ViewModel tests pass. If this fails, fix `TodosForDate` before UI work.

- [ ] **Step 3: Add popover XAML**

In `TodoWidgetWindow.xaml`, add this `Border` as the last child inside the root `Grid`, after the three preset `Grid` blocks:

```xml
            <Border x:Name="EditPopover"
                    Grid.Row="1"
                    Width="260"
                    HorizontalAlignment="Right"
                    VerticalAlignment="Top"
                    Margin="0,24,8,0"
                    Background="#FAF8F4"
                    BorderBrush="{StaticResource TodoWidgetBorderBrush}"
                    BorderThickness="1"
                    CornerRadius="8"
                    Padding="10"
                    Visibility="Collapsed">
                <StackPanel>
                    <TextBlock Text="编辑待办"
                               FontWeight="SemiBold"
                               Margin="0,0,0,8" />
                    <ListBox x:Name="PopoverTodoListBox"
                             MaxHeight="116"
                             BorderThickness="0"
                             Background="Transparent"
                             Margin="0,0,0,8">
                        <ListBox.ItemTemplate>
                            <DataTemplate>
                                <DockPanel Margin="0,2">
                                    <StackPanel DockPanel.Dock="Right" Orientation="Horizontal">
                                        <Button Content="编辑"
                                                Tag="{Binding}"
                                                Click="EditTodoButton_OnClick" />
                                        <Button Content="删除"
                                                Tag="{Binding}"
                                                Margin="4,0,0,0"
                                                Click="DeleteTodoButton_OnClick" />
                                    </StackPanel>
                                    <CheckBox Content="{Binding Text}"
                                              IsChecked="{Binding IsCompleted}"
                                              Tag="{Binding}"
                                              Click="CompleteTodoCheckBox_OnClick" />
                                </DockPanel>
                            </DataTemplate>
                        </ListBox.ItemTemplate>
                    </ListBox>
                    <TextBox x:Name="TodoTextBox"
                             MinHeight="28"
                             TextWrapping="Wrap" />
                    <ComboBox x:Name="TimeKindComboBox"
                              SelectedIndex="0"
                              Margin="0,8,0,0">
                        <ComboBoxItem Content="无时间" />
                        <ComboBoxItem Content="单时间" />
                        <ComboBoxItem Content="时间段" />
                    </ComboBox>
                    <StackPanel Orientation="Horizontal" Margin="0,8,0,0">
                        <TextBox x:Name="StartTimeTextBox"
                                 Width="72"
                                 ToolTip="HH:mm" />
                        <TextBlock Text="-"
                                   Margin="6,0"
                                   VerticalAlignment="Center" />
                        <TextBox x:Name="EndTimeTextBox"
                                 Width="72"
                                 ToolTip="HH:mm" />
                    </StackPanel>
                    <TextBlock x:Name="PopoverErrorTextBlock"
                               Foreground="{StaticResource TodoWidgetDangerBrush}"
                               FontSize="12"
                               TextWrapping="Wrap"
                               Visibility="Collapsed"
                               Margin="0,8,0,0" />
                    <DockPanel Margin="0,10,0,0" LastChildFill="False">
                        <Button x:Name="SaveTodoButton"
                                Content="新增"
                                DockPanel.Dock="Right"
                                Click="AddTodoButton_OnClick" />
                        <Button Content="清空"
                                DockPanel.Dock="Right"
                                Margin="0,0,8,0"
                                Click="ClearTodoFormButton_OnClick" />
                        <Button Content="关闭"
                                DockPanel.Dock="Right"
                                Margin="0,0,8,0"
                                Click="ClosePopoverButton_OnClick" />
                    </DockPanel>
                </StackPanel>
            </Border>
```

- [ ] **Step 4: Add popover code-behind helpers**

Add this using to `TodoWidgetWindow.xaml.cs`:

```csharp
using QingJian.App.Models;
```

Add these methods to `TodoWidgetWindow.xaml.cs` before the final class brace:

```csharp
private TodoItem? _editingTodo;

private void ShowPopover()
{
    EditPopover.Visibility = Visibility.Visible;
    PopoverErrorTextBlock.Visibility = Visibility.Collapsed;
    PopoverTodoListBox.ItemsSource = _viewModel.TodosForDate(_viewModel.ActivePopoverDate);
}

private void ClosePopoverButton_OnClick(object sender, RoutedEventArgs e)
{
    _viewModel.ClearPinnedDate();
    EditPopover.Visibility = Visibility.Collapsed;
}

private async void AddTodoButton_OnClick(object sender, RoutedEventArgs e)
{
    try
    {
        var draft = CreateDraftFromPopover();
        if (_editingTodo is null)
        {
            await _viewModel.CreateTodoAsync(draft);
        }
        else
        {
            await _viewModel.UpdateTodoAsync(_editingTodo, draft);
        }

        ClearTodoForm();
        PopoverTodoListBox.ItemsSource = _viewModel.TodosForDate(_viewModel.ActivePopoverDate);
        PopoverErrorTextBlock.Visibility = Visibility.Collapsed;
        await _coordinator.SavePreferencesAsync();
    }
    catch (Exception ex) when (ex is ArgumentException or FormatException)
    {
        PopoverErrorTextBlock.Text = ex.Message;
        PopoverErrorTextBlock.Visibility = Visibility.Visible;
    }
}

private TodoDraft CreateDraftFromPopover()
{
    var timeKind = TimeKindComboBox.SelectedIndex switch
    {
        1 => TodoTimeKind.Single,
        2 => TodoTimeKind.Range,
        _ => TodoTimeKind.None
    };

    TimeOnly? startTime = string.IsNullOrWhiteSpace(StartTimeTextBox.Text)
        ? null
        : TimeOnly.Parse(StartTimeTextBox.Text);
    TimeOnly? endTime = string.IsNullOrWhiteSpace(EndTimeTextBox.Text)
        ? null
        : TimeOnly.Parse(EndTimeTextBox.Text);

    return new TodoDraft(_viewModel.ActivePopoverDate, TodoTextBox.Text, timeKind, startTime, endTime);
}

private async void CompleteTodoCheckBox_OnClick(object sender, RoutedEventArgs e)
{
    if ((sender as FrameworkElement)?.Tag is not TodoItem todo)
    {
        return;
    }

    await _viewModel.SetCompletedAsync(todo, !todo.IsCompleted);
    PopoverTodoListBox.ItemsSource = _viewModel.TodosForDate(_viewModel.ActivePopoverDate);
}

private void EditTodoButton_OnClick(object sender, RoutedEventArgs e)
{
    if ((sender as FrameworkElement)?.Tag is not TodoItem todo)
    {
        return;
    }

    _editingTodo = todo;
    TodoTextBox.Text = todo.Text;
    TimeKindComboBox.SelectedIndex = todo.StartTime is null ? 0 : todo.EndTime is null ? 1 : 2;
    StartTimeTextBox.Text = todo.StartTime?.ToString("HH:mm") ?? string.Empty;
    EndTimeTextBox.Text = todo.EndTime?.ToString("HH:mm") ?? string.Empty;
    SaveTodoButton.Content = "保存";
}

private async void DeleteTodoButton_OnClick(object sender, RoutedEventArgs e)
{
    if ((sender as FrameworkElement)?.Tag is not TodoItem todo)
    {
        return;
    }

    await _viewModel.DeleteTodoAsync(todo);
    if (_editingTodo?.Id == todo.Id)
    {
        ClearTodoForm();
    }

    PopoverTodoListBox.ItemsSource = _viewModel.TodosForDate(_viewModel.ActivePopoverDate);
}

private void ClearTodoFormButton_OnClick(object sender, RoutedEventArgs e)
{
    ClearTodoForm();
}

private void ClearTodoForm()
{
    _editingTodo = null;
    TodoTextBox.Text = string.Empty;
    TimeKindComboBox.SelectedIndex = 0;
    StartTimeTextBox.Text = string.Empty;
    EndTimeTextBox.Text = string.Empty;
    SaveTodoButton.Content = "新增";
}
```

In `DateCell_OnMouseEnter`, after `_viewModel.SetHoverDate(date);`, add:

```csharp
ShowPopover();
```

In `DateCell_OnMouseLeftButtonDown`, after `_viewModel.PinDate(date);`, add:

```csharp
ShowPopover();
```

- [ ] **Step 5: Build**

Run:

```powershell
dotnet build
```

Expected: build succeeds.

- [ ] **Step 6: Commit popover editing**

Run:

```powershell
git add src\QingJian.App\TodoWidgets\TodoWidgetViewModel.cs src\QingJian.App\TodoWidgets\TodoWidgetWindow.xaml src\QingJian.App\TodoWidgets\TodoWidgetWindow.xaml.cs tests\QingJian.App.Tests\TodoWidgets\TodoWidgetViewModelTests.cs
git commit -m "feat: add todo widget edit popover"
```

---

### Task 8: App And Main Window Wiring

**Files:**
- Modify: `src/QingJian.App/App.xaml.cs`
- Modify: `src/QingJian.App/Views/MainWindow.xaml`
- Modify: `src/QingJian.App/Views/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `TodoRepository`, `TodoService`, `TodoWidgetCoordinator`.
- Produces: startup default widget display and main-window show/hide control.

- [ ] **Step 1: Modify `MainWindow` constructor**

Change `MainWindow.xaml.cs` constructor to accept `TodoWidgetCoordinator` and store it:

```csharp
private readonly TodoWidgetCoordinator _todoWidgetCoordinator;

public MainWindow(
    MainViewModel viewModel,
    AppSettingsService settingsService,
    AttachmentService attachmentService,
    TodoWidgetCoordinator todoWidgetCoordinator)
{
    InitializeComponent();
    _viewModel = viewModel;
    _settingsService = settingsService;
    _attachmentService = attachmentService;
    _todoWidgetCoordinator = todoWidgetCoordinator;
    DataContext = _viewModel;
    Loaded += OnLoaded;
    Closing += OnClosing;
    _viewModel.PropertyChanged += (_, args) =>
    {
        if (args.PropertyName == nameof(MainViewModel.SelectedNote))
        {
            UpdateTitlePlaceholderState();
            _ = LoadSelectedNoteIntoEditorAsync();
        }
    };
}
```

Add this click handler:

```csharp
private void ToggleTodoWidgetButton_OnClick(object sender, RoutedEventArgs e)
{
    _todoWidgetCoordinator.ToggleWidgetVisibility();
}
```

- [ ] **Step 2: Add main-window widget button**

In `MainWindow.xaml`, replace the left-pane top button with a small stack:

```xml
                <StackPanel DockPanel.Dock="Top" Margin="0,0,0,14">
                    <Button Content="新建便签"
                            Command="{Binding NewNoteCommand}" />
                    <Button Content="显示/隐藏桌面待办"
                            Margin="0,8,0,0"
                            Click="ToggleTodoWidgetButton_OnClick" />
                </StackPanel>
```

- [ ] **Step 3: Wire app startup**

Modify `src/QingJian.App/App.xaml.cs` to create todo services and coordinator:

```csharp
using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using QingJian.App.Data;
using QingJian.App.Hotkeys;
using QingJian.App.QuickNotes;
using QingJian.App.Services;
using QingJian.App.TodoWidgets;
using QingJian.App.ViewModels;
using QingJian.App.Views;

namespace QingJian.App;

public partial class App : Application
{
    private GlobalHotkeyService? _hotkeyService;

    protected override async void OnStartup(StartupEventArgs e)
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
        var todoRepository = new TodoRepository(dbContext);
        var todoService = new TodoService(todoRepository);
        var viewModel = new MainViewModel(service);
        var settingsService = new AppSettingsService(appDataFolder);
        var attachmentService = new AttachmentService(Path.Combine(appDataFolder, "attachments"));
        var todoWidgetCoordinator = new TodoWidgetCoordinator(
            todoService,
            settingsService,
            new DesktopLayerService());

        var window = new MainWindow(viewModel, settingsService, attachmentService, todoWidgetCoordinator);
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
        await todoWidgetCoordinator.InitializeAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyService?.Dispose();
        base.OnExit(e);
    }
}
```

- [ ] **Step 4: Build**

Run:

```powershell
dotnet build
```

Expected: build succeeds.

- [ ] **Step 5: Run full tests**

Run:

```powershell
dotnet test
```

Expected: all tests pass.

- [ ] **Step 6: Commit app wiring**

Run:

```powershell
git add src\QingJian.App\App.xaml.cs src\QingJian.App\Views\MainWindow.xaml src\QingJian.App\Views\MainWindow.xaml.cs
git commit -m "feat: wire desktop todo widget"
```

---

### Task 9: Documentation, Manual Verification, And Project Handoff

**Files:**
- Modify: `README.md`
- Modify: `docs/project-status.md`

**Interfaces:**
- Consumes: completed desktop todo widget implementation.
- Produces: updated user-facing and handoff docs.

- [ ] **Step 1: Update README**

Add this section after `## Quick Note Shortcut` in `README.md`:

```markdown
## Desktop Todo Widget

QingJian shows a semi-transparent desktop todo widget by default on first launch after the feature is installed.

- The widget stores todos independently from notes.
- The main window can show or hide the widget.
- The widget supports 8-day, today-list, and monthly-calendar presets.
- The 8-day preset starts from yesterday and shows 8 consecutive days.
- The monthly-calendar preset supports previous month, next month, and return to current month.
- Todos can be added, edited, completed, deleted, and assigned no time, a single time, or a time range from the widget.
- Completed todos remain visible and move to the bottom of their day.
- Widget visibility, mode, position, opacity, lock state, and calendar month are persisted.
```

- [ ] **Step 2: Update project status**

In `docs/project-status.md`, add this under `Completed Features`:

```markdown
### Desktop Todo Widget

- Todos are stored independently from notes in `TodoItems`.
- QingJian shows a semi-transparent desktop todo widget by default on first launch after the feature is installed.
- The main window can show or hide the widget.
- The widget supports 8-day, today-list, and monthly-calendar presets.
- The 8-day preset starts from yesterday and covers 8 consecutive days.
- The monthly-calendar preset supports previous month, next month, and return to current month.
- Widget editing supports add, edit, complete, delete, no-time todos, single-time todos, and time-range todos.
- Completed todos remain visible and move to the bottom of their day.
- Widget visibility, mode, position, opacity, lock state, and calendar month are persisted in settings.
```

Add these known decisions under `Known Decisions`:

```markdown
- Desktop todo reminders and notifications are postponed.
- Repeating todos and cross-day todos are postponed.
- Desktop todo size presets are postponed; first version uses a fixed widget size.
- A full main-window todo management page is postponed; first version edits todos from the widget.
```

- [ ] **Step 3: Run automated verification**

Run:

```powershell
dotnet test
dotnet build
```

Expected: tests pass, then build succeeds.

- [ ] **Step 4: Run app for manual smoke test**

Run:

```powershell
dotnet run --project src\QingJian.App\QingJian.App.csproj
```

Manual checks:

1. QingJian starts normally.
2. The desktop todo widget appears by default if no prior widget visibility setting exists.
3. Main-window `显示/隐藏桌面待办` hides and shows the widget.
4. Widget mode buttons switch between `8日`, `今日`, and `日历`.
5. The 8-day grid starts from yesterday.
6. Calendar previous and next buttons change months.
7. Calendar `本月` returns to the current month.
8. Locking the widget prevents dragging but still allows clicking cells and buttons.
9. Unlocking the widget allows dragging.
10. A todo can be added from a date popover.
11. A no-time todo, single-time todo, and time-range todo can be saved.
12. Completing a todo keeps it visible and places it below incomplete todos for the same day.
13. Closing and reopening QingJian restores widget visibility, position, mode, opacity, lock state, and calendar month.

- [ ] **Step 5: Commit docs**

Run:

```powershell
git add README.md docs\project-status.md
git commit -m "docs: describe desktop todo widget"
```

---

## Self-Review

Spec coverage:

1. Independent todo storage is covered by Tasks 1 and 2.
2. SQLite persistence is covered by Task 1.
3. Widget preference persistence is covered by Task 3 and Task 5.
4. 8-day, today-list, and monthly-calendar modes are covered by Tasks 4 and 6.
5. Calendar month navigation is covered by Tasks 4 and 6.
6. Desktop-layer attachment and fallback are covered by Task 5.
7. Fixed-size, lock/drag behavior, and hover opacity are covered by Task 6.
8. Shared edit popover is covered by Task 7.
9. Main-window show/hide control and default startup display are covered by Task 8.
10. Documentation and handoff updates are covered by Task 9.

No placeholder steps remain. Type names used by later tasks are introduced in earlier tasks: `TodoItem`, `ITodoRepository`, `TodoService`, `TodoDraft`, `TodoWidgetPreferences`, `TodoWidgetViewModel`, `DesktopLayerService`, `TodoWidgetWindow`, and `TodoWidgetCoordinator`.
