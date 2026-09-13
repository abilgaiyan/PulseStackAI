using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using PulseStack.Abstractions.Persistence.AIAssets.Storage;
using PulseStack.Core.DependencyInjection;
using PulseStack.Core.Persistence.AIAssets.Catalog;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetGraphIntegratedProviderConformanceTests
{
    private static readonly AIAssetStorageOptions StorageOptions = new()
    {
        MaximumRepresentationSizeBytes = 1024 * 1024
    };

    [Fact]
    public async Task PublicInMemoryComposition_ShouldLoadEverySupportedAggregateRoot()
    {
        using var provider = BuildInMemoryProvider();
        var writer = provider.GetRequiredService<IAIAssetWriter>();
        var publisher = provider.GetRequiredService<IAIAssetPublisher>();
        var loader = provider.GetRequiredService<IAIAssetGraphLoader>();

        var projectFixture = CreateProjectFixture();
        var libraryFixture = CreateLibraryFixture();
        var packageFixture = CreatePackageFixture();

        await PersistAndPublishAsync(writer, publisher, projectFixture.Definitions);
        await PersistAndPublishAsync(writer, publisher, libraryFixture.Definitions);
        await PersistAndPublishAsync(writer, publisher, packageFixture.Definitions);

        var project = await LoadSuccessAsync(loader, projectFixture.Root.Key);
        var library = await LoadSuccessAsync(loader, libraryFixture.Root.Key);
        var package = await LoadSuccessAsync(loader, packageFixture.Root.Key);

        project.RootKey.Should().Be(projectFixture.Root.Key);
        project.Nodes.Select(static node => node.DefinitionKey)
            .Should().Contain(projectFixture.Definitions.Select(static definition => definition.Key));

        library.RootKey.Should().Be(libraryFixture.Root.Key);
        library.Nodes.Select(static node => node.DefinitionKey)
            .Should().Contain(libraryFixture.Definitions.Select(static definition => definition.Key));

        package.RootKey.Should().Be(packageFixture.Root.Key);
        package.Nodes.Select(static node => node.DefinitionKey)
            .Should().Contain(packageFixture.Definitions.Select(static definition => definition.Key));
    }

    [Fact]
    public async Task InMemoryAndFileBackedComposition_ShouldReturnExactNormalizedGraphAcrossRestartAndConvergence()
    {
        var fixture = CreateConvergentPackageFixture();
        GraphProjection inMemoryProjection;

        using (var inMemoryProvider = BuildInMemoryProvider())
        {
            await PersistAndPublishAsync(inMemoryProvider, fixture.Definitions);
            var graph = await LoadSuccessAsync(
                inMemoryProvider.GetRequiredService<IAIAssetGraphLoader>(),
                fixture.Root.Key);

            graph.Nodes.Count(node => node.DefinitionKey == fixture.Shared.Key).Should().Be(1);
            graph.Relationships.Count(relationship =>
                    AssetDefinitionKey.From(relationship.TargetReference) == fixture.Shared.Key)
                .Should().Be(2);

            inMemoryProjection = Project(graph);
        }

        using var storageRoot = new TemporaryDirectory();
        using var catalogRoot = new TemporaryDirectory();
        GraphProjection firstFileProjection;

        using (var fileProvider = BuildFileProvider(storageRoot.Path, catalogRoot.Path))
        {
            await PersistAndPublishAsync(fileProvider, fixture.Definitions);
            var graph = await LoadSuccessAsync(
                fileProvider.GetRequiredService<IAIAssetGraphLoader>(),
                fixture.Root.Key);
            firstFileProjection = Project(graph);
        }

        AssertExactGraphProjection(firstFileProjection, inMemoryProjection);

        FileAIAssetCatalogProvider.ResetProcessCoordinationForTests(catalogRoot.Path);

        using var reopenedProvider = BuildFileProvider(storageRoot.Path, catalogRoot.Path);
        var reopenedGraph = await LoadSuccessAsync(
            reopenedProvider.GetRequiredService<IAIAssetGraphLoader>(),
            fixture.Root.Key);
        var reopenedProjection = Project(reopenedGraph);

        AssertExactGraphProjection(reopenedProjection, firstFileProjection);
        AssertExactGraphProjection(reopenedProjection, inMemoryProjection);
    }

    [Fact]
    public async Task RealPersistentResolver_ShouldPublishAag001ForUnpublishedAggregateRoot()
    {
        using var provider = BuildInMemoryProvider();
        var loader = provider.GetRequiredService<IAIAssetGraphLoader>();
        var missingRoot = Key(AssetType.Package);

        var result = await loader.LoadAsync(missingRoot);

        var failure = result.Should().BeOfType<AIAssetGraphLoadResult.RootDefinitionUnavailable>().Subject;
        failure.Context.Code.Should().Be("AAG001");
        failure.Context.RootKey.Should().Be(missingRoot);
        failure.Context.CanonicalPath.Segments.Should().BeEmpty();
    }

    [Fact]
    public async Task RealPersistentResolver_ShouldPublishAag002ForUnpublishedRequiredMember()
    {
        using var provider = BuildInMemoryProvider();
        var missing = CreatePrompt("missing");
        var root = CreatePackage("aag002-root", [Reference(missing)]);

        await PersistAndPublishAsync(provider, [root]);

        var result = await provider.GetRequiredService<IAIAssetGraphLoader>().LoadAsync(root.Key);

        var failure = result.Should().BeOfType<AIAssetGraphLoadResult.RequiredDefinitionUnavailable>().Subject;
        failure.Context.Code.Should().Be("AAG002");
        failure.Context.RootKey.Should().Be(root.Key);
        failure.Context.Relationship.TargetReference.Should().Be(ToAssetReference(missing));
    }

    [Fact]
    public async Task RealPersistentResolver_ShouldPublishAag003ForPublishedDefinitionWithMismatchedAuthoredUrn()
    {
        using var provider = BuildInMemoryProvider();
        var child = CreatePrompt("aag003-child");
        var wrongUrn = new AssetUrn($"{child.Urn.Value}:wrong");
        var mismatchedReference = Reference(child, wrongUrn);
        var root = CreatePackage("aag003-root", [mismatchedReference]);

        await PersistAndPublishAsync(provider, [child, root]);

        var result = await provider.GetRequiredService<IAIAssetGraphLoader>().LoadAsync(root.Key);

        var failure = result.Should().BeOfType<AIAssetGraphLoadResult.ReferenceIdentityConflict>().Subject;
        failure.Context.Code.Should().Be("AAG003");
        failure.Context.RootKey.Should().Be(root.Key);
        failure.Context.Relationship.TargetReference.Urn.Should().Be(wrongUrn);
        failure.Context.Evidence.Should().Be(AIAssetGraphReferenceIdentityConflictEvidence.PersistentResolver);
        failure.Context.PredecessorSemanticOutcome.Should().Be(AIAssetGraphPredecessorSemanticOutcome.ReferenceMismatch);
    }

    [Fact]
    public async Task RealPersistentResolver_ShouldPublishAag005ForPersistedRequiredDependencyCycle()
    {
        using var provider = BuildInMemoryProvider();
        var aSeed = CreatePrompt("cycle-a");
        var bSeed = CreatePrompt("cycle-b");
        var a = CreatePrompt("cycle-a", aSeed.Key, aSeed.Urn, [Dependency(bSeed)]);
        var b = CreatePrompt("cycle-b", bSeed.Key, bSeed.Urn, [Dependency(aSeed)]);
        var root = CreatePackage("cycle-root", [Reference(a)]);

        await PersistAndPublishAsync(provider, [a, b, root]);

        var result = await provider.GetRequiredService<IAIAssetGraphLoader>().LoadAsync(root.Key);

        var failure = result.Should().BeOfType<AIAssetGraphLoadResult.RequiredMaterializationCycle>().Subject;
        failure.Context.Code.Should().Be("AAG005");
        failure.Context.RootKey.Should().Be(root.Key);
        failure.Context.CycleEntryKey.Should().Be(a.Key);
        failure.Context.CanonicalPath.Segments.Should().HaveCount(3);
    }

    private static ServiceProvider BuildInMemoryProvider()
    {
        var services = new ServiceCollection();
        services.AddInMemoryAIAssetStorage(StorageOptions);
        services.AddInMemoryAIAssetCatalog();
        services.AddAIAssetGraphLoading();
        return services.BuildServiceProvider();
    }

    private static ServiceProvider BuildFileProvider(string storageRoot, string catalogRoot)
    {
        var services = new ServiceCollection();
        services.AddFileAIAssetStorage(storageRoot, StorageOptions);
        services.AddFileAIAssetCatalog(catalogRoot);
        services.AddAIAssetGraphLoading();
        return services.BuildServiceProvider();
    }

    private static async Task PersistAndPublishAsync(
        IServiceProvider provider,
        IReadOnlyList<Definition> definitions)
    {
        await PersistAndPublishAsync(
            provider.GetRequiredService<IAIAssetWriter>(),
            provider.GetRequiredService<IAIAssetPublisher>(),
            definitions);
    }

    private static async Task PersistAndPublishAsync(
        IAIAssetWriter writer,
        IAIAssetPublisher publisher,
        IReadOnlyList<Definition> definitions)
    {
        foreach (var definition in definitions)
        {
            (await writer.WriteAsync(definition.Key, definition.Document))
                .Should().Be(AIAssetWriteResult.Created);
        }

        foreach (var definition in definitions)
        {
            (await publisher.PublishAsync(definition.Key))
                .Should().Be(AIAssetPublicationResult.Published);
        }
    }

    private static async Task<AIAssetGraph> LoadSuccessAsync(
        IAIAssetGraphLoader loader,
        AssetDefinitionKey rootKey)
    {
        var result = await loader.LoadAsync(rootKey);
        return result.Should().BeOfType<AIAssetGraphLoadResult.Success>().Subject.Graph;
    }

    private static AggregateFixture CreateProjectFixture()
    {
        var workflow = CreateWorkflow("project-entry");
        var entry = Reference(workflow);
        var project = CreateDefinition(
            AssetType.Project,
            "project-root",
            (key, urn) => new ProjectAssetDocument(
                AIAssetSchemaVersion.V1,
                Identity(key, urn),
                Metadata("project-root"),
                AIAssetLifecycleDocument.Draft,
                entry,
                ownedAssets: [entry],
                references: [entry]));

        return new AggregateFixture(project, [workflow, project]);
    }

    private static AggregateFixture CreateLibraryFixture()
    {
        var member = CreatePrompt("library-member");
        var memberReference = Reference(member);
        var library = CreateDefinition(
            AssetType.Library,
            "library-root",
            (key, urn) => new LibraryAssetDocument(
                AIAssetSchemaVersion.V1,
                Identity(key, urn),
                Metadata("library-root"),
                AIAssetLifecycleDocument.Draft,
                members: [memberReference],
                references: [memberReference]));

        return new AggregateFixture(library, [member, library]);
    }

    private static AggregateFixture CreatePackageFixture()
    {
        var member = CreatePrompt("package-member");
        var memberReference = Reference(member);
        var package = CreatePackage("package-root", [memberReference]);
        return new AggregateFixture(package, [member, package]);
    }

    private static ConvergentFixture CreateConvergentPackageFixture()
    {
        var shared = CreatePrompt("shared");
        var left = CreatePrompt("left", dependencies: [Dependency(shared)]);
        var right = CreatePrompt("right", dependencies: [Dependency(shared)]);
        var root = CreatePackage("convergent-root", [Reference(left), Reference(right)]);

        return new ConvergentFixture(root, left, right, shared, [shared, left, right, root]);
    }

    private static Definition CreatePackage(
        string name,
        IReadOnlyList<AIAssetReferenceDocument> members)
    {
        return CreateDefinition(
            AssetType.Package,
            name,
            (key, urn) => new PackageAssetDocument(
                AIAssetSchemaVersion.V1,
                Identity(key, urn),
                Metadata(name),
                AIAssetLifecycleDocument.Draft,
                members,
                references: members));
    }

    private static Definition CreateWorkflow(string name)
    {
        return CreateDefinition(
            AssetType.Workflow,
            name,
            (key, urn) => new WorkflowAssetDocument(
                AIAssetSchemaVersion.V1,
                Identity(key, urn),
                Metadata(name),
                AIAssetLifecycleDocument.Draft));
    }

    private static Definition CreatePrompt(
        string name,
        AssetDefinitionKey? key = null,
        AssetUrn? urn = null,
        IReadOnlyList<AIAssetDependencyDocument>? dependencies = null)
    {
        var resolvedKey = key ?? Key(AssetType.Prompt);
        var resolvedUrn = urn ?? Urn(AssetType.Prompt, resolvedKey.Id, name);
        var document = new PromptAssetDocument(
            AIAssetSchemaVersion.V1,
            Identity(resolvedKey, resolvedUrn),
            Metadata(name),
            AIAssetLifecycleDocument.Draft,
            $"Integrated instructions for {name}.",
            dependencies: dependencies);
        return new Definition(resolvedKey, resolvedUrn, document);
    }

    private static Definition CreateDefinition(
        AssetType type,
        string name,
        Func<AssetDefinitionKey, AssetUrn, AIAssetDocument> createDocument)
    {
        var key = Key(type);
        var urn = Urn(type, key.Id, name);
        return new Definition(key, urn, createDocument(key, urn));
    }

    private static AssetDefinitionKey Key(AssetType type) =>
        new(type, AssetId.New(), AssetVersion.Initial);

    private static AssetUrn Urn(AssetType type, AssetId id, string name) =>
        new($"urn:pulsestack:test:b9:{type.ToString().ToLowerInvariant()}:{name}:{id.Value:N}");

    private static AIAssetIdentityDocument Identity(AssetDefinitionKey key, AssetUrn urn) =>
        new()
        {
            Id = key.Id.Value.ToString(),
            Urn = urn.Value,
            Version = key.Version.Value
        };

    private static AIAssetMetadataDocument Metadata(string name) => new(name);

    private static AIAssetReferenceDocument Reference(Definition definition, AssetUrn? urn = null) =>
        new()
        {
            AssetType = ToDocumentType(definition.Key.Type),
            AssetId = definition.Key.Id.Value.ToString(),
            Urn = (urn ?? definition.Urn).Value,
            Version = definition.Key.Version.Value
        };

    private static AssetReference ToAssetReference(Definition definition) =>
        new(definition.Key.Type, definition.Key.Id, definition.Urn, definition.Key.Version);

    private static AIAssetDependencyDocument Dependency(Definition definition) =>
        new()
        {
            Reference = Reference(definition),
            Required = true
        };

    private static AIAssetDocumentType ToDocumentType(AssetType type) => type switch
    {
        AssetType.Agent => AIAssetDocumentType.Agent,
        AssetType.Workflow => AIAssetDocumentType.Workflow,
        AssetType.Project => AIAssetDocumentType.Project,
        AssetType.Library => AIAssetDocumentType.Library,
        AssetType.Package => AIAssetDocumentType.Package,
        AssetType.Prompt => AIAssetDocumentType.Prompt,
        AssetType.Tool => AIAssetDocumentType.Tool,
        AssetType.Knowledge => AIAssetDocumentType.Knowledge,
        AssetType.Memory => AIAssetDocumentType.Memory,
        AssetType.Policy => AIAssetDocumentType.Policy,
        AssetType.Model => AIAssetDocumentType.Model,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported schema-v1 asset type.")
    };

    private static GraphProjection Project(AIAssetGraph graph) =>
        new(
            graph.RootKey,
            graph.Nodes
                .Select(static node => new NodeProjection(
                    node.DefinitionKey,
                    node.Asset.Type,
                    node.Asset.Id,
                    node.Asset.Urn,
                    node.Asset.Version))
                .ToArray(),
            graph.Relationships
                .Select(static relationship => new RelationshipProjection(
                    relationship.SourceKey,
                    AssetDefinitionKey.From(relationship.TargetReference),
                    relationship.TargetReference.Urn,
                    relationship.RelationshipClass,
                    relationship.MaterializationAuthority,
                    relationship.BoundaryRole,
                    relationship.DependencyRequired,
                    relationship.LocalOrdinal,
                    relationship.AuthoredPath))
                .ToArray());

    private static void AssertExactGraphProjection(GraphProjection actual, GraphProjection expected)
    {
        actual.RootKey.Should().Be(expected.RootKey);
        actual.Nodes.Should().Equal(expected.Nodes);
        actual.Relationships.Should().Equal(expected.Relationships);
    }

    private sealed record Definition(
        AssetDefinitionKey Key,
        AssetUrn Urn,
        AIAssetDocument Document);

    private sealed record AggregateFixture(
        Definition Root,
        IReadOnlyList<Definition> Definitions);

    private sealed record ConvergentFixture(
        Definition Root,
        Definition Left,
        Definition Right,
        Definition Shared,
        IReadOnlyList<Definition> Definitions);

    private sealed record GraphProjection(
        AssetDefinitionKey RootKey,
        IReadOnlyList<NodeProjection> Nodes,
        IReadOnlyList<RelationshipProjection> Relationships);

    private sealed record NodeProjection(
        AssetDefinitionKey DefinitionKey,
        AssetType Type,
        AssetId Id,
        AssetUrn Urn,
        AssetVersion Version);

    private sealed record RelationshipProjection(
        AssetDefinitionKey SourceKey,
        AssetDefinitionKey TargetKey,
        AssetUrn TargetUrn,
        AIAssetGraphRelationshipClass RelationshipClass,
        AIAssetGraphMaterializationAuthority MaterializationAuthority,
        AIAssetGraphBoundaryRole BoundaryRole,
        bool? DependencyRequired,
        int LocalOrdinal,
        string AuthoredPath);

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "PulseStack.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            FileAIAssetCatalogProvider.ResetProcessCoordinationForTests(Path);
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}
