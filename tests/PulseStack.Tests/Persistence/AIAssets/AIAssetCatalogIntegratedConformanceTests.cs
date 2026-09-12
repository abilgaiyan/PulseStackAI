using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using PulseStack.Abstractions.Persistence.AIAssets.Serialization;
using PulseStack.Abstractions.Persistence.AIAssets.Storage;
using PulseStack.Core.DependencyInjection;
using PulseStack.Core.Persistence.AIAssets.Catalog;
using PulseStack.Core.Persistence.AIAssets.Storage;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetCatalogIntegratedConformanceTests
{
    private static readonly AIAssetStorageOptions StorageOptions = new()
    {
        MaximumRepresentationSizeBytes = 1024 * 1024
    };

    [Fact]
    public async Task InMemoryStorageAndCatalog_ShouldAgreeAcrossWritePublishAndEveryResolutionShape()
    {
        var services = new ServiceCollection();
        services.AddInMemoryAIAssetStorage(StorageOptions);
        services.AddInMemoryAIAssetCatalog();
        using var provider = services.BuildServiceProvider();

        await AssertWritePublishAndResolveAgreementAsync(provider);
    }

    [Fact]
    public async Task FileStorageAndInMemoryCatalog_ShouldAgreeAcrossWritePublishAndEveryResolutionShape()
    {
        using var storageRoot = new TemporaryDirectory();
        var services = new ServiceCollection();
        services.AddFileAIAssetStorage(storageRoot.Path, StorageOptions);
        services.AddInMemoryAIAssetCatalog();
        using var provider = services.BuildServiceProvider();

        await AssertWritePublishAndResolveAgreementAsync(provider);
    }

    [Fact]
    public async Task FileStorageAndFileCatalog_ShouldRetainPublishedResolutionAcrossReopen()
    {
        using var storageRoot = new TemporaryDirectory();
        using var catalogRoot = new TemporaryDirectory();
        var definition = CreatePromptDefinition();

        using (var firstProvider = BuildFileProvider(storageRoot.Path, catalogRoot.Path))
        {
            var writer = firstProvider.GetRequiredService<IAIAssetWriter>();
            var publisher = firstProvider.GetRequiredService<IAIAssetPublisher>();

            (await writer.WriteAsync(definition.Key, definition.Document)).Should().Be(AIAssetWriteResult.Created);
            (await publisher.PublishAsync(definition.Key)).Should().Be(AIAssetPublicationResult.Published);
        }

        FileAIAssetCatalogProvider.ResetProcessCoordinationForTests(catalogRoot.Path);

        using var reopenedProvider = BuildFileProvider(storageRoot.Path, catalogRoot.Path);
        var resolver = reopenedProvider.GetRequiredService<IPersistentAIAssetResolver>();

        var exact = await resolver.ResolveAsync(definition.Key);
        AssertResolvedIdentity(exact, definition);

        var reference = new AssetReference(
            definition.Key.Type,
            definition.Key.Id,
            definition.Urn,
            definition.Key.Version);
        AssertResolvedIdentity(await resolver.ResolveAsync(reference), definition);
        AssertResolvedIdentity(await resolver.ResolveAsync(definition.Urn, definition.Key.Version), definition);

        var lineage = await resolver.DiscoverLineageAsync(definition.Urn);
        var found = lineage.Should().BeOfType<CatalogLineageLookupResult.Found>().Subject.Lineage;
        found.Type.Should().Be(definition.Key.Type);
        found.Id.Should().Be(definition.Key.Id);
        found.Urn.Should().Be(definition.Urn);
        found.PublishedVersions.Should().ContainSingle().Which.Should().Be(definition.Key.Version);
    }

    [Fact]
    public async Task ConcurrentIncompatiblePublication_ShouldCommitOneLineageAuthorityAndRejectTheOther()
    {
        var services = new ServiceCollection();
        services.AddInMemoryAIAssetStorage(StorageOptions);
        services.AddInMemoryAIAssetCatalog();
        using var provider = services.BuildServiceProvider();

        var id = AssetId.New();
        var first = CreatePromptDefinition(id, new AssetVersion("1.0"), new AssetUrn("urn:pulsestack:prompt:concurrent-a"));
        var second = CreatePromptDefinition(id, new AssetVersion("2.0"), new AssetUrn("urn:pulsestack:prompt:concurrent-b"));
        var writer = provider.GetRequiredService<IAIAssetWriter>();
        var publisher = provider.GetRequiredService<IAIAssetPublisher>();
        var resolver = provider.GetRequiredService<IPersistentAIAssetResolver>();

        (await writer.WriteAsync(first.Key, first.Document)).Should().Be(AIAssetWriteResult.Created);
        (await writer.WriteAsync(second.Key, second.Document)).Should().Be(AIAssetWriteResult.Created);

        using var gate = new Barrier(2);
        async Task<AIAssetPublicationResult> PublishAfterGateAsync(AssetDefinitionKey key)
        {
            return await Task.Run(async () =>
            {
                gate.SignalAndWait();
                return await publisher.PublishAsync(key);
            });
        }

        var results = await Task.WhenAll(
            PublishAfterGateAsync(first.Key),
            PublishAfterGateAsync(second.Key));

        results.Should().ContainSingle(result => result == AIAssetPublicationResult.Published);
        results.Should().ContainSingle(result => result == AIAssetPublicationResult.IdentityConflict);

        var firstLineage = await resolver.DiscoverLineageAsync(first.Urn);
        var secondLineage = await resolver.DiscoverLineageAsync(second.Urn);
        var publishedLineages = new[] { firstLineage, secondLineage }
            .OfType<CatalogLineageLookupResult.Found>()
            .ToArray();
        publishedLineages.Should().ContainSingle();
        publishedLineages[0].Lineage.PublishedVersions.Should().ContainSingle();
    }

    [Fact]
    public async Task DurableCatalog_WhenStoredDefinitionDisappears_ShouldPreservePublishedDefinitionUnavailableBoundary()
    {
        using var storageRoot = new TemporaryDirectory();
        using var catalogRoot = new TemporaryDirectory();
        using var provider = BuildFileProvider(storageRoot.Path, catalogRoot.Path);
        var definition = CreatePromptDefinition();
        var writer = provider.GetRequiredService<IAIAssetWriter>();
        var publisher = provider.GetRequiredService<IAIAssetPublisher>();
        var resolver = provider.GetRequiredService<IPersistentAIAssetResolver>();
        var store = (FileSerializedAIAssetStore)provider.GetRequiredService<ISerializedAIAssetStore>();

        (await writer.WriteAsync(definition.Key, definition.Document)).Should().Be(AIAssetWriteResult.Created);
        (await publisher.PublishAsync(definition.Key)).Should().Be(AIAssetPublicationResult.Published);
        File.Delete(store.ResolveAssetPath(definition.Key));

        var act = async () => await resolver.ResolveAsync(definition.Key);

        var exception = (await act.Should().ThrowAsync<AIAssetCatalogBoundaryException>()).Which;
        exception.Category.Should().Be(AIAssetCatalogBoundaryFailureCategory.PublishedDefinitionUnavailable);
    }

    [Fact]
    public async Task DurableCatalog_WhenStoredDefinitionUrnChanges_ShouldPreserveCatalogAssetIdentityMismatchBoundary()
    {
        using var storageRoot = new TemporaryDirectory();
        using var catalogRoot = new TemporaryDirectory();
        using var provider = BuildFileProvider(storageRoot.Path, catalogRoot.Path);
        var definition = CreatePromptDefinition();
        var writer = provider.GetRequiredService<IAIAssetWriter>();
        var publisher = provider.GetRequiredService<IAIAssetPublisher>();
        var resolver = provider.GetRequiredService<IPersistentAIAssetResolver>();
        var store = (FileSerializedAIAssetStore)provider.GetRequiredService<ISerializedAIAssetStore>();
        var codec = provider.GetRequiredService<IAIAssetDocumentCodec>();

        (await writer.WriteAsync(definition.Key, definition.Document)).Should().Be(AIAssetWriteResult.Created);
        (await publisher.PublishAsync(definition.Key)).Should().Be(AIAssetPublicationResult.Published);

        var replacementUrn = new AssetUrn("urn:pulsestack:prompt:replacement");
        var replacement = CreatePromptDefinition(definition.Key.Id, definition.Key.Version, replacementUrn);
        var replacementBytes = codec.Serialize(replacement.Document);
        await File.WriteAllBytesAsync(store.ResolveAssetPath(definition.Key), replacementBytes);

        var act = async () => await resolver.ResolveAsync(definition.Key);

        var exception = (await act.Should().ThrowAsync<AIAssetCatalogBoundaryException>()).Which;
        exception.Category.Should().Be(AIAssetCatalogBoundaryFailureCategory.CatalogAssetIdentityMismatch);
    }

    private static ServiceProvider BuildFileProvider(string storageRoot, string catalogRoot)
    {
        var services = new ServiceCollection();
        services.AddFileAIAssetStorage(storageRoot, StorageOptions);
        services.AddFileAIAssetCatalog(catalogRoot);
        return services.BuildServiceProvider();
    }

    private static async Task AssertWritePublishAndResolveAgreementAsync(IServiceProvider provider)
    {
        var definition = CreatePromptDefinition();
        var writer = provider.GetRequiredService<IAIAssetWriter>();
        var publisher = provider.GetRequiredService<IAIAssetPublisher>();
        var resolver = provider.GetRequiredService<IPersistentAIAssetResolver>();

        (await writer.WriteAsync(definition.Key, definition.Document)).Should().Be(AIAssetWriteResult.Created);
        (await publisher.PublishAsync(definition.Key)).Should().Be(AIAssetPublicationResult.Published);
        (await publisher.PublishAsync(definition.Key)).Should().Be(AIAssetPublicationResult.AlreadyPublished);

        AssertResolvedIdentity(await resolver.ResolveAsync(definition.Key), definition);

        var reference = new AssetReference(
            definition.Key.Type,
            definition.Key.Id,
            definition.Urn,
            definition.Key.Version);
        AssertResolvedIdentity(await resolver.ResolveAsync(reference), definition);
        AssertResolvedIdentity(await resolver.ResolveAsync(definition.Urn, definition.Key.Version), definition);

        var lineage = await resolver.DiscoverLineageAsync(definition.Urn);
        var found = lineage.Should().BeOfType<CatalogLineageLookupResult.Found>().Subject.Lineage;
        found.Type.Should().Be(definition.Key.Type);
        found.Id.Should().Be(definition.Key.Id);
        found.Urn.Should().Be(definition.Urn);
        found.PublishedVersions.Should().ContainSingle().Which.Should().Be(definition.Key.Version);
    }

    private static void AssertResolvedIdentity(
        AIAssetResolutionResult result,
        PromptDefinition definition)
    {
        var asset = result.Should().BeOfType<AIAssetResolutionResult.Resolved>().Subject.Asset;
        asset.Type.Should().Be(definition.Key.Type);
        asset.Id.Should().Be(definition.Key.Id);
        asset.Version.Should().Be(definition.Key.Version);
        asset.Urn.Should().Be(definition.Urn);
    }

    private static PromptDefinition CreatePromptDefinition(
        AssetId? id = null,
        AssetVersion? version = null,
        AssetUrn? urn = null)
    {
        var resolvedId = id ?? AssetId.New();
        var resolvedVersion = version ?? new AssetVersion("1.0");
        var resolvedUrn = urn ?? new AssetUrn($"urn:pulsestack:prompt:{resolvedId.Value:N}");
        var key = new AssetDefinitionKey(AssetType.Prompt, resolvedId, resolvedVersion);
        var document = new PromptAssetDocument(
            AIAssetSchemaVersion.V1,
            new AIAssetIdentityDocument
            {
                Id = resolvedId.Value.ToString(),
                Urn = resolvedUrn.Value,
                Version = resolvedVersion.Value
            },
            new AIAssetMetadataDocument("Integrated prompt"),
            AIAssetLifecycleDocument.Draft,
            "Be helpful.");

        return new PromptDefinition(key, resolvedUrn, document);
    }

    private sealed record PromptDefinition(
        AssetDefinitionKey Key,
        AssetUrn Urn,
        PromptAssetDocument Document);

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
