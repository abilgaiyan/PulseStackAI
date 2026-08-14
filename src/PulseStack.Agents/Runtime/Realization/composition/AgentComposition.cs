using PulseStack.Abstractions.Assets;
using Microsoft.Extensions.AI;

namespace PulseStack.Agents.Realization.Composition;

public sealed record AgentComposition
{
    public required AgentDefinition Definition { get; init; }

    public required ModelAsset Model { get; init; }

    public required IChatClient ChatClient { get; init; }

}