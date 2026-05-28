using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Tools;
using PulseStack.Agents.Builders;
using PulseStack.Agents.Pipelines;
using PulseStack.Agents.Runtime.Observability;
using PulseStack.Showcase.Shared;

namespace PulseStack.Showcase.Scenarios;

internal static class ParallelPipelineScenario
{
    public static async Task RunAsync(
        IServiceProvider services)
    {
        ConsoleSection.Print(
            "Parallel Pipeline");

        var client =
            services.GetRequiredService<IChatClient>();

        var toolExecutor =
            services.GetRequiredService<IToolExecutor>();

        var runtimeObserver =
            services.GetRequiredService<CompositeRuntimeObserver>();

        var analyst =
            new AgentBuilder(
                "Analyst",
                client,
                toolExecutor)
            .WithInstructions("""
                Analyze business risks.
                """)
            .Build();

        var architect =
            new AgentBuilder(
                "Architect",
                client,
                toolExecutor)
            .WithInstructions("""
                Analyze system architecture risks.
                """)
            .Build();

        var pipeline =
            new ParallelPipeline(
                "ParallelAnalysis",
                runtimeObserver)
            .Add(analyst)
            .Add(architect);

        var result =
            await pipeline.RunAsync(
                """
                Enterprise ERP modernization project.
                """);

        Console.WriteLine(result.FinalOutput);
    }
}
