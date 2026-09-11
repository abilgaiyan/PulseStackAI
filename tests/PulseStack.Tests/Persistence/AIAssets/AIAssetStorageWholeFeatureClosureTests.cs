using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Persistence.AIAssets.Storage;
using PulseStack.Core.DependencyInjection;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetStorageWholeFeatureClosureTests
{
    [Fact]
    public void PublicStorageOperationAlgebra_ShouldRemainExactAndImmutable()
    {
        typeof(ISerializedAIAssetStore).GetMethods()
            .Select(method => method.Name)
            .Should().BeEquivalentTo(
                [nameof(ISerializedAIAssetStore.ReadAsync), nameof(ISerializedAIAssetStore.WriteAsync)],
                options => options.WithStrictOrdering());

        typeof(IAIAssetLoader).GetMethods()
            .Select(method => method.Name)
            .Should().Equal(nameof(IAIAssetLoader.LoadAsync));

        typeof(IAIAssetWriter).GetMethods()
            .Select(method => method.Name)
            .Should().OnlyContain(name => name == nameof(IAIAssetWriter.WriteAsync));

        typeof(IAIAssetWriter).GetMethods().Should().HaveCount(2);
    }

    [Fact]
    public void AIAssetStorageComposition_ShouldNotRegisterHistoricalOrDeferredAuthorities()
    {
        var services = new ServiceCollection();
        services.AddInMemoryAIAssetStorage(
            new AIAssetStorageOptions { MaximumRepresentationSizeBytes = 1024 * 1024 });

        var registeredTypeNames = services
            .Select(descriptor => descriptor.ServiceType.FullName ?? descriptor.ServiceType.Name)
            .ToArray();

        registeredTypeNames.Should().NotContain(name =>
            name.EndsWith(".IWorkflowStore", StringComparison.Ordinal)
            || name.EndsWith(".IWorkflowPackageStore", StringComparison.Ordinal)
            || name.EndsWith(".IWorkflowSerializer", StringComparison.Ordinal)
            || name.EndsWith(".IWorkflowDeserializer", StringComparison.Ordinal)
            || name.EndsWith(".IAssetResolver", StringComparison.Ordinal)
            || name.EndsWith(".IAssetDefinitionCatalog", StringComparison.Ordinal));
    }

    [Fact]
    public void AIAssetStorageComposition_ShouldRequireExplicitRawProviderSelection()
    {
        var overloads = typeof(PersistenceServiceCollectionExtensions)
            .GetMethods()
            .Where(method => method.Name == nameof(PersistenceServiceCollectionExtensions.AddAIAssetStorage))
            .ToArray();

        overloads.Should().ContainSingle();
        overloads[0].GetParameters().Select(parameter => parameter.ParameterType).Should().Equal(
            typeof(IServiceCollection),
            typeof(ISerializedAIAssetStore),
            typeof(AIAssetStorageOptions));
    }

    [Fact]
    public void MS0097PublicSurface_ShouldContainNoMutableOrDiscoveryOperationNames()
    {
        var publicOperationNames = new[]
            {
                typeof(ISerializedAIAssetStore),
                typeof(IAIAssetWriter),
                typeof(IAIAssetLoader)
            }
            .SelectMany(type => type.GetMethods())
            .Select(method => method.Name)
            .ToArray();

        publicOperationNames.Should().NotContain(name =>
            name.Contains("Delete", StringComparison.Ordinal)
            || name.Contains("Update", StringComparison.Ordinal)
            || name.Contains("Overwrite", StringComparison.Ordinal)
            || name.Contains("Upsert", StringComparison.Ordinal)
            || name.Contains("Exists", StringComparison.Ordinal)
            || name.Contains("List", StringComparison.Ordinal)
            || name.Contains("Search", StringComparison.Ordinal)
            || name.Contains("Resolve", StringComparison.Ordinal)
            || name.Contains("Latest", StringComparison.Ordinal));
    }
}
