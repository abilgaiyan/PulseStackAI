using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;
using PulseStack.Abstractions.Providers;
using PulseStack.Abstractions.Runtime.Realization.Application;
using PulseStack.Abstractions.Runtime.Realization.Binding;
using PulseStack.Abstractions.Runtime.Realization.Composition;
using PulseStack.Abstractions.Runtime.Realization.Evaluation;
using PulseStack.Abstractions.Runtime.Realization.Resolution;
using PulseStack.Abstractions.Tools;
using PulseStack.Agents.DependencyInjection;
using PulseStack.Agents.Runtime.Realization;
using PulseStack.Core.DependencyInjection;
using PulseStack.Core.Runtime.Realization;
using PulseStack.Core.Runtime.Realization.Application;
using PulseStack.Core.Runtime.Realization.Composition;
using PulseStack.Core.Runtime.Realization.Resolution;
using Xunit;

namespace PulseStack.Tests.Runtime.Realization.Application;

public sealed class ApplicationRealizationDependencyInjectionTests
{
    [Fact]
    public void AddPulseStackAgents_ShouldRegisterApplicationRealizationServicesOnce()
    {
        var services = new ServiceCollection();

        services.AddPulseStackAgents();
        services.AddPulseStackAgents();

        var factoryDescriptor = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(IApplicationRealizationChainFactory));
        var realizerDescriptor = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(IApplicationRealizer));

        Assert.Equal(ServiceLifetime.Scoped, factoryDescriptor.Lifetime);
        Assert.Equal(typeof(ApplicationRealizationChainFactory), factoryDescriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, realizerDescriptor.Lifetime);
        Assert.Equal(typeof(ApplicationRealizer), realizerDescriptor.ImplementationType);
    }

    [Fact]
    public void AddPulseStackAgents_ShouldPreservePriorCustomApplicationRealizationRegistrations()
    {
        var services = new ServiceCollection();
        services.AddScoped<IApplicationRealizationChainFactory, CustomChainFactory>();
        services.AddScoped<IApplicationRealizer, CustomApplicationRealizer>();

        services.AddPulseStackAgents();

        var factoryDescriptor = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(IApplicationRealizationChainFactory));
        var realizerDescriptor = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(IApplicationRealizer));

        Assert.Equal(typeof(CustomChainFactory), factoryDescriptor.ImplementationType);
        Assert.Equal(typeof(CustomApplicationRealizer), realizerDescriptor.ImplementationType);
    }

    [Fact]
    public void AddPulseStackAndAgents_ShouldResolveScopedApplicationRealizationTopology()
    {
        var services = new ServiceCollection();
        services.AddPulseStack();
        services.AddPulseStackAgents();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true
            });

        using var scopeA = provider.CreateScope();
        using var scopeB = provider.CreateScope();

        var factoryA1 = scopeA.ServiceProvider.GetRequiredService<IApplicationRealizationChainFactory>();
        var factoryA2 = scopeA.ServiceProvider.GetRequiredService<IApplicationRealizationChainFactory>();
        var factoryB = scopeB.ServiceProvider.GetRequiredService<IApplicationRealizationChainFactory>();
        var realizerA1 = scopeA.ServiceProvider.GetRequiredService<IApplicationRealizer>();
        var realizerA2 = scopeA.ServiceProvider.GetRequiredService<IApplicationRealizer>();
        var realizerB = scopeB.ServiceProvider.GetRequiredService<IApplicationRealizer>();

        Assert.IsType<ApplicationRealizationChainFactory>(factoryA1);
        Assert.IsType<ApplicationRealizer>(realizerA1);
        Assert.Same(factoryA1, factoryA2);
        Assert.Same(realizerA1, realizerA2);
        Assert.NotSame(factoryA1, factoryB);
        Assert.NotSame(realizerA1, realizerB);

        Assert.IsType<InMemoryAssetResolver>(
            scopeA.ServiceProvider.GetRequiredService<IAssetResolver>());
        Assert.IsType<AgentComposer>(
            scopeA.ServiceProvider.GetRequiredService<IAgentComposer>());
        Assert.IsType<WorkflowComposer>(
            scopeA.ServiceProvider.GetRequiredService<IWorkflowComposer>());
    }

    [Fact]
    public void FocusedApplicationRealizationRegistrations_ShouldValidateOnBuildAndScopes()
    {
        var services = new ServiceCollection();

        services.AddSingleton(new ModelRealizer(CreateStub<IProviderResolver>()));
        services.AddSingleton(new PromptRealizer());
        services.AddSingleton(CreateStub<IToolBindingResolver>());
        services.AddSingleton(CreateStub<IKnowledgeBindingResolver>());
        services.AddSingleton(CreateStub<IMemoryBindingResolver>());
        services.AddSingleton(CreateStub<IPolicyBindingResolver>());
        services.AddScoped(_ => CreateStub<IToolExecutor>());
        services.AddSingleton(CreateStub<IConditionBindingResolver>());
        services.AddSingleton(CreateStub<IWorkflowValueEvaluator>());
        services.AddScoped<IApplicationRealizationChainFactory, ApplicationRealizationChainFactory>();
        services.AddScoped<IApplicationRealizer, ApplicationRealizer>();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });

        using var scope = provider.CreateScope();

        Assert.IsType<ApplicationRealizationChainFactory>(
            scope.ServiceProvider.GetRequiredService<IApplicationRealizationChainFactory>());
        Assert.IsType<ApplicationRealizer>(
            scope.ServiceProvider.GetRequiredService<IApplicationRealizer>());
    }

    private static T CreateStub<T>()
        where T : class =>
        DispatchProxy.Create<T, DependencyStubProxy>();

    private sealed class CustomChainFactory : IApplicationRealizationChainFactory
    {
        public IWorkflowComposer Create(IAssetResolver assetResolver) =>
            throw new NotSupportedException();
    }

    private sealed class CustomApplicationRealizer : IApplicationRealizer
    {
        public Task<ApplicationRealizationResult> RealizeAsync(
            AIAssetGraph graph,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private class DependencyStubProxy : DispatchProxy
    {
        protected override object? Invoke(
            MethodInfo? targetMethod,
            object?[]? args) =>
            throw new NotSupportedException("Dependency stub is for DI validation only.");
    }
}
