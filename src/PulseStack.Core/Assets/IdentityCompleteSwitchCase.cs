namespace PulseStack.Core.Assets;

/// <summary>
/// Represents a Switch branch whose child workflow-step subtree is identity-complete.
/// </summary>
public sealed class IdentityCompleteSwitchCase
{
    internal IdentityCompleteSwitchCase(
        string value,
        IdentityCompleteWorkflowStep step)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentNullException.ThrowIfNull(step);

        Value = value;
        Step = step;
    }

    public string Value { get; }

    public IdentityCompleteWorkflowStep Step { get; }
}
