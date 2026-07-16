using PulseStack.Abstractions.Workflows;
namespace PulseStack.Abstractions.Runtime.Pipeline;
public interface IStepExecutorResolver
{
    IStepExecutor Resolve(
        IWorkflowStep step);
}
