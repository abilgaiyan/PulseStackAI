using PulseStack.Abstractions.Tools;

namespace PulseStack.Tools.BuiltIn;

public sealed class DateTimeTool : ITool
{
    public string Name => "datetime";

    public string Description =>
        "Returns the current UTC date and time.";

    public IReadOnlyCollection<string> Tags =>
        ["datetime", "time", "utility"];

    public string Category => "Utility";

    public bool IsEnabled => true;

    public Task<string> ExecuteAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            DateTimeOffset.UtcNow.ToString("u"));
    }
}