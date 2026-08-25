using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Realization.Evaluation;
using PulseStack.Abstractions.Workflows.Values;

namespace PulseStack.Core.Runtime.Realization.Evaluation;

/// <summary>
/// Evaluates Workflow value definitions against the current PipelineContext.
/// </summary>
public sealed class WorkflowValueEvaluator : IWorkflowValueEvaluator
{
    public object? Evaluate(
        WorkflowValueDefinition definition,
        PipelineContext context)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(context);

        return definition switch
        {
            InputValueDefinition => context.Input,
            CurrentOutputValueDefinition => context.CurrentOutput,
            ContextItemValueDefinition item => EvaluateContextItem(item, context),
            LiteralValueDefinition literal => literal.Value,
            _ => throw new NotSupportedException(
                $"Workflow value definition '{definition.GetType().Name}' is not supported.")
        };
    }

    private static object? EvaluateContextItem(
        ContextItemValueDefinition definition,
        PipelineContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Key);

        context.Items.TryGetValue(definition.Key, out var value);
        return value;
    }
}
