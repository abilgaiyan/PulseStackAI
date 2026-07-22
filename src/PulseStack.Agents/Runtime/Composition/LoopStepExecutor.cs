using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Steps;

namespace PulseStack.Agents.Runtime.Composition;

internal sealed class LoopStepExecutor
    : CompositeStepExecutor
{
    public LoopStepExecutor(
        IStepExecutorResolver resolver)
        : base(resolver)
    {
    }

    public override bool CanExecute(
        IWorkflowStep step)
        => step is LoopStep;

    public override async Task<StepExecutionResult> ExecuteAsync(
        IWorkflowStep step,
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(context);

        var loopStep = (LoopStep)step;

        StepExecutionResult? lastResult = null;

        foreach (var item in loopStep.Items(context))
        {
            context.Items["CurrentItem"] = item;

            lastResult =
                await ExecuteStepAsync(
                    loopStep.Step,
                    context,
                    cancellationToken);

            if (!lastResult.Success)
            {
                return new StepExecutionResult
                {
                    StepName = loopStep.Name,
                    Success = lastResult.Success,
                    Output = lastResult.Output,
                    Usage = lastResult.Usage
                };
            }
        }

        return new StepExecutionResult
        {
            StepName = loopStep.Name,
            Success = lastResult?.Success ?? true,
            Output = lastResult?.Output ?? context.CurrentOutput,
            Usage = lastResult?.Usage
        };
    }
}
