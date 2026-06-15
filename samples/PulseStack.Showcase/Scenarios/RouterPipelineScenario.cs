using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using PulseStack.Agents.Builders;
using PulseStack.Agents.Pipelines;
using PulseStack.Agents.Routing;
using PulseStack.Agents.Runtime.Observability;
using PulseStack.Abstractions.Agents.Routing;
using PulseStack.Abstractions.Tools;
using PulseStack.Showcase.Shared;

namespace PulseStack.Showcase.Scenarios;

internal static class RouterPipelineScenario
{
    public static async Task RunAsync(
        IServiceProvider services)
    {
        ConsoleSection.Print(
            "Router Pipeline");

        var client =
            services.GetRequiredService<IChatClient>();

        var toolExecutor =
            services.GetRequiredService<IToolExecutor>();

        var runtimeObserver =
            services.GetRequiredService<CompositeRuntimeObserver>();

        var legalAgent =
            new AgentBuilder(
                "Legal",
                client,
                toolExecutor)
            .WithInstructions("""
                Review legal contracts and identify risks.
                """)
            .Build();

        var financeAgent =
            new AgentBuilder(
                "Finance",
                client,
                toolExecutor)
            .WithInstructions("""
                Analyze invoices and financial documents.
                """)
            .Build();

        var supportAgent =
            new AgentBuilder(
                "Support",
                client,
                toolExecutor)
            .WithInstructions("""
                Handle customer support requests.
                """)
            .Build();

        IAgentSelector selector =
            new KeywordAgentSelector(
                new Dictionary<string, string>
                {
                    ["contract"] = "Legal",
                    ["invoice"] = "Finance",
                    ["ticket"] = "Support",
                    ["support"] = "Support"
                });

        var pipeline =
            new RouterPipeline(
                "RequestRouter",
                selector,
                runtimeObserver)
            .Add(legalAgent)
            .Add(financeAgent)
            .Add(supportAgent);

        Console.WriteLine();
        Console.WriteLine(
            "Input: Review this vendor contract");

        var legalResult =
            await pipeline.RunDetailedAsync(
                "Review this vendor contract.");

        ExecutionSummaryPrinter.Print(
            legalResult);

        Console.WriteLine();
        Console.WriteLine(
            "Input: Process this invoice");

        var financeResult =
            await pipeline.RunDetailedAsync(
                "Process this invoice.");

        ExecutionSummaryPrinter.Print(
            financeResult);

        Console.WriteLine();
        Console.WriteLine(
            "Input: Customer support ticket");

        var supportResult =
            await pipeline.RunDetailedAsync(
                "Customer support ticket.");

        ExecutionSummaryPrinter.Print(
            supportResult);
    }
}