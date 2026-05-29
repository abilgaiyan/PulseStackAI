using PulseStack.Abstractions.Runtime.Costing;
using PulseStack.Abstractions.Runtime.Usage;

namespace PulseStack.Agents.Runtime.Costing;

public sealed class CostCalculator
    : ICostCalculator
{
    private const decimal TokensPerMillion = 1_000_000m;

    public AICost Calculate(
        AIUsage usage,
        ModelPricing pricing)
    {
        ArgumentNullException.ThrowIfNull(usage);
        ArgumentNullException.ThrowIfNull(pricing);

        return new AICost
        {
            PromptCost =
                usage.PromptTokens
                * pricing.InputPricePerMillionTokens
                / TokensPerMillion,

            CompletionCost =
                usage.CompletionTokens
                * pricing.OutputPricePerMillionTokens
                / TokensPerMillion,

            Currency =
                pricing.Currency
        };
    }
}
