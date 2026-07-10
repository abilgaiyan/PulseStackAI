using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Workflows;

namespace PulseStack.Tests.Fakes;

internal sealed class AlwaysFailNodeExecutor
    : IStepExecutor
{
    private int _attempts;

    public int Attempts => _attempts;

    public bool CanExecute(
        IWorkflowStep step)
        => step is Workflow;

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
                Success = false
            });
    }
}
