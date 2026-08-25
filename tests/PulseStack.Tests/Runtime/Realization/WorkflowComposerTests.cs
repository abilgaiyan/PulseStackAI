using FluentAssertions;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Runtime.Realization.Composition;
using PulseStack.Abstractions.Runtime.Realization.Resolution;
using PulseStack.Abstractions.Workflows.Definitions;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Core.Assets;
using PulseStack.Core.Runtime.Realization.Composition;
using Xunit;

namespace PulseStack.Tests.Runtime.Realization;

public sealed class WorkflowComposerTests
{
    [Fact]
    public async Task ComposeAsync_ShouldResolveAgentReference_AndCreateRuntimeRunStep()
    {
        var agentDefinition = new AgentDefinitionFactory().Create(
            new AgentDefinitionOptions
            {
                Name = "Research Agent",
                Goal = "Research the customer",
                Role = "Researcher"
            });

        var agentReference = new AssetReference(
            agentDefinition.Id,
            agentDefinition.Urn);

        var runDefinition = new RunStepDefinition
        {
            Agent = agentReference
        };

        var workflowAsset = new WorkflowAssetFactory().Create(
            new WorkflowAssetOptions
            {
                Name = "Customer Research",
                Description = "Research workflow",
                Steps = [runDefinition]
            });

        var runtimeAgent = new StubAgent("Research Agent");
        var agentComposer = new StubAgentComposer(runtimeAgent);
        var composer = new WorkflowComposer(
            new StubAssetResolver(agentDefinition),
            agentComposer);

        var workflow = await composer.ComposeAsync(workflowAsset);

        workflow.Name.Should().Be("Customer Research");
        workflow.Definition.Description.Should().Be("Research workflow");
        workflow.Steps.Should().ContainSingle();

        var runStep = workflow.Steps.Single()
            .Should().BeOfType<RunStep>()
            .Subject;

        runStep.Id.Should().Be(runDefinition.Id);
        runStep.Agent.Should().BeSameAs(runtimeAgent);
        agentComposer.ComposedDefinitions.Should().ContainSingle()
            .Which.Should().BeSameAs(agentDefinition);
    }

    [Fact]
    public async Task ComposeAsync_ShouldComposeParallelRunStepsRecursively()
    {
        var firstDefinition = CreateAgentDefinition("Research Agent");
        var secondDefinition = CreateAgentDefinition("Policy Agent");
        var parallelDefinition = new ParallelStepDefinition
        {
            Name = "Research and Policy",
            Steps =
            [
                CreateRunDefinition(firstDefinition),
                CreateRunDefinition(secondDefinition)
            ]
        };

        var workflowAsset = new WorkflowAssetFactory().Create(
            new WorkflowAssetOptions
            {
                Name = "Parallel Workflow",
                Steps = [parallelDefinition]
            });

        var composer = new WorkflowComposer(
            new StubAssetResolver(firstDefinition, secondDefinition),
            new StubAgentComposer(
                new StubAgent("Research Agent"),
                new StubAgent("Policy Agent")));

        var workflow = await composer.ComposeAsync(workflowAsset);

        var parallel = workflow.Steps.Single()
            .Should().BeOfType<ParallelStep>()
            .Subject;

        parallel.Id.Should().Be(parallelDefinition.Id);
        parallel.Name.Should().Be("Research and Policy");
        parallel.Steps.Should().HaveCount(2);
        parallel.Steps.Should().AllBeOfType<RunStep>();
        parallel.Steps.Select(step => step.Name)
            .Should().Equal("Research Agent", "Policy Agent");
    }

    [Fact]
    public async Task ComposeAsync_ShouldComposeNestedParallelStepsRecursively()
    {
        var firstDefinition = CreateAgentDefinition("Agent One");
        var secondDefinition = CreateAgentDefinition("Agent Two");
        var nestedDefinition = new ParallelStepDefinition
        {
            Name = "Inner Parallel",
            Steps = [CreateRunDefinition(secondDefinition)]
        };
        var outerDefinition = new ParallelStepDefinition
        {
            Name = "Outer Parallel",
            Steps =
            [
                CreateRunDefinition(firstDefinition),
                nestedDefinition
            ]
        };

        var workflowAsset = new WorkflowAssetFactory().Create(
            new WorkflowAssetOptions
            {
                Name = "Nested Parallel Workflow",
                Steps = [outerDefinition]
            });

        var composer = new WorkflowComposer(
            new StubAssetResolver(firstDefinition, secondDefinition),
            new StubAgentComposer(
                new StubAgent("Agent One"),
                new StubAgent("Agent Two")));

        var workflow = await composer.ComposeAsync(workflowAsset);

        var outer = workflow.Steps.Single()
            .Should().BeOfType<ParallelStep>()
            .Subject;

        outer.Steps.Should().HaveCount(2);
        outer.Steps[0].Should().BeOfType<RunStep>();

        var inner = outer.Steps[1]
            .Should().BeOfType<ParallelStep>()
            .Subject;

        inner.Id.Should().Be(nestedDefinition.Id);
        inner.Steps.Should().ContainSingle();
        inner.Steps.Single().Should().BeOfType<RunStep>()
            .Which.Name.Should().Be("Agent Two");
    }

    [Fact]
    public async Task ComposeAsync_ShouldRejectMissingAgentAsset()
    {
        var missingId = AssetId.New();
        var workflowAsset = new WorkflowAssetFactory().Create(
            new WorkflowAssetOptions
            {
                Name = "Invalid Workflow",
                Steps =
                [
                    new RunStepDefinition
                    {
                        Agent = new AssetReference(
                            missingId,
                            new AssetUrn($"urn:pulsestack:agent:{missingId}"))
                    }
                ]
            });

        var composer = new WorkflowComposer(
            new StubAssetResolver(),
            new StubAgentComposer(new StubAgent("unused")));

        var action = () => composer.ComposeAsync(workflowAsset);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*could not be resolved*");
    }

    private static AgentDefinition CreateAgentDefinition(string name) =>
        new AgentDefinitionFactory().Create(
            new AgentDefinitionOptions
            {
                Name = name,
                Goal = "Execute workflow work",
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

    private sealed class StubAssetResolver(params IAsset[] assets) : IAssetResolver
    {
        private readonly IReadOnlyDictionary<AssetId, IAsset> _assets =
            assets.ToDictionary(asset => asset.Id);

        public ValueTask<IAsset?> ResolveAsync(
            AssetReference reference,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _assets.TryGetValue(reference.Id, out var asset);
            return ValueTask.FromResult(asset);
        }
    }

    private sealed class StubAgentComposer(params IAgent[] agents) : IAgentComposer
    {
        private readonly Queue<IAgent> _agents = new(agents);

        public List<AgentDefinition> ComposedDefinitions { get; } = [];

        public Task<IAgent> ComposeAsync(
            AgentDefinition definition,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ComposedDefinitions.Add(definition);

            if (_agents.Count == 0)
            {
                throw new InvalidOperationException("No stub Agent remains.");
            }

            return Task.FromResult(_agents.Dequeue());
        }
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
