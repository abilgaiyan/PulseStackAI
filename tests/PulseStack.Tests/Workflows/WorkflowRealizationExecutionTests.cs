using FluentAssertions;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Runtime.Realization.Binding;
using PulseStack.Abstractions.Runtime.Realization.Composition;
using PulseStack.Abstractions.Runtime.Realization.Resolution;
using PulseStack.Abstractions.Workflows.Conditions;
using PulseStack.Abstractions.Workflows.Definitions;
using PulseStack.Core.Assets;
using PulseStack.Core.Runtime.Realization.Composition;
using PulseStack.Tests.Fakes;
using Xunit;

namespace PulseStack.Tests.Workflows;

public sealed class WorkflowRealizationExecutionTests
{
    [Fact]
    public async Task WorkflowAsset_ShouldComposeAndExecuteRunStep_EndToEnd()
    {
        var agentDefinition = CreateAgentDefinition("Researcher");
        var workflowAsset = new WorkflowAssetFactory().Create(
            new WorkflowAssetOptions
            {
                Name = "Research Workflow",
                Steps =
                [
                    CreateRunDefinition(agentDefinition)
                ]
            });

        var composer = CreateComposer(
            new StubAssetResolver(agentDefinition),
            new StubAgentComposer(
                new FakeAgent("Researcher", "Research Complete")));

        var runtimeWorkflow = await composer.ComposeAsync(workflowAsset);
        var runtime = WorkflowTestRuntimeFactory.Create();
        var context = new PipelineContext
        {
            Input = "Research AI orchestration"
        };

        var result = await runtime.ExecuteAsync(
            runtimeWorkflow,
            context);

        result.Success.Should().BeTrue();
        result.FinalOutput.Should().Be("Research Complete");
        result.Steps.Should().ContainSingle();
        result.Steps.Single().StepName.Should().Be("Researcher");
        result.Steps.Single().Output.Should().Be("Research Complete");
        context.CurrentOutput.Should().Be("Research Complete");
    }

    [Fact]
    public async Task WorkflowAsset_ShouldComposeAndExecuteConditionalGraph_EndToEnd()
    {
        var approvalDefinition = CreateAgentDefinition("Approval Agent");
        var workflowAsset = new WorkflowAssetFactory().Create(
            new WorkflowAssetOptions
            {
                Name = "Approval Workflow",
                Steps =
                [
                    new ConditionalStepDefinition
                    {
                        Name = "Requires Approval",
                        Condition = new NamedConditionDefinition
                        {
                            Name = "requires-approval"
                        },
                        ThenStep = CreateRunDefinition(approvalDefinition)
                    }
                ]
            });

        var composer = CreateComposer(
            new StubAssetResolver(approvalDefinition),
            new StubAgentComposer(
                new FakeAgent("Approval Agent", "Approved")),
            new StubConditionBindingResolver(
                new StubCondition("requires-approval", true)));

        var runtimeWorkflow = await composer.ComposeAsync(workflowAsset);
        var runtime = WorkflowTestRuntimeFactory.Create();
        var context = new PipelineContext
        {
            Input = "Expense request"
        };

        var result = await runtime.ExecuteAsync(
            runtimeWorkflow,
            context);

        result.Success.Should().BeTrue();
        result.FinalOutput.Should().Be("Approved");
        result.Steps.Should().ContainSingle();
        result.Steps.Single().StepName.Should().Be("Requires Approval");
        result.Steps.Single().Output.Should().Be("Approved");
        context.CurrentOutput.Should().Be("Approved");
    }

    private static WorkflowComposer CreateComposer(
        IAssetResolver assetResolver,
        IAgentComposer agentComposer,
        IConditionBindingResolver? conditionBindingResolver = null) =>
        new(
            assetResolver,
            agentComposer,
            conditionBindingResolver ??
            new StubConditionBindingResolver(
                new StubCondition("default", true)));

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

    private sealed class StubAssetResolver(params IAsset[] assets)
        : IAssetResolver
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

    private sealed class StubAgentComposer(params IAgent[] agents)
        : IAgentComposer
    {
        private readonly Queue<IAgent> _agents = new(agents);

        public Task<IAgent> ComposeAsync(
            AgentDefinition definition,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_agents.Count == 0)
            {
                throw new InvalidOperationException(
                    "No stub Agent remains.");
            }

            return Task.FromResult(_agents.Dequeue());
        }
    }

    private sealed class StubConditionBindingResolver(ICondition condition)
        : IConditionBindingResolver
    {
        public ICondition Resolve(ConditionDefinition definition) => condition;
    }

    private sealed class StubCondition(string name, bool result)
        : ICondition
    {
        public string Name { get; } = name;

        public ValueTask<bool> EvaluateAsync(
            PipelineContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }
}
