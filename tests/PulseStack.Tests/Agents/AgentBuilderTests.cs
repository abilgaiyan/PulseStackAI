using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Agents.Builders;
using Xunit;

namespace PulseStack.Tests.Agents;

public sealed class AgentBuilderTests
{
    [Fact]
    public void Build_Should_Create_AgentDefinition()
    {
        var agent = new AgentBuilder("Assistant")
            .WithGoal("Answer questions.")
            .WithRole("Assistant")
            .Build();

        agent.Should().NotBeNull();
        agent.Type.Should().Be(AssetType.Agent);
        agent.Options.Name.Should().Be("Assistant");
        agent.Options.Goal.Should().Be("Answer questions.");
        agent.Options.Role.Should().Be("Assistant");
    }

    [Fact]
    public void Build_Should_Set_Model_Reference()
    {
        var model = new AssetReference(
            AssetId.New(),
            new AssetUrn(
                "urn:pulsestack:model:openai:gpt-4o-mini"));

        var agent = new AgentBuilder("Assistant")
            .WithGoal("Answer questions.")
            .WithRole("Assistant")
            .UseModel(model)
            .Build();

        agent.Options.Model.Should().Be(model);
    }

    [Fact]
    public void Build_Should_Set_Prompt_Reference()
    {
        var prompt = new AssetReference(
            AssetId.New(),
            new AssetUrn(
                "urn:pulsestack:prompt:assistant"));

        var agent = new AgentBuilder("Assistant")
            .WithGoal("Answer questions.")
            .WithRole("Assistant")
            .UsePrompt(prompt)
            .Build();

        agent.Options.Prompt.Should().Be(prompt);
    }

    [Fact]
    public void Build_Should_Add_Tool_Reference()
    {
        var tool = new AssetReference(
            AssetId.New(),
            new AssetUrn("urn:pulsestack:tool:calculator"));

        var agent = new AgentBuilder("Assistant")
            .WithGoal("Answer calculation questions.")
            .WithRole("Calculation assistant")
            .UseTool(tool)
            .Build();

        agent.Options.Tools
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be(tool);
    }

    [Fact]
    public void Build_Should_Add_Responsibilities()
    {
        var agent = new AgentBuilder("Assistant")
            .WithGoal("Answer questions.")
            .WithRole("Assistant")
            .AddResponsibility("Understand the question")
            .AddResponsibility("Provide a useful answer")
            .Build();

        agent.Options.Responsibilities
            .Should()
            .BeEquivalentTo(
            [
                "Understand the question",
                "Provide a useful answer"
            ]);
    }    

    [Fact]
    public void Build_Should_Add_Knowledge_References()
    {
        var knowledge = new AssetReference(
            AssetId.New(),
            new AssetUrn(
                "urn:pulsestack:knowledge:documentation"));

        var agent = new AgentBuilder("Assistant")
            .WithGoal("Answer questions.")
            .WithRole("Assistant")
            .UseKnowledge(knowledge)
            .Build();

        agent.Options.Knowledge
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be(knowledge);
    }

    [Fact]
    public void Build_Should_Add_Tool_References()
    {
        var tool = new AssetReference(
            AssetId.New(),
            new AssetUrn(
                "urn:pulsestack:tool:calculator"));

        var agent = new AgentBuilder("Assistant")
            .WithGoal("Answer questions.")
            .WithRole("Assistant")
            .UseTool(tool)
            .Build();

        agent.Options.Tools
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be(tool);
    }

    [Fact]
    public void Build_Should_Set_Memory_Reference()
    {
        var memory = new AssetReference(
            AssetId.New(),
            new AssetUrn(
                "urn:pulsestack:memory:conversation"));

        var agent = new AgentBuilder("Assistant")
            .WithGoal("Answer questions.")
            .WithRole("Assistant")
            .UseMemory(memory)
            .Build();

        agent.Options.Memory.Should().Be(memory);
    }

    [Fact]
    public void Build_Should_Add_Policy_References()
    {
        var policy = new AssetReference(
            AssetId.New(),
            new AssetUrn(
                "urn:pulsestack:policy:privacy"));

        var agent = new AgentBuilder("Assistant")
            .WithGoal("Answer questions.")
            .WithRole("Assistant")
            .UsePolicy(policy)
            .Build();

        agent.Options.Policies
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be(policy);
    }

    [Fact]
    public void Build_Should_Require_Goal()
    {
        var action = () =>
            new AgentBuilder("Assistant")
                .WithRole("Assistant")
                .Build();

        action.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Agent goal has not been configured.");
    }

    [Fact]
    public void Build_Should_Require_Role()
    {
        var action = () =>
            new AgentBuilder("Assistant")
                .WithGoal("Answer questions.")
                .Build();

        action.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Agent role has not been configured.");
    }
}