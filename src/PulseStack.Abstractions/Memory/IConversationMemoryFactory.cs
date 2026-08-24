namespace PulseStack.Abstractions.Memory;

public interface IConversationMemoryFactory
{
    string Name { get; }

    IConversationMemory Create();
}
