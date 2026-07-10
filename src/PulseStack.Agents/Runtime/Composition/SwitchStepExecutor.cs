using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Workflows.Steps;

namespace PulseStack.Agents.Runtime.Composition;

internal sealed class SwitchStepExecutor
    : CompositeStepExecutor
{
    public SwitchStepExecutor(
        IStepExecutorResolver resolver)
        : base(resolver)
    {
    }

    public override bool CanExecute(
        IWorkflowStep step)
        => step is SwitchStep;

    public override async Task<StepExecutionResult> ExecuteAsync(
        IWorkflowStep step,
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(context);

        var switchStep =
            (SwitchStep)step;

        var selectedValue =
            switchStep.Selector(
                context);

        var matchedCase =
            switchStep.Cases.FirstOrDefault(
                x => string.Equals(
                    x.Value,
                    selectedValue,
                    StringComparison.OrdinalIgnoreCase));


        StepExecutionResult? result = null;

        if (matchedCase is not null)
        {
            result =
                await ExecuteStepAsync(
                    matchedCase.Step,
                    context,
                    cancellationToken);
        }
        else if (switchStep.DefaultStep is not null)
        {
            result =
                await ExecuteStepAsync(
                    switchStep.DefaultStep,
                    context,
                    cancellationToken);
        }

        return new StepExecutionResult
        {
            StepName = switchStep.Name,
            Success = result?.Success ?? true,
            Output = result?.Output ?? context.CurrentOutput,
            Usage = result?.Usage
        };
    }
}
