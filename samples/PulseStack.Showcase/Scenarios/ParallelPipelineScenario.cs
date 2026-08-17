using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Runtime.Realization.Composition;
using PulseStack.Agents.Builders;
using PulseStack.Agents.Pipelines;
using PulseStack.Agents.Runtime.Observability;
using PulseStack.Showcase.Infrastructure;
using PulseStack.Showcase.Shared;

namespace PulseStack.Showcase.Scenarios;

internal static class ParallelPipelineScenario
{
    public static async Task RunAsync(
        IServiceProvider services)
    {
        ConsoleSection.Print(
            "Parallel Pipeline");

        var composer =
            services.GetRequiredService<IAgentComposer>();

        var modelReference =
            ShowcaseAssets.ModelReference;

        var runtimeObserver =
            services.GetRequiredService<CompositeRuntimeObserver>();

        var analystDefinition =
            new AgentBuilder("Analyst")
                .WithGoal("""
                    Analyze business risks.
                    """)
                .WithRole("Analyst Assistant")
                .UseModel(modelReference)
                .Build();

        var architectDefinition =
            new AgentBuilder("Architect")
                .WithGoal("""
                    Analyze system architecture risks.
                    """)
                .WithRole("Architect Assistant")
                .UseModel(modelReference)
                .Build();

        var analyst =
            await composer.ComposeAsync(
                analystDefinition);

        var architect =
            await composer.ComposeAsync(
                architectDefinition);

        var pipeline =
            new ParallelPipeline(
                "ParallelAnalysis",
                runtimeObserver)
            .Add(analyst)
            .Add(architect);

        var result =
            await pipeline.RunDetailedAsync(
                """
                Enterprise ERP modernization project.
                """);

        ExecutionSummaryPrinter.Print(result);
    }
}