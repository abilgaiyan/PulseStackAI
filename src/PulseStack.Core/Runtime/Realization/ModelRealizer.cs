using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Chat;

namespace PulseStack.Core.Runtime.Realization;

public sealed class ModelRealizer
{
    private readonly IChatClientFactoryRegistry _factoryRegistry;

    public ModelRealizer(IChatClientFactoryRegistry factoryRegistry)
    {
        ArgumentNullException.ThrowIfNull(factoryRegistry);

        _factoryRegistry = factoryRegistry;
    }

    public IChatClient Realize(ModelAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        asset.Id.EnsureValid();
        ArgumentException.ThrowIfNullOrWhiteSpace(asset.Options.Provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(asset.Options.Model);

        var factory = _factoryRegistry.Resolve(asset.Options.Provider);

        return factory.Create(asset.Options.Model);
    }
}
