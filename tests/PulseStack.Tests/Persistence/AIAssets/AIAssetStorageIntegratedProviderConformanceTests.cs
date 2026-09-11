using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using PulseStack.Abstractions.Persistence.AIAssets.Serialization;
using PulseStack.Abstractions.Persistence.AIAssets.Storage;
using PulseStack.Core.DependencyInjection;
using PulseStack.Core.Persistence.AIAssets.Storage;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetStorageIntegratedProviderConformanceTests : IDisposable
{
    private readonly string rootPath = Path.Combine(
        Path.GetTempPath(),
        "PulseStack.Tests",
        nameof(AIAssetStorageIntegratedProviderConformanceTests),
        Guid.NewGuid().ToString("N"));

    public static TheoryData<string> Providers => new()
    {
        "memory",
        "file"
    };

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task ComposedProviders_ShouldRoundTripOneCanonicalAssetAcrossIndependentGraphs(string providerKind)
    {
        var logicalNamespace = new ProviderNamespace(rootPath);
        using var writerProvider = BuildProvider(providerKind, logicalNamespace);
        using var readerProvider = BuildProvider(providerKind, logicalNamespace);
        var document = CreatePromptDocument();
        var key = CreateKey(document);

        var write = await writerProvider.GetRequiredService<IAIAssetWriter>()
            .WriteAsync(key, document);
        var load = await readerProvider.GetRequiredService<IAIAssetLoader>()
            .LoadAsync(key);

        write.Should().Be(AIAssetWriteResult.Created);
        var loaded = load.Should().BeOfType<AIAssetLoadResult.Loaded>().Subject.Asset;
        loaded.Type.Should().Be(AssetType.Prompt);
        loaded.Id.Should().Be(key.Id);
        loaded.Version.Should().Be(key.Version);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task ComposedWriter_ShouldPreserveImmutableFirstWriteAlgebra(string providerKind)
    {
        var logicalNamespace = new ProviderNamespace(rootPath);
        using var firstProvider = BuildProvider(providerKind, logicalNamespace);
        using var secondProvider = BuildProvider(providerKind, logicalNamespace);
        var original = CreatePromptDocument(name: "Original");
        var conflicting = CreatePromptDocument(
            id: original.Identity.Id,
            version: original.Identity.Version,
            name: "Conflicting");
        var key = CreateKey(original);

        var first = await firstProvider.GetRequiredService<IAIAssetWriter>().WriteAsync(key, original);
        var identical = await secondProvider.GetRequiredService<IAIAssetWriter>().WriteAsync(key, original);
        var conflict = await secondProvider.GetRequiredService<IAIAssetWriter>().WriteAsync(key, conflicting);
        var loaded = (AIAssetLoadResult.Loaded)await firstProvider.GetRequiredService<IAIAssetLoader>().LoadAsync(key);

        first.Should().Be(AIAssetWriteResult.Created);
        identical.Should().Be(AIAssetWriteResult.AlreadyPresent);
        conflict.Should().Be(AIAssetWriteResult.Conflict);
        loaded.Asset.Metadata.Name.Should().Be("Original");
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task ComposedRepresentationWriter_ShouldAcceptOnlyExactCanonicalBytes(string providerKind)
    {
        var logicalNamespace = new ProviderNamespace(rootPath);
        using var provider = BuildProvider(providerKind, logicalNamespace);
        var document = CreatePromptDocument();
        var key = CreateKey(document);
        var codec = provider.GetRequiredService<IAIAssetDocumentCodec>();
        var canonical = codec.Serialize(document);

        var created = await provider.GetRequiredService<IAIAssetWriter>().WriteAsync(key, canonical);

        created.Should().Be(AIAssetWriteResult.Created);
        var raw = (SerializedAIAssetReadResult.Found)await provider
            .GetRequiredService<ISerializedAIAssetStore>()
            .ReadAsync(key);
        raw.Representation.ToArray().Should().Equal(canonical);

        var other = CreatePromptDocument();
        var otherKey = CreateKey(other);
        var nonCanonical = Encoding.UTF8.GetBytes(" " + codec.SerializeToString(other));
        var act = async () => await provider.GetRequiredService<IAIAssetWriter>()
            .WriteAsync(otherKey, nonCanonical);

        var failure = await act.Should().ThrowAsync<AIAssetStorageException>();
        failure.Which.Category.Should().Be(AIAssetStorageFailureCategory.NonCanonicalRepresentation);
        (await provider.GetRequiredService<ISerializedAIAssetStore>().ReadAsync(otherKey))
            .Should().BeOfType<SerializedAIAssetReadResult.NotFound>();
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task ComposedLoader_ShouldReturnSuccessfulNotFoundForAbsentExactKey(string providerKind)
    {
        var logicalNamespace = new ProviderNamespace(rootPath);
        using var provider = BuildProvider(providerKind, logicalNamespace);

        var result = await provider.GetRequiredService<IAIAssetLoader>()
            .LoadAsync(new AssetDefinitionKey(AssetType.Prompt, AssetId.New(), AssetVersion.Initial));

        result.Should().BeOfType<AIAssetLoadResult.NotFound>();
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task ComposedLoader_ShouldRejectNonCanonicalStoredRepresentation(string providerKind)
    {
        var logicalNamespace = new ProviderNamespace(rootPath);
        using var provider = BuildProvider(providerKind, logicalNamespace);
        var document = CreatePromptDocument();
        var key = CreateKey(document);
        var codec = provider.GetRequiredService<IAIAssetDocumentCodec>();
        var nonCanonical = Encoding.UTF8.GetBytes(" " + codec.SerializeToString(document));

        (await provider.GetRequiredService<ISerializedAIAssetStore>().WriteAsync(key, nonCanonical))
            .Should().Be(AIAssetWriteResult.Created);

        var act = async () => await provider.GetRequiredService<IAIAssetLoader>().LoadAsync(key);

        var failure = await act.Should().ThrowAsync<AIAssetStorageException>();
        failure.Which.Category.Should().Be(AIAssetStorageFailureCategory.NonCanonicalRepresentation);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task ComposedLoader_ShouldApplyRepresentationSizeGateBeforeCodecWork(string providerKind)
    {
        var logicalNamespace = new ProviderNamespace(rootPath);
        using var provider = BuildProvider(providerKind, logicalNamespace, maximumRepresentationSizeBytes: 16);
        var key = new AssetDefinitionKey(AssetType.Prompt, AssetId.New(), AssetVersion.Initial);

        (await provider.GetRequiredService<ISerializedAIAssetStore>()
            .WriteAsync(key, Enumerable.Repeat((byte)1, 17).ToArray()))
            .Should().Be(AIAssetWriteResult.Created);

        var act = async () => await provider.GetRequiredService<IAIAssetLoader>().LoadAsync(key);

        var failure = await act.Should().ThrowAsync<AIAssetStorageException>();
        failure.Which.Category.Should().Be(AIAssetStorageFailureCategory.RepresentationTooLarge);
        failure.Which.Context!.RepresentationSizeBytes.Should().Be(17);
        failure.Which.Context.MaximumRepresentationSizeBytes.Should().Be(16);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task ComposedWriterAndLoader_ShouldPreserveArgumentBeforeCancellationPrecedence(string providerKind)
    {
        var logicalNamespace = new ProviderNamespace(rootPath);
        using var provider = BuildProvider(providerKind, logicalNamespace);
        using var source = new CancellationTokenSource();
        source.Cancel();
        var invalidKey = new AssetDefinitionKey(AssetType.Prompt, AssetId.Empty, AssetVersion.Initial);

        var write = async () => await provider.GetRequiredService<IAIAssetWriter>()
            .WriteAsync(invalidKey, CreatePromptDocument(), source.Token);
        var load = async () => await provider.GetRequiredService<IAIAssetLoader>()
            .LoadAsync(invalidKey, source.Token);

        await write.Should().ThrowAsync<ArgumentException>();
        await load.Should().ThrowAsync<ArgumentException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    private static ServiceProvider BuildProvider(
        string providerKind,
        ProviderNamespace logicalNamespace,
        long maximumRepresentationSizeBytes = 1024 * 1024)
    {
        var services = new ServiceCollection();
        var options = new AIAssetStorageOptions
        {
            MaximumRepresentationSizeBytes = maximumRepresentationSizeBytes
        };

        switch (providerKind)
        {
            case "memory":
                services.AddInMemoryAIAssetStorage(options, logicalNamespace.Memory);
                break;
            case "file":
                services.AddFileAIAssetStorage(logicalNamespace.FileRoot, options);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(providerKind));
        }

        return services.BuildServiceProvider();
    }

    private static PromptAssetDocument CreatePromptDocument(
        string? id = null,
        string version = "1.0.0",
        string name = "Prompt") =>
        new(
            AIAssetSchemaVersion.V1,
            new AIAssetIdentityDocument
            {
                Id = id ?? Guid.NewGuid().ToString(),
                Urn = $"urn:pulsestack:prompt:{id ?? "prompt"}",
                Version = version
            },
            new AIAssetMetadataDocument(name),
            AIAssetLifecycleDocument.Published,
            "Follow the instructions exactly.");

    private static AssetDefinitionKey CreateKey(PromptAssetDocument document) =>
        new(
            AssetType.Prompt,
            new AssetId(Guid.Parse(document.Identity.Id)),
            new AssetVersion(document.Identity.Version));

    private sealed class ProviderNamespace
    {
        public ProviderNamespace(string fileRoot)
        {
            FileRoot = fileRoot;
        }

        public InMemorySerializedAIAssetStoreNamespace Memory { get; } = new();

        public string FileRoot { get; }
    }
}
