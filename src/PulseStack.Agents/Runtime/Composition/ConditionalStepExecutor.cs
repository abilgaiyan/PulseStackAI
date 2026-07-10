using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Workflows.Conditions;
using PulseStack.Abstractions.Workflows.Steps;

namespace PulseStack.Agents.Runtime.Composition;

internal sealed class ConditionalStepExecutor
    : CompositeStepExecutor
{
    public ConditionalStepExecutor(
        IStepExecutorResolver resolver)
        : base(resolver)
    {
    }

    public override bool CanExecute(
        IWorkflowStep step)
        => step is ConditionalStep;

    public override async Task<StepExecutionResult> ExecuteAsync(
        IWorkflowStep step,
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(context);

        var ConditionalStep =
            (ConditionalStep)step;

        var shouldExecute =
            await ConditionalStep.Condition.EvaluateAsync(
                context,
                cancellationToken);

        if (!shouldExecute)
        {
            return new StepExecutionResult
            {
                StepName = ConditionalStep.Name,
                Success = true
            };
        }

        var result =
            await ExecuteStepAsync(
                ConditionalStep.Step,
                context,
                cancellationToken);

        return new StepExecutionResult
        {
            StepName = ConditionalStep.Name,
            Success = result.Success,
            Output = result.Output,
            Usage = result.Usage
        };
    }
}
