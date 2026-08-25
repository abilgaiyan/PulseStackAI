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

    private sealed class StubAgentComposer(IAgent agent) : IAgentComposer
    {
        public List<AgentDefinition> ComposedDefinitions { get; } = [];

        public Task<IAgent> ComposeAsync(
            AgentDefinition definition,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ComposedDefinitions.Add(definition);
            return Task.FromResult(agent);
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
