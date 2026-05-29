using PulseStack.Abstractions.Runtime.Costing;
using PulseStack.Abstractions.Runtime.Usage;

namespace PulseStack.Showcase.Infrastructure;

internal sealed class StaticModelPricingCatalog
    : IModelPricingCatalog
{
    private readonly IReadOnlyList<ModelPricing> _pricing =
    [
        new ModelPricing
        {
            Provider = "OpenAI",
            Model = "GPT-5",
            InputPricePerMillionTokens = 1.25m,
            OutputPricePerMillionTokens = 10.00m,
            Currency = "USD"
        }
    ];

    public ModelPricing? GetPricing(
        string provider,
        string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        return _pricing.FirstOrDefault(pricing =>
            string.Equals(
                pricing.Provider,
                provider,
                StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                pricing.Model,
                model,
                StringComparison.OrdinalIgnoreCase));
    }
}
