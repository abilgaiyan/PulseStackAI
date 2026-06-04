using PulseStack.Abstractions.Runtime.Costing;
using PulseStack.Abstractions.Runtime.Usage;

namespace PulseStack.Agents.Runtime.Costing;
internal sealed class DefaultModelPricingProvider
    : IModelPricingProvider
{
    private readonly Dictionary<string, ModelPricing> _pricing =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["openai/gpt-4o-mini"] =
                new ModelPricing
                {
                    InputPricePerMillionTokens = 0.15m,
                    OutputPricePerMillionTokens = 0.60m
                }
        };
        public bool TryGetPricing(
            string provider,
            string model,
            out ModelPricing pricing)
        {
            return _pricing.TryGetValue(
                model,
                out pricing!);
        }
}
