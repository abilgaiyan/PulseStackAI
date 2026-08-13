namespace PulseStack.Abstractions.Assets;

/// <summary>
/// Declarative configuration for an Agent Asset.
/// </summary>
public sealed record AgentDefinitionOptions
{
    /// <summary>
    /// Display name of the Agent business capability.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Business goal the Agent is responsible for accomplishing.
    /// </summary>
    public required string Goal { get; init; }

    /// <summary>
    /// Business role performed by the Agent.
    /// </summary>
    public required string Role { get; init; }

    /// <summary>
    /// Business responsibilities owned by the Agent.
    /// </summary>
    public IReadOnlyCollection<string> Responsibilities { get; init; } = [];

    /// <summary>
    /// Model Asset used to provide intelligence to the Agent.
    /// </summary>
    public AssetReference? Model { get; init; }

    /// <summary>
    /// Prompt Asset used to define Agent communication.
    /// </summary>
    public AssetReference? Prompt { get; init; }

    /// <summary>
    /// Knowledge Assets available to the Agent.
    /// </summary>
    public IReadOnlyCollection<AssetReference> Knowledge { get; init; } = [];

    /// <summary>
    /// Tool Assets available to the Agent.
    /// </summary>
    public IReadOnlyCollection<AssetReference> Tools { get; init; } = [];

    /// <summary>
    /// Memory Asset used to provide retained business context.
    /// </summary>
    public AssetReference? Memory { get; init; }

    /// <summary>
    /// Policy Assets governing the Agent.
    /// </summary>
    public IReadOnlyCollection<AssetReference> Policies { get; init; } = [];
}
