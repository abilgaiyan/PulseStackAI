using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
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

        var conditional =
            (ConditionalStep)step;

        var shouldExecuteThen =
            await conditional.Condition.EvaluateAsync(
                context,
                cancellationToken);

        var branch =
            shouldExecuteThen
                ? conditional.ThenStep
                : conditional.ElseStep;

        // No Else branch - condition evaluated false
        if (branch is null)
        {
            return new StepExecutionResult
            {
                StepName = conditional.Name,
                Success = true
            };
        }

        var result =
            await ExecuteStepAsync(
                branch,
                context,
                cancellationToken);

        return new StepExecutionResult
        {
            StepName = conditional.Name,
            Success = result.Success,
            Output = result.Output,
            Usage = result.Usage
        };
    }
}
