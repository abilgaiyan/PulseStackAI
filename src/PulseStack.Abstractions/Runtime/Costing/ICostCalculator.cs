using PulseStack.Abstractions.Runtime.Usage;

namespace PulseStack.Abstractions.Runtime.Costing;

public interface ICostCalculator
{
    AICost Calculate(
        AIUsage usage,
        ModelPricing pricing);
}
