using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;
using PulseStack.Abstractions.Persistence.AIAssets.Storage;
using PulseStack.Core.DependencyInjection;
using PulseStack.Core.Persistence.AIAssets.Catalog;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetCatalogCompositionTests
{
    private static readonly AIAssetStorageOptions StorageOptions = new()
    {
        MaximumRepresentationSizeBytes = 1024 * 1024
    };

    [Fact]
    public void InMemoryStorageAndInMemoryCatalog_ShouldComposeAsTransientAuthorities()
    {
        var services = new ServiceCollection();
        services.AddInMemoryAIAssetStorage(StorageOptions);

        services.AddInMemoryAIAssetCatalog();

        services.Single(x => x.ServiceType == typeof(AIAssetStorageCapabilityProfile))
            .ImplementationInstance.Should().Be(new AIAssetStorageCapabilityProfile(AIAssetAuthorityDurability.Transient));
        services.Single(x => x.ServiceType == typeof(AIAssetCatalogCapabilityProfile))
            .ImplementationInstance.Should().Be(new AIAssetCatalogCapabilityProfile(AIAssetAuthorityDurability.Transient));
        AssertCatalogSingletonDescriptors(services);
    }

    [Fact]
    public void FileStorageAndInMemoryCatalog_ShouldAllowCatalogDurabilityBelowStorageDurability()
    {
        using var storageRoot = new TemporaryDirectory();
        var services = new ServiceCollection();
        services.AddFileAIAssetStorage(storageRoot.Path, StorageOptions);

        var act = () => services.AddInMemoryAIAssetCatalog();

        act.Should().NotThrow();
    }

    [Fact]
    public void FileStorageAndFileCatalog_ShouldComposeAsDurableAuthoritiesWithIndependentRoots()
    {
        using var storageRoot = new TemporaryDirectory();
        using var catalogRoot = new TemporaryDirectory();
        var services = new ServiceCollection();
        services.AddFileAIAssetStorage(storageRoot.Path, StorageOptions);

        services.AddFileAIAssetCatalog(catalogRoot.Path);

        services.Single(x => x.ServiceType == typeof(AIAssetStorageCapabilityProfile))
            .ImplementationInstance.Should().Be(new AIAssetStorageCapabilityProfile(AIAssetAuthorityDurability.Durable));
        services.Single(x => x.ServiceType == typeof(AIAssetCatalogCapabilityProfile))
            .ImplementationInstance.Should().Be(new AIAssetCatalogCapabilityProfile(AIAssetAuthorityDurability.Durable));
        AssertCatalogSingletonDescriptors(services);
    }

    [Fact]
    public void InMemoryStorageAndFileCatalog_ShouldRejectDurableCatalogOverTransientStorage()
    {
        using var catalogRoot = new TemporaryDirectory();
        var services = new ServiceCollection();
        services.AddInMemoryAIAssetStorage(StorageOptions);

        var act = () => services.AddFileAIAssetCatalog(catalogRoot.Path);

        var exception = act.Should().Throw<AIAssetCatalogException>().Which;
        exception.Category.Should().Be(AIAssetCatalogFailureCategory.CompositionConfiguration);
        services.Should().NotContain(x => x.ServiceType == typeof(IAIAssetCatalogProvider));
    }

    [Fact]
    public void LegacyCustomStorageWithoutDurabilityEvidence_ShouldRemainStorageCompatibleButRejectCatalogComposition()
    {
        var services = new ServiceCollection();
        services.AddAIAssetStorage(new TestSerializedStore(), StorageOptions);

        services.Should().ContainSingle(x => x.ServiceType == typeof(IAIAssetLoader));
        services.Should().NotContain(x => x.ServiceType == typeof(AIAssetStorageCapabilityProfile));

        var act = () => services.AddInMemoryAIAssetCatalog();

        var exception = act.Should().Throw<AIAssetCatalogException>().Which;
        exception.Category.Should().Be(AIAssetCatalogFailureCategory.CompositionConfiguration);
    }

    [Fact]
    public void CustomStorageWithExplicitTransientEvidence_ShouldAllowTransientCatalog()
    {
        var services = new ServiceCollection();
        services.AddAIAssetStorage(new TestSerializedStore(), StorageOptions);
        services.AddAIAssetStorageCapability(
            new AIAssetStorageCapabilityProfile(AIAssetAuthorityDurability.Transient));

        var act = () => services.AddInMemoryAIAssetCatalog();

        act.Should().NotThrow();
    }

    [Fact]
    public void CustomStorageWithExplicitDurableEvidence_ShouldAllowDurableCatalog()
    {
        using var catalogRoot = new TemporaryDirectory();
        var services = new ServiceCollection();
        services.AddAIAssetStorage(new TestSerializedStore(), StorageOptions);
        services.AddAIAssetStorageCapability(
            new AIAssetStorageCapabilityProfile(AIAssetAuthorityDurability.Durable));

        var act = () => services.AddFileAIAssetCatalog(catalogRoot.Path);

        act.Should().NotThrow();
    }

    [Fact]
    public void CatalogCompositionWithoutStorageLoader_ShouldFailBeforeRegistration()
    {
        var services = new ServiceCollection();

        var act = () => services.AddInMemoryAIAssetCatalog();

        var exception = act.Should().Throw<AIAssetCatalogException>().Which;
        exception.Category.Should().Be(AIAssetCatalogFailureCategory.CompositionConfiguration);
        services.Should().NotContain(x => x.ServiceType == typeof(IAIAssetCatalogProvider));
    }

    [Fact]
    public void ScopedLoaderAndCapabilityMetadataWithoutSelectedAuthority_ShouldBeRejected()
    {
        var services = new ServiceCollection();
        services.AddScoped<IAIAssetLoader, TestLoader>();
        services.AddSingleton(new AIAssetStorageCapabilityProfile(AIAssetAuthorityDurability.Transient));

        var act = () => services.AddInMemoryAIAssetCatalog();

        var exception = act.Should().Throw<AIAssetCatalogException>().Which;
        exception.Category.Should().Be(AIAssetCatalogFailureCategory.CompositionConfiguration);
        services.Should().NotContain(x => x.ServiceType == typeof(IAIAssetCatalogProvider));
    }

    [Fact]
    public void AdditionalLoaderFacadeRegistration_ShouldNotCreateASecondSelectedAuthority()
    {
        var services = new ServiceCollection();
        services.AddInMemoryAIAssetStorage(StorageOptions);
        services.AddTransient<IAIAssetLoader, TestLoader>();

        var act = () => services.AddInMemoryAIAssetCatalog();

        act.Should().NotThrow();
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IAIAssetPublisher>().Should().BeOfType<AIAssetPublisher>();
        provider.GetRequiredService<IPersistentAIAssetResolver>().Should().BeOfType<PersistentAIAssetResolver>();
    }

    [Fact]
    public void DuplicateCatalogComposition_ShouldBeRejected()
    {
        var services = new ServiceCollection();
        services.AddInMemoryAIAssetStorage(StorageOptions);
        services.AddInMemoryAIAssetCatalog();

        var act = () => services.AddInMemoryAIAssetCatalog();

        var exception = act.Should().Throw<AIAssetCatalogException>().Which;
        exception.Category.Should().Be(AIAssetCatalogFailureCategory.CompositionConfiguration);
    }

    [Fact]
    public void PartialCatalogRegistration_ShouldBeRejected()
    {
        var services = new ServiceCollection();
        services.AddInMemoryAIAssetStorage(StorageOptions);
        services.AddSingleton<IAIAssetCatalogProvider>(new TestCatalogProvider());

        var act = () => services.AddInMemoryAIAssetCatalog();

        var exception = act.Should().Throw<AIAssetCatalogException>().Which;
        exception.Category.Should().Be(AIAssetCatalogFailureCategory.CompositionConfiguration);
    }

    [Fact]
    public void UndefinedStorageDurability_ShouldBeRejectedForExplicitCustomStorage()
    {
        var services = new ServiceCollection();
        services.AddAIAssetStorage(new TestSerializedStore(), StorageOptions);
        var capability = new AIAssetStorageCapabilityProfile((AIAssetAuthorityDurability)42);

        var act = () => services.AddAIAssetStorageCapability(capability);

        var exception = act.Should().Throw<AIAssetStorageException>().Which;
        exception.Category.Should().Be(AIAssetStorageFailureCategory.CompositionConfiguration);
    }

    [Fact]
    public void UndefinedCatalogDurability_ShouldBeRejectedForCustomCatalog()
    {
        var services = new ServiceCollection();
        services.AddInMemoryAIAssetStorage(StorageOptions);
        var capability = new AIAssetCatalogCapabilityProfile((AIAssetAuthorityDurability)42);

        var act = () => services.AddAIAssetCatalog(new TestCatalogProvider(), capability);

        var exception = act.Should().Throw<AIAssetCatalogException>().Which;
        exception.Category.Should().Be(AIAssetCatalogFailureCategory.CompositionConfiguration);
    }

    [Fact]
    public void CatalogServices_ShouldResolveAsSingletonsAndShareConfiguredAuthorities()
    {
        var services = new ServiceCollection();
        services.AddInMemoryAIAssetStorage(StorageOptions);
        services.AddInMemoryAIAssetCatalog();
        using var provider = services.BuildServiceProvider();

        var catalog1 = provider.GetRequiredService<IAIAssetCatalogProvider>();
        var catalog2 = provider.GetRequiredService<IAIAssetCatalogProvider>();
        var publisher1 = provider.GetRequiredService<IAIAssetPublisher>();
        var publisher2 = provider.GetRequiredService<IAIAssetPublisher>();
        var resolver1 = provider.GetRequiredService<IPersistentAIAssetResolver>();
        var resolver2 = provider.GetRequiredService<IPersistentAIAssetResolver>();
        var loader1 = provider.GetRequiredService<IAIAssetLoader>();
        var loader2 = provider.GetRequiredService<IAIAssetLoader>();

        catalog1.Should().BeSameAs(catalog2);
        publisher1.Should().BeSameAs(publisher2);
        resolver1.Should().BeSameAs(resolver2);
        loader1.Should().BeSameAs(loader2);
    }

    [Fact]
    public void CustomCatalogProvider_ShouldBeTheExactComposedAuthority()
    {
        var services = new ServiceCollection();
        services.AddInMemoryAIAssetStorage(StorageOptions);
        var catalog = new TestCatalogProvider();
        services.AddAIAssetCatalog(
            catalog,
            new AIAssetCatalogCapabilityProfile(AIAssetAuthorityDurability.Transient));
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IAIAssetCatalogProvider>().Should().BeSameAs(catalog);
        provider.GetRequiredService<IAIAssetPublisher>().Should().BeOfType<AIAssetPublisher>();
        provider.GetRequiredService<IPersistentAIAssetResolver>().Should().BeOfType<PersistentAIAssetResolver>();
    }

    private static void AssertCatalogSingletonDescriptors(IServiceCollection services)
    {
        services.Single(x => x.ServiceType == typeof(IAIAssetCatalogProvider)).Lifetime
            .Should().Be(ServiceLifetime.Singleton);
        services.Single(x => x.ServiceType == typeof(AIAssetCatalogCapabilityProfile)).Lifetime
            .Should().Be(ServiceLifetime.Singleton);
        services.Single(x => x.ServiceType == typeof(IAIAssetPublisher)).Lifetime
            .Should().Be(ServiceLifetime.Singleton);
        services.Single(x => x.ServiceType == typeof(IPersistentAIAssetResolver)).Lifetime
            .Should().Be(ServiceLifetime.Singleton);
        services.Single(x => x.ServiceType == typeof(IAIAssetLoader)).Lifetime
            .Should().Be(ServiceLifetime.Singleton);
    }

    private sealed class TestSerializedStore : ISerializedAIAssetStore
    {
        public ValueTask<SerializedAIAssetReadResult> ReadAsync(
            AssetDefinitionKey key,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<SerializedAIAssetReadResult>(new SerializedAIAssetReadResult.NotFound());

        public ValueTask<AIAssetWriteResult> WriteAsync(
            AssetDefinitionKey key,
            ReadOnlyMemory<byte> representation,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(AIAssetWriteResult.Created);
    }

    private sealed class TestLoader : IAIAssetLoader
    {
        public ValueTask<AIAssetLoadResult> LoadAsync(
            AssetDefinitionKey key,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<AIAssetLoadResult>(new AIAssetLoadResult.NotFound());
    }

    private sealed class TestCatalogProvider : IAIAssetCatalogProvider
    {
        public ValueTask<ExactCatalogLookupResult> FindExactAsync(
            AssetDefinitionKey key,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<ExactCatalogLookupResult>(new ExactCatalogLookupResult.NotFound());

        public ValueTask<CatalogLineageLookupResult> FindLineageAsync(
            AssetUrn urn,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<CatalogLineageLookupResult>(new CatalogLineageLookupResult.NotFound());

        public ValueTask<CatalogPublicationResult> PublishAsync(
            CatalogRecord record,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(CatalogPublicationResult.Created);
    }

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
