using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Agents;

namespace PulseStack.Agents.Runtime;

internal sealed class Agent : IAgent
{
    private readonly IChatClient _client;
    private readonly string? _instructions;

    public string Name { get; }

    public Agent(
        string name,
        IChatClient client,
        string? instructions,
        float? temperature)
    {
        Name = name;
        _client = client;
        _instructions = instructions;
    }

    public async Task<ChatResponse> RunAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<ChatMessage>();

        if (!string.IsNullOrWhiteSpace(_instructions))
            messages.Add(new(ChatRole.System, _instructions));

        messages.Add(new(ChatRole.User, input));

        return await _client.GetResponseAsync(
            messages,
            cancellationToken: cancellationToken);
    }
}