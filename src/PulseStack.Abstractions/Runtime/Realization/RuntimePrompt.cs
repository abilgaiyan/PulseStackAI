namespace PulseStack.Abstractions.Runtime.Realization;

/// <summary>
/// Runtime representation of a realized Prompt Asset.
/// </summary>
public sealed record RuntimePrompt
{
    public required string SystemInstructions { get; init; }
}
