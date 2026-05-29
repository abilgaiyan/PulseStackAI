using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Tools;
using PulseStack.Agents.Pipelines;
using PulseStack.Agents.Runtime.Observability;
using PulseStack.Showcase.Agents;
using PulseStack.Showcase.Shared;

namespace PulseStack.Showcase.Scenarios;

internal static class TimeoutScenario
{
    public static async Task RunAsync(
        IServiceProvider services)
    {
        ConsoleSection.Print(
            "Timeout Pipeline");

        var observer =
            services.GetRequiredService<IRuntimeObserver>();

        var pipeline =
            new SequentialPipeline(
                "TimeoutPipeline",
                observer)
            .WithPolicy(
                new PipelineExecutionPolicy
                {
                    Timeout = TimeSpan.FromSeconds(3)
                })
            .Add(new SlowAgent());

        var result =
            await pipeline.RunDetailedAsync(
                "Run long analysis");

        ExecutionSummaryPrinter.Print(
            result);
    }
}