using System.Text.Json.Serialization;
using PulseStack.Abstractions.Workflows;

namespace PulseStack.Abstractions.Persistence.Documents;

/// <summary>
/// Base document for all workflow steps.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(RunStepDocument), WorkflowStepKinds.Run)]
public abstract record WorkflowStepDocument
{
    public required WorkflowStepId Id { get; init; }

    /// <summary>
    /// Canonical workflow language step kind.
    ///
    /// Must be one of the values defined in
    /// <see cref="WorkflowStepKinds"/>.
    /// </summary>
    public required string Kind { get; init; }

    public required string Name { get; init; }

    public IReadOnlyList<WorkflowStepDocument> Children { get; init; }
        = [];
}