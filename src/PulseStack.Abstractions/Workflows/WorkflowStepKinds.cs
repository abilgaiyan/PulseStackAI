namespace PulseStack.Abstractions.Workflows;

/// <summary>
/// Defines the canonical workflow language step kinds.
///
/// These values are shared by:
/// - Workflow builders
/// - Persistence documents
/// - Serialization
/// - Validation
/// - Runtime mapping
///
/// They represent the language grammar rather than runtime types.
/// </summary>
public static class WorkflowStepKinds
{
    public const string Run = "Run";

    public const string Conditional = "Conditional";

    public const string Parallel = "Parallel";

    public const string Loop = "Loop";

    public const string Switch = "Switch";
}