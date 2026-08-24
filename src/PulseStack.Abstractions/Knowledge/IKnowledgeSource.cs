namespace PulseStack.Abstractions.Knowledge;

public sealed record KnowledgeQuery
{
    public required string Text { get; init; }
}

public sealed record KnowledgeResult
{
    public IReadOnlyCollection<string> Items { get; init; } = [];
}

public interface IKnowledgeSource
{
    string Name { get; }

    Task<KnowledgeResult> RetrieveAsync(
        KnowledgeQuery query,
        CancellationToken cancellationToken = default);
}
