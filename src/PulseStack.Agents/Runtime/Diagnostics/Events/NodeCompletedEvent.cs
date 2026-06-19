
namespace PulseStack.Agents.Runtime.Diagnostics.Events;

public sealed record NodeCompletedEvent(
    Guid ExecutionId,
    DateTimeOffset Timestamp,
    string NodeName,
    string NodeType,
    bool Success)
    : IRuntimeEvent
{
    public IReadOnlyDictionary<string, object?> Metadata
        => new Dictionary<string, object?>
        {
            ["NodeName"] = NodeName,
            ["NodeType"] = NodeType,
            ["Success"] = Success
        };
}
