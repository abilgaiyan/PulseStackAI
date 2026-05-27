using PulseStack.Showcase.Shared;

namespace PulseStack.Showcase.Scenarios;

internal static class ToolCallingScenario
{
    public static Task RunAsync(
        IServiceProvider services)
    {
        ConsoleSection.Print(
            "Tool Calling");

        Console.WriteLine(
            "Tool orchestration infrastructure is active:");

        Console.WriteLine("- Structured tool execution");
        Console.WriteLine("- Tool descriptors");
        Console.WriteLine("- Runtime tool governance");
        Console.WriteLine("- Tool execution tracking");
        Console.WriteLine("- Cancellation-aware tools");

        Console.WriteLine();

        Console.WriteLine(
            "Example Tool:");

        Console.WriteLine(
            "- ERPInvoiceLookupTool");
            

        return Task.CompletedTask;
    }
}
