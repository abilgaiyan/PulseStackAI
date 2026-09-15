using System.Collections.ObjectModel;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Runtime.Realization.Application;
using PulseStack.Abstractions.Workflows.Steps;

namespace PulseStack.Abstractions.Runtime.Invocation.Application;

public sealed class ApplicationInvocationResult
{
    public AssetReference Project { get; }

    public AssetReference EntryWorkflow { get; }

    public bool Success { get; }

    public string FinalOutput { get; }

    public IReadOnlyList<StepExecutionResult> Steps { get; }

    public ApplicationInvocationResult(
        AssetReference project,
        AssetReference entryWorkflow,
        bool success,
        string finalOutput,
        IReadOnlyList<StepExecutionResult> steps)
    {
        ApplicationRealizationContract.EnsureValidReference(project, nameof(project));
        ApplicationRealizationContract.EnsureValidReference(entryWorkflow, nameof(entryWorkflow));

        if (project.Type != AssetType.Project)
        {
            throw new ArgumentException(
                "The project reference must identify a Project asset.",
                nameof(project));
        }

        if (entryWorkflow.Type != AssetType.Workflow)
        {
            throw new ArgumentException(
                "The entry workflow reference must identify a Workflow asset.",
                nameof(entryWorkflow));
        }

        ArgumentNullException.ThrowIfNull(finalOutput);
        ArgumentNullException.ThrowIfNull(steps);

        var snapshot = new StepExecutionResult[steps.Count];
        for (var index = 0; index < steps.Count; index++)
        {
            var step = steps[index];
            if (step is null)
            {
                throw new ArgumentException(
                    "Invocation result steps must not contain null entries.",
                    nameof(steps));
            }

            snapshot[index] = step;
        }

        Project = project;
        EntryWorkflow = entryWorkflow;
        Success = success;
        FinalOutput = finalOutput;
        Steps = Array.AsReadOnly(snapshot);
    }
}
