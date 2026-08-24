using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Knowledge;
using PulseStack.Abstractions.Runtime.Realization.Binding;

namespace PulseStack.Core.Runtime.Realization.Binding;

public sealed class KnowledgeBindingResolver : IKnowledgeBindingResolver
{
    private readonly IReadOnlyDictionary<AssetId, KnowledgeBindingRegistration> _bindings;
    private readonly IReadOnlyDictionary<string, IKnowledgeSource> _sources;

    public KnowledgeBindingResolver(
        IEnumerable<KnowledgeBindingRegistration> bindings,
        IEnumerable<IKnowledgeSource> sources)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(sources);

        _bindings = bindings.ToDictionary(binding => binding.Asset.Id);
        _sources = sources.ToDictionary(source => source.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IKnowledgeSource Resolve(KnowledgeAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (!_bindings.TryGetValue(asset.Id, out var binding) ||
            !string.Equals(binding.Asset.Urn.Value, asset.Urn.Value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Knowledge Asset '{asset.Urn.Value}' is not bound to a runtime Knowledge source.");
        }

        if (!_sources.TryGetValue(binding.SourceName, out var source))
        {
            throw new InvalidOperationException(
                $"Runtime Knowledge source '{binding.SourceName}' registered for Asset '{asset.Urn.Value}' could not be resolved.");
        }

        return source;
    }
}
