using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Runtime.Realization.Composition;
using PulseStack.Agents.Builders;
using PulseStack.Agents.Pipelines;
using PulseStack.Agents.Runtime.Observability;
using PulseStack.Showcase.Shared;
using PulseStack.Showcase.Infrastructure;

namespace PulseStack.Showcase.Scenarios;

internal static class SequentialPipelineScenario
{
    public static async Task RunAsync(
        IServiceProvider services)
    {
        ConsoleSection.Print(
            "Sequential Pipeline");

        var composer =
            services.GetRequiredService<IAgentComposer>();

        var runtimeObserver =
            services.GetRequiredService<CompositeRuntimeObserver>();

        var modelReference =
            ShowcaseAssets.ModelReference;

        var researcherDefinition =
            new AgentBuilder("Researcher")
                .WithGoal(
                    "Research the topic and provide concise findings.")
                .WithRole(
                    "Research assistant.")
                .UseModel(modelReference)
                .Build();

        var summarizerDefinition =
            new AgentBuilder("Summarizer")
                .WithGoal(
                    "Summarize the findings into an executive summary.")
                .WithRole(
                    "Executive summarizer.")
                .UseModel(modelReference)
                .Build();

        var researcher =
            await composer.ComposeAsync(
                researcherDefinition);

        var summarizer =
            await composer.ComposeAsync(
                summarizerDefinition);

        var pipeline =
            new SequentialPipeline(
                "ResearchPipeline",
                runtimeObserver)
            .Add(researcher)
            .Add(summarizer);

        var result =
            await pipeline.RunDetailedAsync(
                """
                Explain why orchestration runtimes matter
                for enterprise AI systems.
                """);

        ExecutionSummaryPrinter.Print(result);
    }
}