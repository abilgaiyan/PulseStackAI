using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Steps;

namespace PulseStack.Agents.Runtime.Composition;

internal sealed class RetryStepExecutor
    : CompositeStepExecutor
{
    public RetryStepExecutor(
        IStepExecutorResolver resolver)
        : base(resolver)
    {
    }

    public override bool CanExecute(
        IWorkflowStep step)
        => step is RetryStep;

    public override async Task<StepExecutionResult> ExecuteAsync(
        IWorkflowStep step,
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        var retryStep =
            (RetryStep)step;

        StepExecutionResult? lastResult = null;

        for (var attempt = 1;
            attempt <= retryStep.MaxAttempts;
            attempt++)
        {
            lastResult =
                await ExecuteStepAsync(
                    retryStep.Step,
                    context,
                    cancellationToken);

            if (lastResult.Success)
            {
                return new StepExecutionResult
                {
                    StepName = retryStep.Name,
                    Success = lastResult.Success,
                    Output = lastResult.Output,
                    Usage = lastResult.Usage
                };
            }
        }

        return new StepExecutionResult
        {
            StepName = retryStep.Name,
            Success = lastResult!.Success,
            Output = lastResult.Output,
            Usage = lastResult.Usage
        };
    }        
    
}
