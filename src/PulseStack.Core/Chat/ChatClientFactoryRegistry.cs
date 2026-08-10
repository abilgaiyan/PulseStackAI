using PulseStack.Abstractions.Chat;

namespace PulseStack.Core.Chat;

public sealed class ChatClientFactoryRegistry : IChatClientFactoryRegistry
{
    private readonly Dictionary<string, IChatClientFactory> _factories =
        new(StringComparer.OrdinalIgnoreCase);

    public ChatClientFactoryRegistry()
        : this([])
    {
    }

    public ChatClientFactoryRegistry(
        IEnumerable<ChatClientFactoryRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        foreach (var registration in registrations)
        {
            ArgumentNullException.ThrowIfNull(registration);
            Register(registration.Provider, registration.Factory);
        }
    }

    public void Register(
        string provider,
        IChatClientFactory factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentNullException.ThrowIfNull(factory);

        if (!_factories.TryAdd(provider, factory))
        {
            throw new ArgumentException(
                $"A chat client factory is already registered for provider '{provider}'.",
                nameof(provider));
        }
    }

    public IChatClientFactory Resolve(string provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);

        if (_factories.TryGetValue(provider, out var factory))
        {
            return factory;
        }

        throw new InvalidOperationException(
            $"No chat client factory is registered for provider '{provider}'.");
    }
}
