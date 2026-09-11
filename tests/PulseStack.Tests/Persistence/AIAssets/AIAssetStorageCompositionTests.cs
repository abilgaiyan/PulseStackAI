using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Persistence.AIAssets.Mapping;
using PulseStack.Abstractions.Persistence.AIAssets.Serialization;
using PulseStack.Abstractions.Persistence.AIAssets.Storage;
using PulseStack.Abstractions.Persistence.AIAssets.Validation;
using PulseStack.Core.DependencyInjection;
using PulseStack.Core.Persistence.AIAssets.Mapping;
using PulseStack.Core.Persistence.AIAssets.Storage;
using PulseStack.Core.Persistence.AIAssets.Validation;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetStorageCompositionTests : IDisposable
{
    private readonly string rootPath = Path.Combine(
        Path.GetTempPath(),
        "PulseStack.Tests",
        nameof(AIAssetStorageCompositionTests),
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void AddInMemoryAIAssetStorage_ShouldComposeExactlyOneSingletonAuthorityGraph()
    {
        var services = new ServiceCollection();
        var options = CreateOptions();

        services.AddInMemoryAIAssetStorage(options);

        using var provider = services.BuildServiceProvider();

        provider.GetServices<ISerializedAIAssetStore>().Should().ContainSingle()
            .Which.Should().BeOfType<InMemorySerializedAIAssetStore>();
        provider.GetServices<IAIAssetDocumentCodec>().Should().ContainSingle()
            .Which.Should().BeOfType<AIAssetDocumentCodec>();
        provider.GetServices<IAIAssetDocumentValidator>().Should().ContainSingle()
            .Which.Should().BeOfType<AIAssetDocumentValidator>();
        provider.GetServices<IAIAssetDocumentMapper>().Should().ContainSingle()
            .Which.Should().BeOfType<AIAssetDocumentMapper>();
        provider.GetServices<AIAssetStorageOptions>().Should().ContainSingle()
            .Which.Should().BeSameAs(options);

        var firstWriter = provider.GetRequiredService<IAIAssetWriter>();
        var secondWriter = provider.GetRequiredService<IAIAssetWriter>();
        var firstLoader = provider.GetRequiredService<IAIAssetLoader>();
        var secondLoader = provider.GetRequiredService<IAIAssetLoader>();

        firstWriter.Should().BeOfType<AIAssetWriter>();
        secondWriter.Should().BeSameAs(firstWriter);
        firstLoader.Should().BeOfType<AIAssetLoader>();
        secondLoader.Should().BeSameAs(firstLoader);
    }

    [Fact]
    public void AddFileAIAssetStorage_ShouldSelectFileProviderExplicitly()
    {
        var services = new ServiceCollection();

        services.AddFileAIAssetStorage(rootPath, CreateOptions());

        using var provider = services.BuildServiceProvider();
        provider.GetServices<ISerializedAIAssetStore>().Should().ContainSingle()
            .Which.Should().BeOfType<FileSerializedAIAssetStore>();
        provider.GetRequiredService<IAIAssetWriter>().Should().BeOfType<AIAssetWriter>();
        provider.GetRequiredService<IAIAssetLoader>().Should().BeOfType<AIAssetLoader>();
    }

    [Fact]
    public void AddAIAssetStorage_ShouldUseCallerSelectedRawStoreWithoutSubstitution()
    {
        var services = new ServiceCollection();
        ISerializedAIAssetStore store = new InMemorySerializedAIAssetStore();

        services.AddAIAssetStorage(store, CreateOptions());

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ISerializedAIAssetStore>().Should().BeSameAs(store);
    }

    [Fact]
    public void SecondStorageComposition_ShouldFailInsteadOfUsingRegistrationOrder()
    {
        var services = new ServiceCollection();
        services.AddInMemoryAIAssetStorage(CreateOptions());

        Action act = () => services.AddFileAIAssetStorage(rootPath, CreateOptions());

        act.Should().Throw<AIAssetStorageException>()
            .Which.Category.Should().Be(AIAssetStorageFailureCategory.CompositionConfiguration);
    }

    [Fact]
    public void PreRegisteredStorageOwnedService_ShouldFailCompositionDeterministically()
    {
        var services = new ServiceCollection();
        services.AddSingleton(CreateOptions());

        Action act = () => services.AddInMemoryAIAssetStorage(CreateOptions());

        act.Should().Throw<AIAssetStorageException>()
            .Which.Category.Should().Be(AIAssetStorageFailureCategory.CompositionConfiguration);
    }

    [Fact]
    public void MultipleCodecAuthorities_ShouldFailWhenComposedWriterIsResolved()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAIAssetDocumentCodec, AIAssetDocumentCodec>();
        services.AddSingleton<IAIAssetDocumentCodec, AIAssetDocumentCodec>();
        services.AddInMemoryAIAssetStorage(CreateOptions());

        using var provider = services.BuildServiceProvider();
        Action act = () => provider.GetRequiredService<IAIAssetWriter>();

        act.Should().Throw<AIAssetStorageException>()
            .Which.Category.Should().Be(AIAssetStorageFailureCategory.CompositionConfiguration);
    }

    [Fact]
    public void MultipleValidatorAuthorities_ShouldFailWhenComposedLoaderIsResolved()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAIAssetDocumentValidator, AIAssetDocumentValidator>();
        services.AddSingleton<IAIAssetDocumentValidator, AIAssetDocumentValidator>();
        services.AddInMemoryAIAssetStorage(CreateOptions());

        using var provider = services.BuildServiceProvider();
        Action act = () => provider.GetRequiredService<IAIAssetLoader>();

        act.Should().Throw<AIAssetStorageException>()
            .Which.Category.Should().Be(AIAssetStorageFailureCategory.CompositionConfiguration);
    }

    [Fact]
    public void MultipleMapperAuthorities_ShouldFailWhenComposedLoaderIsResolved()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAIAssetDocumentMapper, AIAssetDocumentMapper>();
        services.AddSingleton<IAIAssetDocumentMapper, AIAssetDocumentMapper>();
        services.AddInMemoryAIAssetStorage(CreateOptions());

        using var provider = services.BuildServiceProvider();
        Action act = () => provider.GetRequiredService<IAIAssetLoader>();

        act.Should().Throw<AIAssetStorageException>()
            .Which.Category.Should().Be(AIAssetStorageFailureCategory.CompositionConfiguration);
    }

    [Fact]
    public void InvalidStorageOptions_ShouldFailDuringRegistrationAsCompositionConfiguration()
    {
        var services = new ServiceCollection();
        var invalid = new AIAssetStorageOptions { MaximumRepresentationSizeBytes = 0 };

        Action act = () => services.AddInMemoryAIAssetStorage(invalid);

        act.Should().Throw<AIAssetStorageException>()
            .Which.Category.Should().Be(AIAssetStorageFailureCategory.CompositionConfiguration);
    }

    public void Dispose()
    {
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    private static AIAssetStorageOptions CreateOptions() =>
        new() { MaximumRepresentationSizeBytes = 1024 * 1024 };
}
