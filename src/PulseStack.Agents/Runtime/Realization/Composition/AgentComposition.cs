using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Runtime.Realization;

namespace PulseStack.Agents.Realization.Composition;

public sealed record AgentComposition
{
    public required AgentDefinition Definition { get; init; }

    public required ModelAsset Model { get; init; }

    public required IChatClient ChatClient { get; init; }

    public RuntimePrompt? Prompt { get; init; }
}
