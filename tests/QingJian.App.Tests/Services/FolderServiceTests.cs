using QingJian.App.Data;
using QingJian.App.Models;
using QingJian.App.Services;
using Xunit;

namespace QingJian.App.Tests.Services;

public sealed class FolderServiceTests
{
    [Fact]
    public async Task CreateAsync_TrimsAndPersistsValidName()
    {
        var repository = new FakeFolderRepository();
        var service = new FolderService(repository, () => UtcNow);

        var created = await service.CreateAsync("  项目  ");

        Assert.Equal("项目", created.Name);
        Assert.Equal("项目", repository.Added!.Name);
        Assert.Equal(FolderNamePolicy.NormalizeKey("项目"), repository.Added.NormalizedName);
    }

    [Fact]
    public async Task CreateAsync_RejectsCaseInsensitiveDuplicate()
    {
        var repository = new FakeFolderRepository
        {
            Summaries = new[]
            {
                new FolderSummary("folder-1", "项目", false, 0, true)
            }
        };
        var service = new FolderService(repository, () => UtcNow);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync("项目".ToUpperInvariant()));

        Assert.Equal("文件夹名称已存在。", error.Message);
    }

    [Fact]
    public async Task RenameAsync_AllowsTheSameNameButRejectsAnotherFolderName()
    {
        var repository = new FakeFolderRepository
        {
            Summaries = new[]
            {
                new FolderSummary("folder-1", "项目", false, 0, true),
                new FolderSummary("folder-2", "工作", false, 0, true)
            }
        };
        var service = new FolderService(repository, () => UtcNow);

        var same = await service.RenameAsync(repository.Summaries[0], " 项目 ");
        Assert.Equal("项目", same.Name);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RenameAsync(repository.Summaries[0], "工作"));
        Assert.Equal("文件夹名称已存在。", error.Message);
    }

    [Fact]
    public async Task DeleteAsync_RejectsUncategorized()
    {
        var service = new FolderService(new FakeFolderRepository(), () => UtcNow);
        var system = new FolderSummary(Folder.SystemUncategorizedId, "未分类", true, 3, true);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(system));

        Assert.Equal("未分类文件夹不能删除。", error.Message);
    }

    [Fact]
    public async Task IsValidMoveTargetAsync_RejectsLegacyOverlengthFolder()
    {
        var legacyName = string.Concat(Enumerable.Repeat("旧", 31));
        var repository = new FakeFolderRepository
        {
            Summaries = new[]
            {
                new FolderSummary("legacy", legacyName, false, 2, false),
                new FolderSummary(Folder.SystemUncategorizedId, "未分类", true, 0, true)
            }
        };
        var service = new FolderService(repository, () => UtcNow);

        Assert.False(await service.IsValidMoveTargetAsync(legacyName));
        Assert.True(await service.IsValidMoveTargetAsync("未分类"));
    }

    private static readonly DateTime UtcNow = new(2026, 7, 26, 10, 0, 0, DateTimeKind.Utc);

    private sealed class FakeFolderRepository : IFolderRepository
    {
        public IReadOnlyList<FolderSummary> Summaries { get; init; } = Array.Empty<FolderSummary>();

        public Folder? Added { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<FolderSummary>> GetSummariesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Summaries);

        public Task<bool> NormalizedNameExistsAsync(
            string normalizedName,
            string? excludedFolderId = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Summaries.Any(folder =>
                folder.Id != excludedFolderId
                && FolderNamePolicy.NormalizeKey(folder.Name) == normalizedName));
        }

        public Task AddAsync(Folder folder, CancellationToken cancellationToken = default)
        {
            Added = folder;
            return Task.CompletedTask;
        }

        public Task RenameAsync(
            string folderId,
            string oldName,
            string newName,
            string normalizedName,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteAsync(string folderId, string folderName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
