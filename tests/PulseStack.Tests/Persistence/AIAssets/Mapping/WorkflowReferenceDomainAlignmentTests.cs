using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Workflows.Definitions;
using PulseStack.Core.Assets;
using PulseStack.Core.Persistence.AIAssets.Mapping;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets.Mapping;

public sealed class WorkflowReferenceDomainAlignmentTests
{
    [Fact]
    public void Create_ShouldDeduplicateRepeatedExactRunReferenceByDefinitionKey()
    {
        var agent = AgentReference("urn:pulsestack:agent:one");

        var workflow = CreateWorkflow(
            new RunStepDefinition { Agent = agent },
            new RetryStepDefinition
            {
                Step = new RunStepDefinition { Agent = agent }
            });

        workflow.References.Should().Equal(agent);
    }

    [Fact]
    public void Create_ShouldRejectConflictingUrnsForSameDefinitionKey()
    {
        var first = AgentReference("urn:pulsestack:agent:one");
        var conflicting = first with
        {
            Urn = new AssetUrn("urn:pulsestack:agent:other")
        };

        var act = () => CreateWorkflow(
            new RunStepDefinition { Agent = first },
            new RunStepDefinition { Agent = conflicting });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*conflicting URNs for the same Asset definition identity*");
    }

    [Fact]
    public void Create_ShouldPreserveFirstOccurrenceOrderAcrossNestedRunSteps()
    {
        var first = AgentReference("urn:pulsestack:agent:first");
        var second = AgentReference("urn:pulsestack:agent:second");

        var workflow = CreateWorkflow(
            new ParallelStepDefinition
            {
                Name = "parallel",
                Steps =
                [
                    new RunStepDefinition { Agent = first },
                    new RetryStepDefinition
                    {
                        Step = new RunStepDefinition { Agent = second }
                    },
                    new RunStepDefinition { Agent = first }
                ]
            });

        workflow.References.Should().Equal(first, second);
    }

    [Fact]
    public void Create_ShouldKeepSameAgentIdWithDifferentVersionsDistinct()
    {
        var id = AssetId.New();
        var v1 = new AssetReference(
            AssetType.Agent,
            id,
            new AssetUrn("urn:pulsestack:agent:one:1.0"),
            new AssetVersion("1.0"));
        var v2 = new AssetReference(
            AssetType.Agent,
            id,
            new AssetUrn("urn:pulsestack:agent:one:2.0"),
            new AssetVersion("2.0"));

        var workflow = CreateWorkflow(
            new RunStepDefinition { Agent = v1 },
            new RunStepDefinition { Agent = v2 });

        workflow.References.Should().Equal(v1, v2);
    }

    [Fact]
    public void ToDocument_ShouldRejectCopiedWorkflowWhoseReferencesAreMissing()
    {
        var agent = AgentReference("urn:pulsestack:agent:one");
        var workflow = CreateWorkflow(new RunStepDefinition { Agent = agent });
        var copied = workflow with
        {
            References = []
        };

        AssertProjectionMismatch(copied);
    }

    [Fact]
    public void ToDocument_ShouldRejectCopiedWorkflowWhoseReferencesHaveWrongOrder()
    {
        var first = AgentReference("urn:pulsestack:agent:first");
        var second = AgentReference("urn:pulsestack:agent:second");
        var workflow = CreateWorkflow(
            new RunStepDefinition { Agent = first },
            new RunStepDefinition { Agent = second });
        var copied = workflow with
        {
            References = [second, first]
        };

        AssertProjectionMismatch(copied);
    }

    [Fact]
    public void ToDocument_ShouldRejectCopiedWorkflowWhoseReferenceHasWrongUrn()
    {
        var agent = AgentReference("urn:pulsestack:agent:one");
        var workflow = CreateWorkflow(new RunStepDefinition { Agent = agent });
        var copied = workflow with
        {
            References =
            [
                agent with
                {
                    Urn = new AssetUrn("urn:pulsestack:agent:wrong")
                }
            ]
        };

        AssertProjectionMismatch(copied);
    }

    [Fact]
    public void ToDocument_ShouldRejectCopiedWorkflowWhoseReferenceHasWrongVersion()
    {
        var agent = AgentReference("urn:pulsestack:agent:one");
        var workflow = CreateWorkflow(new RunStepDefinition { Agent = agent });
        var copied = workflow with
        {
            References =
            [
                agent with
                {
                    Version = new AssetVersion("2.0")
                }
            ]
        };

        AssertProjectionMismatch(copied);
    }

    [Fact]
    public void ToDocument_ShouldRejectCopiedWorkflowWhoseReferencesContainDuplicate()
    {
        var agent = AgentReference("urn:pulsestack:agent:one");
        var workflow = CreateWorkflow(new RunStepDefinition { Agent = agent });
        var copied = workflow with
        {
            References = [agent, agent]
        };

        AssertProjectionMismatch(copied);
    }

    [Fact]
    public void ConstructionAndMapping_ShouldUseTheSameCanonicalProjectionSemantics()
    {
        var first = AgentReference("urn:pulsestack:agent:first");
        var second = AgentReference("urn:pulsestack:agent:second");
        var workflow = CreateWorkflow(
            new ParallelStepDefinition
            {
                Name = "parallel",
                Steps =
                [
                    new RunStepDefinition { Agent = first },
                    new RetryStepDefinition
                    {
                        Step = new RunStepDefinition { Agent = second }
                    },
                    new RunStepDefinition { Agent = first }
                ]
            });

        workflow.References.Should().Equal(first, second);

        var act = () => new AIAssetDocumentMapper().ToDocument(workflow);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Workflow*");
    }

    private static void AssertProjectionMismatch(WorkflowAsset workflow)
    {
        var act = () => new AIAssetDocumentMapper().ToDocument(workflow);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Run-step references do not match the canonical common References projection*");
    }

    private static WorkflowAsset CreateWorkflow(params WorkflowStepDefinition[] steps)
        => new WorkflowAssetFactory().Create(
            new WorkflowAssetOptions
            {
                Name = "Reference Alignment",
                Steps = steps
            });

    private static AssetReference AgentReference(string urn)
        => new(
            AssetType.Agent,
            AssetId.New(),
            new AssetUrn(urn),
            AssetVersion.Initial);
}
