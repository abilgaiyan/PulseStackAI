using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;
using PulseStack.Abstractions.Persistence.AIAssets.Storage;
using PulseStack.Core.DependencyInjection;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetCatalogWholeFeatureClosureTests
{
    private static readonly AIAssetStorageOptions StorageOptions = new()
    {
        MaximumRepresentationSizeBytes = 1024 * 1024
    };

    [Fact]
    public void PublicCatalogOperationAlgebra_ShouldRemainExactAndClosed()
    {
        typeof(IAIAssetCatalogProvider).GetMethods()
            .Select(method => method.Name)
            .Should().BeEquivalentTo(
                [
                    nameof(IAIAssetCatalogProvider.FindExactAsync),
                    nameof(IAIAssetCatalogProvider.FindLineageAsync),
                    nameof(IAIAssetCatalogProvider.PublishAsync)
                ],
                options => options.WithStrictOrdering());

        typeof(IAIAssetPublisher).GetMethods()
            .Select(method => method.Name)
            .Should().Equal(nameof(IAIAssetPublisher.PublishAsync));

        typeof(IPersistentAIAssetResolver).GetMethods()
            .Select(method => method.Name)
            .Should().Equal(
                nameof(IPersistentAIAssetResolver.ResolveAsync),
                nameof(IPersistentAIAssetResolver.ResolveAsync),
                nameof(IPersistentAIAssetResolver.ResolveAsync),
                nameof(IPersistentAIAssetResolver.DiscoverLineageAsync));
    }

    [Fact]
    public void PublicCatalogContracts_ShouldContainNoDeferredMutableDiscoveryOrVersionSelectionOperations()
    {
        var operationNames = new[]
            {
                typeof(IAIAssetCatalogProvider),
                typeof(IAIAssetPublisher),
                typeof(IPersistentAIAssetResolver)
            }
            .SelectMany(type => type.GetMethods())
            .Select(method => method.Name)
            .ToArray();

        operationNames.Should().NotContain(name =>
            name.Contains("Delete", StringComparison.Ordinal)
            || name.Contains("Remove", StringComparison.Ordinal)
            || name.Contains("Unpublish", StringComparison.Ordinal)
            || name.Contains("Withdraw", StringComparison.Ordinal)
            || name.Contains("Update", StringComparison.Ordinal)
            || name.Contains("Replace", StringComparison.Ordinal)
            || name.Contains("Upsert", StringComparison.Ordinal)
            || name.Contains("Repair", StringComparison.Ordinal)
            || name.Contains("Rebuild", StringComparison.Ordinal)
            || name.Contains("List", StringComparison.Ordinal)
            || name.Contains("Search", StringComparison.Ordinal)
            || name.Contains("Latest", StringComparison.Ordinal)
            || name.Contains("Range", StringComparison.Ordinal)
            || name.Contains("Enumerate", StringComparison.Ordinal));
    }

    [Fact]
    public void CatalogProviderContract_ShouldRemainPortableAndProviderNeutral()
    {
        typeof(IAIAssetCatalogProvider).Assembly.Should().Be(typeof(IAIAssetPublisher).Assembly);

        var exposedTypes = typeof(IAIAssetCatalogProvider)
            .GetMethods()
            .SelectMany(method => method.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .Append(method.ReturnType))
            .ToArray();

        exposedTypes.Should().OnlyContain(type =>
            !ContainsProviderSpecificSurface(type));
    }

    [Fact]
    public void PublicationModelsAndResults_ShouldExposeNoMutablePublicState()
    {
        var closedTypes = new[]
        {
            typeof(CatalogRecord),
            typeof(CatalogLineage),
            typeof(ExactCatalogLookupResult.Found),
            typeof(CatalogLineageLookupResult.Found),
            typeof(AIAssetResolutionResult.Resolved),
            typeof(AIAssetStorageCapabilityProfile),
            typeof(AIAssetCatalogCapabilityProfile)
        };

        closedTypes
            .SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            .Should().OnlyContain(property => property.SetMethod is null);

        typeof(CatalogLineage).GetProperty(nameof(CatalogLineage.PublishedVersions))!
            .PropertyType.Should().Be(typeof(IReadOnlySet<PulseStack.Abstractions.Assets.AssetVersion>));
    }

    [Fact]
    public void PersistentCatalogComposition_ShouldRemainSeparateFromRuntimeResolutionAuthorities()
    {
        var services = new ServiceCollection();
        services.AddInMemoryAIAssetStorage(StorageOptions);
        services.AddInMemoryAIAssetCatalog();

        var registeredTypeNames = services
            .Select(descriptor => descriptor.ServiceType.FullName ?? descriptor.ServiceType.Name)
            .ToArray();

        registeredTypeNames.Should().Contain(typeof(IPersistentAIAssetResolver).FullName!);
        registeredTypeNames.Should().Contain(typeof(IAIAssetCatalogProvider).FullName!);
        registeredTypeNames.Should().NotContain(name =>
            name.EndsWith(".IAssetResolver", StringComparison.Ordinal)
            || name.EndsWith(".IAssetDefinitionCatalog", StringComparison.Ordinal));
    }

    [Fact]
    public void PersistentResolverContract_ShouldRemainDeclarativeAndNonAggregate()
    {
        var methods = typeof(IPersistentAIAssetResolver).GetMethods();

        methods.Should().OnlyContain(method =>
            method.ReturnType == typeof(ValueTask<AIAssetResolutionResult>)
            || method.ReturnType == typeof(ValueTask<CatalogLineageLookupResult>));

        methods.SelectMany(method => method.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .Should().NotContain(type =>
                typeof(System.Collections.IEnumerable).IsAssignableFrom(type)
                && type != typeof(string));
    }

    [Fact]
    public void CatalogCompositionSurface_ShouldRequireExplicitProviderOrBuiltInProviderSelection()
    {
        var methods = typeof(PersistenceServiceCollectionExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name is
                nameof(PersistenceServiceCollectionExtensions.AddAIAssetCatalog)
                or nameof(PersistenceServiceCollectionExtensions.AddInMemoryAIAssetCatalog)
                or nameof(PersistenceServiceCollectionExtensions.AddFileAIAssetCatalog))
            .ToArray();

        methods.Should().HaveCount(3);

        methods.Single(method => method.Name == nameof(PersistenceServiceCollectionExtensions.AddAIAssetCatalog))
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .Should().Equal(
                typeof(IServiceCollection),
                typeof(IAIAssetCatalogProvider),
                typeof(AIAssetCatalogCapabilityProfile));

        methods.Single(method => method.Name == nameof(PersistenceServiceCollectionExtensions.AddInMemoryAIAssetCatalog))
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .Should().Equal(typeof(IServiceCollection));

        methods.Single(method => method.Name == nameof(PersistenceServiceCollectionExtensions.AddFileAIAssetCatalog))
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .Should().Equal(typeof(IServiceCollection), typeof(string));
    }

    private static bool ContainsProviderSpecificSurface(Type type)
    {
        if (type.IsGenericType)
        {
            if (ContainsProviderSpecificSurface(type.GetGenericTypeDefinition()))
            {
                return true;
            }

            return type.GetGenericArguments().Any(ContainsProviderSpecificSurface);
        }

        var name = type.FullName ?? type.Name;
        return name.Contains("FileAIAsset", StringComparison.Ordinal)
            || name.Contains("InMemoryAIAsset", StringComparison.Ordinal)
            || name.Contains("System.IO.File", StringComparison.Ordinal)
            || type == typeof(string) && false;
    }
}
