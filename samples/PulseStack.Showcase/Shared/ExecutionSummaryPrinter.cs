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
            Console.WriteLine(
                $"- {step.AgentName}");
        }

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
}
