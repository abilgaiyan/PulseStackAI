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

internal static class PartialFailureScenario
{
    public static async Task RunAsync(
        IServiceProvider services)
    {
        ConsoleSection.Print(
            "Partial Failure Pipeline");

        var observer =
            services.GetRequiredService<CompositeRuntimeObserver>();

        var researcher =
            new SuccessfulAgent(
                "Researcher",
                "ERP modernization risk analysis completed.");

        var faultyAgent =
            new FaultyAgent();

        var summarizer =
            new SuccessfulAgent(
                "Summarizer",
                "Successful analysis results summarized.");

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
