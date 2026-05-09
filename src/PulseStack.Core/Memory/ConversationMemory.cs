using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Memory;

namespace PulseStack.Core.Memory;

public sealed class ConversationMemory : IConversationMemory
{
    private readonly List<ChatMessage> _messages = [];

    public IReadOnlyList<ChatMessage> Messages => _messages;

    public void Add(ChatMessage message)
    {
        _messages.Add(message);
    }

    public void Clear()
    {
        _messages.Clear();
    }
}