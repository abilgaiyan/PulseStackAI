using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Workflows;

namespace PulseStack.Abstractions.Runtime.Realization.Application;

public sealed class RealizedApplication
{
    public AssetReference Project { get; }

    public AssetReference EntryWorkflow { get; }

    public Workflow Workflow { get; }

    internal RealizedApplication(
        AssetReference project,
        AssetReference entryWorkflow,
        Workflow workflow)
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

        ArgumentNullException.ThrowIfNull(workflow);

        Project = project;
        EntryWorkflow = entryWorkflow;
        Workflow = workflow;
    }
}
