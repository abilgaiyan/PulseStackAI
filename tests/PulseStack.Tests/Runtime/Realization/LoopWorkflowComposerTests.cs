using FluentAssertions;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Runtime.Realization.Binding;
using PulseStack.Abstractions.Runtime.Realization.Composition;
using PulseStack.Abstractions.Runtime.Realization.Resolution;
using PulseStack.Abstractions.Workflows.Conditions;
using PulseStack.Abstractions.Workflows.Definitions;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Abstractions.Workflows.Values;
using PulseStack.Core.Assets;
using PulseStack.Core.Runtime.Realization.Composition;
using Xunit;

namespace PulseStack.Tests.Runtime.Realization;

public sealed class LoopWorkflowComposerTests
{
    [Fact]
    public async Task ComposeAsync_ShouldComposeForEachFromContextItem()
    {
        var agentDefinition = CreateAgentDefinition("Document Agent");
        var loopDefinition = new LoopStepDefinition
        {
            Name = "Process Documents",
            Items = new ContextItemValueDefinition
            {
                Key = "documents"
            },
            Step = CreateRunDefinition(agentDefinition)
        };

        var workflowAsset = CreateWorkflow(loopDefinition);
        var composer = CreateComposer(
            agentDefinition,
            new StubAgent("Document Agent"));

        var workflow = await composer.ComposeAsync(workflowAsset);

        var loop = workflow.Steps.Single()
            .Should().BeOfType<LoopStep>()
            .Subject;

        loop.Id.Should().Be(loopDefinition.Id);
        loop.Name.Should().Be("Process Documents");
        loop.Step.Should().BeOfType<RunStep>()
            .Which.Name.Should().Be("Document Agent");

        var context = new PipelineContext();
        context.Items["documents"] = new[] { "a.pdf", "b.pdf" };

        loop.Items(context)
            .Should().Equal("a.pdf", "b.pdf");
    }

    [Fact]
    public async Task ComposeAsync_ShouldComposeForEachFromLiteralCollection()
    {
        var agentDefinition = CreateAgentDefinition("Literal Agent");
        var loopDefinition = new LoopStepDefinition
        {
            Items = new LiteralValueDefinition
            {
                Value = new object[] { "one", "two", "three" }
            },
            Step = CreateRunDefinition(agentDefinition)
        };

        var workflowAsset = CreateWorkflow(loopDefinition);
        var composer = CreateComposer(
            agentDefinition,
            new StubAgent("Literal Agent"));

        var workflow = await composer.ComposeAsync(workflowAsset);
        var loop = workflow.Steps.Single()
            .Should().BeOfType<LoopStep>()
            .Subject;

        loop.Items(new PipelineContext())
            .Should().Equal("one", "two", "three");
    }

    [Fact]
    public async Task ComposeAsync_ShouldComposeForEachChildRecursively()
    {
        var agentDefinition = CreateAgentDefinition("Nested Agent");
        var parallelDefinition = new ParallelStepDefinition
        {
            Name = "Nested Work",
            Steps = [CreateRunDefinition(agentDefinition)]
        };
        var loopDefinition = new LoopStepDefinition
        {
            Items = new LiteralValueDefinition
            {
                Value = new[] { "item" }
            },
            Step = parallelDefinition
        };

        var workflowAsset = CreateWorkflow(loopDefinition);
        var composer = CreateComposer(
            agentDefinition,
            new StubAgent("Nested Agent"));

        var workflow = await composer.ComposeAsync(workflowAsset);
        var loop = workflow.Steps.Single()
            .Should().BeOfType<LoopStep>()
            .Subject;

        var parallel = loop.Step
            .Should().BeOfType<ParallelStep>()
            .Subject;

        parallel.Id.Should().Be(parallelDefinition.Id);
        parallel.Steps.Should().ContainSingle()
            .Which.Should().BeOfType<RunStep>();
    }

    [Fact]
    public async Task ComposeAsync_ShouldRejectNonEnumerableForEachValueAtRuntimeEvaluation()
    {
        var agentDefinition = CreateAgentDefinition("Invalid Items Agent");
        var loopDefinition = new LoopStepDefinition
        {
            Name = "Invalid ForEach",
            Items = new LiteralValueDefinition
            {
                Value = 42
            },
            Step = CreateRunDefinition(agentDefinition)
        };

        var workflowAsset = CreateWorkflow(loopDefinition);
        var composer = CreateComposer(
            agentDefinition,
            new StubAgent("Invalid Items Agent"));

        var workflow = await composer.ComposeAsync(workflowAsset);
        var loop = workflow.Steps.Single()
            .Should().BeOfType<LoopStep>()
            .Subject;

        var action = () => loop.Items(new PipelineContext()).ToArray();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*requires an enumerable workflow value*");
    }

    private static WorkflowAsset CreateWorkflow(
        WorkflowStepDefinition step) =>
        new WorkflowAssetFactory().Create(
            new WorkflowAssetOptions
            {
                Name = "ForEach Workflow",
                Steps = [step]
            });

    private static WorkflowComposer CreateComposer(
        AgentDefinition agentDefinition,
        IAgent runtimeAgent) =>
        new(
            new StubAssetResolver(agentDefinition),
            new StubAgentComposer(runtimeAgent),
            new StubConditionBindingResolver());

    private static AgentDefinition CreateAgentDefinition(string name) =>
        new AgentDefinitionFactory().Create(
            new AgentDefinitionOptions
            {
                Name = name,
                Goal = "Process workflow item",
                Role = "Worker"
            });

    private static RunStepDefinition CreateRunDefinition(
        AgentDefinition definition) =>
        new()
        {
            Agent = new AssetReference(
                definition.Id,
                definition.Urn)
        };

    private sealed class StubAssetResolver(AgentDefinition definition)
        : IAssetResolver
    {
        public ValueTask<IAsset?> ResolveAsync(
            AssetReference reference,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.FromResult<IAsset?>(
                reference.Id == definition.Id
                    ? definition
                    : null);
        }
    }

    private sealed class StubAgentComposer(IAgent agent) : IAgentComposer
    {
        public Task<IAgent> ComposeAsync(
            AgentDefinition definition,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(agent);
        }
    }

    private sealed class StubConditionBindingResolver
        : IConditionBindingResolver
    {
        public ICondition Resolve(ConditionDefinition definition) =>
            throw new NotSupportedException();
    }

    private sealed class StubAgent(string name) : IAgent
    {
        public string Name { get; } = name;

        public Task<AgentResponse> RunAsync(
            string input,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

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
