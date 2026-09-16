using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;
using PulseStack.Abstractions.Runtime.Application;
using PulseStack.Abstractions.Runtime.Invocation.Application;
using PulseStack.Abstractions.Runtime.Realization.Application;
using PulseStack.Agents.DependencyInjection;
using PulseStack.Agents.Runtime.Application;
using Xunit;

namespace PulseStack.Tests.Runtime.Application;

public sealed class ApplicationOperationDependencyInjectionTests
{
    [Fact]
    public void AddPulseStackAgents_ShouldRegisterApplicationOperationOnceAsScoped()
    {
        var services = new ServiceCollection();

        services.AddPulseStackAgents();
        services.AddPulseStackAgents();

        var descriptor = Assert.Single(
            services,
            candidate => candidate.ServiceType == typeof(IApplicationOperation));

        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(typeof(ApplicationOperation), descriptor.ImplementationType);
    }

    [Fact]
    public void AddPulseStackAgents_ShouldPreservePriorCustomApplicationOperationRegistration()
    {
        var services = new ServiceCollection();
        services.AddScoped<IApplicationOperation, CustomApplicationOperation>();

        services.AddPulseStackAgents();

        var descriptor = Assert.Single(
            services,
            candidate => candidate.ServiceType == typeof(IApplicationOperation));

        Assert.Equal(typeof(CustomApplicationOperation), descriptor.ImplementationType);
    }

    [Fact]
    public void ApplicationOperation_ShouldBeSharedWithinScopeAndIndependentAcrossScopes()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAIAssetGraphLoader, StubGraphLoader>();
        services.AddScoped<IApplicationRealizer, StubRealizer>();
        services.AddSingleton<IApplicationInvoker, StubInvoker>();
        services.AddScoped<IApplicationOperation, ApplicationOperation>();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
        using var scopeA = provider.CreateScope();
        using var scopeB = provider.CreateScope();

        var operationA1 = scopeA.ServiceProvider.GetRequiredService<IApplicationOperation>();
        var operationA2 = scopeA.ServiceProvider.GetRequiredService<IApplicationOperation>();
        var operationB = scopeB.ServiceProvider.GetRequiredService<IApplicationOperation>();

        Assert.IsType<ApplicationOperation>(operationA1);
        Assert.Same(operationA1, operationA2);
        Assert.NotSame(operationA1, operationB);
    }

    private sealed class CustomApplicationOperation : IApplicationOperation
    {
        public Task<ApplicationOperationResult> ExecuteAsync(
            AssetDefinitionKey projectKey,
            ApplicationInvocationRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubGraphLoader : IAIAssetGraphLoader
    {
        public ValueTask<AIAssetGraphLoadResult> LoadAsync(
            AssetDefinitionKey rootKey,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubRealizer : IApplicationRealizer
    {
        public Task<ApplicationRealizationResult> RealizeAsync(
            AIAssetGraph graph,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubInvoker : IApplicationInvoker
    {
        public Task<ApplicationInvocationResult> InvokeAsync(
            RealizedApplication application,
            ApplicationInvocationRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
