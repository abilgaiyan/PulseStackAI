using PulseStack.Abstractions.Agents;

namespace PulseStack.Agents.Runtime.Context;

internal sealed class PipelineContextCloner : IPipelineContextCloner
{
    public PipelineContext Clone(
        PipelineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var clone = new PipelineContext
        {
            Input = context.Input,
            CurrentOutput = context.CurrentOutput
        };

        // Shallow clone orchestration collections. Contained mutable object
        // references, including values in Items, intentionally remain shared.
        foreach (var item in context.Items)
        {
            clone.Items[item.Key] = item.Value;
        }

        foreach (var step in context.Steps)
        {
            clone.Steps.Add(step);
        }

        foreach (var toolResult in context.ToolResults)
        {
            clone.ToolResults.Add(toolResult);
        }

        return clone;
    }
}
