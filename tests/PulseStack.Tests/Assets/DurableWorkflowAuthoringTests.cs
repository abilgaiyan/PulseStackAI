using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Conditions;
using PulseStack.Abstractions.Workflows.Definitions;
using PulseStack.Abstractions.Workflows.Values;
using PulseStack.Core.Assets;
using Xunit;

namespace PulseStack.Tests.Assets;

public sealed class DurableWorkflowAuthoringTests
{
    [Fact]
    public void Run_ShouldPreserveExplicitIdentity()
    {
        var id = StepId("10000000-0000-0000-0000-000000000001");
        var agent = AgentReference();

        var qualified = DurableWorkflowStep.Run(id, agent);

        var run = qualified.Definition.Should().BeOfType<RunStepDefinition>().Subject;
        run.Id.Should().Be(id);
        run.Agent.Should().Be(agent);
    }

    [Fact]
    public void CompositeAuthoring_ShouldPreserveExplicitIdentitiesRecursively()
    {
        var runId = StepId("10000000-0000-0000-0000-000000000002");
        var retryId = StepId("10000000-0000-0000-0000-000000000003");
        var parallelId = StepId("10000000-0000-0000-0000-000000000004");
        var conditionalId = StepId("10000000-0000-0000-0000-000000000005");
        var loopId = StepId("10000000-0000-0000-0000-000000000006");

        var run = DurableWorkflowStep.Run(runId, AgentReference());
        var retry = DurableWorkflowStep.Retry(retryId, run);
        var parallel = DurableWorkflowStep.Parallel(parallelId, "Parallel", [retry]);
        var conditional = DurableWorkflowStep.Conditional(
            conditionalId,
            "Conditional",
            new NamedConditionDefinition { Name = "is-ready" },
            parallel);
        var loop = DurableWorkflowStep.Loop(
            loopId,
            new InputValueDefinition(),
            conditional);

        var loopDefinition = loop.Definition.Should().BeOfType<LoopStepDefinition>().Subject;
        loopDefinition.Id.Should().Be(loopId);

        var conditionalDefinition = loopDefinition.Step.Should()
            .BeOfType<ConditionalStepDefinition>().Subject;
        conditionalDefinition.Id.Should().Be(conditionalId);

        var parallelDefinition = conditionalDefinition.ThenStep.Should()
            .BeOfType<ParallelStepDefinition>().Subject;
        parallelDefinition.Id.Should().Be(parallelId);

        var retryDefinition = parallelDefinition.Steps.Single().Should()
            .BeOfType<RetryStepDefinition>().Subject;
        retryDefinition.Id.Should().Be(retryId);

        retryDefinition.Step.Id.Should().Be(runId);
    }

    [Fact]
    public void SwitchAuthoring_ShouldRequireQualifiedCasesAndPreserveCaseAndDefaultIdentities()
    {
        var switchId = StepId("10000000-0000-0000-0000-000000000007");
        var caseId = StepId("10000000-0000-0000-0000-000000000008");
        var defaultId = StepId("10000000-0000-0000-0000-000000000009");

        var @case = DurableWorkflowStep.SwitchCase(
            "approved",
            DurableWorkflowStep.Run(caseId, AgentReference()));
        var defaultStep = DurableWorkflowStep.Run(defaultId, AgentReference());

        var qualified = DurableWorkflowStep.Switch(
            switchId,
            new InputValueDefinition(),
            [@case],
            defaultStep);

        var definition = qualified.Definition.Should().BeOfType<SwitchStepDefinition>().Subject;
        definition.Id.Should().Be(switchId);
        definition.Cases.Should().ContainSingle();
        definition.Cases.Single().Step.Id.Should().Be(caseId);
        definition.DefaultStep.Should().NotBeNull();
        definition.DefaultStep!.Id.Should().Be(defaultId);
    }

    [Fact]
    public void CompositeAuthoring_ShouldSnapshotQualifiedChildren()
    {
        var children = new List<IdentityCompleteWorkflowStep>
        {
            DurableWorkflowStep.Run(
                StepId("10000000-0000-0000-0000-000000000010"),
                AgentReference())
        };

        var qualified = DurableWorkflowStep.Parallel(
            StepId("10000000-0000-0000-0000-000000000011"),
            "Parallel",
            children);

        children.Add(
            DurableWorkflowStep.Run(
                StepId("10000000-0000-0000-0000-000000000012"),
                AgentReference()));

        var definition = qualified.Definition.Should().BeOfType<ParallelStepDefinition>().Subject;
        definition.Steps.Should().ContainSingle();
    }

    [Fact]
    public void IdentityCompleteWorkflowAssetCreation_ShouldPreserveQualifiedGraphExactly()
    {
        var assetId = new AssetId(Guid.Parse("20000000-0000-0000-0000-000000000001"));
        var outerId = StepId("20000000-0000-0000-0000-000000000002");
        var innerId = StepId("20000000-0000-0000-0000-000000000003");

        var inner = DurableWorkflowStep.Run(innerId, AgentReference());
        var outer = DurableWorkflowStep.Retry(outerId, inner);

        var asset = new WorkflowAssetFactory().Create(
            assetId,
            new IdentityCompleteWorkflowAssetOptions
            {
                Name = "Durable Workflow",
                Description = "Explicit identity graph.",
                Steps = [outer]
            });

        asset.Id.Should().Be(assetId);
        var retry = asset.Options.Steps.Single().Should().BeOfType<RetryStepDefinition>().Subject;
        retry.Id.Should().Be(outerId);
        retry.Step.Id.Should().Be(innerId);
    }

    [Fact]
    public void ExistingWorkflowAuthoring_ShouldContinueToGenerateImplicitStepIdentity()
    {
        var definition = new RunStepDefinition
        {
            Agent = AgentReference()
        };

        definition.Id.Value.Should().NotBe(Guid.Empty);

        var asset = new WorkflowAssetFactory().Create(
            new WorkflowAssetOptions
            {
                Name = "Existing Workflow",
                Steps = [definition]
            });

        asset.Options.Steps.Single().Should().BeSameAs(definition);
    }

    private static WorkflowStepId StepId(string value)
        => new(Guid.Parse(value));

    private static AssetReference AgentReference()
    {
        var id = new AssetId(Guid.Parse("30000000-0000-0000-0000-000000000001"));
        return new AssetReference(
            AssetType.Agent,
            id,
            new AssetUrn($"urn:pulsestack:agent:{id}"),
            AssetVersion.Initial);
    }
}
