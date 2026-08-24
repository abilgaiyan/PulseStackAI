using PulseStack.Abstractions.Memory;

namespace PulseStack.Core.Memory;

public sealed class ConversationMemoryFactory : IConversationMemoryFactory
{
    public string Name => "conversation";

    public IConversationMemory Create() =>
        new ConversationMemory();
}
