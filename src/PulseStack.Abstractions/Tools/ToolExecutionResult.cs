namespace PulseStack.Abstractions.Tools;

public class ToolExecutionResult
    : IToolExecutionResult
{
    public bool IsSuccess { get; init; }

    public string? ErrorMessage { get; init; }

    public object? Value { get; init; }

    public ToolExecutionMetadata Metadata { get; init; }
        = new();

    public IReadOnlyCollection<IToolArtifact> Artifacts { get; init; }
        = [];

    public static ToolExecutionResult Success(
        object? value = null)
        => new()
        {
            IsSuccess = true,
            Value = value
        };

    public static ToolExecutionResult Failure(
        string error)
        => new()
        {
            IsSuccess = false,
            ErrorMessage = error
        };
}
