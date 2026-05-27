using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Tools;
using PulseStack.Showcase.Shared;
using PulseStack.Showcase.Tools;

namespace PulseStack.Showcase.Scenarios;

internal static class ERPInvoiceLookupToolCallingScenario
{
    public static async Task RunAsync(
        IServiceProvider services)
    {
        ConsoleSection.Print(
            "ERP Invoice Lookup Tool");

        var tool =
            new ERPInvoiceLookupTool();

        var toolExecutor =
            services.GetRequiredService<IToolExecutor>();

        var executionContext =
            new ToolExecutionContext
            {
                ToolName =
                    tool.Name,

                Input =
                    """
                    {
                      "invoiceId": "INV-1001"
                    }
                    """,

                Services =
                    services
            };

        Console.WriteLine(
            "Executing Tool...");

        Console.WriteLine();

        var result =
            await toolExecutor.ExecuteAsync(
                tool,
                executionContext);

        Console.WriteLine(
            $"Tool Name : {tool.Name}");

        Console.WriteLine(
            $"Category  : {tool.Category}");

        Console.WriteLine();

        Console.WriteLine(
            "Tool Result");

        Console.WriteLine(
            "-----------");

       Console.WriteLine(
            result.IsSuccess
                ? result.Value
                : result.ErrorMessage);

        Console.WriteLine();

        Console.WriteLine(
            "Tool Metadata");

        Console.WriteLine(
            "-------------");

        Console.WriteLine(
            $"Tags : {string.Join(", ", tool.Tags)}");
    }
}