using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Runtime.Invocation.Application;
using PulseStack.Agents.DependencyInjection;
using PulseStack.Core.DependencyInjection;
using PulseStack.Core.Runtime.Invocation.Application;
using Xunit;

namespace PulseStack.Tests.Runtime.Invocation.Application;

public sealed class ApplicationInvocationDependencyInjectionTests
{
    [Fact]
    public void AddPulseStackAgents_ShouldRegisterApplicationInvocationServicesOnce()
    {
        var services = new ServiceCollection();

        services.AddPulseStackAgents();
        services.AddPulseStackAgents();

        var authorityDescriptor = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(ApplicationInvocationCoordinationAuthority));
        var invokerDescriptor = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(IApplicationInvoker));

        Assert.Equal(ServiceLifetime.Singleton, authorityDescriptor.Lifetime);
        Assert.Equal(
            typeof(ApplicationInvocationCoordinationAuthority),
            authorityDescriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, invokerDescriptor.Lifetime);
        Assert.Equal(typeof(ApplicationInvoker), invokerDescriptor.ImplementationType);
    }

    [Fact]
    public void AddPulseStackAgents_ShouldPreservePriorCustomApplicationInvokerRegistration()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IApplicationInvoker, CustomApplicationInvoker>();

        services.AddPulseStackAgents();

        var descriptor = Assert.Single(
            services,
            candidate => candidate.ServiceType == typeof(IApplicationInvoker));

        Assert.Equal(typeof(CustomApplicationInvoker), descriptor.ImplementationType);
    }

    [Fact]
    public void ApplicationInvocationServices_ShouldBeSharedAcrossRootAndChildScopes()
    {
        var services = new ServiceCollection();
        services.AddPulseStack();
        services.AddPulseStackAgents();
        services.AddPulseStackWorkflows();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
        using var scopeA = provider.CreateScope();
        using var scopeB = provider.CreateScope();

        var rootInvoker = provider.GetRequiredService<IApplicationInvoker>();
        var scopeAInvoker = scopeA.ServiceProvider.GetRequiredService<IApplicationInvoker>();
        var scopeBInvoker = scopeB.ServiceProvider.GetRequiredService<IApplicationInvoker>();
        var rootAuthority = provider.GetRequiredService<ApplicationInvocationCoordinationAuthority>();
        var scopeAAuthority = scopeA.ServiceProvider.GetRequiredService<ApplicationInvocationCoordinationAuthority>();
        var scopeBAuthority = scopeB.ServiceProvider.GetRequiredService<ApplicationInvocationCoordinationAuthority>();

        Assert.IsType<ApplicationInvoker>(rootInvoker);
        Assert.Same(rootInvoker, scopeAInvoker);
        Assert.Same(rootInvoker, scopeBInvoker);
        Assert.Same(rootAuthority, scopeAAuthority);
        Assert.Same(rootAuthority, scopeBAuthority);
    }

    [Fact]
    public void ApplicationInvocationServices_ShouldBeIndependentAcrossRootProviders()
    {
        using var providerA = CreateProvider();
        using var providerB = CreateProvider();

        var invokerA = providerA.GetRequiredService<IApplicationInvoker>();
        var invokerB = providerB.GetRequiredService<IApplicationInvoker>();
        var authorityA = providerA.GetRequiredService<ApplicationInvocationCoordinationAuthority>();
        var authorityB = providerB.GetRequiredService<ApplicationInvocationCoordinationAuthority>();

        Assert.NotSame(invokerA, invokerB);
        Assert.NotSame(authorityA, authorityB);
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddPulseStack();
        services.AddPulseStackAgents();
        services.AddPulseStackWorkflows();

        return services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
    }

    private sealed class CustomApplicationInvoker : IApplicationInvoker
    {
        public Task<ApplicationInvocationResult> InvokeAsync(
            PulseStack.Abstractions.Runtime.Realization.Application.RealizedApplication application,
            ApplicationInvocationRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
