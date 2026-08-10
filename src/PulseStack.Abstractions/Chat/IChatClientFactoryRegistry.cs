namespace PulseStack.Abstractions.Chat;

public interface IChatClientFactoryRegistry
{
    void Register(
        string provider,
        IChatClientFactory factory);

    IChatClientFactory Resolve(string provider);
}
