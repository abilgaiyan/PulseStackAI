using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Policies;

namespace PulseStack.Abstractions.Runtime.Realization.Binding;

public interface IPolicyBindingResolver
{
    IRuntimePolicy Resolve(PolicyAsset asset);
}
