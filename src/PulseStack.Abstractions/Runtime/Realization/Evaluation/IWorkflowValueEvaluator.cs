using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Workflows.Values;

namespace PulseStack.Abstractions.Runtime.Realization.Evaluation;

/// <summary>
/// Evaluates declarative Workflow value sources against runtime state.
/// </summary>
public interface IWorkflowValueEvaluator
{
    object? Evaluate(
        WorkflowValueDefinition definition,
        PipelineContext context);
}
