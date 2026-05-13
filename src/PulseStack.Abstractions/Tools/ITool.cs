namespace PulseStack.Abstractions.Tools;

public interface ITool
{
    string Name { get; }

    string Description { get; }

    string Category { get; }

    bool IsEnabled => true;

    IReadOnlyCollection<string> Tags { get; }

    Task<ToolExecutionResult> ExecuteAsync(
        string input,
        CancellationToken cancellationToken = default);
}