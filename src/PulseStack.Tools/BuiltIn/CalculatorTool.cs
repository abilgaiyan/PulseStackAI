using PulseStack.Abstractions.Tools;
using System.Data;


namespace PulseStack.Tools.BuiltIn;

public sealed class CalculatorTool : ITool
{
    public string Name => "calculator";

    public string Description => "Evaluates basic math expressions.";

    public IReadOnlyCollection<string> Tags => ["utility", "math"];

    public Task<string> ExecuteAsync(string input, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = new DataTable().Compute(input, null);
            return Task.FromResult(result?.ToString() ?? "0");
        }
        catch
        {
            return Task.FromResult("Invalid expression.");
        }
    }
}