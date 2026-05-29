using PulseStack.Abstractions.Runtime.Usage;
using PulseStack.Agents.Runtime.Costing;
using PulseStack.Agents.Runtime.Usage;
using PulseStack.Showcase.Infrastructure;
using PulseStack.Showcase.Shared;

namespace PulseStack.Showcase.Scenarios;

internal static class UsageAndCostScenario
{
    public static Task RunAsync()
    {
        ConsoleSection.Print(
            "AI Usage & Cost Intelligence");

        var usage =
            new UsageAggregator()
                .Aggregate(
                [
                    new AIUsage
                    {
                        Provider = "OpenAI",
                        Model = "GPT-5",
                        PromptTokens = 1_200,
                        CompletionTokens = 300
                    },
                    new AIUsage
                    {
                        Provider = "OpenAI",
                        Model = "GPT-5",
                        PromptTokens = 800,
                        CompletionTokens = 200
                    }
                ]);

        var pricingCatalog =
            new StaticModelPricingCatalog();

        var pricing =
            pricingCatalog.GetPricing(
                usage.Provider,
                usage.Model);

        Console.WriteLine(
            "Mocked runtime usage was aggregated from agent execution results.");

        Console.WriteLine();
        Console.WriteLine(
            "Execution Usage");

        Console.WriteLine(
            "---------------");

        Console.WriteLine(
            $"Provider           : {usage.Provider}");

        Console.WriteLine(
            $"Model              : {usage.Model}");

        Console.WriteLine(
            $"Prompt Tokens      : {usage.PromptTokens}");

        Console.WriteLine(
            $"Completion Tokens  : {usage.CompletionTokens}");

        Console.WriteLine(
            $"Total Tokens       : {usage.TotalTokens}");

        if (pricing is null)
        {
            Console.WriteLine();
            Console.WriteLine(
                "Pricing information not available.");

            return Task.CompletedTask;
        }

        var cost =
            new CostCalculator()
                .Calculate(
                    usage,
                    pricing);

        Console.WriteLine();
        Console.WriteLine(
            "Estimated Cost");

        Console.WriteLine(
            "--------------");

        Console.WriteLine(
            $"Prompt Cost        : {cost.PromptCost:0.000000} {cost.Currency}");

        Console.WriteLine(
            $"Completion Cost    : {cost.CompletionCost:0.000000} {cost.Currency}");

        Console.WriteLine(
            $"Total Cost         : {cost.TotalCost:0.000000} {cost.Currency}");

        Console.WriteLine();
        Console.WriteLine(
            "Pricing is showcase-only demonstration data.");

        return Task.CompletedTask;
    }
}
