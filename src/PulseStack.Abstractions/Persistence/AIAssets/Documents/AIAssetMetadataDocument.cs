namespace PulseStack.Abstractions.Persistence.AIAssets.Documents;

public sealed record AIAssetMetadataDocument
{
    private readonly IReadOnlyList<string> tags;

    public AIAssetMetadataDocument(
        string name,
        string? description = null,
        string? author = null,
        IEnumerable<string>? tags = null,
        string? category = null)
    {
        Name = name;
        Description = description;
        Author = author;
        this.tags = Array.AsReadOnly(tags?.ToArray() ?? Array.Empty<string>());
        Category = category;
    }

    public string Name { get; }

    public string? Description { get; }

    public string? Author { get; }

    public IReadOnlyList<string> Tags => tags;

    public string? Category { get; }
}
