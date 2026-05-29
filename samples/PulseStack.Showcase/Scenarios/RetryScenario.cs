using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Tools;
using PulseStack.Agents.Pipelines;
using PulseStack.Agents.Runtime.Observability;
using PulseStack.Showcase.Agents;
using PulseStack.Showcase.Shared;

namespace PulseStack.Showcase.Scenarios;

internal static class RetryScenario
{
    public static async Task RunAsync(
        IServiceProvider services)
    {
        ConsoleSection.Print(
            "Retry Pipeline");

        var observer =
            services.GetRequiredService<IRuntimeObserver>();

        var pipeline =
            new SequentialPipeline(
                "RetryPipeline",
                observer)
            .WithPolicy(
                new PipelineExecutionPolicy
                {
                    MaxRetries = 2,
                    ContinueOnAgentFailure = false,
                    CaptureDiagnostics = true
                })
            .Add(new FlakyAgent());

        var result =
            await pipeline.RunDetailedAsync(
                """
                Validate transient orchestration recovery.
                """);

        ExecutionSummaryPrinter.Print(
            result);
    }
}