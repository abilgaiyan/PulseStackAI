using System.Reflection;
using PulseStack.Abstractions.Chat;
using PulseStack.Abstractions.Knowledge;
using PulseStack.Abstractions.Memory;
using PulseStack.Abstractions.Policies;
using PulseStack.Abstractions.Providers;
using PulseStack.Abstractions.Runtime.Realization.Binding;
using PulseStack.Abstractions.Runtime.Realization.Evaluation;
using PulseStack.Abstractions.Runtime.Realization.Resolution;
using PulseStack.Abstractions.Tools;
using PulseStack.Abstractions.Workflows.Conditions;
using PulseStack.Abstractions.Workflows.Values;
using PulseStack.Agents.Runtime.Realization;
using PulseStack.Core.Runtime.Realization;
using Xunit;

namespace PulseStack.Tests.Runtime.Realization.Application;

public sealed class ApplicationRealizationChainFactoryTests
{
    [Fact]
    public void Create_ShouldRejectNullResolver()
    {
        var factory = CreateFactory();

        Assert.Throws<ArgumentNullException>(() => factory.Create(null!));
    }

    [Fact]
    public void Create_ShouldBindSuppliedResolverToWorkflowAndAgentComposersWithoutResolving()
    {
        var resolver = new RecordingAssetResolver();
        var factory = CreateFactory();

        var workflowComposer = factory.Create(resolver);

        Assert.Equal(0, resolver.Calls);
        Assert.Same(resolver, ReadField(workflowComposer, "_assetResolver"));

        var agentComposer = ReadField(workflowComposer, "_agentComposer");
        Assert.NotNull(agentComposer);
        Assert.Same(resolver, ReadField(agentComposer!, "_assetResolver"));
    }

    [Fact]
    public void Create_ShouldReturnIndependentResolverBoundChains()
    {
        var resolverA = new RecordingAssetResolver();
        var resolverB = new RecordingAssetResolver();
        var factory = CreateFactory();

        var chainA = factory.Create(resolverA);
        var chainB = factory.Create(resolverB);

        Assert.NotSame(chainA, chainB);

        var agentA = ReadField(chainA, "_agentComposer");
        var agentB = ReadField(chainB, "_agentComposer");

        Assert.NotNull(agentA);
        Assert.NotNull(agentB);
        Assert.NotSame(agentA, agentB);

        Assert.Same(resolverA, ReadField(chainA, "_assetResolver"));
        Assert.Same(resolverA, ReadField(agentA!, "_assetResolver"));
        Assert.Same(resolverB, ReadField(chainB, "_assetResolver"));
        Assert.Same(resolverB, ReadField(agentB!, "_assetResolver"));
        Assert.Equal(0, resolverA.Calls);
        Assert.Equal(0, resolverB.Calls);
    }

    [Fact]
    public void Factory_ShouldNotCaptureResolverOrServiceProviderState()
    {
        var fields = typeof(ApplicationRealizationChainFactory)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.DoesNotContain(
            fields,
            static field => typeof(IAssetResolver).IsAssignableFrom(field.FieldType));
        Assert.DoesNotContain(
            fields,
            static field => typeof(IServiceProvider).IsAssignableFrom(field.FieldType));
    }

    private static ApplicationRealizationChainFactory CreateFactory() =>
        new(
            new ModelRealizer(new StubProviderResolver()),
            new PromptRealizer(),
            new StubToolBindingResolver(),
            new StubKnowledgeBindingResolver(),
            new StubMemoryBindingResolver(),
            new StubPolicyBindingResolver(),
            new StubToolExecutor(),
            new StubConditionBindingResolver(),
            new StubWorkflowValueEvaluator());

    private static object? ReadField(object instance, string name)
    {
        var field = instance.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        return field!.GetValue(instance);
    }

    private sealed class RecordingAssetResolver : IAssetResolver
    {
        public int Calls { get; private set; }

        public ValueTask<PulseStack.Abstractions.Assets.IAsset?> ResolveAsync(
            PulseStack.Abstractions.Assets.AssetReference reference,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return ValueTask.FromResult<PulseStack.Abstractions.Assets.IAsset?>(null);
        }
    }

    private sealed class StubProviderResolver : IProviderResolver
    {
        public IChatClientFactory Resolve(string provider) =>
            throw new InvalidOperationException("Factory construction must not realize models.");
    }

    private sealed class StubToolBindingResolver : IToolBindingResolver
    {
        public ITool Resolve(PulseStack.Abstractions.Assets.ToolAsset asset) =>
            throw new InvalidOperationException("Factory construction must not bind tools.");
    }

    private sealed class StubKnowledgeBindingResolver : IKnowledgeBindingResolver
    {
        public IKnowledgeSource Resolve(PulseStack.Abstractions.Assets.KnowledgeAsset asset) =>
            throw new InvalidOperationException("Factory construction must not bind knowledge.");
    }

    private sealed class StubMemoryBindingResolver : IMemoryBindingResolver
    {
        public IConversationMemory Resolve(PulseStack.Abstractions.Assets.MemoryAsset asset) =>
            throw new InvalidOperationException("Factory construction must not bind memory.");
    }

    private sealed class StubPolicyBindingResolver : IPolicyBindingResolver
    {
        public IRuntimePolicy Resolve(PulseStack.Abstractions.Assets.PolicyAsset asset) =>
            throw new InvalidOperationException("Factory construction must not bind policies.");
    }

    private sealed class StubToolExecutor : IToolExecutor
    {
        public Task<IToolExecutionResult> ExecuteAsync(
            ITool tool,
            ToolExecutionContext context,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubConditionBindingResolver : IConditionBindingResolver
    {
        public ICondition Resolve(ConditionDefinition definition) =>
            throw new InvalidOperationException("Factory construction must not bind conditions.");
    }

    private sealed class StubWorkflowValueEvaluator : IWorkflowValueEvaluator
    {
        public object? Evaluate(
            WorkflowValueDefinition definition,
            PulseStack.Abstractions.Agents.PipelineContext context) =>
            throw new InvalidOperationException("Factory construction must not evaluate values.");
    }
}
