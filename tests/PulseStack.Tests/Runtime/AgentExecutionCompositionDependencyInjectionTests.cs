using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Agents.DependencyInjection;
using PulseStack.Agents.Runtime;
using PulseStack.Agents.Runtime.Composition;
using Xunit;

namespace PulseStack.Tests.Runtime;

public sealed class AgentExecutionCompositionDependencyInjectionTests
{
    [Fact]
    public void AddPulseStackAgents_ShouldRegisterSharedAgentExecutionCompositionOnce()
    {
        var services = new ServiceCollection();

        services.AddPulseStackAgents();
        services.AddPulseStackAgents();

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(AgentRuntime));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IAgentRuntime));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IAgentExecutionRuntime));
    }

    [Fact]
    public void AddPulseStackAgents_ShouldProjectOneAgentRuntimeThroughPublicAndInternalExecutionViews()
    {
        var services = new ServiceCollection();
        services.AddPulseStackAgents();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true
            });

        var runtime = provider.GetRequiredService<AgentRuntime>();
        var publicRuntime = provider.GetRequiredService<IAgentRuntime>();
        var executionRuntime = provider.GetRequiredService<IAgentExecutionRuntime>();

        Assert.Same(runtime, publicRuntime);
        Assert.Same(runtime, executionRuntime);
    }

    [Fact]
    public void AddPulseStackAgentsAndWorkflows_ShouldShareOneExecutionViewAcrossConsumers()
    {
        var services = new ServiceCollection();
        services.AddPulseStackAgents();
        services.AddPulseStackWorkflows();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });

        var executionRuntime = provider.GetRequiredService<IAgentExecutionRuntime>();
        var sequential = provider.GetRequiredService<SequentialPipelineExecutionStrategy>();
        var parallel = provider.GetRequiredService<ParallelPipelineExecutionStrategy>();
        var run = provider.GetServices<IStepExecutor>().OfType<RunStepExecutor>().Single();

        Assert.Same(executionRuntime, GetExecutionRuntime(sequential));
        Assert.Same(executionRuntime, GetExecutionRuntime(parallel));
        Assert.Same(executionRuntime, GetExecutionRuntime(run));
        Assert.NotNull(provider.GetRequiredService<IWorkflowRuntime>());
    }

    [Fact]
    public void AddPulseStackAgents_ShouldPreservePriorCustomPublicAgentRuntime()
    {
        var services = new ServiceCollection();
        var customRuntime = new CustomAgentRuntime();
        services.AddSingleton<IAgentRuntime>(customRuntime);

        services.AddPulseStackAgents();

        using var provider = services.BuildServiceProvider();

        Assert.Same(customRuntime, provider.GetRequiredService<IAgentRuntime>());

        var ownedRuntime = provider.GetRequiredService<AgentRuntime>();
        var executionRuntime = provider.GetRequiredService<IAgentExecutionRuntime>();

        Assert.Same(ownedRuntime, executionRuntime);
    }

    private static IAgentExecutionRuntime GetExecutionRuntime(object consumer)
        => (IAgentExecutionRuntime)(consumer.GetType()
            .GetField("_agentRuntime", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(consumer)!);

    private sealed class CustomAgentRuntime : IAgentRuntime
    {
        public Task<AgentResponse> RunAsync(
            PipelineContext context,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public async IAsyncEnumerable<string> StreamAsync(
            string input,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
