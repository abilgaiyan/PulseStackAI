using PulseStack.Abstractions.Workflows.Steps;
namespace PulseStack.Abstractions.Runtime.Pipeline;
public interface IStepExecutorResolver
{
    IStepExecutor Resolve(
        IWorkflowStep step);
}
