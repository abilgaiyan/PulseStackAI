using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Workflows.Steps;

namespace PulseStack.Abstractions.Runtime.Pipeline;
public interface IStepExecutor
{
    bool CanExecute(
        IWorkflowStep step);

    Task<StepExecutionResult> ExecuteAsync(
        IWorkflowStep step,
        PipelineContext context,
        CancellationToken cancellationToken = default);
}