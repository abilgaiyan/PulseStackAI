using PulseStack.Abstractions.Runtime.Usage;

namespace PulseStack.Abstractions.Runtime.Costing;

public interface IModelPricingCatalog
{
    ModelPricing? GetPricing(
        string provider,
        string model);
}
