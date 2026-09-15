using PulseStack.Abstractions.Runtime.Invocation.Application;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Runtime.Realization.Application;
using PulseStack.Abstractions.Workflows.Steps;

namespace PulseStack.Core.Runtime.Invocation.Application;

internal static class ApplicationInvocationResultProjector
{
    public static ApplicationInvocationResult Project(
        RealizedApplication application,
        WorkflowExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(application);

        if (executionResult is null)
        {
            throw new InvalidOperationException(
                "The workflow runtime returned a null execution result.");
        }

        if (executionResult.FinalOutput is null)
        {
            throw new InvalidOperationException(
                "The workflow runtime returned a null final output.");
        }

        if (executionResult.Steps is null)
        {
            throw new InvalidOperationException(
                "The workflow runtime returned a null steps collection.");
        }

        var steps = new StepExecutionResult[executionResult.Steps.Count];
        for (var index = 0; index < executionResult.Steps.Count; index++)
        {
            var step = executionResult.Steps[index];
            if (step is null)
            {
                throw new InvalidOperationException(
                    "The workflow runtime returned a null step result.");
            }

            steps[index] = step;
        }

        return new ApplicationInvocationResult(
            application.Project,
            application.EntryWorkflow,
            executionResult.Success,
            executionResult.FinalOutput,
            steps);
    }
}
