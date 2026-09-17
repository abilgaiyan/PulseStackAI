using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using PulseStack.Abstractions.Persistence.AIAssets.Storage;
using PulseStack.Abstractions.Runtime.Application;
using PulseStack.Abstractions.Runtime.Invocation.Application;
using PulseStack.Agents.DependencyInjection;
using PulseStack.Core.DependencyInjection;
using Xunit;

namespace PulseStack.Tests.Runtime.Application;

public sealed class ApplicationOperationIntegratedConformanceTests
{
    private static readonly AIAssetStorageOptions StorageOptions = new()
    {
        MaximumRepresentationSizeBytes = 1024 * 1024
    };

    [Fact]
    public async Task PublicComposition_ShouldExecutePersistedProjectThroughRealOperationPipeline()
    {
        await using var provider = BuildProvider();
        var workflow = CreateWorkflow("operation-entry");
        var project = CreateProject("operation-project", workflow);

        await PersistAndPublishAsync(provider, [workflow, project]);

        await using var scope = provider.CreateAsyncScope();
        var operation = scope.ServiceProvider.GetRequiredService<IApplicationOperation>();
        var request = new ApplicationInvocationRequest("integrated-input");

        var result = await operation.ExecuteAsync(project.Key, request);

        var invocation = Assert.IsType<ApplicationOperationResult.InvocationOutcome>(result).Result;
        Assert.True(invocation.Success);
        Assert.Equal("integrated-input", invocation.FinalOutput);
        Assert.Empty(invocation.Steps);
        Assert.Equal(project.Key, AssetDefinitionKey.From(invocation.Project));
        Assert.Equal(workflow.Key, AssetDefinitionKey.From(invocation.EntryWorkflow));
    }

    [Fact]
    public async Task PublicComposition_ShouldPreserveRealLoaderTerminalOutcome()
    {
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var operation = scope.ServiceProvider.GetRequiredService<IApplicationOperation>();
        var missingProject = Key(AssetType.Project);

        var result = await operation.ExecuteAsync(
            missingProject,
            new ApplicationInvocationRequest("unreached-input"));

        var loadOutcome = Assert.IsType<ApplicationOperationResult.LoadOutcome>(result);
        var failure = Assert.IsType<AIAssetGraphLoadResult.RootDefinitionUnavailable>(loadOutcome.Result);
        Assert.Equal("AAG001", failure.Context.Code);
        Assert.Equal(missingProject, failure.Context.RootKey);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddInMemoryAIAssetStorage(StorageOptions);
        services.AddInMemoryAIAssetCatalog();
        services.AddAIAssetGraphLoading();
        services.AddPulseStack();
        services.AddPulseStackAgents();
        services.AddPulseStackWorkflows();

        return services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true
            });
    }

    private static async Task PersistAndPublishAsync(
        IServiceProvider provider,
        IReadOnlyList<Definition> definitions)
    {
        var writer = provider.GetRequiredService<IAIAssetWriter>();
        var publisher = provider.GetRequiredService<IAIAssetPublisher>();

        foreach (var definition in definitions)
        {
            Assert.Equal(
                AIAssetWriteResult.Created,
                await writer.WriteAsync(definition.Key, definition.Document));
        }

        foreach (var definition in definitions)
        {
            Assert.Equal(
                AIAssetPublicationResult.Published,
                await publisher.PublishAsync(definition.Key));
        }
    }

    private static Definition CreateProject(string name, Definition workflow)
    {
        var key = Key(AssetType.Project);
        var urn = Urn(AssetType.Project, key.Id, name);
        var entry = Reference(workflow);
        var document = new ProjectAssetDocument(
            AIAssetSchemaVersion.V1,
            Identity(key, urn),
            Metadata(name),
            AIAssetLifecycleDocument.Draft,
            entry,
            ownedAssets: [entry],
            references: [entry]);

        return new Definition(key, urn, document);
    }

    private static Definition CreateWorkflow(string name)
    {
        var key = Key(AssetType.Workflow);
        var urn = Urn(AssetType.Workflow, key.Id, name);
        var document = new WorkflowAssetDocument(
            AIAssetSchemaVersion.V1,
            Identity(key, urn),
            Metadata(name),
            AIAssetLifecycleDocument.Draft);

        return new Definition(key, urn, document);
    }

    private static AssetDefinitionKey Key(AssetType type) =>
        new(type, AssetId.New(), AssetVersion.Initial);

    private static AssetUrn Urn(AssetType type, AssetId id, string name) =>
        new($"urn:pulsestack:test:ms0113f:{type.ToString().ToLowerInvariant()}:{name}:{id.Value:N}");

    private static AIAssetIdentityDocument Identity(AssetDefinitionKey key, AssetUrn urn) =>
        new()
        {
            Id = key.Id.Value.ToString(),
            Urn = urn.Value,
            Version = key.Version.Value
        };

    private static AIAssetMetadataDocument Metadata(string name) => new(name);

    private static AIAssetReferenceDocument Reference(Definition definition) =>
        new()
        {
            AssetType = ToDocumentType(definition.Key.Type),
            AssetId = definition.Key.Id.Value.ToString(),
            Urn = definition.Urn.Value,
            Version = definition.Key.Version.Value
        };

    private static AIAssetDocumentType ToDocumentType(AssetType type) => type switch
    {
        AssetType.Workflow => AIAssetDocumentType.Workflow,
        AssetType.Project => AIAssetDocumentType.Project,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported MS-011.3F fixture asset type.")
    };

    private sealed record Definition(
        AssetDefinitionKey Key,
        AssetUrn Urn,
        AIAssetDocument Document);
}
