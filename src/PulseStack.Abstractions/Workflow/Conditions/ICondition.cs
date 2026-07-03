using PulseStack.Abstractions.Agents;

namespace PulseStack.Abstractions.Workflow.Conditions;

/// <summary>
/// Defines a condition that can be evaluated in the context of a pipeline execution.
/// </summary>
public interface ICondition
{

    string Name { get; }
    /// <summary>
    /// Evaluates the condition using the provided context.
    /// </summary>
    /// <param name="context">The pipeline context.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>True if the condition is met, false otherwise.</returns>
    ValueTask<bool> EvaluateAsync(PipelineContext context, CancellationToken cancellationToken = default);
}