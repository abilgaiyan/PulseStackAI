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
using PulseStack.Core.Runtime.Realization.Evaluation;
using Xunit;

namespace PulseStack.Tests.Runtime.Realization;

public sealed class SwitchWorkflowComposerTests
{
    [Fact]
    public async Task ComposeAsync_ShouldComposeSwitchCasesAndDefaultRecursively()
    {
        var billingDefinition = CreateAgentDefinition("Billing Agent");
        var technicalDefinition = CreateAgentDefinition("Technical Agent");
        var defaultDefinition = CreateAgentDefinition("General Agent");

        var switchDefinition = new SwitchStepDefinition
        {
            Name = "Route Request",
            Selector = new ContextItemValueDefinition
            {
                Key = "classification"
            },
            Cases =
            [
                new SwitchCaseDefinition
                {
                    Value = "billing",
                    Step = CreateRunDefinition(billingDefinition)
                },
                new SwitchCaseDefinition
                {
                    Value = "technical",
                    Step = new ParallelStepDefinition
                    {
                        Name = "Technical Work",
                        Steps = [CreateRunDefinition(technicalDefinition)]
                    }
                }
            ],
            DefaultStep = CreateRunDefinition(defaultDefinition)
        };

        var workflowAsset = new WorkflowAssetFactory().Create(
            new WorkflowAssetOptions
            {
                Name = "Routing Workflow",
                Steps = [switchDefinition]
            });

        var composer = CreateComposer(
            new StubAssetResolver(
                billingDefinition,
                technicalDefinition,
                defaultDefinition),
            new StubAgentComposer(
                new StubAgent("Billing Agent"),
                new StubAgent("Technical Agent"),
                new StubAgent("General Agent")));

        var workflow = await composer.ComposeAsync(workflowAsset);

        var runtime = workflow.Steps.Single()
            .Should().BeOfType<SwitchStep>()
            .Subject;

        runtime.Id.Should().Be(switchDefinition.Id);
        runtime.Name.Should().Be("Route Request");
        runtime.Cases.Should().HaveCount(2);
        runtime.Cases[0].Value.Should().Be("billing");
        runtime.Cases[0].Step.Should().BeOfType<RunStep>()
            .Which.Name.Should().Be("Billing Agent");

        var technical = runtime.Cases[1].Step
            .Should().BeOfType<ParallelStep>()
            .Subject;

        technical.Steps.Should().ContainSingle();
        technical.Steps.Single().Should().BeOfType<RunStep>()
            .Which.Name.Should().Be("Technical Agent");

        runtime.DefaultStep.Should().BeOfType<RunStep>()
            .Which.Name.Should().Be("General Agent");

        var context = new PipelineContext();
        context.Items["classification"] = "technical";

        runtime.Selector(context).Should().Be("technical");
    }

    [Fact]
    public async Task ComposeAsync_ShouldPreserveNullSelectorValue()
    {
        var agentDefinition = CreateAgentDefinition("Fallback Agent");
        var switchDefinition = new SwitchStepDefinition
        {
            Selector = new ContextItemValueDefinition
            {
                Key = "missing"
            },
            Cases =
            [
                new SwitchCaseDefinition
                {
                    Value = "known",
                    Step = CreateRunDefinition(agentDefinition)
                }
            ]
        };

        var workflowAsset = CreateWorkflow(switchDefinition);
        var composer = CreateComposer(
            new StubAssetResolver(agentDefinition),
            new StubAgentComposer(new StubAgent("Fallback Agent")));

        var workflow = await composer.ComposeAsync(workflowAsset);
        var runtime = workflow.Steps.Single()
            .Should().BeOfType<SwitchStep>()
            .Subject;

        runtime.Selector(new PipelineContext()).Should().BeNull();
    }

    [Fact]
    public async Task ComposeAsync_ShouldRejectSwitchWithoutCases()
    {
        var switchDefinition = new SwitchStepDefinition
        {
            Name = "Empty Switch",
            Selector = new LiteralValueDefinition
            {
                Value = "anything"
            }
        };

        var composer = CreateComposer(
            new StubAssetResolver(),
            new StubAgentComposer());

        var action = () => composer.ComposeAsync(
            CreateWorkflow(switchDefinition));

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*requires at least one case*");
    }

    [Fact]
    public async Task ComposeAsync_ShouldRejectNonStringSelectorAtRuntimeEvaluation()
    {
        var agentDefinition = CreateAgentDefinition("Worker");
        var switchDefinition = new SwitchStepDefinition
        {
            Name = "Typed Switch",
            Selector = new LiteralValueDefinition
            {
                Value = 42
            },
            Cases =
            [
                new SwitchCaseDefinition
                {
                    Value = "42",
                    Step = CreateRunDefinition(agentDefinition)
                }
            ]
        };

        var composer = CreateComposer(
            new StubAssetResolver(agentDefinition),
            new StubAgentComposer(new StubAgent("Worker")));

        var workflow = await composer.ComposeAsync(
            CreateWorkflow(switchDefinition));
        var runtime = workflow.Steps.Single()
            .Should().BeOfType<SwitchStep>()
            .Subject;

        var action = () => runtime.Selector(new PipelineContext());

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*requires a string workflow value*");
    }

    private static WorkflowAsset CreateWorkflow(
        SwitchStepDefinition step) =>
        new WorkflowAssetFactory().Create(
            new WorkflowAssetOptions
            {
                Name = "Switch Workflow",
                Steps = [step]
            });

    private static WorkflowComposer CreateComposer(
        IAssetResolver assetResolver,
        IAgentComposer agentComposer) =>
        new(
            assetResolver,
            agentComposer,
            new StubConditionBindingResolver(),
            new WorkflowValueEvaluator());

    private static AgentDefinition CreateAgentDefinition(string name) =>
        new AgentDefinitionFactory().Create(
            new AgentDefinitionOptions
            {
                Name = name,
                Goal = "Execute routed workflow work",
                Role = "Worker"
            });

    private static RunStepDefinition CreateRunDefinition(
        AgentDefinition definition) =>
        new()
        {
            Agent = new AssetReference(
                definition.Type,
                definition.Id,
                definition.Urn,
                definition.Version)
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
