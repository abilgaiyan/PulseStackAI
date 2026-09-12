using System.Collections.ObjectModel;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;
using PulseStack.Abstractions.Workflows.Definitions;

namespace PulseStack.Core.Persistence.AIAssets.GraphLoading;

internal sealed class AIAssetGraphRelationshipEnumerator
{
    internal IReadOnlyList<AIAssetGraphRelationship> Enumerate(
        IAsset source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();

        var sourceKey = AssetDefinitionKey.From(source);
        var relationships = new List<AIAssetGraphRelationship>();

        switch (source)
        {
            case ProjectAsset project when source.Type == AssetType.Project:
                AddProject(project, sourceKey, relationships, cancellationToken);
                break;

            case LibraryAsset library when source.Type == AssetType.Library:
                AddMembers(sourceKey, library.Options.Members, AIAssetGraphRelationshipClass.InternalMembership, "$.members", relationships, cancellationToken);
                AddDependencies(source, sourceKey, AIAssetGraphBoundaryRole.External, relationships, cancellationToken);
                break;

            case PackageAsset package when source.Type == AssetType.Package:
                AddMembers(sourceKey, package.Options.Members, AIAssetGraphRelationshipClass.InternalDistribution, "$.members", relationships, cancellationToken);
                AddDependencies(source, sourceKey, AIAssetGraphBoundaryRole.External, relationships, cancellationToken);
                break;

            case WorkflowAsset workflow when source.Type == AssetType.Workflow:
                AddWorkflow(workflow, sourceKey, relationships, cancellationToken);
                AddDependencies(source, sourceKey, AIAssetGraphBoundaryRole.NotApplicable, relationships, cancellationToken);
                break;

            case AgentDefinition agent when source.Type == AssetType.Agent:
                AddAgent(agent, sourceKey, relationships, cancellationToken);
                AddDependencies(source, sourceKey, AIAssetGraphBoundaryRole.NotApplicable, relationships, cancellationToken);
                break;

            case IAsset when source.Type is AssetType.Prompt
                or AssetType.Tool
                or AssetType.Knowledge
                or AssetType.Memory
                or AssetType.Policy
                or AssetType.Model:
                AddDependencies(source, sourceKey, AIAssetGraphBoundaryRole.NotApplicable, relationships, cancellationToken);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported or inconsistent schema-v1 AI Asset type '{source.Type}' for graph relationship enumeration.");
        }

        return new ReadOnlyCollection<AIAssetGraphRelationship>(relationships.ToArray());
    }

    private static void AddProject(
        ProjectAsset project,
        AssetDefinitionKey sourceKey,
        List<AIAssetGraphRelationship> output,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        AddTyped(sourceKey, project.Options.EntryWorkflow, AIAssetGraphRelationshipClass.DistinguishedStructural, AIAssetGraphBoundaryRole.Structural, "$.entryWorkflow", output);
        AddMembers(sourceKey, project.Options.OwnedAssets, AIAssetGraphRelationshipClass.InternalOwnership, "$.ownedAssets", output, cancellationToken);
        AddDependencies(project, sourceKey, AIAssetGraphBoundaryRole.External, output, cancellationToken);
    }

    private static void AddAgent(
        AgentDefinition agent,
        AssetDefinitionKey sourceKey,
        List<AIAssetGraphRelationship> output,
        CancellationToken cancellationToken)
    {
        var options = agent.Options;

        if (options.Model is not null)
        {
            AddTyped(sourceKey, options.Model, AIAssetGraphRelationshipClass.DeclarativeReference, AIAssetGraphBoundaryRole.NotApplicable, "$.model", output);
        }

        if (options.Prompt is not null)
        {
            AddTyped(sourceKey, options.Prompt, AIAssetGraphRelationshipClass.DeclarativeReference, AIAssetGraphBoundaryRole.NotApplicable, "$.prompt", output);
        }

        AddTypedCollection(sourceKey, options.Knowledge, "$.knowledge", output, cancellationToken);
        AddTypedCollection(sourceKey, options.Tools, "$.tools", output, cancellationToken);

        if (options.Memory is not null)
        {
            AddTyped(sourceKey, options.Memory, AIAssetGraphRelationshipClass.DeclarativeReference, AIAssetGraphBoundaryRole.NotApplicable, "$.memory", output);
        }

        AddTypedCollection(sourceKey, options.Policies, "$.policies", output, cancellationToken);
    }

