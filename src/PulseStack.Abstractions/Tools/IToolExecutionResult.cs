namespace PulseStack.Abstractions.Tools;
public interface IToolExecutionResult
{
    bool IsSuccess { get; }

    string? ErrorMessage { get; }

    object? Value { get; }

    ToolExecutionMetadata Metadata { get; }

    IReadOnlyCollection<IToolArtifact> Artifacts { get; }
}
