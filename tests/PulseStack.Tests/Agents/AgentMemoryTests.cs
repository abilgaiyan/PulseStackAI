using Xunit;
using FluentAssertions;
using PulseStack.Agents.Builders;
using PulseStack.Core.Memory;
using PulseStack.Tests.Fakes;

namespace PulseStack.Tests.Agents;

public class AgentMemoryTests
{
    [Fact]
    public async Task RunAsync_Should_Persist_Conversation_Memory()
    {
        // Arrange
        var memory = new ConversationMemory();

        var client = new FakeChatClient(
        [
            "Hello Ajay!",
            "Your name is Ajay."
        ]);

        var agent = new AgentBuilder("Assistant", client)
            .WithMemory(memory)
            .Build();

        // Act
        await agent.RunAsync("My name is Ajay.");

        await agent.RunAsync("What is my name?");

        // Assert
        memory.Messages.Should().HaveCount(4);

        memory.Messages[0].Text.Should().Contain("My name is Ajay");
        memory.Messages[1].Text.Should().Contain("Hello Ajay");
        memory.Messages[2].Text.Should().Contain("What is my name");
        memory.Messages[3].Text.Should().Contain("Ajay");
    }
}