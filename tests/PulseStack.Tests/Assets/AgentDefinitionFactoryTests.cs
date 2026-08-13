using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Core.Assets;

namespace PulseStack.Tests.Assets;

public sealed class AgentDefinitionFactoryTests
{
    [Fact]
    public void Create_ShouldCreateAgentAsset()
    {
        var factory = new AgentDefinitionFactory();
        var options = CreateOptions();

        var agent = factory.Create(options);

        agent.Type.Should().Be(AssetType.Agent);
        agent.Id.IsEmpty.Should().BeFalse();
        agent.Urn.Value.Should().Be($"urn:pulsestack:agent:{agent.Id}");
        agent.Version.Should().Be(AssetVersion.Initial);
        agent.Lifecycle.Should().Be(AssetLifecycle.Draft);
        agent.Metadata.Name.Should().Be(options.Name);
        agent.Options.Should().Be(options);
    }

    [Fact]
    public void Create_ShouldCollectAssetReferences()
    {
        var model = Reference("urn:pulsestack:model:openai:gpt-4.1-mini");
        var prompt = Reference("urn:pulsestack:prompt:contract-review");
        var knowledge = Reference("urn:pulsestack:knowledge:contracts");
        var tool = Reference("urn:pulsestack:tool:document-search");
        var memory = Reference("urn:pulsestack:memory:conversation");
        var policy = Reference("urn:pulsestack:policy:privacy");

        var factory = new AgentDefinitionFactory();
        var agent = factory.Create(new AgentDefinitionOptions
        {
            Name = "Contract Reviewer",
            Goal = "Review contracts",
            Role = "Contract review specialist",
            Model = model,
            Prompt = prompt,
            Knowledge = [knowledge],
            Tools = [tool],
            Memory = memory,
            Policies = [policy]
        });

        agent.References.Should().BeEquivalentTo(
            [model, prompt, knowledge, tool, memory, policy]);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_ShouldRejectMissingName(string name)
    {
        var factory = new AgentDefinitionFactory();
        var options = CreateOptions() with { Name = name };

        var action = () => factory.Create(options);

        action.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_ShouldRejectMissingGoal(string goal)
    {
        var factory = new AgentDefinitionFactory();
        var options = CreateOptions() with { Goal = goal };

        var action = () => factory.Create(options);

        action.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_ShouldRejectMissingRole(string role)
    {
        var factory = new AgentDefinitionFactory();
        var options = CreateOptions() with { Role = role };

        var action = () => factory.Create(options);

        action.Should().Throw<ArgumentException>();
    }

    private static AgentDefinitionOptions CreateOptions()
        => new()
        {
            Name = "Contract Reviewer",
            Goal = "Review contracts",
            Role = "Contract review specialist"
        };

    private static AssetReference Reference(string urn)
        => new(AssetId.New(), new AssetUrn(urn));
}
