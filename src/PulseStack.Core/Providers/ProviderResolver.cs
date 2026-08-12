using PulseStack.Abstractions.Chat;
using PulseStack.Abstractions.Providers;

namespace PulseStack.Core.Providers;

public sealed class ProviderResolver : IProviderResolver
{
    private readonly IChatClientFactoryRegistry _registry;

    public ProviderResolver(IChatClientFactoryRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    public IChatClientFactory Resolve(string provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);

        return _registry.Resolve(provider);
    }
}
