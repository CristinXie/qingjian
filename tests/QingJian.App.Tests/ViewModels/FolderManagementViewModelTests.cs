using QingJian.App.Models;
using QingJian.App.Services;
using QingJian.App.ViewModels;
using Xunit;

namespace QingJian.App.Tests.ViewModels;

public sealed class FolderManagementViewModelTests
{
    [Fact]
    public async Task LoadAsync_PutsUncategorizedFirstAndCalculatesAllCount()
    {
        var service = new FakeFolderService(
            new FolderSummary("work", "工作", false, 2, true),
            new FolderSummary(Folder.SystemUncategorizedId, "未分类", true, 3, true),
            new FolderSummary("project", "项目", false, 0, true));
        var viewModel = new FolderManagementViewModel(service);

        await viewModel.LoadAsync();

        Assert.Equal(new[] { "未分类", "工作", "项目" },
            viewModel.Items.Select(item => item.Name));
        Assert.Equal(5, viewModel.AllNotesCount);
    }

    [Fact]
    public async Task ConfirmEditAsync_CreatesFolderAndLeavesEditMode()
    {
        var service = new FakeFolderService();
        var viewModel = new FolderManagementViewModel(service);
        await viewModel.LoadAsync();
        viewModel.BeginCreateCommand.Execute(null);
        viewModel.EditorText = " 项目 ";

        await viewModel.ConfirmEditAsync();

        Assert.Contains(viewModel.Items, item => item.Name == "项目");
        Assert.Equal("项目", service.CreatedNames.Single());
        Assert.False(viewModel.IsEditing);
        Assert.Equal(string.Empty, viewModel.ErrorMessage);
    }

    [Fact]
    public async Task ConfirmEditAsync_KeepsEditModeWhenNameIsInvalid()
    {
        var service = new FakeFolderService
        {
            CreateException = new InvalidOperationException("文件夹名称已存在。")
        };
        var viewModel = new FolderManagementViewModel(service);
        await viewModel.LoadAsync();
        viewModel.BeginCreateCommand.Execute(null);
        viewModel.EditorText = "项目";

        await viewModel.ConfirmEditAsync();

        Assert.True(viewModel.IsEditing);
        Assert.Equal("文件夹名称已存在。", viewModel.ErrorMessage);
    }

    [Fact]
    public async Task RenameAndDeleteAsync_RaiseMutationEventsAndReloadCounts()
    {
        var folder = new FolderSummary("project", "项目", false, 2, true);
        var service = new FakeFolderService(folder);
        var viewModel = new FolderManagementViewModel(service);
        string? renamedFrom = null;
        string? renamedTo = null;
        string? deleted = null;
        viewModel.FolderRenamed += (oldName, newName) =>
        {
            renamedFrom = oldName;
            renamedTo = newName;
        };
        viewModel.FolderDeleted += name => deleted = name;
        await viewModel.LoadAsync();

        var item = Assert.Single(viewModel.Items);
        viewModel.BeginRename(item);
        viewModel.EditorText = "工作";
        await viewModel.ConfirmEditAsync();
        await viewModel.DeleteAsync(viewModel.Items.Single(item => item.Name == "工作"));

        Assert.Equal("项目", renamedFrom);
        Assert.Equal("工作", renamedTo);
        Assert.Equal("工作", deleted);
        Assert.Empty(viewModel.Items);
    }

    private sealed class FakeFolderService : IFolderService
    {
        private readonly List<FolderSummary> _folders;

        public FakeFolderService(params FolderSummary[] folders)
        {
            _folders = folders.ToList();
        }

        public List<string> CreatedNames { get; } = new();

        public Exception? CreateException { get; init; }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<FolderSummary>> GetFoldersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FolderSummary>>(_folders.ToList());

        public Task<FolderSummary> CreateAsync(string name, CancellationToken cancellationToken = default)
        {
            if (CreateException is not null)
            {
                throw CreateException;
            }

            var displayName = FolderNamePolicy.NormalizeDisplayName(name);
            CreatedNames.Add(displayName);
            var folder = new FolderSummary($"created-{_folders.Count}", displayName, false, 0, true);
            _folders.Add(folder);
            return Task.FromResult(folder);
        }

        public Task<FolderSummary> RenameAsync(FolderSummary folder, string newName, CancellationToken cancellationToken = default)
        {
            var replacement = folder with { Name = newName };
            _folders[_folders.FindIndex(item => item.Id == folder.Id)] = replacement;
            return Task.FromResult(replacement);
        }

        public Task DeleteAsync(FolderSummary folder, CancellationToken cancellationToken = default)
        {
            _folders.RemoveAll(item => item.Id == folder.Id);
            return Task.CompletedTask;
        }

        public Task<bool> IsValidMoveTargetAsync(string folderName, CancellationToken cancellationToken = default)
            => Task.FromResult(_folders.Any(item => item.Name == folderName));
    }
}
