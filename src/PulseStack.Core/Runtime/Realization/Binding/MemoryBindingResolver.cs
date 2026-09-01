using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Memory;
using PulseStack.Abstractions.Runtime.Realization.Binding;

namespace PulseStack.Core.Runtime.Realization.Binding;

public sealed class MemoryBindingResolver : IMemoryBindingResolver
{
    private readonly IReadOnlyDictionary<AssetReferenceKey, MemoryBindingRegistration> _bindings;
    private readonly IReadOnlyDictionary<string, IConversationMemoryFactory> _factories;

    public MemoryBindingResolver(
        IEnumerable<MemoryBindingRegistration> bindings,
        IEnumerable<IConversationMemoryFactory> factories)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(factories);

        _bindings = bindings.ToDictionary(
            binding => AssetReferenceKey.From(binding.Asset));
        _factories = factories.ToDictionary(factory => factory.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IConversationMemory Resolve(MemoryAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (!_bindings.TryGetValue(AssetReferenceKey.From(asset), out var binding))
        {
            throw new InvalidOperationException(
                $"Memory Asset '{asset.Urn.Value}' is not bound to a runtime Memory factory.");
        }

        if (!_factories.TryGetValue(binding.FactoryName, out var factory))
        {
            throw new InvalidOperationException(
                $"Runtime Memory factory '{binding.FactoryName}' registered for Asset '{asset.Urn.Value}' could not be resolved.");
        }

        return factory.Create();
    }
}
