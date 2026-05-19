namespace PulseStack.Abstractions.Tools;

public sealed class ToolDescriptor
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public ToolActionType ActionType { get; init; }

    public IReadOnlyList<string> RequiredRoles { get; init; } = [];

    public IReadOnlyList<string> RequiredPermissions { get; init; } = [];

    public IReadOnlyList<string> AllowedScopes { get; init; } = [];

    public bool IsDestructive { get; init; }

    public bool RequiresConfirmation { get; init; }
}