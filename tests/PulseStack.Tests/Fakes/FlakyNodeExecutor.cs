using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Agents;

namespace PulseStack.Tests.Fakes;

internal sealed class FlakyStepExecutor : IStepExecutor
{
    private int _attempts;

    public int Attempts => _attempts;

    public bool CanExecute(
        IWorkflowStep step)
        => true;

    public Task<StepExecutionResult> ExecuteAsync(
        IWorkflowStep step,
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        _attempts++;

        return Task.FromResult(
            new StepExecutionResult
            {
                StepName = step.Name,
                Success = _attempts >= 2,
                Output = _attempts >= 2
                    ? "Success"
                    : null
            });
    }
}
