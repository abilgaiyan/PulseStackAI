using Xunit;
using FluentAssertions;
using PulseStack.Core.Memory;
using PulseStack.Core.Security;
using PulseStack.Core.Tools;
using PulseStack.Tests.Fakes;
using PulseStack.Tests.TestInfrastructure;

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
            
        var agent = AgentTestFactory.Create(
            client,
            memory: memory);
        // Act
        await foreach (var _ in agent.StreamAsync("Hi"))
        {
        }

        // Assert
        memory.Messages.Should().HaveCount(2);

        memory.Messages[1].Text.Should().Be("Hello Ajay");
    }
}