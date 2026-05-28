using PulseStack.Abstractions.Agents;

namespace PulseStack.Showcase.Shared;

internal static class ExecutionSummaryPrinter
{
    public static void Print(
        PipelineResult result)
    {
        Console.WriteLine();

        Console.WriteLine(
            "Execution Summary");

        Console.WriteLine(
            "-----------------");

        Console.WriteLine(
            $"Steps : {result.Steps.Count}");

        Console.WriteLine();

        Console.WriteLine(
            "Pipeline Steps");

        Console.WriteLine(
            "--------------");

        foreach (var step in result.Steps)
        {
            Console.WriteLine(
                $"✓ {step.AgentName}");
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
