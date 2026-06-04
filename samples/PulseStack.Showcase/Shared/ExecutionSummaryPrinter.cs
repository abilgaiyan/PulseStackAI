using PulseStack.Abstractions.Runtime.Pipeline;

namespace PulseStack.Showcase.Shared;

internal static class ExecutionSummaryPrinter
{
    public static void Print(
        PipelineExecutionResult result)
    {
        Console.WriteLine();

        Console.WriteLine(
            "Execution Summary");

        Console.WriteLine(
            "-----------------");

        Console.WriteLine(
            $"ExecutionId : {result.ExecutionId}");

        Console.WriteLine(
            $"Status      : {result.Status}");

        Console.WriteLine(
            $"Duration    : {result.Duration.TotalMilliseconds:n0} ms");

        Console.WriteLine(
            $"Steps       : {result.Steps.Count}");

        Console.WriteLine(
            $"Errors      : {result.Errors.Count}");

        Console.WriteLine();

        Console.WriteLine(
            "Pipeline Steps");

        Console.WriteLine(
            "--------------");

        foreach (var step in result.Steps)
        {
            var symbol = step.Success  ? "✓" : "✗";
            Console.WriteLine($"{symbol} {step.AgentName,-20} {step.Duration.TotalMilliseconds,8:N0} ms   Retries : {step.RetryCount}");
        }

        Console.WriteLine();

        Console.WriteLine(
            "Execution Usage");

        Console.WriteLine(
            "---------------");

        if (result.TotalUsage.TotalTokens > 0)
        {
            Console.WriteLine(
                $"Provider           : {FormatUsageValue(result.TotalUsage.Provider)}");

            Console.WriteLine(
                $"Model              : {FormatUsageValue(result.TotalUsage.Model)}");

            Console.WriteLine(
                $"Prompt Tokens      : {result.TotalUsage.PromptTokens}");

            Console.WriteLine(
                $"Completion Tokens  : {result.TotalUsage.CompletionTokens}");

            Console.WriteLine(
                $"Total Tokens       : {result.TotalUsage.TotalTokens}");
        }
        else
        {
            Console.WriteLine(
                "Usage information not available.");
        }

        Console.WriteLine();

        if (result.TotalCost is not null)
        {
            Console.WriteLine();

            Console.WriteLine(
                "Execution Cost");

            Console.WriteLine(
                "--------------");

            Console.WriteLine(
                $"Prompt Cost       : {result.TotalCost.PromptCost:F6} USD");

            Console.WriteLine(
                $"Completion Cost   : {result.TotalCost.CompletionCost:F6} USD");

            Console.WriteLine(
                $"Total Cost        : {result.TotalCost.TotalCost:F6} USD");
        }        

        Console.WriteLine();
        
        Console.WriteLine(
            "Tool Summary");

        Console.WriteLine(
            "------------");

        Console.WriteLine(
            $"Total Invocations : {result.ToolSummary.TotalInvocations}");

        Console.WriteLine(
            $"Successful        : {result.ToolSummary.SuccessfulExecutions}");

        Console.WriteLine(
            $"Failed            : {result.ToolSummary.FailedExecutions}");

        Console.WriteLine(
            $"Total Duration    : {result.ToolSummary.TotalDuration.TotalMilliseconds:n0} ms");

        if (result.Errors.Count > 0)
        {
            Console.WriteLine();

            Console.WriteLine(
                "Errors");

            Console.WriteLine(
                "------");

            foreach (var error in result.Errors)
            {
                Console.WriteLine(
                    $"- {error.Code} : {error.Message}");
            }
        }

        Console.WriteLine();

        Console.WriteLine(
            "Final Output");

        Console.WriteLine(
            "-------------");

        Console.WriteLine(
            result.FinalOutput);
    }

    private static string FormatUsageValue(
        string value)
        => string.IsNullOrWhiteSpace(value)
            ? "Not reported"
            : value;
}
