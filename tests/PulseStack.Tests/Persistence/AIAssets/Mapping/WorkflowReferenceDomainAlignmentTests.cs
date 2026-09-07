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
        var second = new AssetReference(
            AssetType.Agent,
            AssetId.New(),
            new AssetUrn("urn:pulsestack:agent:second"),
            AssetVersion.Initial);

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
    public void ToDocument_ShouldRejectCopiedWorkflowWhoseReferencesDoNotMatchStepProjection()
    {
        var agent = AgentReference("urn:pulsestack:agent:one");
        var workflow = CreateWorkflow(new RunStepDefinition { Agent = agent });
        var copied = workflow with
        {
            References = []
        };

        var act = () => new AIAssetDocumentMapper().ToDocument(copied);

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
    {
        var id = AssetId.New();
        return new AssetReference(
            AssetType.Agent,
            id,
            new AssetUrn(urn),
            AssetVersion.Initial);
    }
}
