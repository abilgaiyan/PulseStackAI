using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Providers;

namespace PulseStack.Core.Runtime.Realization;

public sealed class ModelRealizer
{
    private readonly IProviderResolver _providerResolver;

    public ModelRealizer(IProviderResolver providerResolver)
    {
        ArgumentNullException.ThrowIfNull(providerResolver);
        _providerResolver = providerResolver;
    }

    public IChatClient Realize(ModelAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        asset.Id.EnsureValid();
        ArgumentException.ThrowIfNullOrWhiteSpace(asset.Options.Provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(asset.Options.Model);

        var provider = _providerResolver.Resolve(asset.Options.Provider);

        return provider.Create(asset.Options.Model);
    }
}
