using PulseStack.Abstractions.Chat;

namespace PulseStack.Abstractions.Providers;

public interface IProviderResolver
{
    IChatClientFactory Resolve(string provider);
}
