namespace PulseStack.Abstractions.Tools;

public interface ITool
{
    string Name { get; }

    string Description { get; }

    IReadOnlyCollection<string> Tags { get; }

    Task<string> ExecuteAsync(
        string input,
        CancellationToken cancellationToken = default);
}