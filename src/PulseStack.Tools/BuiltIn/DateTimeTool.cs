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

    public Task<IToolExecutionResult> ExecuteAsync(
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IToolExecutionResult>(
            ToolExecutionResult<string>.Success(GetCurrentTime()));
    }

    private static string GetCurrentTime()
    {
        return DateTimeOffset.UtcNow.ToString("O");
    }
}