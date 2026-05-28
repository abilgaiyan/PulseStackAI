using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Tools;
using PulseStack.Agents.Builders;
using PulseStack.Agents.Pipelines;
using PulseStack.Agents.Runtime.Observability;
using PulseStack.Showcase.Agents;
using PulseStack.Showcase.Shared;

namespace PulseStack.Showcase.Scenarios;

internal static class PartialFailureScenario
{
    public static async Task RunAsync(
        IServiceProvider services)
    {
        ConsoleSection.Print(
            "Partial Failure Pipeline");

        var client =
            services.GetRequiredService<IChatClient>();

        var toolExecutor =
            services.GetRequiredService<IToolExecutor>();

        var observer =
            services.GetRequiredService<IRuntimeObserver>();

        var researcher =
            new AgentBuilder(
                "Researcher",
                client,
                toolExecutor)
            .WithInstructions("""
                Research enterprise ERP modernization risks.
                """)
            .Build();

        var summarizer =
            new AgentBuilder(
                "Summarizer",
                client,
                toolExecutor)
            .WithInstructions("""
                Summarize all successful analysis results.
                """)
            .Build();

        var faultyAgent =
            new FaultyAgent();

        var pipeline =
            new SequentialPipeline(
                "ResilientPipeline",
                observer)
            .WithPolicy(
                new PipelineExecutionPolicy
                {
                    ContinueOnAgentFailure = true,
                    CaptureDiagnostics = true
                })
            .Add(researcher)
            .Add(faultyAgent)
            .Add(summarizer);

        var result =
            await pipeline.RunDetailedAsync(
                """
                Analyze ERP modernization risks
                for enterprise transformation.
                """);

        ExecutionSummaryPrinter.Print(
            result);
    }
}