    private static void AddTypedCollection(
        AssetDefinitionKey sourceKey,
        IEnumerable<AssetReference> references,
        string pathPrefix,
        List<AIAssetGraphRelationship> output,
        CancellationToken cancellationToken)
    {
        var index = 0;
        foreach (var reference in references)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddTyped(sourceKey, reference, AIAssetGraphRelationshipClass.DeclarativeReference, AIAssetGraphBoundaryRole.NotApplicable, $"{pathPrefix}[{index}]", output);
            index++;
        }
    }

    private static void AddMembers(
        AssetDefinitionKey sourceKey,
        IEnumerable<AssetReference> references,
        AIAssetGraphRelationshipClass relationshipClass,
        string pathPrefix,
        List<AIAssetGraphRelationship> output,
        CancellationToken cancellationToken)
    {
        var index = 0;
        foreach (var reference in references)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddTyped(sourceKey, reference, relationshipClass, AIAssetGraphBoundaryRole.Internal, $"{pathPrefix}[{index}]", output);
            index++;
        }
    }

    private static void AddWorkflow(
        WorkflowAsset workflow,
        AssetDefinitionKey sourceKey,
        List<AIAssetGraphRelationship> output,
        CancellationToken cancellationToken)
    {
        var index = 0;
        foreach (var step in workflow.Options.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddWorkflowStep(step, $"$.steps[{index}]", sourceKey, output, cancellationToken);
            index++;
        }
    }

    private static void AddWorkflowStep(
        WorkflowStepDefinition step,
        string path,
        AssetDefinitionKey sourceKey,
        List<AIAssetGraphRelationship> output,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (step)
        {
            case RunStepDefinition run:
                AddTyped(sourceKey, run.Agent, AIAssetGraphRelationshipClass.DeclarativeReference, AIAssetGraphBoundaryRole.NotApplicable, $"{path}.agent", output);
                break;

            case ParallelStepDefinition parallel:
                for (var index = 0; index < parallel.Steps.Count; index++)
                {
                    AddWorkflowStep(parallel.Steps[index], $"{path}.children[{index}]", sourceKey, output, cancellationToken);
                }
                break;

            case ConditionalStepDefinition conditional:
                AddWorkflowStep(conditional.ThenStep, $"{path}.then", sourceKey, output, cancellationToken);
                if (conditional.ElseStep is not null)
                {
                    AddWorkflowStep(conditional.ElseStep, $"{path}.else", sourceKey, output, cancellationToken);
                }
                break;

            case RetryStepDefinition retry:
                AddWorkflowStep(retry.Step, $"{path}.child", sourceKey, output, cancellationToken);
                break;

            case LoopStepDefinition loop:
                AddWorkflowStep(loop.Step, $"{path}.child", sourceKey, output, cancellationToken);
                break;

            case SwitchStepDefinition @switch:
                for (var index = 0; index < @switch.Cases.Count; index++)
                {
                    AddWorkflowStep(@switch.Cases[index].Step, $"{path}.cases[{index}].child", sourceKey, output, cancellationToken);
                }

                if (@switch.DefaultStep is not null)
                {
                    AddWorkflowStep(@switch.DefaultStep, $"{path}.default", sourceKey, output, cancellationToken);
                }
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported schema-v1 Workflow step type '{step.GetType().Name}' for graph relationship enumeration.");
        }
    }

    private static void AddDependencies(
        IAsset source,
        AssetDefinitionKey sourceKey,
        AIAssetGraphBoundaryRole boundaryRole,
        List<AIAssetGraphRelationship> output,
        CancellationToken cancellationToken)
    {
        var dependencies = source.Dependencies
            .Select(static (dependency, index) => (Dependency: dependency, OriginalIndex: index))
            .ToArray();

        AddDependencyPartition(dependencies, required: true, sourceKey, boundaryRole, output, cancellationToken);
        AddDependencyPartition(dependencies, required: false, sourceKey, boundaryRole, output, cancellationToken);
    }

    private static void AddDependencyPartition(
        IEnumerable<(AssetDependency Dependency, int OriginalIndex)> dependencies,
        bool required,
        AssetDefinitionKey sourceKey,
        AIAssetGraphBoundaryRole boundaryRole,
        List<AIAssetGraphRelationship> output,
        CancellationToken cancellationToken)
    {
        foreach (var item in dependencies)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.Dependency.Required != required)
            {
                continue;
            }

            output.Add(new AIAssetGraphRelationship(
                sourceKey,
                item.Dependency.Reference,
                AIAssetGraphRelationshipClass.ExplicitRequirement,
                required ? AIAssetGraphMaterializationAuthority.Required : AIAssetGraphMaterializationAuthority.Excluded,
                boundaryRole,
                required,
                output.Count,
                $"$.dependencies[{item.OriginalIndex}]"));
        }
    }

    private static void AddTyped(
        AssetDefinitionKey sourceKey,
        AssetReference target,
        AIAssetGraphRelationshipClass relationshipClass,
        AIAssetGraphBoundaryRole boundaryRole,
        string authoredPath,
        List<AIAssetGraphRelationship> output)
    {
        output.Add(new AIAssetGraphRelationship(
            sourceKey,
            target,
            relationshipClass,
            AIAssetGraphMaterializationAuthority.Required,
            boundaryRole,
            null,
            output.Count,
            authoredPath));
    }
}
