using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;
using PulseStack.Core.DependencyInjection;
using PulseStack.Core.Persistence.AIAssets.GraphLoading;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetGraphLoadingCompositionTests
{
    private const string ResolverConfigurationMessage =
        "Exactly one singleton IPersistentAIAssetResolver authority must be configured before AI Asset graph loading.";

    private const string ExistingGraphLoaderMessage =
        "AI Asset graph loading has already been configured or partially configured.";

    [Fact]
    public void AddAIAssetGraphLoading_ShouldRejectNullServices()
    {
        IServiceCollection? services = null;

        var act = () => services!.AddAIAssetGraphLoading();

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("services");
    }

    [Fact]
    public void AddAIAssetGraphLoading_ShouldRejectMissingResolverWithoutMutation()
    {
        var services = new ServiceCollection();
        var before = services.ToArray();

        var act = () => services.AddAIAssetGraphLoading();

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Be(ResolverConfigurationMessage);
        services.Should().Equal(before);
    }

    [Fact]
    public void AddAIAssetGraphLoading_ShouldRejectDuplicateResolversWithoutMutation()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPersistentAIAssetResolver>(new TestResolver());
        services.AddSingleton<IPersistentAIAssetResolver>(new TestResolver());
        var before = services.ToArray();

        var act = () => services.AddAIAssetGraphLoading();

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Be(ResolverConfigurationMessage);
        services.Should().Equal(before);
    }

    [Fact]
    public void AddAIAssetGraphLoading_ShouldRejectScopedResolverWithoutMutation()
    {
        var services = new ServiceCollection();
        services.AddScoped<IPersistentAIAssetResolver, TestResolver>();
        var before = services.ToArray();

        var act = () => services.AddAIAssetGraphLoading();

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Be(ResolverConfigurationMessage);
        services.Should().Equal(before);
    }

    [Fact]
    public void AddAIAssetGraphLoading_ShouldRejectTransientResolverWithoutMutation()
    {
        var services = new ServiceCollection();
        services.AddTransient<IPersistentAIAssetResolver, TestResolver>();
        var before = services.ToArray();

        var act = () => services.AddAIAssetGraphLoading();

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Be(ResolverConfigurationMessage);
        services.Should().Equal(before);
    }

    [Fact]
    public void AddAIAssetGraphLoading_ShouldRejectPreExistingGraphLoaderWithoutMutation()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPersistentAIAssetResolver>(new TestResolver());
        services.AddSingleton<IAIAssetGraphLoader, TestGraphLoader>();
        var before = services.ToArray();

        var act = () => services.AddAIAssetGraphLoading();

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Be(ExistingGraphLoaderMessage);
        services.Should().Equal(before);
    }

    [Fact]
    public void AddAIAssetGraphLoading_ShouldRejectRepeatedInvocationWithoutMutation()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPersistentAIAssetResolver>(new TestResolver());
        services.AddAIAssetGraphLoading();
        var before = services.ToArray();

        var act = () => services.AddAIAssetGraphLoading();

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Be(ExistingGraphLoaderMessage);
        services.Should().Equal(before);
    }

    [Fact]
    public void AddAIAssetGraphLoading_ShouldRegisterExactlyOneSingletonImplementationTypeAndReturnSameCollection()
    {
        var services = new ServiceCollection();
        var resolver = new TestResolver();
        services.AddSingleton<IPersistentAIAssetResolver>(resolver);
        var resolverDescriptor = services.Single(
            descriptor => descriptor.ServiceType == typeof(IPersistentAIAssetResolver));

        var returned = services.AddAIAssetGraphLoading();

        returned.Should().BeSameAs(services);
        services.Single(descriptor => descriptor.ServiceType == typeof(IPersistentAIAssetResolver))
            .Should().BeSameAs(resolverDescriptor);

        var graphLoaderDescriptor = services.Single(
            descriptor => descriptor.ServiceType == typeof(IAIAssetGraphLoader));
        graphLoaderDescriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
        graphLoaderDescriptor.ImplementationType.Should().Be(typeof(AIAssetGraphLoader));
        graphLoaderDescriptor.ImplementationFactory.Should().BeNull();
        graphLoaderDescriptor.ImplementationInstance.Should().BeNull();
    }

    [Fact]
    public void AddAIAssetGraphLoading_ShouldResolveFrozenLoaderAsSingletonUsingConfiguredResolver()
    {
        var services = new ServiceCollection();
        var resolver = new TestResolver();
        services.AddSingleton<IPersistentAIAssetResolver>(resolver);
        services.AddAIAssetGraphLoading();
        using var provider = services.BuildServiceProvider();

        var loader1 = provider.GetRequiredService<IAIAssetGraphLoader>();
        var loader2 = provider.GetRequiredService<IAIAssetGraphLoader>();

        loader1.Should().BeOfType<AIAssetGraphLoader>();
        loader2.Should().BeSameAs(loader1);
        provider.GetRequiredService<IPersistentAIAssetResolver>().Should().BeSameAs(resolver);
    }

    private sealed class TestResolver : IPersistentAIAssetResolver
    {
        public ValueTask<AIAssetResolutionResult> ResolveAsync(
            AssetDefinitionKey key,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<AIAssetResolutionResult>(new AIAssetResolutionResult.DefinitionNotPublished());

        public ValueTask<AIAssetResolutionResult> ResolveAsync(
            AssetReference reference,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<AIAssetResolutionResult>(new AIAssetResolutionResult.DefinitionNotPublished());

        public ValueTask<AIAssetResolutionResult> ResolveAsync(
            AssetUrn urn,
            AssetVersion version,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<AIAssetResolutionResult>(new AIAssetResolutionResult.DefinitionNotPublished());

        public ValueTask<CatalogLineageLookupResult> DiscoverLineageAsync(
            AssetUrn urn,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<CatalogLineageLookupResult>(new CatalogLineageLookupResult.NotFound());
    }

    private sealed class TestGraphLoader : IAIAssetGraphLoader
    {
        public ValueTask<AIAssetGraphLoadResult> LoadAsync(
            AssetDefinitionKey rootKey,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
