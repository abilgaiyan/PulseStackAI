using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Workflows.Definitions;
using PulseStack.Core.Assets;
using Xunit;

namespace PulseStack.Tests.Assets;

public sealed class WorkflowAssetFactoryTests
{
    [Fact]
    public void Create_ShouldCreateWorkflowAsset_WithDeclarativeAgentReference()
    {
        var agentId = AssetId.New();
        var agentReference = new AssetReference(
            AssetType.Agent,
            agentId,
            new AssetUrn($"urn:pulsestack:agent:{agentId}"),
            AssetVersion.Initial);

        var asset = new WorkflowAssetFactory().Create(
            new WorkflowAssetOptions
            {
                Name = "Research Workflow",
                Description = "Runs a research agent.",
                Steps =
                [
                    new RunStepDefinition
                    {
                        Agent = agentReference
                    }
                ]
            });

        asset.Type.Should().Be(AssetType.Workflow);
        asset.Metadata.Name.Should().Be("Research Workflow");
        asset.Options.Steps.Should().ContainSingle();
        asset.References.Should().ContainSingle()
            .Which.Should().Be(agentReference);
    }

    [Fact]
    public void Create_ShouldUseExplicitIdentity_WhenProvided()
    {
        var id = new AssetId(Guid.Parse("44444444-4444-4444-4444-444444444444"));
        var options = new WorkflowAssetOptions
        {
            Name = "Research Workflow",
            Steps = []
        };

        var asset = new WorkflowAssetFactory().Create(id, options);

        asset.Id.Should().Be(id);
        asset.Urn.Should().Be(new AssetUrn($"urn:pulsestack:workflow:{id}"));
    }

    [Fact]
    public void Create_ShouldCollectAgentReferences_FromNestedWorkflowSteps()
    {
        var agentId = AssetId.New();
        var agentReference = new AssetReference(
            AssetType.Agent,
            agentId,
            new AssetUrn($"urn:pulsestack:agent:{agentId}"),
            AssetVersion.Initial);

        var asset = new WorkflowAssetFactory().Create(
            new WorkflowAssetOptions
            {
                Name = "Nested Workflow",
                Steps =
                [
                    new ParallelStepDefinition
                    {
                        Name = "Parallel",
                        Steps =
                        [
                            new RetryStepDefinition
                            {
                                Step = new RunStepDefinition
                                {
                                    Agent = agentReference
                                }
                            },
                            new RunStepDefinition
                            {
                                Agent = agentReference
                            }
                        ]
                    }
                ]
            });

        asset.References.Should().ContainSingle()
            .Which.Should().Be(agentReference);
    }
}
