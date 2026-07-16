using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Workflows;

namespace PulseStack.Agents.Runtime.Composition;

internal sealed class PipelineStepExecutor
    : IStepExecutor
{
    public bool CanExecute(
        IWorkflowStep step)
        => step is IAgentPipeline;

    public async Task<StepExecutionResult> ExecuteAsync(
        IWorkflowStep step,
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(context);

        if (step is not IAgentPipeline pipeline)
        {
            throw new InvalidOperationException(
                $"Unsupported step type '{step.GetType().Name}'.");
        }

        var result =
            await pipeline.RunDetailedAsync(
                context,
                cancellationToken);

        return new StepExecutionResult
        {
            StepName = pipeline.Name,
            Success = result.Success,
            Output = result.FinalOutput,
            Usage = result.TotalUsage
        };
    }
}
