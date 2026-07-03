using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Workflow.Conditions;
using PulseStack.Abstractions.Tools;
using PulseStack.Agents.Builders;
using PulseStack.Agents.Pipelines;
using PulseStack.Agents.Runtime.Observability;
using PulseStack.Showcase.Shared;

namespace PulseStack.Showcase.Scenarios;

internal static class ConditionalPipelineScenario
{
    public static async Task RunAsync(
        IServiceProvider services)
    {
        ConsoleSection.Print(
            "Conditional Pipeline");

        var client =
            services.GetRequiredService<IChatClient>();

        var toolExecutor =
            services.GetRequiredService<IToolExecutor>();

        var runtimeObserver =
            services.GetRequiredService<CompositeRuntimeObserver>();

        var complianceAgent =
            new AgentBuilder(
                "Compliance",
                client,
                toolExecutor)
            .WithInstructions("""
                Review the request from a compliance perspective.
                Highlight risks and governance concerns.
                """)
            .Build();

        var summaryAgent =
            new AgentBuilder(
                "Summary",
                client,
                toolExecutor)
            .WithInstructions("""
                Provide a concise executive summary.
                """)
            .Build();

        var condition =
            new DelegateCondition(
                context =>
                {
                    var input =
                        context.Input
                        ?? string.Empty;

                    return input.Contains(
                        "high risk",
                        StringComparison.OrdinalIgnoreCase);
                }, "High Risk Detection");

        var pipeline =
            new ConditionalPipeline(
                "RiskAssessment",
                condition,
                runtimeObserver)
            .AddTrueAgent(complianceAgent)
            .AddFalseAgent(summaryAgent);

        Console.WriteLine();
        Console.WriteLine(
            "Input: High risk vendor contract");

        var highRiskResult =
            await pipeline.RunDetailedAsync(
                "Review this high risk vendor contract.");

        ExecutionSummaryPrinter.Print(
            highRiskResult);

        Console.WriteLine();
        Console.WriteLine(
            "Input: Monthly project update");

        var normalResult =
            await pipeline.RunDetailedAsync(
                "Prepare a monthly project update.");

        ExecutionSummaryPrinter.Print(
            normalResult);
    }
}