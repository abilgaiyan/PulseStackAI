using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Policies;
using PulseStack.Abstractions.Runtime.Realization.Binding;

namespace PulseStack.Core.Runtime.Realization.Binding;

public sealed class PolicyBindingResolver : IPolicyBindingResolver
{
    private readonly IReadOnlyDictionary<AssetReferenceKey, PolicyBindingRegistration> _bindings;
    private readonly IReadOnlyDictionary<string, IRuntimePolicy> _policies;

    public PolicyBindingResolver(
        IEnumerable<PolicyBindingRegistration> bindings,
        IEnumerable<IRuntimePolicy> policies)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(policies);

        _bindings = bindings.ToDictionary(
            binding => AssetReferenceKey.From(binding.Asset));
        _policies = policies.ToDictionary(policy => policy.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IRuntimePolicy Resolve(PolicyAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (!_bindings.TryGetValue(AssetReferenceKey.From(asset), out var binding))
        {
            throw new InvalidOperationException(
                $"Policy Asset '{asset.Urn.Value}' is not bound to a runtime Policy implementation.");
        }

        if (!_policies.TryGetValue(binding.PolicyName, out var policy))
        {
            throw new InvalidOperationException(
                $"Runtime Policy '{binding.PolicyName}' registered for Asset '{asset.Urn.Value}' could not be resolved.");
        }

        return policy;
    }
}
