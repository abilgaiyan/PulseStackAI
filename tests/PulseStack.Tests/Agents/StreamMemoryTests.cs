using Xunit;
using FluentAssertions;
using Microsoft.Extensions.AI;
using PulseStack.Agents.Builders;
using PulseStack.Core.Memory;
using PulseStack.Tests.Fakes;

namespace PulseStack.Tests.Agents;

public class StreamMemoryTests
{
    [Fact]
    public async Task StreamAsync_Should_Persist_Final_Response()
    {
        // Arrange
        var memory = new ConversationMemory();

        var client = new FakeChatClient(
            ["Hello ", "Ajay"]);

        var agent = new AgentBuilder("Streamer", client)
            .WithMemory(memory)
            .Build();

        // Act
        await foreach (var _ in agent.StreamAsync("Hi"))
        {
        }

        // Assert
        memory.Messages.Should().HaveCount(2);

        memory.Messages[1].Text.Should().Be("Hello Ajay");
    }
}