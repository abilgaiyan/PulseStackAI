using PulseStack.Abstractions.Runtime.Usage;

namespace PulseStack.Abstractions.Runtime.Costing;
public interface IModelPricingProvider
{
    bool TryGetPricing(
        string provider,
        string model,
        out ModelPricing pricing);
}