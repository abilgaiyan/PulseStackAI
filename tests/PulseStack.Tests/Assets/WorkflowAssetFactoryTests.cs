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
            agentId,
            new AssetUrn($"urn:pulsestack:agent:{agentId}"));

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
}
