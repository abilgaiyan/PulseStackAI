using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Runtime.Realization.Composition;
using PulseStack.Abstractions.Agents.Routing;
using PulseStack.Agents.Builders;
using PulseStack.Agents.Pipelines;
using PulseStack.Agents.Routing;
using PulseStack.Agents.Runtime.Observability;
using PulseStack.Showcase.Shared;
using PulseStack.Showcase.Infrastructure;

namespace PulseStack.Showcase.Scenarios;

internal static class RouterPipelineScenario
{
    public static async Task RunAsync(
        IServiceProvider services)
    {
        ConsoleSection.Print(
            "Router Pipeline");

        var composer =
            services.GetRequiredService<IAgentComposer>();

        var runtimeObserver =
            services.GetRequiredService<CompositeRuntimeObserver>();

        var modelReference =
            ShowcaseAssets.ModelReference;

        var legalDefinition =
            new AgentBuilder(
                "Legal")
            .WithGoal("""
                Review legal contracts and identify risks.
                """)
            .WithRole(
                "Legal Assistant.")
            .UseModel(modelReference)
            .Build();

        var financeDefinition =
            new AgentBuilder(
                "Finance")
            .WithGoal("""
                Analyze invoices and financial documents.
                """)
            .WithRole("Finance Assistant")
            .UseModel(modelReference)
            .Build();

        var supportDefinition =
            new AgentBuilder(
                "Support")
            .WithGoal("""
                Handle customer support requests.
                """)
            .WithRole("Support Assistant")
            .UseModel(modelReference)
            .Build();

        var contract =
            await composer.ComposeAsync(
                legalDefinition);
        var finance =
            await composer.ComposeAsync(
                financeDefinition);
        var support =
            await composer.ComposeAsync(
                supportDefinition);

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
            .Add(contract)
            .Add(finance)
            .Add(support);

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