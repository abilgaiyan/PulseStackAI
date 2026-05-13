using System.Data;
using PulseStack.Abstractions.Tools;

namespace PulseStack.Tools.BuiltIn;

public sealed class CalculatorTool : ITool
{
    public string Name => "calculator";

    public string Description =>
        "Evaluates basic math expressions.";

    public string Category => "Utility";

    public bool IsEnabled => true;

    public IReadOnlyCollection<string> Tags =>
        ["utility", "math"];

    public Task<ToolExecutionResult> ExecuteAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // TODO:
            // Replace DataTable.Compute with a dedicated math parser
            // for safer and more predictable evaluation.

            var result = new DataTable()
                .Compute(input, null);

            return Task.FromResult(
                new ToolExecutionResult(
                    Success: true,
                    Output: result?.ToString() ?? "0"));
        }
        catch
        {
            return Task.FromResult(
                new ToolExecutionResult(
                    Success: false,
                    Output: string.Empty,
                    Error: "Invalid expression."));
        }
    }
}