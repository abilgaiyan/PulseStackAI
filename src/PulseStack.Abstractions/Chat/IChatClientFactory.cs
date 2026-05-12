using Microsoft.Extensions.AI;

namespace PulseStack.Abstractions.Chat;

public interface IChatClientFactory
{
    IChatClient Create(string model);
}