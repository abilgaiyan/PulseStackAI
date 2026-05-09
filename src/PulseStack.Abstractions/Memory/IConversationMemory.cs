using Microsoft.Extensions.AI;

namespace PulseStack.Abstractions.Memory;

public interface IConversationMemory
{
    IReadOnlyList<ChatMessage> Messages { get; }

    void Add(ChatMessage message);

    void Clear();
}