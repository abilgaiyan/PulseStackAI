using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Runtime.Realization.Composition;
using PulseStack.Abstractions.Workflows.Conditions;
using PulseStack.Agents.Builders;
using PulseStack.Agents.Pipelines;
using PulseStack.Agents.Runtime.Observability;
using PulseStack.Showcase.Shared;
using PulseStack.Showcase.Infrastructure;

namespace PulseStack.Showcase.Scenarios;

internal static class ConditionalPipelineScenario
{
    public static async Task RunAsync(
        IServiceProvider services)
    {
        ConsoleSection.Print(
            "Conditional Pipeline");

        var composer =
            services.GetRequiredService<IAgentComposer>();

        var runtimeObserver =
            services.GetRequiredService<CompositeRuntimeObserver>();

        var modelReference =
            ShowcaseAssets.ModelReference;

        var complianceDefinition =
            new AgentBuilder("Compliance")
                .WithGoal("""
                    Review the request from a compliance perspective.
                    Highlight risks and governance concerns.
                    """)
                .WithRole("Compliance Assistant")
                .UseModel(modelReference)
                .Build();

        var summaryDefinition =
            new AgentBuilder("Summary")
                .WithGoal("""
                    Provide a concise executive summary.
                    """)
                .WithRole("Summary Assistant")
                .UseModel(modelReference)
                .Build();

        var compliance =
            await composer.ComposeAsync(
                complianceDefinition);

        var summary =
            await composer.ComposeAsync(
                summaryDefinition);

        var condition =
            new DelegateCondition(
                context =>
                {
                    var input =
                        context.Input ?? string.Empty;

                    return input.Contains(
                        "high risk",
                        StringComparison.OrdinalIgnoreCase);
                },
                "High Risk Detection");

        var pipeline =
            new ConditionalPipeline(
                "RiskAssessment",
                condition,
                runtimeObserver)
            .AddTrueAgent(compliance)
            .AddFalseAgent(summary);

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