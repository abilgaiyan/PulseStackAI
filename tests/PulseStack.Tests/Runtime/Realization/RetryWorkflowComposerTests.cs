using FluentAssertions;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Runtime.Realization.Binding;
using PulseStack.Abstractions.Runtime.Realization.Composition;
using PulseStack.Abstractions.Runtime.Realization.Resolution;
using PulseStack.Abstractions.Workflows.Conditions;
using PulseStack.Abstractions.Workflows.Definitions;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Core.Assets;
using PulseStack.Core.Runtime.Realization.Composition;
using Xunit;

namespace PulseStack.Tests.Runtime.Realization;

public sealed class RetryWorkflowComposerTests
{
    [Fact]
    public async Task ComposeAsync_ShouldComposeRetryRunStep_AndPreserveIdentity()
    {
        var agentDefinition = CreateAgentDefinition("Submission Agent");
        var retryDefinition = new RetryStepDefinition
        {
            Name = "Submit With Retry",
            MaxAttempts = 4,
            Step = CreateRunDefinition(agentDefinition)
        };

        var workflowAsset = CreateWorkflow(retryDefinition);
        var composer = CreateComposer(
            new StubAssetResolver(agentDefinition),
            new StubAgentComposer(new StubAgent("Submission Agent")));

        var workflow = await composer.ComposeAsync(workflowAsset);

        var retry = workflow.Steps.Single()
            .Should().BeOfType<RetryStep>()
            .Subject;

        retry.Id.Should().Be(retryDefinition.Id);
        retry.Name.Should().Be("Submit With Retry");
        retry.MaxAttempts.Should().Be(4);
        retry.Step.Should().BeOfType<RunStep>()
            .Which.Name.Should().Be("Submission Agent");
    }

    [Fact]
    public async Task ComposeAsync_ShouldComposeRetryChildRecursively()
    {
        var firstAgent = CreateAgentDefinition("Primary Agent");
        var secondAgent = CreateAgentDefinition("Secondary Agent");
        var parallelDefinition = new ParallelStepDefinition
        {
            Name = "Retry Parallel Work",
            Steps =
            [
                CreateRunDefinition(firstAgent),
                CreateRunDefinition(secondAgent)
            ]
        };
        var retryDefinition = new RetryStepDefinition
        {
            Name = "Retry Composite",
            MaxAttempts = 2,
            Step = parallelDefinition
        };

        var composer = CreateComposer(
            new StubAssetResolver(firstAgent, secondAgent),
            new StubAgentComposer(
                new StubAgent("Primary Agent"),
                new StubAgent("Secondary Agent")));

        var workflow = await composer.ComposeAsync(
            CreateWorkflow(retryDefinition));

        var retry = workflow.Steps.Single()
            .Should().BeOfType<RetryStep>()
            .Subject;

        var parallel = retry.Step
            .Should().BeOfType<ParallelStep>()
            .Subject;

        parallel.Id.Should().Be(parallelDefinition.Id);
        parallel.Steps.Should().HaveCount(2);
        parallel.Steps.Should().AllBeOfType<RunStep>();
    }

    [Fact]
    public async Task ComposeAsync_ShouldRejectRetryWithInvalidMaxAttempts()
    {
        var agentDefinition = CreateAgentDefinition("Invalid Retry Agent");
        var retryDefinition = new RetryStepDefinition
        {
            Name = "Invalid Retry",
            MaxAttempts = 0,
            Step = CreateRunDefinition(agentDefinition)
        };

        var composer = CreateComposer(
            new StubAssetResolver(agentDefinition),
            new StubAgentComposer(new StubAgent("Invalid Retry Agent")));

        var action = () => composer.ComposeAsync(
            CreateWorkflow(retryDefinition));

        await action.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    private static WorkflowAsset CreateWorkflow(
        WorkflowStepDefinition step) =>
        new WorkflowAssetFactory().Create(
            new WorkflowAssetOptions
            {
                Name = "Retry Workflow",
                Steps = [step]
            });

    private static WorkflowComposer CreateComposer(
        IAssetResolver assetResolver,
        IAgentComposer agentComposer) =>
        new(
            assetResolver,
            agentComposer,
            new StubConditionBindingResolver());

    private static AgentDefinition CreateAgentDefinition(string name) =>
        new AgentDefinitionFactory().Create(
            new AgentDefinitionOptions
            {
                Name = name,
                Goal = "Execute retry workflow work",
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

        public Task<IAgent> ComposeAsync(
            AgentDefinition definition,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_agents.Count == 0)
            {
                throw new InvalidOperationException("No stub Agent remains.");
            }

            return Task.FromResult(_agents.Dequeue());
        }
    }

    private sealed class StubConditionBindingResolver : IConditionBindingResolver
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
