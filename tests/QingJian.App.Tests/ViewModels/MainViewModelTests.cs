using QingJian.App.Models;
using QingJian.App.Services;
using QingJian.App.ViewModels;
using System.Windows.Data;
using Xunit;

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

        viewModel.NewNoteCommand.Execute(null);
        await WaitUntilAsync(() => viewModel.Notes.Count == 1 && ReferenceEquals(viewModel.SelectedNote, viewModel.Notes[0]));

        Assert.Single(viewModel.Notes);
        Assert.Same(viewModel.Notes[0], viewModel.SelectedNote);
        Assert.False(viewModel.IsEmpty);
    }

    [Fact]
    public async Task DeleteSelectedNoteCommand_CanExecute_TracksSelectedNoteState()
    {
        var service = new InMemoryNoteService();
        var viewModel = new MainViewModel(service);

        Assert.False(viewModel.DeleteSelectedNoteCommand.CanExecute(null));

        viewModel.NewNoteCommand.Execute(null);
        await WaitUntilAsync(() => viewModel.SelectedNote is not null);
        Assert.True(viewModel.DeleteSelectedNoteCommand.CanExecute(null));

        viewModel.DeleteSelectedNoteCommand.Execute(null);
        await WaitUntilAsync(() => viewModel.SelectedNote is null && viewModel.Notes.Count == 0);
        Assert.False(viewModel.DeleteSelectedNoteCommand.CanExecute(null));
    }

    [Fact]
    public async Task DeleteSelectedNoteCommand_DeletesSelectedNoteAndSelectsNextAvailableNote()
    {
        var first = CreateNote("first", "First");
        var second = CreateNote("second", "Second");
        var service = new InMemoryNoteService(first, second);
        var viewModel = new MainViewModel(service);
        await viewModel.LoadAsync();

        viewModel.DeleteSelectedNoteCommand.Execute(null);
        await WaitUntilAsync(() => viewModel.Notes.Count == 1 && viewModel.SelectedNote?.Id == "second" && service.DeletedIds.Contains("first"));

        Assert.Single(viewModel.Notes);
        Assert.Equal("second", viewModel.SelectedNote?.Id);
        Assert.DoesNotContain(viewModel.Notes, note => note.Id == "first");
        Assert.Contains("first", service.DeletedIds);
    }

    [Fact]
    public async Task DeleteSelectedNoteCommand_RaisesCanExecuteChanged_WhenSelectedNoteChanges()
    {
        var service = new InMemoryNoteService();
        var viewModel = new MainViewModel(service);
        var canExecuteChangedCount = 0;

        viewModel.DeleteSelectedNoteCommand.CanExecuteChanged += (_, _) => canExecuteChangedCount++;

        viewModel.NewNoteCommand.Execute(null);
        await WaitUntilAsync(() => canExecuteChangedCount > 0 && viewModel.SelectedNote is not null);

        Assert.True(canExecuteChangedCount > 0);
    }

    [Fact]
    public async Task IsEmpty_PropertyChanged_FiresWhenNotesTransitionBetweenEmptyAndNonEmpty()
    {
        var service = new InMemoryNoteService();
        var viewModel = new MainViewModel(service);
        var becameNonEmpty = false;
        var becameEmpty = false;

        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(MainViewModel.IsEmpty))
            {
                return;
            }

            if (viewModel.Notes.Count == 1)
            {
                becameNonEmpty = true;
            }

            if (viewModel.Notes.Count == 0)
            {
                becameEmpty = true;
            }
        };

        viewModel.NewNoteCommand.Execute(null);
        await WaitUntilAsync(() => becameNonEmpty && viewModel.Notes.Count == 1);

        viewModel.DeleteSelectedNoteCommand.Execute(null);
        await WaitUntilAsync(() => becameEmpty && viewModel.Notes.Count == 0);

        Assert.True(becameNonEmpty);
        Assert.True(becameEmpty);
    }

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

    [Fact]
    public async Task NotesView_GroupsAndSortsNotesByUpdatedAtDescending()
    {
        var now = new DateTime(2026, 7, 20, 12, 0, 0, DateTimeKind.Local);
        var notes = new[]
        {
            CreateNote("last-year", "Last year", new DateTime(2025, 12, 31, 8, 0, 0, DateTimeKind.Local)),
            CreateNote("today-old", "Today old", new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Local)),
            CreateNote("june", "June", new DateTime(2026, 6, 19, 8, 0, 0, DateTimeKind.Local)),
            CreateNote("yesterday", "Yesterday", new DateTime(2026, 7, 19, 8, 0, 0, DateTimeKind.Local)),
            CreateNote("today-new", "Today new", new DateTime(2026, 7, 20, 10, 0, 0, DateTimeKind.Local))
        };
        var viewModel = new MainViewModel(new InMemoryNoteService(notes), TimeSpan.FromMilliseconds(700), () => now);

        await viewModel.LoadAsync();

        var groupNames = viewModel.NotesView.Groups!
            .Cast<CollectionViewGroup>()
            .Select(group => group.Name)
            .ToArray();

        Assert.Equal(new object[] { "今天", "过去30天", "6月", "2025年" }, groupNames);
        Assert.Equal(new[] { "today-new", "today-old", "yesterday", "june", "last-year" },
            viewModel.NotesView.Cast<Note>().Select(note => note.Id));
    }

    [Fact]
    public async Task SearchText_GroupsTitleMatchesBeforeBodyMatchesAndPreservesSelection()
    {
        var now = new DateTime(2026, 7, 20, 12, 0, 0, DateTimeKind.Local);
        var selected = CreateNote("selected", "Unrelated", now.AddHours(-3));
        selected.Content = "No match";
        var bodyMatch = CreateNote("body", "Other", now.AddHours(-1));
        bodyMatch.Content = "contains alpha";
        var titleMatch = CreateNote("title", "Alpha title", now.AddHours(-2));
        titleMatch.Content = "Body";
        var viewModel = new MainViewModel(
            new InMemoryNoteService(selected, bodyMatch, titleMatch),
            TimeSpan.FromMilliseconds(700),
            () => now);
        await viewModel.LoadAsync();
        viewModel.SelectedNote = selected;

        viewModel.SearchText = "ALPHA";

        Assert.Same(selected, viewModel.SelectedNote);
        Assert.Equal(new[] { "title", "body" }, viewModel.NotesView.Cast<Note>().Select(note => note.Id));
        Assert.Equal(
            new object[] { "标题匹配", "正文匹配" },
            viewModel.NotesView.Groups!.Cast<CollectionViewGroup>().Select(group => group.Name));
        Assert.False(viewModel.ShowNoSearchResults);
    }

    [Fact]
    public async Task FavoriteSort_GroupsFavoritesByFavoriteTimeAndOthersByUpdatedAt()
    {
        var now = new DateTime(2026, 7, 20, 12, 0, 0, DateTimeKind.Local);
        var favoriteOld = CreateNote("favorite-old", "Old favorite", now.AddHours(-1));
        favoriteOld.IsFavorite = true;
        favoriteOld.FavoritedAt = now.AddHours(-2);
        var favoriteNew = CreateNote("favorite-new", "New favorite", now.AddHours(-4));
        favoriteNew.IsFavorite = true;
        favoriteNew.FavoritedAt = now.AddHours(-1);
        var normalOld = CreateNote("normal-old", "Normal old", now.AddHours(-3));
        var normalNew = CreateNote("normal-new", "Normal new", now.AddHours(-2));
        var viewModel = new MainViewModel(
            new InMemoryNoteService(normalNew, favoriteOld, normalOld, favoriteNew),
            TimeSpan.FromMilliseconds(700),
            () => now);
        await viewModel.LoadAsync();

        viewModel.ToggleNavigationSortCommand.Execute(null);

        Assert.Equal(NoteNavigationSortMode.Favorite, viewModel.NavigationSortMode);
        Assert.Equal("按时间", viewModel.SortToggleToolTip);
        Assert.Equal(
            new[] { "favorite-new", "favorite-old", "normal-new", "normal-old" },
            viewModel.NotesView.Cast<Note>().Select(note => note.Id));
        Assert.Equal(
            new object[] { "收藏", "其他" },
            viewModel.NotesView.Groups!.Cast<CollectionViewGroup>().Select(group => group.Name));
    }

    [Fact]
    public async Task SearchText_SetsNoResultsOnlyWhenNotesExist()
    {
        var note = CreateNote("note", "Title");
        var viewModel = new MainViewModel(new InMemoryNoteService(note));
        await viewModel.LoadAsync();

        viewModel.SearchText = "missing";

        Assert.True(viewModel.ShowNoSearchResults);
        Assert.Empty(viewModel.NotesView.Cast<Note>());

        var emptyViewModel = new MainViewModel(new InMemoryNoteService());
        await emptyViewModel.LoadAsync();
        emptyViewModel.SearchText = "missing";
        Assert.False(emptyViewModel.ShowNoSearchResults);
    }

    [Fact]
    public async Task ToggleFavoriteAsync_RefreshesProjectionWithoutChangingSelectionOrUpdatedAt()
    {
        var now = new DateTime(2026, 7, 20, 12, 0, 0, DateTimeKind.Local);
        var selected = CreateNote("selected", "Selected", now.AddHours(-1));
        var target = CreateNote("target", "Target", now.AddHours(-2));
        var originalUpdatedAt = target.UpdatedAt;
        var service = new InMemoryNoteService(selected, target)
        {
            FavoriteUpdatedAt = now
        };
        var viewModel = new MainViewModel(service, TimeSpan.FromMilliseconds(700), () => now);
        await viewModel.LoadAsync();
        viewModel.SelectedNote = selected;
        viewModel.ToggleNavigationSortCommand.Execute(null);

        await viewModel.ToggleFavoriteAsync(target);

        Assert.Same(selected, viewModel.SelectedNote);
        Assert.True(target.IsFavorite);
        Assert.Equal(now, target.FavoritedAt);
        Assert.Equal(originalUpdatedAt, target.UpdatedAt);
        Assert.Equal("target", viewModel.NotesView.Cast<Note>().First().Id);
    }

    [Fact]
    public async Task ToggleFavoriteAsync_LeavesSelectionAndValuesIntactWhenServiceFails()
    {
        var selected = CreateNote("selected", "Selected");
        var target = CreateNote("target", "Target");
        var service = new InMemoryNoteService(selected, target)
        {
            FavoriteException = new InvalidOperationException("boom")
        };
        var viewModel = new MainViewModel(service);
        await viewModel.LoadAsync();
        viewModel.SelectedNote = selected;

        await Assert.ThrowsAsync<InvalidOperationException>(() => viewModel.ToggleFavoriteAsync(target));

        Assert.Same(selected, viewModel.SelectedNote);
        Assert.False(target.IsFavorite);
        Assert.Null(target.FavoritedAt);
    }

    [Fact]
    public async Task SaveSelectedNoteNowAsync_RefreshesNavigationGrouping()
    {
        var now = new DateTime(2026, 7, 20, 12, 0, 0, DateTimeKind.Local);
        var note = CreateNote("note-1", "Original", new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Local));
        var service = new InMemoryNoteService(note)
        {
            SavedUpdatedAt = now
        };
        var viewModel = new MainViewModel(service, TimeSpan.FromMilliseconds(700), () => now);
        await viewModel.LoadAsync();

        await viewModel.SaveSelectedNoteNowAsync();

        var group = Assert.Single(viewModel.NotesView.Groups!.Cast<CollectionViewGroup>());
        Assert.Equal("今天", group.Name);
    }

    [Fact]
    public async Task EditingSelectedNote_AutoSavesAfterDelay()
    {
        var note = CreateNote("note-1", "Original");
        var service = new InMemoryNoteService(note);
        var viewModel = new MainViewModel(service, TimeSpan.FromMilliseconds(10));
        await viewModel.LoadAsync();

        viewModel.SelectedNote!.Content = "Changed";
        await WaitUntilAsync(() => service.SavedIds.Contains("note-1"));

        Assert.Contains("note-1", service.SavedIds);
    }

    [Fact]
    public async Task EditingOneNoteThenSelectingAnother_AutoSavesTheEditedNote()
    {
        var first = CreateNote("first", "First");
        var second = CreateNote("second", "Second");
        var service = new InMemoryNoteService(first, second);
        var viewModel = new MainViewModel(service, TimeSpan.FromMilliseconds(10));
        await viewModel.LoadAsync();

        viewModel.SelectedNote = first;
        first.Content = "Changed";
        viewModel.SelectedNote = second;

        await WaitUntilAsync(() => service.SavedIds.Contains("first"));

        Assert.Contains("first", service.SavedIds);
        Assert.DoesNotContain("second", service.SavedIds);
    }

    [Fact]
    public async Task FolderFilter_CombinesWithSearchAndExcludesOtherFolders()
    {
        var now = new DateTime(2026, 7, 26, 12, 0, 0, DateTimeKind.Local);
        var projectTitle = CreateNote("project-title", "Alpha project", now.AddMinutes(-1));
        projectTitle.FolderName = "项目";
        var projectBody = CreateNote("project-body", "Other", now.AddMinutes(-2));
        projectBody.Content = "contains alpha";
        projectBody.FolderName = "项目";
        var other = CreateNote("other", "Alpha other", now.AddMinutes(-3));
        other.FolderName = "工作";
        var viewModel = new MainViewModel(
            new InMemoryNoteService(projectTitle, projectBody, other),
            new InMemoryFolderService("项目", "工作"),
            TimeSpan.FromMilliseconds(700),
            () => now);

        await viewModel.LoadAsync();
        viewModel.ApplyFolderFilter("项目");
        viewModel.SearchText = "alpha";

        Assert.Equal(new[] { "project-title", "project-body" },
            viewModel.NotesView.Cast<Note>().Select(note => note.Id));
        Assert.DoesNotContain(viewModel.NotesView.Cast<Note>(), note => note.FolderName == "工作");
    }

    [Fact]
    public async Task NewNoteAsync_UsesCurrentFolderAsTheNewNoteDestination()
    {
        var service = new InMemoryNoteService();
        var viewModel = new MainViewModel(
            service,
            new InMemoryFolderService("项目"),
            TimeSpan.FromMilliseconds(700),
            () => DateTime.Now);
        await viewModel.LoadAsync();
        viewModel.ApplyFolderFilter("项目");

        await viewModel.NewNoteAsync();

        Assert.Equal("项目", service.LastCreatedFolder);
        Assert.Equal("项目", viewModel.SelectedNote?.FolderName);
    }

    [Fact]
    public async Task MoveNoteAsync_RemovesMovedNoteAndSelectsNextVisibleNote()
    {
        var now = new DateTime(2026, 7, 26, 12, 0, 0, DateTimeKind.Local);
        var moved = CreateNote("moved", "Moved", now.AddMinutes(-1));
        moved.FolderName = "项目";
        var next = CreateNote("next", "Next", now.AddMinutes(-2));
        next.FolderName = "项目";
        var service = new InMemoryNoteService(moved, next);
        var viewModel = new MainViewModel(
            service,
            new InMemoryFolderService("项目"),
            TimeSpan.FromMilliseconds(700),
            () => now);
        await viewModel.LoadAsync();
        viewModel.ApplyFolderFilter("项目");
        viewModel.SelectedNote = moved;

        await viewModel.MoveNoteAsync(moved, "未分类");

        Assert.DoesNotContain(moved, viewModel.NotesView.Cast<Note>());
        Assert.Same(next, viewModel.SelectedNote);
    }

    [Fact]
    public async Task ApplyFolderRename_UpdatesFilterAndInMemoryNotes()
    {
        var note = CreateNote("note", "Note");
        note.FolderName = "项目";
        var viewModel = new MainViewModel(
            new InMemoryNoteService(note),
            new InMemoryFolderService("项目"),
            TimeSpan.FromMilliseconds(700),
            () => DateTime.Now);
        await viewModel.LoadAsync();
        viewModel.ApplyFolderFilter("项目");

        viewModel.ApplyFolderRename("项目", "工作");

        Assert.Equal("工作", viewModel.CurrentFolderName);
        Assert.Equal("工作", note.FolderName);
        Assert.Contains(note, viewModel.NotesView.Cast<Note>());
    }

    [Fact]
    public async Task ApplyFolderDeletion_MovesInMemoryNotesAndFilterToUncategorized()
    {
        var note = CreateNote("note", "Note");
        note.FolderName = "项目";
        var viewModel = new MainViewModel(
            new InMemoryNoteService(note),
            new InMemoryFolderService("项目"),
            TimeSpan.FromMilliseconds(700),
            () => DateTime.Now);
        await viewModel.LoadAsync();
        viewModel.ApplyFolderFilter("项目");

        viewModel.ApplyFolderDeletion("项目");

        Assert.Equal("未分类", viewModel.CurrentFolderName);
        Assert.Equal("未分类", note.FolderName);
        Assert.Contains(note, viewModel.NotesView.Cast<Note>());
    }

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

    [Fact]
    public async Task AddSavedNote_AllowsInsertionAfterAsynchronousContinuation()
    {
        var added = CreateNote("added", "Added");
        var viewModel = new MainViewModel(new InMemoryNoteService());

        var exception = await Record.ExceptionAsync(() => Task.Run(() => viewModel.AddSavedNote(added, select: true)));

        Assert.Null(exception);
        Assert.Single(viewModel.Notes);
        Assert.Same(added, viewModel.SelectedNote);
    }

    private static Note CreateNote(string id, string title, DateTime? updatedAt = null)
    {
        var timestamp = updatedAt ?? DateTime.UtcNow;

        return new Note
        {
            Id = id,
            Title = title,
            Content = string.Empty,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
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

        public List<string> SavedIds { get; } = new();

        public string? LastCreatedFolder { get; private set; }

        public DateTime? SavedUpdatedAt { get; init; }

        public DateTime? FavoriteUpdatedAt { get; init; }

        public Exception? FavoriteException { get; init; }

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
            var note = CreateNote($"note-{_notes.Count + 1}", NoteService.DefaultTitle);
            _notes.Insert(0, note);
            return Task.FromResult(note);
        }

        public Task<Note> CreateNoteInFolderAsync(string folderName, CancellationToken cancellationToken = default)
        {
            LastCreatedFolder = folderName;
            var note = CreateNote($"note-{_notes.Count + 1}", NoteService.DefaultTitle);
            note.FolderName = folderName;
            _notes.Insert(0, note);
            return Task.FromResult(note);
        }

        public Task SaveNoteAsync(Note note, CancellationToken cancellationToken = default)
        {
            SavedIds.Add(note.Id);
            if (SavedUpdatedAt is DateTime updatedAt)
            {
                note.UpdatedAt = updatedAt;
            }

            return Task.CompletedTask;
        }

        public Task SetFavoriteAsync(Note note, bool isFavorite, CancellationToken cancellationToken = default)
        {
            if (FavoriteException is not null)
            {
                throw FavoriteException;
            }

            note.IsFavorite = isFavorite;
            note.FavoritedAt = isFavorite ? FavoriteUpdatedAt ?? DateTime.UtcNow : null;
            return Task.CompletedTask;
        }

        public Task MoveNoteAsync(Note note, string folderName, CancellationToken cancellationToken = default)
        {
            note.FolderName = folderName;
            return Task.CompletedTask;
        }

        public Task SetFavoritesAsync(
            IReadOnlyCollection<Note> notes,
            bool isFavorite,
            CancellationToken cancellationToken = default)
        {
            foreach (var note in notes.DistinctBy(note => note.Id))
            {
                note.IsFavorite = isFavorite;
                note.FavoritedAt = isFavorite ? DateTime.UtcNow : null;
            }

            return Task.CompletedTask;
        }

        public Task MoveNotesAsync(
            IReadOnlyCollection<Note> notes,
            string folderName,
            CancellationToken cancellationToken = default)
        {
            foreach (var note in notes.DistinctBy(note => note.Id))
            {
                note.FolderName = folderName;
            }

            return Task.CompletedTask;
        }

        public Task DeleteNotesAsync(
            IReadOnlyCollection<Note> notes,
            CancellationToken cancellationToken = default)
        {
            foreach (var note in notes.DistinctBy(note => note.Id))
            {
                note.IsDeleted = true;
            }

            return Task.CompletedTask;
        }

        public Task DeleteNoteAsync(Note note, CancellationToken cancellationToken = default)
        {
            DeletedIds.Add(note.Id);
            _notes.RemoveAll(item => item.Id == note.Id);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryFolderService : IFolderService
    {
        private readonly List<FolderSummary> _folders;

        public InMemoryFolderService(params string[] folderNames)
        {
            _folders = new List<FolderSummary>
            {
                new(Folder.SystemUncategorizedId, FolderNamePolicy.UncategorizedName, true, 0, true)
            };
            _folders.AddRange(folderNames.Select((name, index) =>
                new FolderSummary($"folder-{index}", name, false, 0, true)));
        }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<FolderSummary>> GetFoldersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FolderSummary>>(_folders);

        public Task<FolderSummary> CreateAsync(string name, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<FolderSummary> RenameAsync(FolderSummary folder, string newName, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(FolderSummary folder, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> IsValidMoveTargetAsync(string folderName, CancellationToken cancellationToken = default)
            => Task.FromResult(_folders.Any(folder =>
                string.Equals(folder.Name, folderName, StringComparison.OrdinalIgnoreCase)));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition())
        {
            cancellation.Token.ThrowIfCancellationRequested();
            await Task.Delay(10, cancellation.Token);
        }
    }
}
