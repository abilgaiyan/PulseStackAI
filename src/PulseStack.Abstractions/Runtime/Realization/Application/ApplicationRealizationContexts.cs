using PulseStack.Abstractions.Assets;

namespace PulseStack.Abstractions.Runtime.Realization.Application;

public sealed class ApplicationRealizationUnsupportedRootContext
{
    public ApplicationRealizationUnsupportedRootContext(AssetDefinitionKey rootKey)
    {
        ApplicationRealizationContract.EnsureValidDefinitionKey(rootKey, nameof(rootKey));

        if (rootKey.Type is not (AssetType.Library or AssetType.Package))
        {
            throw new ArgumentException(
                "UnsupportedRoot must identify a Library or Package definition.",
                nameof(rootKey));
        }

        RootKey = rootKey;
    }

    public AssetDefinitionKey RootKey { get; }
}

public sealed class ApplicationRealizationEntryWorkflowUnresolvedContext
{
    public ApplicationRealizationEntryWorkflowUnresolvedContext(
        AssetDefinitionKey rootKey,
        AssetReference entryWorkflow)
    {
        ApplicationRealizationContextContract.EnsureProjectEntryWorkflow(
            rootKey,
            entryWorkflow);

        RootKey = rootKey;
        EntryWorkflow = entryWorkflow;
    }

    public AssetDefinitionKey RootKey { get; }

    public AssetReference EntryWorkflow { get; }
}

public sealed class ApplicationRealizationEntryWorkflowTypeIncoherentContext
{
    public ApplicationRealizationEntryWorkflowTypeIncoherentContext(
        AssetDefinitionKey rootKey,
        AssetReference entryWorkflow)
    {
        ApplicationRealizationContextContract.EnsureProjectEntryWorkflow(
            rootKey,
            entryWorkflow);

        RootKey = rootKey;
        EntryWorkflow = entryWorkflow;
    }

    public AssetDefinitionKey RootKey { get; }

    public AssetReference EntryWorkflow { get; }
}

internal static class ApplicationRealizationContextContract
{
    public static void EnsureProjectEntryWorkflow(
        AssetDefinitionKey rootKey,
        AssetReference? entryWorkflow)
    {
        ApplicationRealizationContract.EnsureValidDefinitionKey(rootKey, nameof(rootKey));

        if (rootKey.Type != AssetType.Project)
        {
            throw new ArgumentException(
                "The application realization root must identify a Project definition.",
                nameof(rootKey));
        }

        ApplicationRealizationContract.EnsureValidReference(
            entryWorkflow,
            nameof(entryWorkflow));

        if (entryWorkflow!.Type != AssetType.Workflow)
        {
            throw new ArgumentException(
                "The Project entry workflow must identify a Workflow definition.",
                nameof(entryWorkflow));
        }
    }
}
