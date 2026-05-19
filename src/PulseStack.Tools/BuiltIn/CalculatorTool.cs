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

   public async Task<IToolExecutionResult> ExecuteAsync(
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var expression = context.Input?.ToString();

        if (string.IsNullOrWhiteSpace(expression))
        {
            return ToolExecutionResult<string>.Failure(
                "Expression is required.");
        }

        try
        {
            var result = EvaluateExpression(expression);

            return ToolExecutionResult<double>.Success(
                result);
        }
        catch (Exception ex)
        {
            return ToolExecutionResult<string>.Failure(
                ex.Message);
        }
    }

    private static double EvaluateExpression(
        string expression)
    {
        return Convert.ToDouble(
            new DataTable().Compute(expression, null));
    }    
}