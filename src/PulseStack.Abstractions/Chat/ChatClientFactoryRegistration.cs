namespace PulseStack.Abstractions.Chat;

public sealed record ChatClientFactoryRegistration(
    string Provider,
    IChatClientFactory Factory);
