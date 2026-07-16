using PulseStack.Abstractions.Workflows;

namespace PulseStack.Abstractions.Persistence.Documents;

/// <summary>
/// Represents the canonical portable representation of a workflow.
/// </summary>
public sealed record WorkflowDocument
{
    /// <summary>
    /// Workflow document schema identifier.
    /// </summary>
    public required string Schema { get; init; }

    /// <summary>
    /// Workflow document schema version.
    /// </summary>
    public required string SchemaVersion { get; init; }

    /// <summary>
    /// Workflow identity.
    /// </summary>
    public required WorkflowIdentity Identity { get; init; }

    /// <summary>
    /// Workflow definition.
    /// </summary>
    public required WorkflowDefinition Definition { get; init; }

    /// <summary>
    /// Root workflow steps.
    /// </summary>
    public required IReadOnlyList<WorkflowStepDocument> Steps { get; init; }
}