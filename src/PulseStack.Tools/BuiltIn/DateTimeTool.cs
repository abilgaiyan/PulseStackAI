using PulseStack.Abstractions.Tools;

namespace PulseStack.Tools.BuiltIn;

public sealed class DateTimeTool : ITool
{
    public string Name => "datetime";

    public string Description =>
        "Returns the current UTC date and time.";

    public string Category => "Utility";

    public bool IsEnabled => true;

    public IReadOnlyCollection<string> Tags =>
        ["utility", "datetime", "clock"];

    public Task<string> ExecuteAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        return Task.FromResult(
            $"Current UTC date/time: {now:O}");
    }
}