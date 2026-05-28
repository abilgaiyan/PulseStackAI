using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Tools;
using PulseStack.Agents.Builders;
using PulseStack.Agents.Pipelines;
using PulseStack.Agents.Runtime.Observability;
using PulseStack.Showcase.Shared;

namespace PulseStack.Showcase.Scenarios;

internal static class SequentialPipelineScenario
{
    public static async Task RunAsync(
        IServiceProvider services)
    {
        ConsoleSection.Print(
            "Sequential Pipeline");

        var client =
            services.GetRequiredService<IChatClient>();

        var toolExecutor =
            services.GetRequiredService<IToolExecutor>();

        var runtimeObserver =
            services.GetRequiredService<CompositeRuntimeObserver>();

        var researcher =
            new AgentBuilder(
                "Researcher",
                client,
                toolExecutor)
            .WithInstructions("""
                Research the topic and provide concise findings.
                """)
            .Build();

        var summarizer =
            new AgentBuilder(
                "Summarizer",
                client,
                toolExecutor)
            .WithInstructions("""
                Summarize the findings into an executive summary.
                """)
            .Build();

        var pipeline =
            new SequentialPipeline(
                "ResearchPipeline",
                runtimeObserver)
            .Add(researcher)
            .Add(summarizer);

        var result =
            await pipeline.RunAsync(
                """
                Explain why orchestration runtimes matter
                for enterprise AI systems.
                """);

        Console.WriteLine(result.FinalOutput);
    }
}
