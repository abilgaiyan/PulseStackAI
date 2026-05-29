using FluentAssertions;
using PulseStack.Abstractions.Runtime.Usage;
using PulseStack.Agents.Runtime.Costing;
using PulseStack.Agents.Runtime.Usage;
using Xunit;

namespace PulseStack.Tests.Agents;

public class RuntimeUsageTests
{
    [Fact]
    public void UsageAggregator_Should_Combine_Token_Counts()
    {
        var aggregator = new UsageAggregator();

        var usage = aggregator.Aggregate(
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
            },
            null
        ]);

        usage.Provider.Should().Be("OpenAI");
        usage.Model.Should().Be("GPT-5");
        usage.PromptTokens.Should().Be(2_000);
        usage.CompletionTokens.Should().Be(500);
        usage.TotalTokens.Should().Be(2_500);
    }

    [Fact]
    public void UsageAggregator_Should_Mark_Mixed_Providers_And_Models()
    {
        var aggregator = new UsageAggregator();

        var usage = aggregator.Aggregate(
        [
            new AIUsage
            {
                Provider = "OpenAI",
                Model = "GPT-5",
                PromptTokens = 1
            },
            new AIUsage
            {
                Provider = "Groq",
                Model = "llama",
                CompletionTokens = 2
            }
        ]);

        usage.Provider.Should().Be("Multiple");
        usage.Model.Should().Be("Multiple");
        usage.TotalTokens.Should().Be(3);
    }

    [Fact]
    public void CostCalculator_Should_Calculate_Per_Million_Token_Costs()
    {
        var calculator = new CostCalculator();

        var cost = calculator.Calculate(
            new AIUsage
            {
                Provider = "OpenAI",
                Model = "GPT-5",
                PromptTokens = 1_200,
                CompletionTokens = 300
            },
            new ModelPricing
            {
                Provider = "OpenAI",
                Model = "GPT-5",
                InputPricePerMillionTokens = 1.25m,
                OutputPricePerMillionTokens = 10.00m
            });

        cost.PromptCost.Should().Be(0.0015m);
        cost.CompletionCost.Should().Be(0.003m);
        cost.TotalCost.Should().Be(0.0045m);
        cost.Currency.Should().Be("USD");
    }
}
