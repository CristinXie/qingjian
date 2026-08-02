namespace QingJian.App.Models;

public sealed class Folder
{
    public const string SystemUncategorizedId = "system-uncategorized";

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public bool IsSystem { get; set; }
}
